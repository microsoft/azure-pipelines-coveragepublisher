using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.FileContainer.Client;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    /// <summary>
    /// We require FileContainerClientHelper because FileContainerHttpClient does not define its method as virtual
    /// </summary>
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

        public async Task<HttpResponseMessage> UploadFileAsync(
            long containerId,
            string itemPath,
            Stream fileStream,
            Guid scopeIdentifier,
            CancellationToken cancellationToken,
            int chunkSize)
        {
            return await _client.UploadFileAsync(containerId, itemPath, fileStream, scopeIdentifier, cancellationToken, chunkSize: chunkSize);
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
