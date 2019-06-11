// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.TeamFoundation.TestManagement.WebApi;
using System.Collections.Generic;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    /// <summary>
    /// Model for coverage summary.
    /// </summary>
    public class CoverageSummary
    {
        public CoverageSummary(string buildFlavor, string buildPlatform)
        {
            CodeCoverageData = new CodeCoverageData();

            CodeCoverageData.BuildFlavor = buildFlavor;
            CodeCoverageData.BuildPlatform = buildPlatform;
            CodeCoverageData.CoverageStats = new List<CodeCoverageStatistics>();
        }

        public CoverageSummary(): this(string.Empty, string.Empty) { }

        /// <summary>
        /// Code coverage summary data
        /// </summary>
        public CodeCoverageData CodeCoverageData { get; private set; }

        /// <summary>
        /// Add coverage statistics for a run to summary.
        /// </summary>
        /// <param name="label">Label for the statistics.</param>
        /// <param name="total">Total entities.</param>
        /// <param name="covered">Number of entities covered.</param>
        /// <param name="priority">Priority order for the statistics.</param>
        public void AddCoverageStatistics(string label, int total, int covered, Priority priority)
        {
            var stats = new CodeCoverageStatistics();

            stats.Covered = covered;
            stats.Total = total;
            stats.Position = (int)priority;
            stats.Label = label;

            this.CodeCoverageData.CoverageStats.Add(stats);
        }

        /// <summary>
        /// Priority based on type of coverage.
        /// </summary>
        public enum Priority
        {
            Class = 1,
            Complexity = 2,
            Method = 3,
            Line = 4,
            Instruction = 5,
            Other = 6
        }

    }
}
