// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public class ClientFactory : IClientFactory
    {
        public ClientFactory(VssConnection vssConnection)
        {
            _vssConnection = vssConnection;
        }

        /// <inheritdoc />
        public virtual T GetClient<T>() where T : VssHttpClientBase
        {
            return _vssConnection.GetClient<T>();
        }

        private readonly VssConnection _vssConnection;
    }
}
