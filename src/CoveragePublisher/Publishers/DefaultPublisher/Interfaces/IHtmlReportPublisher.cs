// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    public interface IHtmlReportPublisher
    {
        /// <summary>
        /// Publish code coverage files as build artifacts
        /// </summary>
        /// <param name="reportDirectory">Path to report directory.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        Task PublishHTMLReportAsync(string reportDirectory, CancellationToken cancellationToken);
    }
}
