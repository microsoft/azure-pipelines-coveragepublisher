using Microsoft.CodeCoverage.Core;
using Microsoft.CodeCoverage.IO.Coverage;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers.CoverageParserTools
{
    public class PublisherCoverageFileConfiguration : ICoverageFileConfiguration
    {
        public bool ReadModules { get; set; }

        public bool ReadSkippedModules { get; set; }

        public bool ReadSkippedFunctions { get; set; }

        public bool ReadSnapshotsData { get; set; }

        public bool GenerateCoverageBufferFiles { get; set; }

        public bool FixCoverageBuffersMismatch { get; set; }

        public int MaxDegreeOfParallelism { get; set; } = 10;

        public bool SkipInvalidData { get; set; }

        public CoverageMergeOperation MergeOperation { get; set; }

        internal static PublisherCoverageFileConfiguration Default { get; } = new PublisherCoverageFileConfiguration
        {
            ReadModules = true,
            ReadSkippedFunctions = true,
            ReadSnapshotsData = true,
            ReadSkippedModules = true,
            GenerateCoverageBufferFiles = true,
            FixCoverageBuffersMismatch = true,
        };

        internal static PublisherCoverageFileConfiguration NoSkippedData { get; } = new PublisherCoverageFileConfiguration
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