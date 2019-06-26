using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    public interface IPipelinesExecutionContext: IExecutionContext
    {
        int BuildId { get; }

        long ContainerId { get; }

        string AccessToken { get; }
        
        string CollectionUri { get; }

        Guid ProjectId { get; }

        string TempPath { get; }
    }
}
