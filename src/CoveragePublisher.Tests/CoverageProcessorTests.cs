using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers;
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
        private Mock<Parser> _mockParser;

        [TestInitialize]
        public void TestInitialize()
        {
            _context = new TestPipelinesExecutionContext(_logger);
            _mockPublisher.Setup(x => x.PublishCoverageSummary(It.IsAny<CoverageSummary>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockPublisher.Setup(x => x.PublishFileCoverage(It.IsAny<IList<FileCoverageInfo>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockPublisher.Setup(x => x.PublishHTMLReport(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            _mockParser = new Mock<Parser>(_config, _context);
        }

        [TestMethod]
        public void ParseAndPublishCoverageWillPublishSummary()
        {
            var token = new CancellationToken();
            var processor = new CoverageProcessor(_mockPublisher.Object, _context);
            var summary = new CoverageSummary();

            _mockPublisher.Setup(x => x.IsFileCoverageJsonSupported()).Returns(false);
            _mockParser.Setup(x => x.GetCoverageSummary()).Returns(summary);
            
            processor.ParseAndPublishCoverage(_config, token, _mockParser.Object);

            _mockPublisher.Verify(x => x.PublishCoverageSummary(
                It.Is<CoverageSummary>(a => a == summary),
                It.Is<CancellationToken>(b => b == token)));
        }

        [TestMethod]
        public void ParseAndPublishCoverageWillPublishFileCoverage()
        {
            var token = new CancellationToken();
            var processor = new CoverageProcessor(_mockPublisher.Object, _context);
            var coverage = new List<FileCoverageInfo>();

            _mockPublisher.Setup(x => x.IsFileCoverageJsonSupported()).Returns(true);
            _mockParser.Setup(x => x.GetFileCoverageInfos()).Returns(coverage);

            processor.ParseAndPublishCoverage(_config, token, _mockParser.Object);

            _mockPublisher.Verify(x => x.PublishFileCoverage(
                It.Is<List<FileCoverageInfo>>(a => a == coverage),
                It.Is<CancellationToken>(b => b == token)));
        }
    }
    
}
