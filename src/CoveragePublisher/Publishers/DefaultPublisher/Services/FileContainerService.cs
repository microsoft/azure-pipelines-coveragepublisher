// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.FileContainer.Client;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    public class FileContainerService
    {
        private readonly ConcurrentQueue<string> _fileUploadQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _fileUploadTraceLog = new ConcurrentDictionary<string, ConcurrentQueue<string>>();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _fileUploadProgressLog = new ConcurrentDictionary<string, ConcurrentQueue<string>>();
        private readonly IFileContainerClientHelper _fileContainerHelper;
        private readonly IPipelinesExecutionContext _context;
        
        private const int defaultChunkSize = 4 * 1024 * 1024;
        private const int concurrentUploadsMax = 8;
        private const int batchSize = 50;
        private bool isBatchingEnabled = false;

        private int filesProcessed = 0;

        public FileContainerService(IClientFactory clientFactory, IPipelinesExecutionContext context)
        {
            _fileContainerHelper = new FileContainerClientHelper(clientFactory);
            _context = context;
            _featureFlagHelper = new FeatureFlagHelper(clientFactory);
            isBatchingEnabled = true; //_featureFlagHelper.GetFeatureFlagState(Constants.FeatureFlags.EnableBatchingInFileUploadFF, true);
        }

        public FileContainerService(IFileContainerClientHelper fileContainerHelper, IPipelinesExecutionContext context)
        {
            _fileContainerHelper = fileContainerHelper;
            _context = context;
        }

        /// <summary>
        /// Copy files to container.
        /// </summary>
        /// <param name="directoryAndcontainerPath">Directory and container path as tuple.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <param name="retryDelay">Bool for weather to delay 1 minute before retrying.</param>
        /// <returns></returns>
        public virtual async Task CopyToContainerAsync(Tuple<string, string> directoryAndcontainerPath, CancellationToken cancellationToken, bool retryDelay = true)
        {
            int maxConcurrentUploads = Math.Max(Environment.ProcessorCount / 2, 1);
            string sourceParentDirectory;
            var uploadDirectory = directoryAndcontainerPath.Item1;
            var containerPath = directoryAndcontainerPath.Item2;

            List<string> files;
            files = Directory.EnumerateFiles(uploadDirectory, "*", SearchOption.AllDirectories).ToList();
            sourceParentDirectory = uploadDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            TraceLogger.Info(string.Format(Resources.TotalUploadFiles, files.Count()));

            using (var uploadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                // hook up reporting event from file container client.
                _fileContainerHelper.UploadFileReportTrace += UploadFileTraceReportReceived;
                _fileContainerHelper.UploadFileReportProgress += UploadFileProgressReportReceived;

                try
                {
                    // try upload all files for the first time.
                    List<string> failedFiles = isBatchingEnabled ? await ParallelUploadOptimizedAsync(files, sourceParentDirectory, containerPath, concurrentUploads, cancellationToken) : await ParallelUploadAsync(files, sourceParentDirectory, containerPath, maxConcurrentUploads, uploadCancellationTokenSource.Token);

                    if (failedFiles.Count == 0)
                    {
                        // all files have been upload succeed.
                        TraceLogger.Info(Resources.FileUploadSucceed);
                        return;
                    }
                    else
                    {
                        TraceLogger.Info(string.Format(Resources.FileUploadFailedRetryLater, failedFiles.Count));
                    }

                    if (retryDelay)
                    {
                        // Delay 1 min then retry failed files.
                        for (int timer = 60; timer > 0; timer -= 5)
                        {
                            TraceLogger.Info(string.Format(Resources.FileUploadRetryInSecond, timer));
                            await Task.Delay(TimeSpan.FromSeconds(5), uploadCancellationTokenSource.Token);
                        }
                    }

                    // Retry upload all failed files.
                    TraceLogger.Info(string.Format(Resources.FileUploadRetry, failedFiles.Count));
                    failedFiles = isBatchingEnabled ? await ParallelUploadOptimizedAsync(failedFiles, sourceParentDirectory, containerPath, concurrentUploads, cancellationToken) : await ParallelUploadAsync(failedFiles, sourceParentDirectory, containerPath, maxConcurrentUploads, uploadCancellationTokenSource.Token);

                    if (failedFiles.Count == 0)
                    {
                        // all files have been upload succeed after retry.
                        TraceLogger.Info(Resources.FileUploadRetrySucceed);
                        return;
                    }
                    else
                    {
                        throw new Exception(Resources.FileUploadFailedAfterRetry);
                    }
                }
                finally
                {
                    _fileContainerHelper.UploadFileReportTrace -= UploadFileTraceReportReceived;
                    _fileContainerHelper.UploadFileReportProgress -= UploadFileProgressReportReceived;
                }
            }
        }

        /// <summary>
        /// Creates tasks for uploading files.
        /// </summary>
        /// <param name="files">List of files to be uploaded.</param>
        /// <param name="sourceParentDirectory">Path to the parent directory of the files.</param>
        /// <param name="containerPath">Container path.</param>
        /// <param name="concurrentUploads">Concurrency value.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
        /// <returns>List of files failed to upload.</returns>
        private async Task<List<string>> ParallelUploadAsync(List<string> files, string sourceParentDirectory, string containerPath, int concurrentUploads, CancellationToken cancellationToken)
        {
            // return files that fail to upload
            List<string> failedFiles = new List<string>();

            // nothing needs to upload
            if (files.Count == 0)
            {
                return failedFiles;
            }

            // Original logic
            TraceLogger.Info("Using ORIGINAL upload path");
            // ensure the file upload queue is empty.
            if (!_fileUploadQueue.IsEmpty)
            {
                throw new ArgumentOutOfRangeException(nameof(_fileUploadQueue));
            }

            // enqueue file into upload queue.
            foreach (var file in files)
            {
                _fileUploadQueue.Enqueue(file);
            }

            // Start upload monitor task.
            filesProcessed = 0;
            var uploadFinished = new TaskCompletionSource<int>();
            _fileUploadTraceLog.Clear();
            _fileUploadProgressLog.Clear();
            Task uploadMonitor = ReportingAsync(files.Count(), uploadFinished, cancellationToken);

            // Start parallel upload tasks.
            List<Task<List<string>>> parallelUploadingTasks = new List<Task<List<string>>>();
            for (int uploader = 0; uploader < concurrentUploads; uploader++)
            {
                parallelUploadingTasks.Add(UploadAsync(sourceParentDirectory, containerPath, cancellationToken));
            }

            // Wait for parallel upload finish.
            await Task.WhenAll(parallelUploadingTasks);
            foreach (var uploadTask in parallelUploadingTasks)
            {
                // record all failed files.
                failedFiles.AddRange(await uploadTask);
            }

            // Stop monitor task;
            uploadFinished.TrySetResult(0);
            await uploadMonitor;

            return failedFiles;
        }

        private async Task<List<string>> ParallelUploadOptimizedAsync(List<string> files, string sourceParentDirectory, string containerPath, int concurrentUploads, CancellationToken cancellationToken)
        {
            TraceLogger.Info($"Pipeline Artifact style upload: Processing {files.Count} files in batches of {batchSize}");

            List<string> failedFiles = new List<string>();
            filesProcessed = 0;
            var uploadFinished = new TaskCompletionSource<int>();
            _fileUploadTraceLog.Clear();
            _fileUploadProgressLog.Clear();

            Task uploadMonitor = ReportingAsync(files.Count, uploadFinished, cancellationToken);

            try
            {
                // Process files in batches like Pipeline Artifact does
                for (int batchStart = 0; batchStart < files.Count; batchStart += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var batchEnd = Math.Min(batchStart + batchSize, files.Count);
                    var batch = files.GetRange(batchStart, batchEnd - batchStart);
                    
                    TraceLogger.Info($"Processing batch {(batchStart / batchSize) + 1}: files {batchStart + 1}-{batchEnd} of {files.Count}");
                    
                    var batchFailures = await ProcessBatchAsync(batch, sourceParentDirectory, containerPath, concurrentUploads, cancellationToken);
                    failedFiles.AddRange(batchFailures);
                    
                    // Adaptive throttling - if many failures, reduce concurrency for next batch
                    if (batchFailures.Count > batch.Count / 2)
                    {
                        concurrentUploads = Math.Max(concurrentUploads / 2, 1);
                        TraceLogger.Warning($"High failure rate detected. Reducing concurrency to {concurrentUploads}");
                    }
                    else if (batchFailures.Count == 0 && concurrentUploads < concurrentUploadsMax)
                    {
                        concurrentUploads = Math.Min(concurrentUploads + 1, concurrentUploadsMax);
                        TraceLogger.Info($"Good performance. Increasing concurrency to {concurrentUploads}");
                    }
                }
            }
            catch (Exception ex)
            {
                TraceLogger.Error($"Error in pipeline artifact style upload: {ex}");
                throw;
            }
            finally
            {
                uploadFinished.TrySetResult(0);
                await uploadMonitor;
            }

            TraceLogger.Info($"Pipeline artifact upload completed: {files.Count - failedFiles.Count} succeeded, {failedFiles.Count} failed");
            return failedFiles;
        }

        private async Task<List<string>> ProcessBatchAsync(List<string> batchFiles, string sourceParentDirectory, string containerPath, int concurrentUploads, CancellationToken cancellationToken)
        {
            // Clear and populate queue for this batch
            while (_fileUploadQueue.TryDequeue(out _)) { }
            foreach (var file in batchFiles)
            {
                _fileUploadQueue.Enqueue(file);
            }

            // Create workers for this batch - Pipeline Artifact approach
            var uploadTasks = new List<Task<List<string>>>();
            int actualWorkers = Math.Min(concurrentUploads, batchFiles.Count);

            for (int i = 0; i < actualWorkers; i++)
            {
                uploadTasks.Add(UploadWorkerPipelineStyleAsync(sourceParentDirectory, containerPath, cancellationToken));
            }

            await Task.WhenAll(uploadTasks);

            var failedFiles = new List<string>();
            foreach (var task in uploadTasks)
            {
                failedFiles.AddRange(await task);
            }

            return failedFiles;
        }

        private async Task<List<string>> UploadWorkerPipelineStyleAsync(string sourceParentDirectory, string containerPath, CancellationToken cancellationToken)
        {
            var failedFiles = new List<string>();
            var containerId = _context.ContainerId;
            var projectId = _context.ProjectId;
            int fileCount = 0;

            // Pipeline Artifact uses simple dequeue without complex semaphore logic
            while (_fileUploadQueue.TryDequeue(out string fileToUpload))
            {
                fileCount++;
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Pipeline Artifact approach: simple retry with backoff, no complex semaphore
                    var success = await UploadSingleFileWithBackoffAsync(fileToUpload, sourceParentDirectory, containerPath, containerId, projectId, cancellationToken);
                    if (!success)
                    {
                        failedFiles.Add(fileToUpload);
                    }
                    Interlocked.Increment(ref filesProcessed);
                }
                catch (Exception ex)
                {
                    TraceLogger.Warning($"Worker exception uploading {Path.GetFileName(fileToUpload)}: {ex.Message}");
                    failedFiles.Add(fileToUpload);
                }
            }

            return failedFiles;
        }

        private async Task<bool> UploadSingleFileWithBackoffAsync(string fileToUpload, string sourceParentDirectory, string containerPath, long containerId, Guid projectId, CancellationToken cancellationToken)
        {
            // Pipeline Artifact retry pattern: 3 attempts with exponential backoff
            var delays = new[] { TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3) };
            
            for (int attempt = 0; attempt < delays.Length; attempt++)
            {
                try
                {
                    if (delays[attempt] > TimeSpan.Zero)
                    {
                        await Task.Delay(delays[attempt], cancellationToken);
                    }

                    var itemPath = (containerPath.TrimEnd('/') + "/" + fileToUpload.Remove(0, sourceParentDirectory.Length + 1)).Replace('\\', '/');

                    using (var fs = File.Open(fileToUpload, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var response = await _fileContainerHelper.UploadFileAsync(containerId, itemPath, fs, projectId, cancellationToken, chunkSize: defaultChunkSize);

                        if (response.StatusCode == HttpStatusCode.Created)
                        {
                            return true;
                        }
                        else if (attempt == delays.Length - 1)
                        {
                            TraceLogger.Warning($"Upload failed with {response.StatusCode} for {Path.GetFileName(fileToUpload)}");
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < delays.Length - 1)
                {
                    TraceLogger.Debug($"Upload attempt {attempt + 1} failed for {Path.GetFileName(fileToUpload)}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    TraceLogger.Warning($"Upload failed for {Path.GetFileName(fileToUpload)}: {ex.Message}");
                    break;
                }
            }

            return false;
        }

        private async Task<List<string>> UploadAsync(string sourceParentDirectory, string containerPath, CancellationToken cancellationToken)
        {
            var failedFiles = new List<string>();
            var containerId = _context.ContainerId;
            var projectId = _context.ProjectId;

            while (_fileUploadQueue.TryDequeue(out string fileToUpload))
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var fs = File.OpenRead(fileToUpload))
                {
                    var itemPath = (containerPath.TrimEnd('/') + "/" + fileToUpload.Remove(0, sourceParentDirectory.Length + 1)).Replace('\\', '/');

                    try
                    {
                        var response = await _fileContainerHelper.UploadFileAsync(containerId, itemPath, fs, projectId, cancellationToken, chunkSize: defaultChunkSize);

                        if (response.StatusCode == HttpStatusCode.Created)
                        {
                            TraceLogger.Debug($"Successfully uploaded {fileToUpload}");
                        }
                        else
                        {
                            TraceLogger.Warning($"Upload returned {response.StatusCode} for {fileToUpload}");
                            failedFiles.Add(fileToUpload);
                        }
                    }
                    catch (Exception ex)
                    {
                        TraceLogger.Warning($"Upload failed for {fileToUpload}: {ex.Message}");
                        failedFiles.Add(fileToUpload);
                    }
                }
            }

            return failedFiles;
        }

        private async Task ReportingAsync(int totalFiles, TaskCompletionSource<int> uploadFinished, CancellationToken token)
        {
            int traceInterval = 0;
            while (!uploadFinished.Task.IsCompleted && !token.IsCancellationRequested)
            {
                bool hasDetailProgress = false;
                foreach (var file in _fileUploadProgressLog)
                {
                    string message;
                    while (file.Value.TryDequeue(out message))
                    {
                        hasDetailProgress = true;
                        TraceLogger.Info(message);
                    }
                }

                // trace total file progress every 25 seconds when there is no file level detail progress
                if (++traceInterval % 2 == 0 && !hasDetailProgress)
                {
                    TraceLogger.Info(string.Format(Resources.FileUploadProgress, totalFiles, filesProcessed, (filesProcessed * 100) / totalFiles));
                }

                await Task.WhenAny(uploadFinished.Task, Task.Delay(5000, token));
            }
        }

        private void UploadFileTraceReportReceived(object sender, ReportTraceEventArgs e)
        {
            ConcurrentQueue<string> logQueue = _fileUploadTraceLog.GetOrAdd(e.File, new ConcurrentQueue<string>());
            logQueue.Enqueue(e.Message);
        }

        private void UploadFileProgressReportReceived(object sender, ReportProgressEventArgs e)
        {
            ConcurrentQueue<string> progressQueue = _fileUploadProgressLog.GetOrAdd(e.File, new ConcurrentQueue<string>());
            progressQueue.Enqueue(string.Format(string.Format(Resources.FileUploadProgressDetail, e.File, (e.CurrentChunk * 100) / e.TotalChunks)));
        }
    }

    // Add this class to support upload sessions
    public class UploadSession
    {
        public Guid SessionId { get; set; }
        public string ItemPath { get; set; }
        public long ContainerId { get; set; }
        public Guid ProjectId { get; set; }
        public long FileSize { get; set; }
        public int ChunkSize { get; set; }
    }
}