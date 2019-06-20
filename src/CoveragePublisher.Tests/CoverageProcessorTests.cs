using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class CoverageProcessorTests
    {
        private Mock<ICoveragePublisher> _mockPublisher = new Mock<ICoveragePublisher>();
        private TestLogger _logger = new TestLogger();
        private IPipelinesExecutionContext _context;
        private PublisherConfiguration _config = new PublisherConfiguration() { ReportDirectory = "directory" };

        [TestInitialize]
        public void TestInitialize()
        {
            _context = new TestPipelinesExecutionContext(_logger);
            _mockPublisher.Setup(x => x.PublishCoverageSummary(It.IsAny<CoverageSummary>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockPublisher.Setup(x => x.PublishFileCoverage(It.IsAny<IList<FileCoverageInfo>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockPublisher.Setup(x => x.PublishHTMLReport(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }

        [TestMethod]
        public void ParseAndPublishCoverageWillPublishSummary()
        {
            var mockPublisher = new Mock<ICoveragePublisher>();
            var logger = new TestLogger();
            var context = new TestPipelinesExecutionContext(logger);
            var token = new CancellationToken();
            var processor = new CoverageProcessor(mockPublisher.Object, context);

            _mockPublisher.Setup(x => x.IsFileCoverageJsonSupported()).Returns(false);
            
            processor.ParseAndPublishCoverage(_config, token, new Parser();

            _mockPublisher.Verify(x => x.PublishCoverageSummary(It.IsAny<CoverageSummary>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockPublisher.Setup(x => x.PublishFileCoverage(It.IsAny<IList<FileCoverageInfo>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockPublisher.Setup(x => x.PublishHTMLReport(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }
    }
}
