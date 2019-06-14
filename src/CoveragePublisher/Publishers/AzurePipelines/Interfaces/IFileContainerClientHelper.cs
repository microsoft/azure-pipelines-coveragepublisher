using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.FileContainer.Client;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    public interface IFileContainerClientHelper
    {
        event EventHandler<ReportTraceEventArgs> UploadFileReportTrace;
        event EventHandler<ReportProgressEventArgs> UploadFileReportProgress;

        Task<HttpResponseMessage> UploadFileAsync(
            long containerId,
            string itemPath,
            Stream fileStream,
            Guid scopeIdentifier,
            CancellationToken cancellationToken,
            int chunkSize);
    }
}
