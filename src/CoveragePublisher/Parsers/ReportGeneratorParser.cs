// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Palmmedia.ReportGenerator.Core;
using Palmmedia.ReportGenerator.Core.CodeAnalysis;
using Palmmedia.ReportGenerator.Core.Parser;
using Palmmedia.ReportGenerator.Core.Parser.Filtering;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers
{
    // Will use ReportGenerator to parse xml files and generate IList<FileCoverageInfo>
    internal class ReportGeneratorParser: ICoverageParser
    {
        public List<FileCoverageInfo> GetFileCoverageInfos(PublisherConfiguration config)
        {
            List<FileCoverageInfo> fileCoverages = new List<FileCoverageInfo>();

            if (config.CoverageFiles == null)
            {
                return fileCoverages;
            }

            var parserResult = ParseCoverageFiles(new List<string>(config.CoverageFiles));


            foreach (var assembly in parserResult.Assemblies)
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

            this.CreateHTMLReport(parserResult, config.ReportDirectory, config.SourceDirectories);

            return fileCoverages;
        }

        public CoverageSummary GetCoverageSummary(PublisherConfiguration config)
        {
            var summary = new CoverageSummary();

            if(config.CoverageFiles == null)
            {
                return summary;
            }

            var parserResult = ParseCoverageFiles(new List<string>(config.CoverageFiles));

            int totalLines = 0;
            int coveredLines = 0;

            foreach (var assembly in parserResult.Assemblies)
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

            this.CreateHTMLReport(parserResult, config.ReportDirectory, config.SourceDirectories);

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

        private bool CreateHTMLReport(ParserResult parserResult, string reportDirectory, string sourceDirectories)
        {
            if (!string.IsNullOrEmpty(reportDirectory) && Directory.Exists(reportDirectory))
            {
                try
                {
                    var config = new ReportConfigurationBuilder().Create(new Dictionary<string, string>() {
                        { "targetdir", reportDirectory },
                        { "sourcedirs", sourceDirectories },
                        { "reporttypes", "HtmlInline_AzurePipelines" }
                    });


                    var generator = new Generator();

                    generator.GenerateReport(config, new Settings(), new RiskHotspotsAnalysisThresholds(), parserResult);
                }
                catch(Exception e)
                {
                    //TODO: log exception
                    return false;
                }

                return true;

            }

            return false;
        }
    }
}
