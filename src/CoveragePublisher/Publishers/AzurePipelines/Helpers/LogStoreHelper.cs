using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.TestClient.PublishTestResults;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    internal class LogStoreHelper: ILogStoreHelper
    {
        private TestLogStore _logStore;

        public LogStoreHelper(IClientFactory clientFactory)
        {
            _logStore = new TestLogStore(clientFactory.VssConnection, new CoveragePublisherTraceListener());
        }

        public Task<TestLogStatus> UploadTestBuildLogAsync(Guid projectId, int buildId, TestLogType logType, string logFileSourcePath, Dictionary<string, string> metaData, string destDirectoryPath, bool allowDuplicate, CancellationToken cancellationToken)
        {
            return _logStore.UploadTestBuildLogAsync(projectId, buildId, logType, logFileSourcePath, metaData, destDirectoryPath, allowDuplicate, cancellationToken);
        }
    }
}
