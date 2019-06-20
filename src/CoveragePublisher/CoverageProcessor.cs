using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public class CoverageProcessor
    {
        private ICoveragePublisher _publisher;
        private IExecutionContext _executionContext;
        private Parser _parser;

        public CoverageProcessor(ICoveragePublisher publisher, IExecutionContext context)
        {
            _publisher = publisher;
            _executionContext = context;
        }

        public void ParseAndPublishCoverage(PublisherConfiguration config, CancellationToken token, Parser parser)
        {
            if (_publisher != null)
            {
                var supportsFileCoverageJson = _publisher.IsFileCoverageJsonSupported();

                if (supportsFileCoverageJson)
                {
                    var fileCoverage = parser.GetFileCoverageInfos();
                    _publisher.PublishFileCoverage(fileCoverage, token).Wait();
                }
                else
                {
                    var summary = parser.GetCoverageSummary();
                    _publisher.PublishCoverageSummary(summary, token).Wait();
                }


                if (config.GenerateHTMLReport && Directory.Exists(config.ReportDirectory))
                {
                    _publisher.PublishHTMLReport(config.ReportDirectory, token).Wait();
                }
            }

        }
    }
}
