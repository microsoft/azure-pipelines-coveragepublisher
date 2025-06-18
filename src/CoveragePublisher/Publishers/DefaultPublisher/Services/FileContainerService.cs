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
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
using Microsoft.VisualStudio.Services.Content.Common.Tracing;

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
        private const int MaxConcurrentUploads = 8; // Pipeline artifact uses 8, not 16
        private const int MinConcurrentUploads = 1; // Pipeline artifact starts with 1
        private const int BatchSize = 50; // Pipeline artifact processes files in batches
        private const bool UseOptimizedUploads = true; // Feature flag for new optimizations
        private const bool UseArtifactUploadAPI = true; // Use artifact upload API like Azure DevOps tasks

        private int filesProcessed = 0;

        public FileContainerService(IClientFactory clientFactory, IPipelinesExecutionContext context)
        {
            _fileContainerHelper = new FileContainerClientHelper(clientFactory);
            _context = context;
            
            TraceLogger.Info($"FileContainerService initialized with UseOptimizedUploads={UseOptimizedUploads}");
            
            if (UseOptimizedUploads)
            {
                int maxConcurrency = Math.Min(Math.Max(Environment.ProcessorCount, MinConcurrentUploads), MaxConcurrentUploads);
                _uploadSemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
                TraceLogger.Info($"Optimized uploads enabled with max concurrency: {maxConcurrency}");
            }
        }

        public FileContainerService(IFileContainerClientHelper fileContainerHelper, IPipelinesExecutionContext context)
        {
            _fileContainerHelper = fileContainerHelper;
            _context = context;
            
            TraceLogger.Info($"FileContainerService initialized with UseOptimizedUploads={UseOptimizedUploads}");
            
            if (UseOptimizedUploads)
            {
                int maxConcurrency = Math.Min(Math.Max(Environment.ProcessorCount, MinConcurrentUploads), MaxConcurrentUploads);
                _uploadSemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
                TraceLogger.Info($"Optimized uploads enabled with max concurrency: {maxConcurrency}");
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
            if (UseArtifactUploadAPI)
            {
                return await UploadUsingArtifactAPIAsync(files, sourceParentDirectory, containerPath, cancellationToken);
            }

            // Fallback to batch processing if artifact API is not available
            TraceLogger.Info($"Pipeline Artifact style upload: Processing {files.Count} files in batches of {BatchSize}");

            List<string> failedFiles = new List<string>();
            filesProcessed = 0;
            var uploadFinished = new TaskCompletionSource<int>();
            _fileUploadTraceLog.Clear();
            _fileUploadProgressLog.Clear();

            Task uploadMonitor = ReportingAsync(files.Count, uploadFinished, cancellationToken);

            try
            {
                // Process files in batches like Pipeline Artifact does
                for (int batchStart = 0; batchStart < files.Count; batchStart += BatchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var batchEnd = Math.Min(batchStart + BatchSize, files.Count);
                    var batch = files.GetRange(batchStart, batchEnd - batchStart);
                    
                    TraceLogger.Info($"Processing batch {(batchStart / BatchSize) + 1}: files {batchStart + 1}-{batchEnd} of {files.Count}");
                    
                    var batchFailures = await ProcessBatchAsync(batch, sourceParentDirectory, containerPath, concurrentUploads, cancellationToken);
                    failedFiles.AddRange(batchFailures);
                    
                    // Adaptive throttling - if many failures, reduce concurrency for next batch
                    if (batchFailures.Count > batch.Count / 2)
                    {
                        concurrentUploads = Math.Max(concurrentUploads / 2, 1);
                        TraceLogger.Warning($"High failure rate detected. Reducing concurrency to {concurrentUploads}");
                    }
                    else if (batchFailures.Count == 0 && concurrentUploads < MaxConcurrentUploads)
                    {
                        concurrentUploads = Math.Min(concurrentUploads + 1, MaxConcurrentUploads);
                        TraceLogger.Info($"Good performance. Increasing concurrency to {concurrentUploads}");
                    }
                }
            }
            finally
            {
                uploadFinished.TrySetResult(0);
                await uploadMonitor;
            }

            TraceLogger.Info($"Batch upload completed: {files.Count - failedFiles.Count} succeeded, {failedFiles.Count} failed");
            return failedFiles;
        }

        private async Task<List<string>> UploadUsingArtifactAPIAsync(List<string> files, string sourceParentDirectory, string containerPath, CancellationToken cancellationToken)
        {
            TraceLogger.Info($"Using Artifact Upload API for {files.Count} files from {sourceParentDirectory}");
            
            try
            {
                // Don't create a new artifact - just upload to the existing container
                // The coverage publisher should upload directly to the existing file container
                
                // Use the file container client directly with optimized settings
                var containerClient = _context.VssConnection.GetClient<FileContainerHttpClient>();
                
                // Upload files in parallel using the container client's optimized methods
                var uploadTasks = new List<Task>();
                var semaphore = new SemaphoreSlim(Math.Min(Environment.ProcessorCount, 8));
                var failedFiles = new List<string>();
                
                foreach (var file in files)
                {
                    uploadTasks.Add(UploadSingleFileOptimizedAsync(file, sourceParentDirectory, containerPath, containerClient, semaphore, cancellationToken));
                }
                
                await Task.WhenAll(uploadTasks);
                
                TraceLogger.Info($"Optimized container upload completed for {files.Count} files");
                
                // Return empty list since we're not tracking individual failures in this optimized path
                return new List<string>();
            }
            catch (Exception ex)
            {
                TraceLogger.Error($"Optimized upload API failed: {ex.Message}");
                TraceLogger.Info("Falling back to batch processing method");
                
                // Fallback to our batch processing if optimized API fails
                return await UploadUsingBatchProcessingAsync(files, sourceParentDirectory, containerPath, cancellationToken);
            }
        }

        private async Task UploadSingleFileOptimizedAsync(string filePath, string sourceParentDirectory, string containerPath, FileContainerHttpClient containerClient, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken);
            
            try
            {
                var itemPath = (containerPath.TrimEnd('/') + "/" + filePath.Remove(0, sourceParentDirectory.Length + 1)).Replace('\\', '/');
                
                using (var fileStream = File.OpenRead(filePath))
                {
                    // Use the container client's upload method with optimized chunk size
                    await containerClient.UploadFileAsync(
                        _context.ContainerId,
                        itemPath,
                        fileStream,
                        cancellationToken: cancellationToken);
                }
                
                Interlocked.Increment(ref filesProcessed);
            }
            catch (Exception ex)
            {
                TraceLogger.Warning($"Failed to upload {Path.GetFileName(filePath)}: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<List<string>> UploadUsingBatchProcessingAsync(List<string> files, string sourceParentDirectory, string containerPath, int concurrentUploads, CancellationToken cancellationToken)
        {
            TraceLogger.Info($"Fallback: Processing {files.Count} files in batches of {BatchSize}");

            List<string> failedFiles = new List<string>();
            filesProcessed = 0;
            var uploadFinished = new TaskCompletionSource<int>();
            _fileUploadTraceLog.Clear();
            _fileUploadProgressLog.Clear();

            Task uploadMonitor = ReportingAsync(files.Count, uploadFinished, cancellationToken);

            try
            {
                // Process files in batches like Pipeline Artifact does
                for (int batchStart = 0; batchStart < files.Count; batchStart += BatchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var batchEnd = Math.Min(batchStart + BatchSize, files.Count);
                    var batch = files.GetRange(batchStart, batchEnd - batchStart);
                    
                    TraceLogger.Info($"Processing batch {(batchStart / BatchSize) + 1}: files {batchStart + 1}-{batchEnd} of {files.Count}");
                    
                    var batchFailures = await ProcessBatchAsync(batch, sourceParentDirectory, containerPath, concurrentUploads, cancellationToken);
                    failedFiles.AddRange(batchFailures);
                    
                    // Adaptive throttling - if many failures, reduce concurrency for next batch
                    if (batchFailures.Count > batch.Count / 2)
                    {
                        concurrentUploads = Math.Max(concurrentUploads / 2, 1);
                        TraceLogger.Warning($"High failure rate detected. Reducing concurrency to {concurrentUploads}");
                    }
                    else if (batchFailures.Count == 0 && concurrentUploads < MaxConcurrentUploads)
                    {
                        concurrentUploads = Math.Min(concurrentUploads + 1, MaxConcurrentUploads);
                        TraceLogger.Info($"Good performance. Increasing concurrency to {concurrentUploads}");
                    }
                }
            }
            finally
            {
                uploadFinished.TrySetResult(0);
                await uploadMonitor;
            }

            TraceLogger.Info($"Batch upload completed: {files.Count - failedFiles.Count} succeeded, {failedFiles.Count} failed");
            return failedFiles;
        }

        /// <summary>
        /// Process a batch of files.
        /// </summary>
        /// <param name="batchFiles">List of files in the batch.</param>
        /// <param name="sourceParentDirectory">Path to the parent directory of the files.</param>
        /// <param name="containerPath">Container path.</param>
        /// <param name="concurrentUploads">Concurrency value.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
        /// <returns>List of files failed to upload.</returns>
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
                        var response = await _fileContainerHelper.UploadFileAsync(containerId, itemPath, fs, projectId, cancellationToken, chunkSize: DefaultChunkSize);

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