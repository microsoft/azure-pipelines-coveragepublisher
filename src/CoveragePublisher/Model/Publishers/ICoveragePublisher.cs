// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    /// <summary>
    /// Model for coverage publisher
    /// </summary>
    public interface ICoveragePublisher
    {
        /// <summary>
        /// Publish individual file coverage data.
        /// </summary>
        /// <param name="coverageInfos">List of FileCoverageInfo objects.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        Task PublishFileCoverage(IList<FileCoverageInfo> coverageInfos, CancellationToken cancellationToken);

        /// <summary>
        /// Publish all native coverage files to log store
        /// </summary>
        /// <param name="nativeCoverageFiles">List of native coverage files</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns></returns>
        Task PublishNativeCoverageFiles(IList<string> nativeCoverageFiles, CancellationToken cancellationToken);

        /// <summary>
        /// Publish coverage summary.
        /// </summary>
        /// <param name="coverageSummary">CoverageSummary object.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        Task PublishCoverageSummary(CoverageSummary coverageSummary, CancellationToken cancellationToken);

        /// <summary>
        /// Publish coverage HTML report.
        /// </summary>
        /// <param name="reportDirectory">Path to coverage report directory.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        Task PublishHTMLReport(string reportDirectory, CancellationToken cancellationToken);

        /// <summary>
        /// Gets weather we can upload native file to tcm logstore.
        /// </summary>
        /// <returns></returns>
        bool IsUploadNativeFilesToTCMSupported();
    }
}