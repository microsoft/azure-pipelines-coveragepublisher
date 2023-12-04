// Copyright (C) Microsoft Corporation. All Rights Reserved.

using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.CodeCoverage.Core;
using Microsoft.CodeCoverage.IO.Coverage;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Palmmedia.ReportGenerator.Core;
using Palmmedia.ReportGenerator.Core.CodeAnalysis;
using Palmmedia.ReportGenerator.Core.Parser;
using Palmmedia.ReportGenerator.Core.Parser.Filtering;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers
{
    public class OneESTestCoverageFileConfiguration : ICoverageFileConfiguration
    {
        /// <inheritdoc/>
        public bool ReadModules { get; set; }

        /// <inheritdoc/>
        public bool ReadSkippedModules { get; set; }

        /// <inheritdoc/>
        public bool ReadSkippedFunctions { get; set; }

        /// <inheritdoc/>
        public bool ReadSnapshotsData { get; set; }

        /// <inheritdoc/>
        public bool GenerateCoverageBufferFiles { get; set; }

        /// <inheritdoc/>
        public bool FixCoverageBuffersMismatch { get; set; }

        /// <inheritdoc/>
        public int MaxDegreeOfParallelism { get; set; } = 10;

        public bool SkipInvalidData { get; set; }

        public CoverageMergeOperation MergeOperation { get; set; }

        internal static OneESTestCoverageFileConfiguration Default { get; } = new OneESTestCoverageFileConfiguration
        {
            ReadModules = true,
            ReadSkippedFunctions = true,
            ReadSnapshotsData = true,
            ReadSkippedModules = true,
            GenerateCoverageBufferFiles = true,
            FixCoverageBuffersMismatch = true,
        };

        internal static OneESTestCoverageFileConfiguration NoSkippedData { get; } = new OneESTestCoverageFileConfiguration
        {
            ReadModules = true,
            ReadSkippedFunctions = false,
            ReadSnapshotsData = true,
            ReadSkippedModules = false,
            GenerateCoverageBufferFiles = true,
            FixCoverageBuffersMismatch = true,
        };
    }
}
