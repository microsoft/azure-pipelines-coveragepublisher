using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    internal static class Constants
    {
        internal static class EnvironmentVariables
        {
            public const string BuildContainerId = "Build.ContainerId";
            public const string AccessToken = "System.AccessToken";
        }
    }
}
