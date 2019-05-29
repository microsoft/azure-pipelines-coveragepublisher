// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Palmmedia.ReportGenerator.Core.Parser;
using Palmmedia.ReportGenerator.Core.Parser.Filtering;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers
{
    // Will use ReportGenerator to parse xml files and generate IList<FileCoverageInfo>
    internal class ReportGeneratorParser: ICoverageParser
    {
        public List<FileCoverageInfo> GetFileCoverageInfos(List<string> coverageFiles)
        {
            var parseResult = ParseCoverageFiles(coverageFiles);

            List<FileCoverageInfo> fileCoverages = new List<FileCoverageInfo>();

            foreach (var assembly in parseResult.Assemblies)
            {
                foreach (var @class in assembly.Classes)
                {
                    foreach (var file in @class.Files)
                    {
                        FileCoverageInfo resultFileCoverageInfo = new FileCoverageInfo { FilePath = file.Path, LineCoverageStatus = new Dictionary<uint, CoverageStatus>() };
                        int lineNumber = 0;

                        foreach (var line in file.LineCoverage)
                        {
                            if (line != -1 && lineNumber != 0)
                            {
                                resultFileCoverageInfo.LineCoverageStatus.Add((uint)lineNumber, line == 0 ? CoverageStatus.NotCovered : CoverageStatus.Covered);
                            }
                            ++lineNumber;
                        }

                        fileCoverages.Add(resultFileCoverageInfo);
                    }
                }
            }

            return fileCoverages;
        }

        public CoverageSummary GetCoverageSummary(List<string> coverageFiles)
        {
            var parseResult = ParseCoverageFiles(coverageFiles);
            var summary = new CoverageSummary();

            int totalLines = 0;
            int coveredLines = 0;

            foreach (var assembly in parseResult.Assemblies)
            {
                foreach (var @class in assembly.Classes)
                {
                    foreach (var file in @class.Files)
                    {
                        totalLines += file.CoverableLines;
                        coveredLines += file.CoveredLines;
                    }
                }
            }

            summary.AddCoverageStatistics("line", totalLines, coveredLines, CoverageSummary.Priority.Line);

            return summary;
        }

        private ParserResult ParseCoverageFiles(List<string> coverageFiles)
        {
            CoverageReportParser parser = new CoverageReportParser(1, new string[] { }, new DefaultFilter(new string[] { }),
                new DefaultFilter(new string[] { }),
                new DefaultFilter(new string[] { }));

            ReadOnlyCollection<string> collection = new ReadOnlyCollection<string>(coverageFiles);
            return parser.ParseFiles(collection);
        }

        private string CreateHTMLReport(ParserResult result)
        {
            /* HTML renderer and SummaryResult constructor isn't exposed yet in ReportGenerator.Core
            var builder = new HtmlReportBuilder();

            var summaryResult = new SummaryResult();
            builder.CreateSummaryReport(summaryResult);

            var rendered = new HTMLRenderer()
            */
            return "";
        }
    }
}
