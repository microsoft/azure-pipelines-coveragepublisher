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
        private readonly SemaphoreSlim _uploadSemaphore;
        private const int DefaultChunkSize = 4 * 1024 * 1024; // Keep original 4MB for test compatibility
        private const int MaxConcurrentUploads = 16;
        private const int MinConcurrentUploads = 2;
        private const bool UseOptimizedUploads = true; // Feature flag for new optimizations

        private int filesProcessed = 0;

        public FileContainerService(IClientFactory clientFactory, IPipelinesExecutionContext context)
        {
            _fileContainerHelper = new FileContainerClientHelper(clientFactory);
            _context = context;
            
            if (UseOptimizedUploads)
            {
                int maxConcurrency = Math.Min(Math.Max(Environment.ProcessorCount, MinConcurrentUploads), MaxConcurrentUploads);
                _uploadSemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            }
        }

        public FileContainerService(IFileContainerClientHelper fileContainerHelper, IPipelinesExecutionContext context)
        {
            _fileContainerHelper = fileContainerHelper;
            _context = context;
            
            if (UseOptimizedUploads)
            {
                int maxConcurrency = Math.Min(Math.Max(Environment.ProcessorCount, MinConcurrentUploads), MaxConcurrentUploads);
                _uploadSemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            }
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
                    List<string> failedFiles = await ParallelUploadAsync(files, sourceParentDirectory, containerPath, maxConcurrentUploads, uploadCancellationTokenSource.Token);

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
                    failedFiles = await ParallelUploadAsync(failedFiles, sourceParentDirectory, containerPath, maxConcurrentUploads, uploadCancellationTokenSource.Token);

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

            if (UseOptimizedUploads)
            {
                TraceLogger.Info("Using OPTIMIZED upload path with enhanced concurrency and retry logic");
                return await ParallelUploadOptimizedAsync(files, sourceParentDirectory, containerPath, concurrentUploads, cancellationToken);
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
            TraceLogger.Info($"Optimized upload: Processing {files.Count} files with max concurrency of {_uploadSemaphore?.CurrentCount ?? 0}");

            List<string> failedFiles = new List<string>();

            // ensure the file upload queue is empty.
            if (!_fileUploadQueue.IsEmpty)
            {
                throw new ArgumentOutOfRangeException(nameof(_fileUploadQueue));
            }

            // Enqueue all files
            foreach (var file in files)
            {
                _fileUploadQueue.Enqueue(file);
            }

            filesProcessed = 0;
            var uploadFinished = new TaskCompletionSource<int>();
            _fileUploadTraceLog.Clear();
            _fileUploadProgressLog.Clear();

            Task uploadMonitor = ReportingAsync(files.Count, uploadFinished, cancellationToken);

            // Create upload tasks with optimized approach
            var uploadTasks = new List<Task<List<string>>>();
            int actualConcurrency = Math.Min(concurrentUploads, files.Count);

            for (int i = 0; i < actualConcurrency; i++)
            {
                uploadTasks.Add(UploadWorkerOptimizedAsync(sourceParentDirectory, containerPath, cancellationToken));
            }

            await Task.WhenAll(uploadTasks);

            foreach (var task in uploadTasks)
            {
                failedFiles.AddRange(await task);
            }

            uploadFinished.TrySetResult(0);
            await uploadMonitor;

            return failedFiles;
        }

        private async Task<List<string>> UploadWorkerOptimizedAsync(string sourceParentDirectory, string containerPath, CancellationToken cancellationToken)
        {
            var failedFiles = new List<string>();
            var containerId = _context.ContainerId;
            var projectId = _context.ProjectId;
            int fileCount = 0;

            while (_fileUploadQueue.TryDequeue(out string fileToUpload))
            {
                fileCount++;
                TraceLogger.Debug($"Optimized worker: Processing file #{fileCount}: {Path.GetFileName(fileToUpload)}");
                
                cancellationToken.ThrowIfCancellationRequested();
                
                await _uploadSemaphore.WaitAsync(cancellationToken);
                try
                {
                    var success = await UploadFileWithRetryAsync(fileToUpload, sourceParentDirectory, containerPath, containerId, projectId, cancellationToken);
                    if (!success)
                    {
                        failedFiles.Add(fileToUpload);
                    }
                    Interlocked.Increment(ref filesProcessed);
                }
                finally
                {
                    _uploadSemaphore.Release();
                }
            }

            TraceLogger.Debug($"Optimized worker completed: Processed {fileCount} files, {failedFiles.Count} failed");
            return failedFiles;
        }

        private async Task<bool> UploadFileWithRetryAsync(string fileToUpload, string sourceParentDirectory, string containerPath, long containerId, Guid projectId, CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            var delay = TimeSpan.FromSeconds(1);

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        await Task.Delay(delay, cancellationToken);
                        delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
                    }

                    var itemPath = (containerPath.TrimEnd('/') + "/" + fileToUpload.Remove(0, sourceParentDirectory.Length + 1)).Replace('\\', '/');

                    using (var fs = File.Open(fileToUpload, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var response = await _fileContainerHelper.UploadFileAsync(containerId, itemPath, fs, projectId, cancellationToken, chunkSize: DefaultChunkSize);

                        if (response.StatusCode == HttpStatusCode.Created)
                        {
                            TraceLogger.Debug($"Successfully uploaded {fileToUpload}");

                            // Log trace information
                            if (_fileUploadTraceLog.TryGetValue(itemPath, out var logQueue))
                            {
                                while (logQueue.TryDequeue(out var message))
                                {
                                    TraceLogger.Debug(message);
                                }
                            }

                            return true;
                        }
                        else
                        {
                            TraceLogger.Warning($"Upload returned {response.StatusCode} for {fileToUpload}");
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    TraceLogger.Warning($"Upload attempt {attempt + 1} failed for {fileToUpload}: {ex.Message}");
                    if (attempt == maxRetries - 1)
                    {
                        TraceLogger.Error($"Upload failed after {maxRetries} attempts for {fileToUpload}: {ex}");
                        return false;
                    }
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
                        var response = await _fileContainerHelper.UploadFileAsync(containerId, itemPath, fs, projectId, cancellationToken, chunkSize: DefaultChunkSize);

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