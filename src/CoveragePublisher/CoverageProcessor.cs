// Copyright (c) Microsoft Corporation. All rights reserved.
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
        private IFeatureFlagHelper _featureFlagHelper;

        public CoverageProcessor(ICoveragePublisher publisher, ITelemetryDataCollector telemetry, IFeatureFlagHelper featureFlagHelper)
        {
            _publisher = publisher;
            _telemetry = telemetry;
            _featureFlagHelper = featureFlagHelper;
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

                    var supportsFileCoverageJson = _publisher.IsFileCoverageJsonSupported();

                    if (supportsFileCoverageJson)
                    {
                        var fileCoverage = parser.GetFileCoverageInfos();

                        var summary = parser.GetCoverageSummary();

                        bool IsCodeCoverageData = (summary.CodeCoverageData != null);

                        bool IsCoverageStats = (summary.CodeCoverageData.CoverageStats != null);

                        _telemetry.AddOrUpdate("UniqueFilesCovered", fileCoverage.Count);

                        TraceLogger.Debug("Publishing code coverage summary supported");

                        if (summary == null || (IsCodeCoverageData  && IsCoverageStats  && summary.CodeCoverageData.CoverageStats.Count == 0)) 
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
                    }
                    else
                    {
                        TraceLogger.Debug("Publishing file json coverage is not supported.");
                        var summary = parser.GetCoverageSummary();

                        if (summary == null || summary.CodeCoverageData.CoverageStats.Count == 0)
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
                    }

                    var IsPublishHTMLReportDeprecationEnabled = _featureFlagHelper.GetFeatureFlagState(Constants.FeatureFlags.DeprecatePublishHTMLReport, true);


                    // Feature Flag for testing and deprecating PublishHTMLReport
                    //if (IsPublishHTMLReportDeprecationEnabled && config.GenerateHTMLReport)
                    //{
                    //    if (!Directory.Exists(config.ReportDirectory))
                    //    {
                    //        TraceLogger.Warning(Resources.NoReportDirectoryGenerated);
                    //    }
                    //    else
                    //    {
                    //        using (new SimpleTimer("CoverageProcesser", "PublishHTMLReport", _telemetry))
                    //        {
                    //            await _publisher.PublishHTMLReport(config.ReportDirectory, token);
                    //        }
                    //    }
                    //}
                }
                // Only catastrophic failures should trickle down to these catch blocks
                catch(ParsingException ex)
                {
                    _telemetry.AddFailure(ex);
                    TraceLogger.Error($"{ex.Message} {ex.InnerException}");
                }
                catch(Exception ex)
                {
                    _telemetry.AddFailure(ex);
                    TraceLogger.Error(string.Format(Resources.ErrorOccuredWhilePublishing, ex));
                }
            }
        }
    }
}
