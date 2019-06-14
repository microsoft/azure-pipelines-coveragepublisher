using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.VisualStudio.Services.FileContainer.Client;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    internal class FileContainerService
    {
        private readonly ConcurrentQueue<string> _fileUploadQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _fileUploadTraceLog = new ConcurrentDictionary<string, ConcurrentQueue<string>>();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _fileUploadProgressLog = new ConcurrentDictionary<string, ConcurrentQueue<string>>();
        private readonly IFileContainerClientHelper _fileContainerHelper;

        private Guid projectId;
        private long containerId;
        private string containerPath;
        private int filesProcessed = 0;

        public FileContainerService(IClientFactory clientFactory, Guid projectId, long containerId, string containerPath)
        {
            this.projectId = projectId;
            this.containerId = containerId;
            this.containerPath = containerPath;

            _fileContainerHelper = new FileContainerClientHelper(clientFactory);
        }

        public FileContainerService(IFileContainerClientHelper fileContainerHelper, Guid projectId, long containerId, string containerPath)
        {
            this.projectId = projectId;
            this.containerId = containerId;
            this.containerPath = containerPath;

            this._fileContainerHelper = fileContainerHelper;
        }

        /// <summary>
        /// Copy files to container.
        /// </summary>
        /// <param name="logger"><see cref="ILogger"/> instance.</param>
        /// <param name="uploadDirectory">Path to file or directory.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
        /// <param name="retryDelay">Delay when retrying.</param>
        /// <returns></returns>
        public async Task CopyToContainerAsync(ILogger logger, string uploadDirectory, CancellationToken cancellationToken, bool retryDelay = true)
        {
            int maxConcurrentUploads = Math.Max(Environment.ProcessorCount / 2, 1);
            string sourceParentDirectory;

            List<string> files;
            files = Directory.EnumerateFiles(uploadDirectory, "*", SearchOption.AllDirectories).ToList();
            sourceParentDirectory = uploadDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            logger.Info(string.Format(Resources.TotalUploadFiles, files.Count()));

            using (var uploadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                // hook up reporting event from file container client.
                _fileContainerHelper.UploadFileReportTrace += UploadFileTraceReportReceived;
                _fileContainerHelper.UploadFileReportProgress += UploadFileProgressReportReceived;

                try
                {
                    // try upload all files for the first time.
                    List<string> failedFiles = await ParallelUploadAsync(logger, files, sourceParentDirectory, maxConcurrentUploads, uploadCancellationTokenSource.Token);

                    if (failedFiles.Count == 0)
                    {
                        // all files have been upload succeed.
                        logger.Info(Resources.FileUploadSucceed);
                        return;
                    }
                    else
                    {
                        logger.Info(string.Format(Resources.FileUploadFailedRetryLater, failedFiles.Count));
                    }

                    if (retryDelay)
                    {
                        // Delay 1 min then retry failed files.
                        for (int timer = 60; timer > 0; timer -= 5)
                        {
                            logger.Info(string.Format(Resources.FileUploadRetryInSecond, timer));
                            await Task.Delay(TimeSpan.FromSeconds(5), uploadCancellationTokenSource.Token);
                        }
                    }

                    // Retry upload all failed files.
                    logger.Info(string.Format(Resources.FileUploadRetry, failedFiles.Count));
                    failedFiles = await ParallelUploadAsync(logger, failedFiles, sourceParentDirectory, maxConcurrentUploads, uploadCancellationTokenSource.Token);

                    if (failedFiles.Count == 0)
                    {
                        // all files have been upload succeed after retry.
                        logger.Info(Resources.FileUploadRetrySucceed);
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
        /// <param name="logger"><see cref="ILogger"/> instance</param>
        /// <param name="files">List of files to be uploaded.</param>
        /// <param name="sourceParentDirectory">Path to the parent directory of the files.</param>
        /// <param name="concurrentUploads">Concurrency value.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
        /// <returns>List of files failed to upload.</returns>
        private async Task<List<string>> ParallelUploadAsync(ILogger logger, List<string> files, string sourceParentDirectory, int concurrentUploads, CancellationToken cancellationToken)
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
            Task uploadMonitor = ReportingAsync(logger, files.Count(), uploadFinished, cancellationToken);

            // Start parallel upload tasks.
            List<Task<List<string>>> parallelUploadingTasks = new List<Task<List<string>>>();
            for (int uploader = 0; uploader < concurrentUploads; uploader++)
            {
                parallelUploadingTasks.Add(UploadAsync(logger, uploader, sourceParentDirectory, cancellationToken));
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

        private async Task<List<string>> UploadAsync(ILogger logger, int uploaderId, string sourceParentDirectory, CancellationToken cancellationToken)
        {
            List<string> failedFiles = new List<string>();
            string fileToUpload;
            Stopwatch uploadTimer = new Stopwatch();
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
                            logger.Info(string.Format(Resources.FileUploadCancelled, fileToUpload));
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
                            logger.Info(string.Format(Resources.FileUploadFailed, fileToUpload, ex.Message));
                            logger.Info(ex.ToString());
                        }

                        uploadTimer.Stop();
                        if (coughtExceptionDuringUpload || (response != null && response.StatusCode != HttpStatusCode.Created))
                        {
                            if (response != null)
                            {
                                logger.Info(string.Format(Resources.FileContainerUploadFailed, response.StatusCode, response.ReasonPhrase, fileToUpload, itemPath));
                            }

                            // output detail upload trace for the file.
                            ConcurrentQueue<string> logQueue;
                            if (_fileUploadTraceLog.TryGetValue(itemPath, out logQueue))
                            {
                                logger.Info(string.Format(Resources.FileUploadDetailTrace, itemPath));
                                string message;
                                while (logQueue.TryDequeue(out message))
                                {
                                    logger.Info(message);
                                }
                            }

                            // tracking file that failed to upload.
                            failedFiles.Add(fileToUpload);
                        }
                        else
                        {
                            logger.Debug(string.Format(Resources.FileUploadFinish, fileToUpload, uploadTimer.ElapsedMilliseconds));

                            // debug detail upload trace for the file.
                            ConcurrentQueue<string> logQueue;
                            if (_fileUploadTraceLog.TryGetValue(itemPath, out logQueue))
                            {
                                logger.Debug($"Detail upload trace for file: {itemPath}");
                                string message;
                                while (logQueue.TryDequeue(out message))
                                {
                                    logger.Debug(message);
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
                    logger.Info(string.Format(Resources.FileUploadFileOpenFailed, ex.Message, fileToUpload));
                    throw ex;
                }
            }

            return failedFiles;
        }

        private async Task ReportingAsync(ILogger logger, int totalFiles, TaskCompletionSource<int> uploadFinished, CancellationToken token)
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
                        logger.Info(message);
                    }
                }

                // trace total file progress every 25 seconds when there is no file level detail progress
                if (++traceInterval % 2 == 0 && !hasDetailProgress)
                {
                    logger.Info(string.Format(Resources.FileUploadProgress, totalFiles, filesProcessed, (filesProcessed * 100) / totalFiles));
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
