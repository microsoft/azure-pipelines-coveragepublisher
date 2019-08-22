// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    public class ServiceFactory
    {
        public virtual BuildService GetBuildService(IClientFactory clientFactory, IPipelinesExecutionContext executionContext)
        {
            return new BuildService(clientFactory, executionContext.ProjectId);
        }

        public virtual FileContainerService GetFileContainerService(IClientFactory clientFactory, IPipelinesExecutionContext executionContext)
        {
            return new FileContainerService(clientFactory, executionContext);
        }
    }
}
