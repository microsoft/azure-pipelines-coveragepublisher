using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers;
using Microsoft.Azure.Pipelines.CoveragePublisher.Utils;
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
                    _telemetry.AddOrUpdate("PublisherConfig", JsonUtility.Serialize(config));

                    var supportsFileCoverageJson = _publisher.IsFileCoverageJsonSupported();

                    if (supportsFileCoverageJson)
                    {
                        TraceLogger.Debug("Publishing file json coverage is supported.");
                        var fileCoverage = parser.GetFileCoverageInfos();

                        _telemetry.AddOrUpdate("UniqueFilesCovered", fileCoverage.Count);

                        using (new SimpleTimer("CoverageProcesser", "PublishFileCoverage", _telemetry))
                        {
                            await _publisher.PublishFileCoverage(fileCoverage, token);
                        }
                    }
                    else
                    {
                        TraceLogger.Debug("Publishing file json coverage is not supported.");
                        var summary = parser.GetCoverageSummary();

                        using (new SimpleTimer("CoverageProcesser", "PublishCoverageSummary", _telemetry))
                        {
                            await _publisher.PublishCoverageSummary(summary, token);
                        }
                    }


                    if (config.GenerateHTMLReport && Directory.Exists(config.ReportDirectory))
                    {
                        using (new SimpleTimer("CoverageProcesser", "PublishHTMLReport", _telemetry))
                        {
                            await _publisher.PublishHTMLReport(config.ReportDirectory, token);
                        }
                    }
                }
                catch(ParsingException ex)
                {
                    TraceLogger.Error(ex.Message + ex);
                }
                catch(Exception ex)
                {
                    TraceLogger.Error(string.Format(Resources.ErrorOccuredWhilePublishing, ex));
                }
            }
        }
    }
}
