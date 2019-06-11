using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    internal class PipelinesExecutionContext: IExecutionContext
    {
        public ILogger ConsoleLogger { get; private set; }

        public PipelinesExecutionContext()
        {
            ConsoleLogger = new PipelinesLogger();
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

                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new ArgumentNullException("AccessToken envrionment variable was null or empty.");
                }

                return accessToken;
            }
        }
    }
}
