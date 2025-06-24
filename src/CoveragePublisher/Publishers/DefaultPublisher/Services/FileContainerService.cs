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
        private readonly FeatureFlagHelper _featureFlagHelper;
        
        private const int defaultChunkSize = Constants.BatchUploadConfig.DefaultChunkSize;
        private const int concurrentUploadsMax = Constants.BatchUploadConfig.ConcurrentUploadsMax;
        private const int batchSize = Constants.BatchUploadConfig.BatchSize;
        private bool isBatchingEnabled = false;

        private int filesProcessed = 0;

        public FileContainerService(IClientFactory clientFactory, IPipelinesExecutionContext context)
        {
            _fileContainerHelper = new FileContainerClientHelper(clientFactory);
            _context = context;
            _featureFlagHelper = new FeatureFlagHelper(clientFactory);
            isBatchingEnabled = _featureFlagHelper.GetFeatureFlagStateForTcm(Constants.FeatureFlags.EnableBatchingInFileUploadFF);
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
                    List<string> failedFiles = isBatchingEnabled ? await ParallelUploadOptimizedAsync(files, sourceParentDirectory, containerPath, maxConcurrentUploads, cancellationToken) : await ParallelUploadAsync(files, sourceParentDirectory, containerPath, maxConcurrentUploads, uploadCancellationTokenSource.Token);

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
                    failedFiles = isBatchingEnabled ? await ParallelUploadOptimizedAsync(failedFiles, sourceParentDirectory, containerPath, maxConcurrentUploads, cancellationToken) : await ParallelUploadAsync(failedFiles, sourceParentDirectory, containerPath, maxConcurrentUploads, uploadCancellationTokenSource.Token);

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

        /// <summary>
        /// Uploads files in parallel using batching and concurrency optimization.
        /// </summary>
        /// <param name="files">List of files to upload.</param>
        /// <param name="sourceParentDirectory">Source parent directory.</param>
        /// <param name="containerPath">Destination container path.</param>
        /// <param name="concurrentUploads">Number of concurrent uploads.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of files that failed to upload.</returns>
        private async Task<List<string>> ParallelUploadOptimizedAsync(List<string> files, string sourceParentDirectory, string containerPath, int concurrentUploads, CancellationToken cancellationToken)
        {
            List<string> failedFiles = new List<string>();
            filesProcessed = 0;
            var uploadFinished = new TaskCompletionSource<int>();
            _fileUploadTraceLog.Clear();
            _fileUploadProgressLog.Clear();

            Task uploadMonitor = ReportingAsync(files.Count, uploadFinished, cancellationToken);

            try
            {
                for (int batchStart = 0; batchStart < files.Count; batchStart += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var batchEnd = Math.Min(batchStart + batchSize, files.Count);
                    var batch = files.GetRange(batchStart, batchEnd - batchStart);
                    
                    TraceLogger.Info(string.Format(Resources.ProcessingBatch, (batchStart / batchSize) + 1, batchStart + 1, batchEnd, files.Count));
                    
                    var batchFailures = await ProcessBatchAsync(batch, sourceParentDirectory, containerPath, concurrentUploads, cancellationToken);
                    failedFiles.AddRange(batchFailures);
                    
                    if (batchFailures.Count > batch.Count / 2)
                    {
                        concurrentUploads = Math.Max(concurrentUploads / 2, 1);
                        TraceLogger.Debug($"Reducing concurrency to {concurrentUploads}");
                    }
                    else if (batchFailures.Count == 0 && concurrentUploads < concurrentUploadsMax)
                    {
                        concurrentUploads = Math.Min(concurrentUploads + 1, concurrentUploadsMax);
                        TraceLogger.Debug($"Increasing concurrency to {concurrentUploads}");
                    }
                }
            }
            catch (Exception ex)
            {
                TraceLogger.Error(string.Format(Resources.ErrorInUpload, ex));
                throw;
            }
            finally
            {
                uploadFinished.TrySetResult(0);
                await uploadMonitor;
            }

            TraceLogger.Info(string.Format(Resources.ArtifactUploadCompleted, files.Count - failedFiles.Count, failedFiles.Count));
            return failedFiles;
        }

        /// <summary>
        /// Processes a batch of files for upload with the specified concurrency.
        /// </summary>
        /// <param name="batchFiles">Batch of files to upload.</param>
        /// <param name="sourceParentDirectory">Source parent directory.</param>
        /// <param name="containerPath">Destination container path.</param>
        /// <param name="concurrentUploads">Number of concurrent uploads.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of files that failed to upload.</returns>
        private async Task<List<string>> ProcessBatchAsync(List<string> batchFiles, string sourceParentDirectory, string containerPath, int concurrentUploads, CancellationToken cancellationToken)
        {
            while (_fileUploadQueue.TryDequeue(out _)) { }
            foreach (var file in batchFiles)
            {
                _fileUploadQueue.Enqueue(file);
            }

            var uploadTasks = new List<Task<List<string>>>();
            int actualWorkers = Math.Min(concurrentUploads, batchFiles.Count);

            for (int i = 0; i < actualWorkers; i++)
            {
                uploadTasks.Add(UploadOptimizedAsync(sourceParentDirectory, containerPath, cancellationToken));
            }

            await Task.WhenAll(uploadTasks);

            var failedFiles = new List<string>();
            foreach (var task in uploadTasks)
            {
                failedFiles.AddRange(await task);
            }

            return failedFiles;
        }

        /// <summary>
        /// Worker method for uploading files from the queue in optimized mode.
        /// </summary>
        /// <param name="sourceParentDirectory">Source parent directory.</param>
        /// <param name="containerPath">Destination container path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of files that failed to upload.</returns>
        private async Task<List<string>> UploadOptimizedAsync(string sourceParentDirectory, string containerPath, CancellationToken cancellationToken)
        {
            var failedFiles = new List<string>();
            var containerId = _context.ContainerId;
            var projectId = _context.ProjectId;
            int fileCount = 0;

            while (_fileUploadQueue.TryDequeue(out string fileToUpload))
            {
                fileCount++;
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var success = await UploadSingleFileWithBackoffAsync(fileToUpload, sourceParentDirectory, containerPath, containerId, projectId, cancellationToken);
                    if (!success)
                    {
                        failedFiles.Add(fileToUpload);
                    }
                    Interlocked.Increment(ref filesProcessed);
                }
                catch (Exception ex)
                {
                    TraceLogger.Debug($"Worker exception uploading {Path.GetFileName(fileToUpload)}: {ex.Message}");
                    failedFiles.Add(fileToUpload);
                }
            }

            return failedFiles;
        }

        /// <summary>
        /// Uploads a single file with retry and backoff logic.
        /// </summary>
        /// <param name="fileToUpload">File to upload.</param>
        /// <param name="sourceParentDirectory">Source parent directory.</param>
        /// <param name="containerPath">Destination container path.</param>
        /// <param name="containerId">Container ID.</param>
        /// <param name="projectId">Project ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if upload succeeded, false otherwise.</returns>
        private async Task<bool> UploadSingleFileWithBackoffAsync(string fileToUpload, string sourceParentDirectory, string containerPath, long containerId, Guid projectId, CancellationToken cancellationToken)
        {
            var delays = new[] { TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3) };
            
            for (int attempt = 0; attempt < delays.Length; attempt++)
            {
                try
                {
                    if (delays[attempt] > TimeSpan.Zero)
                    {
                        await Task.Delay(delays[attempt], cancellationToken);
                    }

                    // Updated itemPath logic as requested
                    var relativePath = Path.GetRelativePath(sourceParentDirectory, fileToUpload);
                    var itemPath = Path.Combine(containerPath.TrimEnd('/'), relativePath).Replace('\\', '/');

                    using (var fs = File.Open(fileToUpload, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var response = await _fileContainerHelper.UploadFileAsync(containerId, itemPath, fs, projectId, cancellationToken, chunkSize: defaultChunkSize);

                        if (response.StatusCode == HttpStatusCode.Created)
                        {
                            return true;
                        }
                        else if (attempt == delays.Length - 1)
                        {
                            TraceLogger.Debug($"Upload failed with {response.StatusCode} for {Path.GetFileName(fileToUpload)}");
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    TraceLogger.Error(string.Format(Resources.FileUploadCancelled, fileToUpload));
                    throw;
                }
                catch (Exception ex) when (attempt < delays.Length - 1)
                {
                    TraceLogger.Debug($"Upload attempt {attempt + 1} failed for {Path.GetFileName(fileToUpload)}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    TraceLogger.Debug($"Upload failed for {Path.GetFileName(fileToUpload)}: {ex.Message}");
                    break;
                }
            }

            return false;
        }

        private async Task<List<string>> UploadAsync(string sourceParentDirectory, string containerPath, CancellationToken cancellationToken)
        {
            List<string> failedFiles = new List<string>();
            string fileToUpload;
            Stopwatch uploadTimer = new Stopwatch();
            var containerId = _context.ContainerId;
            var projectId = _context.ProjectId;

            while (_fileUploadQueue.TryDequeue(out fileToUpload))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using (FileStream fs = File.Open(fileToUpload, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        string itemPath = (containerPath.TrimEnd('/') + "/" + fileToUpload.Remove(0, sourceParentDirectory.Length + 1)).Replace('\\', '/');
                        uploadTimer.Restart();
                        bool coughtExceptionDuringUpload = false;
                        HttpResponseMessage response = null;
                        try
                        {
                            response = await _fileContainerHelper.UploadFileAsync(containerId, itemPath, fs, projectId, cancellationToken, chunkSize: 4 * 1024 * 1024);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            TraceLogger.Error(string.Format(Resources.FileUploadCancelled, fileToUpload));
                            if (response != null)
                            {
                                response.Dispose();
                                response = null;
                            }

                            throw;
                        }
                        catch (Exception ex)
                        {
                            coughtExceptionDuringUpload = true;
                            TraceLogger.Error(string.Format(Resources.FileUploadFailed, fileToUpload, ex));
                        }

                        uploadTimer.Stop();
                        if (coughtExceptionDuringUpload || (response != null && response.StatusCode != HttpStatusCode.Created))
                        {
                            if (response != null)
                            {
                                TraceLogger.Info(string.Format(Resources.FileContainerUploadFailed, response.StatusCode, response.ReasonPhrase, fileToUpload, itemPath));
                            }

                            // output detail upload trace for the file.
                            ConcurrentQueue<string> logQueue;
                            if (_fileUploadTraceLog.TryGetValue(itemPath, out logQueue))
                            {
                                TraceLogger.Info(string.Format(Resources.FileUploadDetailTrace, itemPath));
                                string message;
                                while (logQueue.TryDequeue(out message))
                                {
                                    TraceLogger.Info(message);
                                }
                            }

                            // tracking file that failed to upload.
                            failedFiles.Add(fileToUpload);
                        }
                        else
                        {
                            TraceLogger.Debug(string.Format(Resources.FileUploadFinish, fileToUpload, uploadTimer.ElapsedMilliseconds));

                            // debug detail upload trace for the file.
                            ConcurrentQueue<string> logQueue;
                            if (_fileUploadTraceLog.TryGetValue(itemPath, out logQueue))
                            {
                                TraceLogger.Debug($"Detail upload trace for file: {itemPath}");
                                string message;
                                while (logQueue.TryDequeue(out message))
                                {
                                    TraceLogger.Debug(message);
                                }
                            }
                        }

                        if (response != null)
                        {
                            response.Dispose();
                            response = null;
                        }
                    }

                    Interlocked.Increment(ref filesProcessed);
                }
                catch (Exception ex)
                {
                    TraceLogger.Error(string.Format(Resources.FileUploadFileOpenFailed, ex.Message, fileToUpload));
                    throw ex;
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
}