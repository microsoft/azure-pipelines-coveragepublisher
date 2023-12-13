using Microsoft.CodeCoverage.Core;
using Microsoft.CodeCoverage.IO.Coverage;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers.CoverageParserTools
{
    internal class PublisherCoverageFileConfiguration : ICoverageFileConfiguration
    {
        public PublisherCoverageFileConfiguration(
            bool ReadModules,
            bool ReadSkippedModules,
            bool ReadSkippedFunctions,
            bool ReadSnapshotsData,
            bool FixCoverageBuffersMismatch,
            bool GenerateCoverageBufferFiles
            ) {
            this.ReadModules = ReadModules;
            this.ReadSkippedModules = ReadSkippedModules;
            this.ReadSkippedFunctions = ReadSkippedFunctions;
            this.ReadSnapshotsData = ReadSnapshotsData;
            this.FixCoverageBuffersMismatch = FixCoverageBuffersMismatch;
            this.GenerateCoverageBufferFiles = GenerateCoverageBufferFiles;
        }
        public bool ReadModules { get; set; }

        public bool ReadSkippedModules { get; set; }

        public bool ReadSkippedFunctions { get; set; }

        public bool ReadSnapshotsData { get; set; }

        public bool GenerateCoverageBufferFiles { get; set; }

        public bool FixCoverageBuffersMismatch { get; set; }

        public int MaxDegreeOfParallelism { get; set; } = 10;

        public bool SkipInvalidData { get; set; }

        public CoverageMergeOperation MergeOperation { get; set; }

    }
}