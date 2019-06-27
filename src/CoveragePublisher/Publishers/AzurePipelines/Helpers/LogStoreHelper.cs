using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.TestClient.PublishTestResults;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.DefaultPublisher
{
    internal class LogStoreHelper: ILogStoreHelper
    {
        private TestLogStore _logStore;

        public LogStoreHelper(IClientFactory clientFactory)
        {
            _logStore = new TestLogStore(clientFactory.VssConnection, new LogStoreTraceListener());
        }

        public Task<TestLogStatus> UploadTestBuildLogAsync(Guid projectId, int buildId, TestLogType logType, string logFileSourcePath, Dictionary<string, string> metaData, string destDirectoryPath, bool allowDuplicate, CancellationToken cancellationToken)
        {
            return _logStore.UploadTestBuildLogAsync(projectId, buildId, logType, logFileSourcePath, metaData, destDirectoryPath, allowDuplicate, cancellationToken);
        }
    }

    internal class LogStoreTraceListener : TraceListener
    {
        public override void Write(string message)
        {
            TraceLogger.Debug(message);
        }

        public override void WriteLine(string message)
        {
            TraceLogger.Debug(message);
        }
    }
}
