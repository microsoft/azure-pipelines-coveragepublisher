// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    public class ClientFactory : IClientFactory
    {
        public VssConnection VssConnection { get; private set; }

        public ClientFactory(VssConnection vssConnection)
        {
            VssConnection = vssConnection;
        }

        public T GetClient<T>() where T : VssHttpClientBase
        {
            return VssConnection.GetClient<T>();
        }

        public T GetClient<T>(VssClientHttpRequestSettings settings) where T : VssHttpClientBase
        {
            var connection = new VssConnection(VssConnection.Uri, VssConnection.Credentials, settings);
            return VssConnection.GetClient<T>();
        }
    }
}
