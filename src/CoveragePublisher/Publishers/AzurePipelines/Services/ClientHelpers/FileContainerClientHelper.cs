using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.FileContainer.Client;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    public class FileContainerClientHelper: IFileContainerClientHelper
    {
        private FileContainerHttpClient _client;

        public event EventHandler<ReportTraceEventArgs> UploadFileReportTrace;
        public event EventHandler<ReportProgressEventArgs> UploadFileReportProgress;

        public FileContainerClientHelper(IClientFactory clientFactory)
        {
            var connection = clientFactory.VssConnection;

            // default file upload request timeout to 600 seconds
            var fileContainerClientConnectionSetting = connection.Settings.Clone();
            if (fileContainerClientConnectionSetting.SendTimeout < TimeSpan.FromSeconds(600))
            {
                fileContainerClientConnectionSetting.SendTimeout = TimeSpan.FromSeconds(600);
            }

            _client = clientFactory.GetClient<FileContainerHttpClient>(fileContainerClientConnectionSetting);

            _client.UploadFileReportProgress += InvokeClientUploadFileReportProgress;
            _client.UploadFileReportTrace += InvokeClientUploadFileReportTrace;
        }

        public Task<HttpResponseMessage> UploadFileAsync(
            long containerId,
            string itemPath,
            Stream fileStream,
            Guid scopeIdentifier,
            CancellationToken cancellationToken,
            int chunkSize)
        {
            return _client.UploadFileAsync(containerId, itemPath, fileStream, scopeIdentifier, cancellationToken, chunkSize: chunkSize);
        }

        protected void InvokeClientUploadFileReportProgress(object sender, ReportProgressEventArgs e)
        {
            UploadFileReportProgress.Invoke(sender, e);
        }

        protected void InvokeClientUploadFileReportTrace(object sender, ReportTraceEventArgs e)
        {
            UploadFileReportTrace.Invoke(sender, e);
        }
    }
}
