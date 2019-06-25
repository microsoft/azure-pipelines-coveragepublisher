using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public class CoverageProcessor
    {
        private ICoveragePublisher _publisher;

        public CoverageProcessor(ICoveragePublisher publisher)
        {
            _publisher = publisher;
        }

        public async Task ParseAndPublishCoverage(PublisherConfiguration config, CancellationToken token, Parser parser)
        {
            if (_publisher != null)
            {
                var supportsFileCoverageJson = _publisher.IsFileCoverageJsonSupported();

                if (supportsFileCoverageJson)
                {
                    TraceLogger.Debug("Publishing file json coverage is supported.");
                    var fileCoverage = parser.GetFileCoverageInfos();
                    await _publisher.PublishFileCoverage(fileCoverage, token);
                }
                else
                {
                    TraceLogger.Debug("Publishing file json coverage is not supported.");
                    var summary = parser.GetCoverageSummary();
                    await _publisher.PublishCoverageSummary(summary, token);
                }


                if (config.GenerateHTMLReport && Directory.Exists(config.ReportDirectory))
                {
                    await _publisher.PublishHTMLReport(config.ReportDirectory, token);
                }
            }
        }
    }
}
