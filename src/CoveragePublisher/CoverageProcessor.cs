﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers;
using Microsoft.Azure.Pipelines.CoveragePublisher.Utils;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public class CoverageProcessor
    {
        private ICoveragePublisher _publisher;
        private ITelemetryDataCollector _telemetry;

        public CoverageProcessor(ICoveragePublisher publisher, ITelemetryDataCollector telemetry)
        {
            _publisher = publisher;
            _telemetry = telemetry;
        }

        public async Task ParseAndPublishCoverage(PublisherConfiguration config, CancellationToken token, Parser parser)
        {
            if (_publisher != null)
            {
                try
                {
                    TraceLogger.Debug("Publishing file json coverage supported.");

                    _telemetry.AddOrUpdate("PublisherConfig", () =>
                    {
                        return "{" +
                            $"\"InputFilesCount\": {config.CoverageFiles.Count}," +
                            $"\"SourceDirectoryProvided\": {config.SourceDirectory != ""}," +
                            $"\"GenerateHtmlReport\": {config.GenerateHTMLReport}," +
                            $"\"GenerateHtmlReport\": {config.TimeoutInSeconds}" +
                        "}";
                    });

                    // Upload native coverage files to TCM
                    TraceLogger.Debug("Publishing native coverage files is supported.");

                    await _publisher.PublishNativeCoverageFiles(config.CoverageFiles, token);

                    var fileCoverage = parser.GetFileCoverageInfos();

                    var summary = parser.GetCoverageSummary();

                    bool IsCodeCoverageData = (summary.CodeCoverageData != null);

                    bool IsCoverageStats = (summary.CodeCoverageData.CoverageStats != null);

                    _telemetry.AddOrUpdate("UniqueFilesCovered", fileCoverage.Count);

                    TraceLogger.Debug("Publishing code coverage summary supported");

                    if (summary == null || (IsCodeCoverageData && IsCoverageStats && summary.CodeCoverageData.CoverageStats.Count == 0))
                    {
                        TraceLogger.Warning(Resources.NoSummaryStatisticsGenerated);
                    }
                    else
                    {
                        using (new SimpleTimer("CoverageProcesser", "PublishCoverageSummary", _telemetry))
                        {
                            await _publisher.PublishCoverageSummary(summary, token);
                        }
                    }

                    if (fileCoverage.Count == 0)
                    {
                        TraceLogger.Warning(Resources.NoCoverageFilesGenerated);
                    }
                    else
                    {
                        using (new SimpleTimer("CoverageProcesser", "PublishFileCoverage", _telemetry))
                        {
                            await _publisher.PublishFileCoverage(fileCoverage, token);
                        }
                    }
                    
                    //Feature Flag for PublishHTMLReport; To be cleaned up post PCCRV2 upgrade
                    if (config.GenerateHTMLReport)
                    {
                        if (!Directory.Exists(config.ReportDirectory))
                        {
                            TraceLogger.Warning(Resources.NoReportDirectoryGenerated);
                        }

                        else
                        {
                            if(IsContainsNativeCoverage(config))
                            {
                                using (new SimpleTimer("CoverageProcesser", "PublishHTMLReport", _telemetry))
                                {
                                    await _publisher.PublishHTMLReport(config.ReportDirectory, token);
                                }
                            }
                            else{
                                TraceLogger.Debug("Native coverage files are not present. Skipping HTML report generation.");
                            }
                        }
                    }
                }
                // Only catastrophic failures should trickle down to these catch blocks
                catch (ParsingException ex)
                {
                    _telemetry.AddFailure(ex);
                    TraceLogger.Error($"{ex.Message} {ex.InnerException}");
                }
                catch (Exception ex)
                {
                    _telemetry.AddFailure(ex);
                    TraceLogger.Error(string.Format(Resources.ErrorOccuredWhilePublishing, ex));
                }
            }
        }

        private bool IsContainsNativeCoverage(PublisherConfiguration config)
        {
            foreach (var coverageFile in config.CoverageFiles)
            {
                if (!(coverageFile.EndsWith(Constants.CoverageConstants.CoverageBufferFileExtension) || // .coveragebuffer
                    coverageFile.EndsWith(Constants.CoverageConstants.CoverageFileExtension) ||         // .coverage
                    coverageFile.EndsWith(Constants.CoverageConstants.CoverageBFileExtension) ||        //.covb 
                    coverageFile.EndsWith(Constants.CoverageConstants.CoverageJsonFileExtension) ||     //.cjson  
                    coverageFile.EndsWith(Constants.CoverageConstants.CoverageXFileExtension)))         //.covx
                {
                    return true;
                }
            }
            return false;
        }
    }
}
