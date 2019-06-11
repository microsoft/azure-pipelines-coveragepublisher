// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

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
        /// <param name="coverageFiles">List of FileCoverageInfo objects.</param>
        void PublishFileCoverage(IList<FileCoverageInfo> coverageFiles);

        /// <summary>
        /// Publish coverage summary.
        /// </summary>
        /// <param name="coverageSummary">CoverageSummary object.</param>
        void PublishCoverageSummary(CoverageSummary coverageSummary);

        /// <summary>
        /// Publish coverage HTML report.
        /// </summary>
        /// <param name="reportDirectory">Path to coverage report directory.</param>
        void PublishHTMLReport(string reportDirectory);
    }
}
