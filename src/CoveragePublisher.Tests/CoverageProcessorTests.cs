using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class CoverageProcessorTests
    {
        private Mock<ICoveragePublisher> _mockPublisher = new Mock<ICoveragePublisher>();
        private PublisherConfiguration _config = new PublisherConfiguration() { ReportDirectory = "directory" };
        private Mock<Parser> _mockParser;
        private Mock<ITelemetryDataCollector> _mockTelemetryDataCollector = new Mock<ITelemetryDataCollector>();

        [TestInitialize]
        public void TestInitialize()
        {
            _mockParser = new Mock<Parser>(_config, _mockTelemetryDataCollector.Object);

            _mockPublisher.Setup(x => x.PublishCoverageSummary(It.IsAny<CoverageSummary>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockPublisher.Setup(x => x.PublishFileCoverage(It.IsAny<IList<FileCoverageInfo>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockPublisher.Setup(x => x.PublishHTMLReport(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }

        [TestMethod]
        public void ParseAndPublishCoverageWillPublishSummary()
        {
            var token = new CancellationToken();
            var processor = new CoverageProcessor(_mockPublisher.Object, _mockTelemetryDataCollector.Object);
            var summary = new CoverageSummary();

            _mockPublisher.Setup(x => x.IsFileCoverageJsonSupported()).Returns(false);
            _mockParser.Setup(x => x.GetCoverageSummary()).Returns(summary);
            
            processor.ParseAndPublishCoverage(_config, token, _mockParser.Object).Wait();

            _mockPublisher.Verify(x => x.PublishCoverageSummary(
                It.Is<CoverageSummary>(a => a == summary),
                It.Is<CancellationToken>(b => b == token)));
        }

        [TestMethod]
        public void ParseAndPublishCoverageWillPublishFileCoverage()
        {
            var token = new CancellationToken();
            var processor = new CoverageProcessor(_mockPublisher.Object, _mockTelemetryDataCollector.Object);
            var coverage = new List<FileCoverageInfo>();

            _mockPublisher.Setup(x => x.IsFileCoverageJsonSupported()).Returns(true);
            _mockParser.Setup(x => x.GetFileCoverageInfos()).Returns(coverage);

            processor.ParseAndPublishCoverage(_config, token, _mockParser.Object).Wait();

            _mockPublisher.Verify(x => x.PublishFileCoverage(
                It.Is<List<FileCoverageInfo>>(a => a == coverage),
                It.Is<CancellationToken>(b => b == token)));
        }

        [TestMethod]
        public void WillCatchAndReportExceptions()
        {
            var logger = new TestLogger();
            TraceLogger.Initialize(logger);

            var token = new CancellationToken();
            var processor = new CoverageProcessor(_mockPublisher.Object, _mockTelemetryDataCollector.Object);
            var coverage = new List<FileCoverageInfo>();

            _mockPublisher.Setup(x => x.IsFileCoverageJsonSupported()).Returns(true);
            _mockParser.Setup(x => x.GetFileCoverageInfos()).Throws(new ParsingException("message", new Exception("error")));

            processor.ParseAndPublishCoverage(_config, token, _mockParser.Object).Wait();

            Assert.IsTrue(logger.Log.Contains("error: message System.Exception: error"));

            logger.Log = "";

            _mockParser.Setup(x => x.GetFileCoverageInfos()).Throws(new Exception("error"));

            processor.ParseAndPublishCoverage(_config, token, _mockParser.Object).Wait();

            Assert.IsTrue(logger.Log.Contains("error: An error occured while publishing coverage files. System.Exception: error"));
        }
    }
}
