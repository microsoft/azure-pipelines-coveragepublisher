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
        /// Get coverage information for individual files.
        /// </summary>
        /// <param name="coverageFiles">List of xml coverage files.</param>
        /// <param name="reportDirectory">If specified parser will generate html report to the given path.</param>
        /// <returns>List of <see cref="FileCoverageInfo"/></returns>
        List<FileCoverageInfo> GetFileCoverageInfos(List<string> coverageFiles, string reportDirectory = "");
        
        /// <summary>
        /// Get coverage summary, contains combined coverage summary data.
        /// </summary>
        /// <param name="coverageFiles">List of xml coverage files.</param>
        /// <param name="reportDirectory">If specified parser will generate html report to the given path.</param>
        /// <returns><see cref="CoverageSummary"/></returns>
        CoverageSummary GetCoverageSummary(List<string> coverageFiles, string reportDirectory = "");
    }
}
