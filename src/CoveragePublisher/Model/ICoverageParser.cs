// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    /// <summary>
    /// Model for tools that understand coverage formats and parse to IList<FileCoverageInfo>.
    /// </summary>
    internal interface ICoverageParser
    {
        /// <summary>
        /// Get coverage report, contains coverage information for individual files along with path to report directory.
        /// </summary>
        /// <param name="coverageFiles">List of xml coverage files.</param>
        /// <returns></returns>
        List<FileCoverageInfo> GetFileCoverageInfos(List<string> coverageFiles);
        
        /// <summary>
        /// Get coverage summary, contains combined coverage summary data along with path to report directory.
        /// </summary>
        /// <param name="coverageFiles">List of xml coverage files.</param>
        /// <returns></returns>
        CoverageSummary GetCoverageSummary(List<string> coverageFiles);
    }
}
