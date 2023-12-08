﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.CodeCoverage.Core;
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
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers.CoverageParserTools;
using CoverageStatus = Microsoft.TeamFoundation.TestManagement.WebApi.CoverageStatus;
using Microsoft.CodeCoverage.IO.Coverage;
using Microsoft.CodeCoverage.IO;

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
                            ++lineNumber;
                        }

                        fileCoverages.Add(resultFileCoverageInfo);
                    }
                }
            }

            return fileCoverages;
        }

        public List<FileCoverageInfo> GetFileCoverageInfos(CancellationToken token)
        {
            TraceLogger.Debug("ReportGeneratorTool.GetFileCoverageInfos: Generating file coverage info from coverage files.");

            List<FileCoverageInfo> fileCoverages = new List<FileCoverageInfo>();

            Console.WriteLine($"These are the Configuration NAtive Coverage files: {Configuration.CoverageFiles}");



            string[] input = Configuration.CoverageFiles.ToArray();

            var target = new CoverageFileUtilityV2(new PublisherCoverageFileConfiguration() , null);
            string output = "";
            string[] mergedReports =  target.MergeCoverageFilesAsync(output, input, CoverageMergeOperation.MergeToXml, CancellationToken.None).Result;

            var transformedXml= mergedReports.ToList();

            Console.WriteLine("THESE ARE DOTCOVERAGE FILES");
            //var transformedXml = TransformCoverageFilesToXml(Configuration.CoverageFiles, token);
            Console.WriteLine("TRANSFORMED COVERAGE TO XML");
            Console.WriteLine(transformedXml.ToString());

            _parserResult = ParseCoverageFiles(transformedXml);

            Console.WriteLine("These are the Parser Results", _parserResult);

            if (Configuration.CoverageFiles == null)
            {
                Console.WriteLine("THIS IS TRUE");
                return fileCoverages;
            }

            foreach (var assembly in _parserResult.Assemblies)
            {
                foreach (var @class in assembly.Classes)
                {
                    foreach (var file in @class.Files)
                    {
                        Console.WriteLine("WE HAVE REACHED THE THRIPLE FOR LOOP");
                        FileCoverageInfo resultFileCoverageInfo = new FileCoverageInfo { FilePath = file.Path, LineCoverageStatus = new Dictionary<uint, TeamFoundation.TestManagement.WebApi.CoverageStatus>() };
                        int lineNumber = 0;

                        foreach (var line in file.LineCoverage)
                        {
                            if (line != -1 && lineNumber != 0)
                            {
                                resultFileCoverageInfo.LineCoverageStatus.Add((uint)lineNumber, line == 0 ? TeamFoundation.TestManagement.WebApi.CoverageStatus.NotCovered : TeamFoundation.TestManagement.WebApi.CoverageStatus.Covered);
                            }
                            ++lineNumber;
                        }

                        fileCoverages.Add(resultFileCoverageInfo);
                    }
                }
            }

            Console.WriteLine("FILECOVERAGE", fileCoverages.Count);

            return fileCoverages;
        }

        private async Task<List<string>> TransformCoverageFilesToXml(IList<string> inputCoverageFiles, CancellationToken cancellationToken)
        {
            // Customers like intune invoke vstest.console.exe multiple times inside a single job. Transform cov files resulting from each run into a different subdirectory
            var utility = new CoverageFileUtilityV2(PublisherCoverageFileConfiguration.Default);

            var transformedXmls = new List<string>();
            foreach (var nativeCoverageFile in inputCoverageFiles)
            {
                if ((nativeCoverageFile.EndsWith(Constants.CoverageFormats.CoverageDotFileFormat) ||
                            nativeCoverageFile.EndsWith(Constants.CoverageFormats.CoverageXFileExtension) ||
                            nativeCoverageFile.EndsWith(Constants.CoverageFormats.CoverageBFileExtension)
                            ))
                {
                    string transformedXml = Path.ChangeExtension(nativeCoverageFile, ".xml");
                    await utility.ToXmlFileAsync(
                        path: nativeCoverageFile,
                        outputPath: transformedXml,
                        cancellationToken: cancellationToken);

                    transformedXmls.Add(transformedXml);
                }
            }
            return transformedXmls;
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

        public void GenerateHTMLReport()
        {
            TraceLogger.Debug("ReportGeneratorTool.CreateHTMLReportFromParserResult: Creating HTML report.");

            if (!Directory.Exists(Configuration.ReportDirectory))
            {
                Directory.CreateDirectory(Configuration.ReportDirectory);
            }

            // Generate the html report with custom configuration for report generator.
            var reportGeneratorConfig = new ReportConfigurationBuilder().Create(new Dictionary<string, string>() {
                { "targetdir", Configuration.ReportDirectory },
                { "sourcedirs", string.IsNullOrEmpty(Configuration.SourceDirectory) ? "" : Configuration.SourceDirectory },
                { "reporttypes", "HtmlInline_AzurePipelines" }
            });

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
