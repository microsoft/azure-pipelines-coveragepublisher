// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelinesPublisher
{
    internal class AzurePipelinesPublisher : ICoveragePublisher
    {
        public void PublishCoverageSummary(CoverageSummary coverageSummary)
        {
        }

        public void PublishFileCoverage(IList<FileCoverageInfo> coverageFiles)
        {
        }

        public void PublishHTMLReport(string reportDirectory)
        {
        }
    }
}
