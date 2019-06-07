using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    internal class PipelinesExecutionContext: IExecutionContext
    {
        public ILogger Logger { get; private set; }

        public PipelinesExecutionContext()
        {
            Logger = new PipelinesLogger();
        }


        public long ContainerId
        {
            get
            {
                long.TryParse(Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.BuildContainerId), out long result);
                return result;
            }
        }

        public string AccessToken
        {
            get
            {
                var accessToken = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.AccessToken);
                return string.IsNullOrEmpty(accessToken) ? "" : accessToken;
            }
        }
    }
}
