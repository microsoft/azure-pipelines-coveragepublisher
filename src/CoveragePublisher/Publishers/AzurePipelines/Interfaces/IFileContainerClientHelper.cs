using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.FileContainer.Client;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.DefaultPublisher
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
