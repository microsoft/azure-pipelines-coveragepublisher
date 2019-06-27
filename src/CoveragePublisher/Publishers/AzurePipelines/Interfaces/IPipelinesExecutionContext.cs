using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using System;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.DefaultPublisher
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
