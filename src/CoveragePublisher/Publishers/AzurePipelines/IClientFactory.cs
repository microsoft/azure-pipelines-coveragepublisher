// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public interface IClientFactory
    {
        /// <summary>
        /// Access any pipeline client through factory
        /// </summary>
        T GetClient<T>() where T : VssHttpClientBase;
    }

}
