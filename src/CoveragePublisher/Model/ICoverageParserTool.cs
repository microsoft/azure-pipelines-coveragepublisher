// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    /// <summary>
    /// Model for tools that understand coverage formats and parse to IList<FileCoverageInfo>.
    /// </summary>
    public interface ICoverageParserTool
    {
        /// <summary>
        /// Get coverage information for individual files.
        /// </summary>
        /// <returns>List of <see cref="FileCoverageInfo"/></returns>
        List<FileCoverageInfo> GetFileCoverageInfos();

        /// <summary>
        /// Get coverage information for individual files. Specifically used for .coverage/.covx scenarios
        /// </summary>
        /// <returns>List of <see cref="FileCoverageInfo"/></returns>
        List<FileCoverageInfo> GetFileCoverageInfos(CancellationToken token);

        /// <summary>
        /// Get coverage summary, contains combined coverage summary data.
        /// </summary>
        /// <returns><see cref="CoverageSummary"/></returns>
        CoverageSummary GetCoverageSummary();

        /// <summary>
        /// Generate HTML report from the already parsed result.
        /// </summary>
        void GenerateHTMLReport();
    }
}
