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

        private int filesProcessed = 0;

        public FileContainerService(IClientFactory clientFactory, IPipelinesExecutionContext context)
        {
            _fileContainerHelper = new FileContainerClientHelper(clientFactory);
            _context = context;
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
            var failedFiles = new ConcurrentBag<string>();

            if (files.Count == 0)
                return failedFiles.ToList();

            var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true
            });

            // Producer: write all files into the channel
            _ = Task.Run(async () =>
            {
                foreach (var file in files)
                {
                    await channel.Writer.WriteAsync(file, cancellationToken);
                }

                channel.Writer.Complete();
            }, cancellationToken);

            // Consumers: upload files concurrently
            var uploadTasks = Enumerable.Range(0, concurrentUploads).Select(_ =>
                Task.Run(() => UploadFromChannelAsync(channel.Reader, sourceParentDirectory, containerPath, failedFiles, cancellationToken))
            ).ToArray();

            await Task.WhenAll(uploadTasks);
            return failedFiles.ToList();
        }

        private async Task UploadFromChannelAsync(ChannelReader<string> reader, string sourceParentDirectory, string containerPath, ConcurrentBag<string> failedFiles, CancellationToken cancellationToken)
        {
            while (await reader.WaitToReadAsync(cancellationToken))
            {
                while (reader.TryRead(out var fileToUpload))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var relativePath = fileToUpload.Substring(sourceParentDirectory.Length + 1).Replace('\\', '/');
                        var itemPath = containerPath.TrimEnd('/') + "/" + relativePath;

                        bool success = false;
                        int retry = 0;
                        while (retry < MaxRetries && !success)
                        {
                            try
                            {
                                using var fs = new FileStream(fileToUpload, FileMode.Open, FileAccess.Read, FileShare.Read);
                                var response = await _fileContainerHelper.UploadFileAsync(_context.ContainerId, itemPath, fs, _context.ProjectId, cancellationToken, UploadChunkSize);

                                if (response != null && response.StatusCode == HttpStatusCode.Created)
                                {
                                    success = true;
                                }
                                else
                                {
                                    retry++;
                                    await Task.Delay(1000 * retry, cancellationToken);
                                }
                            }
                            catch (Exception ex) when (retry < MaxRetries)
                            {
                                retry++;
                                await Task.Delay(1000 * retry, cancellationToken);
                                TraceLogger.Warning($"Retry {retry} for file {fileToUpload}: {ex.Message}");
                            }
                        }

                        if (!success)
                        {
                            failedFiles.Add(fileToUpload);
                            TraceLogger.Error($"Upload failed after {MaxRetries} retries: {fileToUpload}");
                        }
                        else
                        {
                            Interlocked.Increment(ref filesProcessed);
                        }
                    }
                    catch (Exception ex)
                    {
                        TraceLogger.Error($"Fatal error on file {fileToUpload}: {ex.Message}");
                        failedFiles.Add(fileToUpload);
                    }
                }
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
