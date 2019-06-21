// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    public interface IClientFactory
    {
        /// <summary>
        /// Get the instance of <see cref="VssConnection"/>.
        /// </summary>
        VssConnection VssConnection { get; }

        /// <summary>
        /// Access any pipeline client through factory.
        /// </summary>
        T GetClient<T>() where T : VssHttpClientBase;

        /// <summary>
        /// Access any pipeline client through factory.
        /// </summary>
        /// <param name="serviceIdentifier">Guid for service instance.</param>
        T GetClient<T>(Guid serviceIdentifier) where T : VssHttpClientBase;

        /// <summary>
        /// Access any pipeline client through factory.
        /// </summary>
        /// <param name="settings">Custom request settings.</param>
        T GetClient<T>(VssClientHttpRequestSettings settings) where T : VssHttpClientBase;
    }

}
