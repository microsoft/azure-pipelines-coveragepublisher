// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    /// <summary>
    /// Model for tools that understand coverage formats and parse to IList<FileCoverageInfo>.
    /// </summary>
    public interface ICoverageParser
    {
        /// <summary>
        /// Get coverage information for individual files.
        /// </summary>
        /// <param name="configuration"><see cref="PublisherConfiguration"/></param>
        /// <returns>List of <see cref="FileCoverageInfo"/></returns>
        List<FileCoverageInfo> GetFileCoverageInfos(PublisherConfiguration configuration);
        
        /// <summary>
        /// Get coverage summary, contains combined coverage summary data.
        /// </summary>
        /// <param name="configuration"><see cref="PublisherConfiguration"/></param>
        /// <returns><see cref="CoverageSummary"/></returns>
        CoverageSummary GetCoverageSummary(PublisherConfiguration configuration);
    }
}
