using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    public interface ILogStoreHelper
    {
        Task<TestLogStatus> UploadTestBuildLogAsync(Guid projectId, int buildId, TestLogType logType, string logFileSourcePath, Dictionary<string, string> metaData, string destDirectoryPath, bool allowDuplicate, CancellationToken cancellationToken);
    }
}
