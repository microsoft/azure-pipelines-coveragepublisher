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
            TraceLogger.Instance.Info("ReportGeneratorParser.GetFileCoverageInfo: Generating coverage info from coverage files.");
            List<FileCoverageInfo> fileCoverages = new List<FileCoverageInfo>();

            if (config.CoverageFiles == null)
            {
                TraceLogger.Instance.Info("ReportGeneratorParser.GetFileCoverageInfos: No input received, generating empty coverage.");
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

            CreateHTMLReportFromParserResult(parserResult, config, config.SourceDirectories);

            return fileCoverages;
        }

        public CoverageSummary GetCoverageSummary(PublisherConfiguration config)
        {
            TraceLogger.Instance.Info("ReportGeneratorParser.GetCoverageSummary: Generate coverage summary for the coverage files.");

            var summary = new CoverageSummary();

            if(config.CoverageFiles == null)
            {
                TraceLogger.Instance.Info("ReportGeneratorParser.GetCoverageSummary: No input received, generating empty coverage.");
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

            this.CreateHTMLReportFromParserResult(parserResult, config, config.SourceDirectories);

            return summary;
        }

        private ParserResult ParseCoverageFiles(List<string> coverageFiles)
        {
            TraceLogger.Instance.Info("ReportGeneratorParser.ParseCoverageFiles: Parsing coverage files.");

            CoverageReportParser parser = new CoverageReportParser(1, new string[] { }, new DefaultFilter(new string[] { }),
                new DefaultFilter(new string[] { }),
                new DefaultFilter(new string[] { }));

            ReadOnlyCollection<string> collection = new ReadOnlyCollection<string>(coverageFiles);
            return parser.ParseFiles(collection);
        }

        private void CreateHTMLReportFromParserResult(ParserResult parserResult, PublisherConfiguration config, string sourceDirectories)
        {
            if (config.GenerateHTMLReport && Directory.Exists(config.ReportDirectory))
            {
                TraceLogger.Instance.Info("ReportGeneratorParser.CreateHTMLReportFromParserResult: Creating HTML report.");

                try
                {
                    // Generate the html report with custom configuration for report generator.
                    var reportGeneratorConfig = new ReportConfigurationBuilder().Create(new Dictionary<string, string>() {
                        { "targetdir", config.ReportDirectory },
                        { "sourcedirs", string.IsNullOrEmpty(sourceDirectories) ? "" : sourceDirectories },
                        { "reporttypes", "HtmlInline_AzurePipelines" }
                    });

                    var generator = new Generator();

                    generator.GenerateReport(reportGeneratorConfig, new Settings(), new RiskHotspotsAnalysisThresholds(), parserResult);

                }
                catch(Exception e)
                {
                    TraceLogger.Instance.Error(string.Format("ReportGeneratorParser.CreateHTMLReportFromParserResult: Error while generating HTML report, Error: {0}", e));
                }
            }
            else
            {
                TraceLogger.Instance.Info("ReportGeneratorParser.CreateHTMLReportFromParserResult: Skipping creation of HTML report.");
            }

            // Copy coverage when report directory is specified even if we're not creating custom html reports
            if (!string.IsNullOrEmpty(config.ReportDirectory))
            {
                CopyCoverageInputFilesToReportDirectory(config);
            }
        }

        private void CopyCoverageInputFilesToReportDirectory(PublisherConfiguration config)
        {
            if (Directory.Exists(config.ReportDirectory))
            {
                string summaryFilesSubDir;

                // Create a unique folder
                do
                {
                    summaryFilesSubDir = Path.Combine(config.ReportDirectory, "Summary_" + Guid.NewGuid().ToString().Substring(0, 8));
                } while (Directory.Exists(summaryFilesSubDir));

                TraceLogger.Instance.Verbose("ReportGeneratorParser.CopyCoverageInputFilesToReportDirectory: Creating summary file directory: " + summaryFilesSubDir);

                Directory.CreateDirectory(summaryFilesSubDir);

                // Copy the files
                foreach (var summaryFile in config.CoverageFiles)
                {
                    var summaryFileName = Path.GetFileName(summaryFile);
                    var destinationSummaryFile = Path.Combine(summaryFilesSubDir, summaryFileName);

                    TraceLogger.Instance.Verbose("ReportGeneratorParser.CopyCoverageInputFilesToReportDirectory: Copying summary file " + summaryFile);
                    File.Copy(summaryFile, destinationSummaryFile, true);
                }
            }
            else
            {
                TraceLogger.Instance.Verbose("ReportGeneratorParser.CopyCoverageInputFilesToReportDirectory: Directory " + config.ReportDirectory + " doesn't exist, skipping copying of coverage input files.");
            }
        }
    }
}
