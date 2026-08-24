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

        /// <summary>FOR INTERNAL TEST USAGE ONLY. Runs after the approved text is captured and before the report is drawn.</summary>
        internal Action TEST_BeforeGenerateReport { get; set; }

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

        /// <summary>
        /// ReportGenerator settings that <see cref="PublisherConfiguration.ReportGeneratorArguments"/> may not replace
        /// </summary>
        private static readonly HashSet<string> ReservedReportGeneratorSettings =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "targetdir", "sourcedirs", "reports" };

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

            string[] sourceDirectories = SplitDirectories(Configuration.SourceDirectory);
            ParserResult htmlParserResult = _parserResult;

            if (Configuration.EnforceTrustedSourcePathFiltering)
            {
                var trustedSourcePathFilter = new TrustedSourcePathFilter(
                    sourceDirectories.Concat(SplitDirectories(Configuration.TrustedSourceDirectory)));
                htmlParserResult = ParseCoverageFiles(
                    new List<string>(Configuration.CoverageFiles),
                    trustedSourcePathFilter);

                if (trustedSourcePathFilter.ExcludedPathCount > 0)
                {
                    TraceLogger.Warning($"Skipped {trustedSourcePathFilter.ExcludedPathCount} untrusted source path(s) while generating the HTML coverage report.");
                }

                List<Palmmedia.ReportGenerator.Core.Parser.Analysis.CodeFile> sourceFiles = htmlParserResult.Assemblies
                    .SelectMany(assembly => assembly.Classes)
                    .SelectMany(@class => @class.Files)
                    .ToList();

                // Depends on DistributedTask.Tasks.EnablePublishCodeCoverageResultsV2SourcePathContainment — see Cross-Flag Dependencies in the feature-flag-creation skill.
                // The pin runs only when that check is already on and PublishCodeCoverageResultsV2ApprovedSourceTextFixEnabled is on.
                // If the pin flag is off, keep the existing yes-or-no check and let the report library open the source name.
                if (Configuration.PinApprovedSourceText)
                {
                    var approvedLines = new Dictionary<string, string[]>(StringComparer.Ordinal);
                    foreach (var file in sourceFiles)
                    {
                        ApprovedSourceStatus status = trustedSourcePathFilter.TryReadApprovedLines(
                            file.Path,
                            sourceDirectories,
                            out string[] lines);
                        if (status == ApprovedSourceStatus.Unsafe)
                        {
                            TraceLogger.Warning("HTML source report generation was blocked because coverage data referenced a source path outside the trusted directories.");
                            WriteBlockedSourceReport(Configuration.ReportDirectory);
                            return;
                        }

                        if (status == ApprovedSourceStatus.Missing)
                        {
                            int lineCount = file.LineCoverage == null ? 0 : file.LineCoverage.Count;
                            lines = Enumerable.Repeat(string.Empty, lineCount).ToArray();
                        }

                        approvedLines[file.Path] = lines;
                    }

                    if (!ApprovedSourcePin.TryPin(sourceFiles, approvedLines))
                    {
                        TraceLogger.Warning("HTML source report generation was blocked because the approved source text could not be kept for rendering.");
                        WriteBlockedSourceReport(Configuration.ReportDirectory);
                        return;
                    }
                }
                else
                {
                    foreach (var file in sourceFiles)
                    {
                        if (!trustedSourcePathFilter.IsSafeForRead(file.Path, sourceDirectories))
                        {
                            TraceLogger.Warning("HTML source report generation was blocked because coverage data referenced a source path outside the trusted directories.");
                            WriteBlockedSourceReport(Configuration.ReportDirectory);
                            return;
                        }
                    }
                }

                TEST_BeforeGenerateReport?.Invoke();
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
                // The source directories and report directory are set by --sourceDirectory and --reportDirectory:
                // the trusted source path checks above find files in the same source directories ReportGenerator reads from
                if (ReservedReportGeneratorSettings.Contains(kvp.Key))
                {
                    TraceLogger.Warning($"Ignored ReportGenerator argument -{kvp.Key}: it is set by the publisher's own options.");
                    continue;
                }

                reportGeneratorSettings[kvp.Key] = kvp.Value;
            }

            var reportGeneratorConfig = new ReportConfigurationBuilder().Create(reportGeneratorSettings);

            var generator = new Generator();

            generator.GenerateReport(reportGeneratorConfig, new Settings(), new RiskHotspotsAnalysisThresholds(), htmlParserResult);
        }

        private ParserResult ParseCoverageFiles(List<string> coverageFiles)
        {
            return ParseCoverageFiles(coverageFiles, new DefaultFilter(new string[] { }));
        }

        private ParserResult ParseCoverageFiles(List<string> coverageFiles, IFilter fileFilter)
        {
            TraceLogger.Debug("ReportGeneratorTool.ParseCoverageFiles: Parsing coverage files.");

            CoverageReportParser parser = new CoverageReportParser(1, 1, new string[] { }, new DefaultFilter(new string[] { }),
                new DefaultFilter(new string[] { }),
                fileFilter);

            ReadOnlyCollection<string> collection = new ReadOnlyCollection<string>(coverageFiles);
            return parser.ParseFiles(collection);
        }

        private static string[] SplitDirectories(string directories)
        {
            return string.IsNullOrWhiteSpace(directories)
                ? Array.Empty<string>()
                : directories.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(directory => directory.Trim())
                    .Where(directory => directory.Length > 0)
                    .ToArray();
        }

        private static void WriteBlockedSourceReport(string reportDirectory)
        {
            const string report = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Code coverage</title></head>"
                + "<body><h1>Code coverage</h1><p>Source rendering was omitted because the coverage data referenced a file outside the trusted source directories.</p></body></html>";

            File.WriteAllText(Path.Combine(reportDirectory, "index.html"), report);
            File.WriteAllText(Path.Combine(reportDirectory, "index.htm"), report);
        }
    }
}
