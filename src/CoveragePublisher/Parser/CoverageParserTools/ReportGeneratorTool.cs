﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parser
{
    // Will use ReportGenerator to parse xml files and generate IList<FileCoverageInfo>
    public class ReportGeneratorTool: ICoverageParserTool
    {
        protected PublisherConfiguration Configuration { get; private set; }
        private ParserResult _parserResult;

        public ReportGeneratorTool(PublisherConfiguration configuration) {
            Configuration = configuration;

            if (Configuration.CoverageFiles == null)
            {
                TraceLogger.Instance.Info("ReportGeneratorTool: No input coverage files to parse.");
                return;
            }

            _parserResult = ParseCoverageFiles(new List<string>(Configuration.CoverageFiles));
        }

        public List<FileCoverageInfo> GetFileCoverageInfos()
        {
            TraceLogger.Instance.Info("ReportGeneratorTool.GetFileCoverageInfos: Generating file coverage info from coverage files.");

            List<FileCoverageInfo> fileCoverages = new List<FileCoverageInfo>();

            if (Configuration.CoverageFiles == null)
            {
                return fileCoverages;
            }

            foreach (var assembly in _parserResult.Assemblies)
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

        public CoverageSummary GetCoverageSummary()
        {
            TraceLogger.Instance.Info("ReportGeneratorTool.GetCoverageSummary: Generating coverage summary for the coverage files.");

            var summary = new CoverageSummary();

            if(Configuration.CoverageFiles == null)
            {
                return summary;
            }

            int totalLines = 0;
            int coveredLines = 0;

            foreach (var assembly in _parserResult.Assemblies)
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

        public void GenerateHTMLReport()
        {
            TraceLogger.Instance.Info("ReportGeneratorTool.CreateHTMLReportFromParserResult: Creating HTML report.");

            try
            {
                if (!Directory.Exists(Configuration.ReportDirectory))
                {
                    Directory.CreateDirectory(Configuration.ReportDirectory);
                }

                // Generate the html report with custom configuration for report generator.
                var reportGeneratorConfig = new ReportConfigurationBuilder().Create(new Dictionary<string, string>() {
                    { "targetdir", Configuration.ReportDirectory },
                    { "sourcedirs", string.IsNullOrEmpty(Configuration.SourceDirectories) ? "" : Configuration.SourceDirectories },
                    { "reporttypes", "HtmlInline_AzurePipelines" }
                });

                var generator = new Generator();

                generator.GenerateReport(reportGeneratorConfig, new Settings(), new RiskHotspotsAnalysisThresholds(), _parserResult);

            }
            catch (Exception e)
            {
                TraceLogger.Instance.Error(string.Format("ReportGeneratorTool.CreateHTMLReportFromParserResult: Error while generating HTML report, Error: {0}", e));
            }
            
        }

        private ParserResult ParseCoverageFiles(List<string> coverageFiles)
        {
            TraceLogger.Instance.Info("ReportGeneratorTool.ParseCoverageFiles: Parsing coverage files.");

            CoverageReportParser parser = new CoverageReportParser(1, new string[] { }, new DefaultFilter(new string[] { }),
                new DefaultFilter(new string[] { }),
                new DefaultFilter(new string[] { }));

            ReadOnlyCollection<string> collection = new ReadOnlyCollection<string>(coverageFiles);
            return parser.ParseFiles(collection);
        }
    }
}
