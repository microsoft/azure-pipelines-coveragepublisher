// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    internal class PipelinesExecutionContext: IPipelinesExecutionContext
    {
        private int buildId = -1;
        private long containerId = -1;
        private string accessToken = null;
        private Guid? projectId = null;
        private string collectionUri = null;
        private string tempPath = null;

        public PipelinesExecutionContext()
        {
            Logger = new PipelinesLogger();
        }

        public ILogger Logger { get; private set; }

        public ITelemetryDataCollector TelemetryDataCollector { get; private set; }

        public void SetTelemetryDataCollector(ITelemetryDataCollector telemetry)
        {
            // only keep the first set value
            if(TelemetryDataCollector == null)
            {
                TelemetryDataCollector = telemetry;
            }
        }

        public int BuildId
        {
            get
            {
                if (buildId == -1)
                {
                    int.TryParse(Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.BuildId) ?? "", out buildId);
                }
                return buildId;
            }
        }

        public long ContainerId
        {
            get
            {
                if (containerId == -1)
                {
                    long.TryParse(Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.BuildContainerId) ?? "", out containerId);
                }
                return containerId;
            }
        }

        public string AccessToken
        {
            get
            {
                if (accessToken == null)
                {
                    accessToken = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.AccessToken) ?? "";
                }

                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception(string.Format(Resources.EnvVarNullOrEmpty, Constants.EnvironmentVariables.AccessToken));
                }

                return accessToken;
            }
        }

        public string CollectionUri
        {
            get
            {
                if (collectionUri == null)
                {
                    collectionUri = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.CollectionUri) ?? "";
                }

                if (string.IsNullOrEmpty(collectionUri))
                {
                    throw new Exception(string.Format(Resources.EnvVarNullOrEmpty, Constants.EnvironmentVariables.CollectionUri));
                }

                return collectionUri;
            }
        }

        public Guid ProjectId
        {
            get
            {
                if (projectId == null)
                {
                    Guid.TryParse(Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ProjectId) ?? "", out Guid parsedGuid);
                    projectId = parsedGuid;
                }
                return (Guid)projectId;
            }
        }

        public string TempPath
        {
            get
            {
                if (tempPath == null)
                {
                    tempPath = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.AgentTempPath) ?? "";

                    if(string.IsNullOrEmpty(tempPath) || !Directory.Exists(tempPath))
                    {
                        tempPath = Path.GetTempPath();
                    }
                }
                return tempPath;
            }
        }
    }
}
