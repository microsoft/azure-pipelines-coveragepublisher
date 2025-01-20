﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Utils;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers
{
    public class Parser
    {
        private PublisherConfiguration _configuration;
        private Lazy<ICoverageParserTool> _coverageParserTool;
        private ITelemetryDataCollector _telemetry;

        public Parser(PublisherConfiguration config, ITelemetryDataCollector telemetry)
        {
            _configuration = config;
            _telemetry = telemetry;

            using (new SimpleTimer("Parser", "Parsing", _telemetry))
            {
                _coverageParserTool = new Lazy<ICoverageParserTool>(() => this.GetCoverageParserTool(_configuration));
            }
        }

        public virtual List<FileCoverageInfo> GetFileCoverageInfos()
        {
            try
            {
                var tool = _coverageParserTool.Value;
                GenerateHTMLReport(tool);
                return tool.GetFileCoverageInfos();
            }
            catch (Exception ex)
            {
                throw new ParsingException(Resources.ParsingError, ex);
            }
        }

        public virtual CoverageSummary GetCoverageSummary()
        {
            try
            {
                var tool = _coverageParserTool.Value;
                GenerateHTMLReport(tool);
                return tool.GetCoverageSummary();
            }
            catch (Exception ex)
            {
                throw new ParsingException(Resources.ParsingError, ex);
            }
        }

        protected virtual void GenerateHTMLReport(ICoverageParserTool tool)
        {
            if (_configuration.GenerateHTMLReport)
            {
                try
                {
                    using (new SimpleTimer("Parser", "ReportGeneration", _telemetry))
                    {
                        // Generate report
                        tool.GenerateHTMLReport();

                        // Copy coverage input files to report directory in a unique folder
                        if (Directory.Exists(_configuration.ReportDirectory))
                        {
                            string summaryFilesSubDir;

                            // Create a unique folder
                            do
                            {
                                summaryFilesSubDir = Path.Combine(_configuration.ReportDirectory, "Summary_" + Guid.NewGuid().ToString().Substring(0, 8));
                            } while (Directory.Exists(summaryFilesSubDir));

                            TraceLogger.Debug("Parser.GenerateHTMLReport: Creating summary file directory: " + summaryFilesSubDir);

                            Directory.CreateDirectory(summaryFilesSubDir);

                            // Copy the files
                            foreach (var summaryFile in _configuration.CoverageFiles)
                            {
                                if (!(summaryFile.EndsWith(Constants.CoverageConstants.CoverageBufferFileExtension) || // .coveragebuffer
                                    summaryFile.EndsWith(Constants.CoverageConstants.CoverageFileExtension) ||         // .coverage
                                    summaryFile.EndsWith(Constants.CoverageConstants.CoverageBFileExtension) ||        //.covb 
                                    summaryFile.EndsWith(Constants.CoverageConstants.CoverageJsonFileExtension) ||     //.cjson  
                                    summaryFile.EndsWith(Constants.CoverageConstants.CoverageXFileExtension)))
                                    {
                                        var summaryFileName = Path.GetFileName(summaryFile);
                                        var destinationSummaryFile = Path.Combine(summaryFilesSubDir, summaryFileName);

                                        TraceLogger.Debug("Parser.GenerateHTMLReport: Copying summary file " + summaryFile);
                                        File.Copy(summaryFile, destinationSummaryFile, true);
                                    }

                                    else{
                                        TraceLogger.Debug("Parser.GenerateHTMLReport: Skipping copying of coverage input file " + summaryFile);
                                    }         
                            }
                        }
                        else
                        {
                            TraceLogger.Debug("Parser.GenerateHTMLReport: Directory " + _configuration.ReportDirectory + " doesn't exist, skipping copying of coverage input files.");
                        }
                    }
                }
                catch (Exception e)
                {
                    _telemetry.AddFailure(e);
                    TraceLogger.Error(string.Format(Resources.HTMLReportError, e.Message));
                }
            }
        }

        protected virtual ICoverageParserTool GetCoverageParserTool(PublisherConfiguration config)
        {
            // Currently there's only one parser tool, so simply return that instead of having a factory
            return new ReportGeneratorTool(config);
        }
    }
}
