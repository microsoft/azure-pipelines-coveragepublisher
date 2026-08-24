// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
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
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers
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
                TraceLogger.Debug("ReportGeneratorTool: No input coverage files to parse.");
                return;
            }

            _parserResult = ParseCoverageFiles(new List<string>(Configuration.CoverageFiles));
        }

        public List<FileCoverageInfo> GetFileCoverageInfos()
        {
            TraceLogger.Debug("ReportGeneratorTool.GetFileCoverageInfos: Generating file coverage info from coverage files.");

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
                            int coveredBranches = (file.CoveredBranches != null) ? file.CoveredBranches.Value : 0;
                            int totalBranches = (file.TotalBranches != null) ? file.TotalBranches.Value : 0;
                            if (file.BranchesByLine != null && file.BranchesByLine.TryGetValue(lineNumber, out var branches))
                            {
                                if (resultFileCoverageInfo.BranchCoverageStatus == null)
                                {
                                    resultFileCoverageInfo.BranchCoverageStatus = new Dictionary<uint, BranchCoverageStatistics>();
                                }
                                var branchcoveragestatistics = new BranchCoverageStatistics
                                {
                                    TotalBranches = branches.Count,
                                    CoveredBranches = branches.Count(b => b.BranchVisits > 0)
                                };
                                resultFileCoverageInfo.BranchCoverageStatus.Add((uint)lineNumber, branchcoveragestatistics);
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
            TraceLogger.Debug("ReportGeneratorTool.GetCoverageSummary: Generating coverage summary for the coverage files.");

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

        private static readonly Regex ReportGeneratorArgumentRegex =
            new Regex(@"-(?<key>[a-zA-Z]{2,}):(?<value>""(?:[^""\\]|\\.)*""|\S+)", RegexOptions.Compiled);

        /// <summary>
        /// Parses a "-key:value -key2:value2 ..." string (ReportGenerator's own command line syntax)
        /// into a case-insensitive dictionary. Quoted values ("...") are supported
        /// so a value may contain spaces; unquoted values may not. Within a quoted value, \" is
        /// unescaped to a literal ".
        /// </summary>
        private static Dictionary<string, string> ParseReportGeneratorArguments(string arguments)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(arguments))
            {
                return result;
            }

            foreach (Match match in ReportGeneratorArgumentRegex.Matches(arguments))
            {
                var value = match.Groups["value"].Value;
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                {
                    value = value.Substring(1, value.Length - 2).Replace("\\\"", "\"");
                }

                result[match.Groups["key"].Value] = value;
            }

            return result;
        }

        public void GenerateHTMLReport()
        {
            TraceLogger.Debug("ReportGeneratorTool.CreateHTMLReportFromParserResult: Creating HTML report.");

            if (!Directory.Exists(Configuration.ReportDirectory))
            {
                Directory.CreateDirectory(Configuration.ReportDirectory);
            }

            // Task defaults first, then merge in caller-supplied arguments so that e.g. a
            // user-supplied -reporttypes: replaces the default instead of being combined with it.
            var reportGeneratorSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                { "targetdir", Configuration.ReportDirectory },
                { "sourcedirs", string.IsNullOrEmpty(Configuration.SourceDirectory) ? "" : Configuration.SourceDirectory },
                { "reporttypes", "HtmlInline_AzurePipelines" }
            };

            foreach (var kvp in ParseReportGeneratorArguments(Configuration.ReportGeneratorArguments))
            {
                reportGeneratorSettings[kvp.Key] = kvp.Value;
            }

            var reportGeneratorConfig = new ReportConfigurationBuilder().Create(reportGeneratorSettings);

            var generator = new Generator();

            generator.GenerateReport(reportGeneratorConfig, new Settings(), new RiskHotspotsAnalysisThresholds(), _parserResult);
        }

        private ParserResult ParseCoverageFiles(List<string> coverageFiles)
        {
            TraceLogger.Debug("ReportGeneratorTool.ParseCoverageFiles: Parsing coverage files.");

            CoverageReportParser parser = new CoverageReportParser(1, 1, new string[] { }, new DefaultFilter(new string[] { }),
                new DefaultFilter(new string[] { }),
                new DefaultFilter(new string[] { }));

            ReadOnlyCollection<string> collection = new ReadOnlyCollection<string>(coverageFiles);
            return parser.ParseFiles(collection);
        }
    }
}
