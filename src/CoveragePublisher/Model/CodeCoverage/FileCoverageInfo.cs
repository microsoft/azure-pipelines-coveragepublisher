// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    /// <summary>
    /// Model for file coverage for a single file.
    /// </summary>
    public class FileCoverageInfo
    {
        /// <summary>
        /// File path for the covered file.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Map of coverage lines to their covered status.
        /// </summary>
        public Dictionary<uint, CoverageStatus> LineCoverageStatus;

        /// </summary>
        /// Branch coverage status of the file
        /// </summary>

        public BranchCoverageInfo BranchCoverageStatus { get; set; }
    }

    public class BranchCoverageInfo
    {
        /// <summary>
        /// Number of total branches
        /// </summary>
        public int TotalBranches { get; set; }

        /// <summary>
        /// Number of covered branches
        /// </summary>
        public int CoveredBranches { get; set; }
    }

}
