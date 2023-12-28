// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            _mockPublisher.Setup(x => x.PublishNativeCoverageFiles(It.IsAny<IList<string>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockPublisher.Setup(x => x.PublishFileCoverage(It.IsAny<IList<FileCoverageInfo>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockPublisher.Setup(x => x.PublishHTMLReport(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }

        [TestMethod]
        public void ParseAndPublishCoverageWillPublishSummary()
        {
            var token = new CancellationToken();
            var processor = new CoverageProcessor(_mockPublisher.Object, _mockTelemetryDataCollector.Object);
            var summary = new CoverageSummary();

            summary.AddCoverageStatistics("", 0, 0, CoverageSummary.Priority.Class);

            _mockPublisher.Setup(x => x.IsFileCoverageJsonSupported()).Returns(false);
            _mockParser.Setup(x => x.GetCoverageSummary()).Returns(summary);
            
            processor.ParseAndPublishCoverage(_config, token, _mockParser.Object).Wait();

            _mockPublisher.Verify(x => x.PublishCoverageSummary(
                It.Is<CoverageSummary>(a => a == summary),
                It.Is<CancellationToken>(b => b == token)));
        }

        [TestMethod]
        public void WillNotPublishFileCoverageIfCountIsZero()
        {
            var token = new CancellationToken();
            var processor = new CoverageProcessor(_mockPublisher.Object, _mockTelemetryDataCollector.Object);
            var coverage = new List<FileCoverageInfo>();

            _mockPublisher.Setup(x => x.IsFileCoverageJsonSupported()).Returns(true);
            _mockParser.Setup(x => x.GetFileCoverageInfos()).Returns(coverage);

            processor.ParseAndPublishCoverage(_config, token, _mockParser.Object).Wait();

            _mockPublisher.Verify(x => x.PublishFileCoverage(
                It.Is<List<FileCoverageInfo>>(a => a == coverage),
                It.Is<CancellationToken>(b => b == token)), Times.Never);
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

        [TestMethod]
        public void PublishNativeCoverageFiles()
        {
            // Arrange
            var logger = new TestLogger();
            TraceLogger.Initialize(logger);

            var token = new CancellationToken();
            var processor = new CoverageProcessor(_mockPublisher.Object, _mockTelemetryDataCollector.Object);
            var nativeCoverageFiles = new List<string>();
            var coverage = new List<FileCoverageInfo>
            {
                new FileCoverageInfo()
            };

            _mockPublisher.Setup(x => x.IsFileCoverageJsonSupported()).Returns(true);
            _mockPublisher.Setup(x => x.IsUploadNativeFilesToTCMSupported()).Returns(true);
            _mockParser.Setup(x => x.GetFileCoverageInfos()).Returns(coverage);

            _mockPublisher.Verify(x => x.PublishNativeCoverageFiles(
                It.Is<List<string>>( a=> a == nativeCoverageFiles),
                It.Is<CancellationToken>(b => b == token)), Times.Never);

            // Act
            processor.ParseAndPublishCoverage(_config, token, _mockParser.Object).Wait();

            // Assert
            Assert.IsTrue(logger.Log.Contains("Publishing native coverage files is supported."));
        }
      
        [TestMethod]
        public void ParseAndPublishCoverageWillPublishFileAndCodeCoverageSummary()
        {
            var token = new CancellationToken();
            var processor = new CoverageProcessor(_mockPublisher.Object, _mockTelemetryDataCollector.Object);
            var coverage = new List<FileCoverageInfo>();
            coverage.Add(new FileCoverageInfo());

            var summary = new CoverageSummary();

            summary.AddCoverageStatistics("", 3, 3, CoverageSummary.Priority.Class);

            _mockPublisher.Setup(x => x.IsFileCoverageJsonSupported()).Returns(false);
            _mockParser.Setup(x => x.GetCoverageSummary()).Returns(summary);
            
            _mockPublisher.Setup(x => x.IsFileCoverageJsonSupported()).Returns(true);
            _mockParser.Setup(x => x.GetFileCoverageInfos()).Returns(coverage);
            
            processor.ParseAndPublishCoverage(_config, token, _mockParser.Object).Wait();

            _mockPublisher.Verify(x => x.PublishCoverageSummary(
                It.Is<CoverageSummary>(a => a == summary),
                It.Is<CancellationToken>(b => b == token)));

            _mockPublisher.Verify(x => x.PublishFileCoverage(
                It.Is<List<FileCoverageInfo>>(a => a == coverage),
                It.Is<CancellationToken>(b => b == token)));
        }
      
        [TestMethod]
        public void WillNotPublishCoverageSummaryIfDataIsNotNull()
        {
            var token = new CancellationToken();
            var processor = new CoverageProcessor(_mockPublisher.Object, _mockTelemetryDataCollector.Object);
            var summary = new CoverageSummary();

            summary.AddCoverageStatistics("", 0, 0, CoverageSummary.Priority.Class);

            _mockPublisher.Setup(x => x.IsFileCoverageJsonSupported()).Returns(false);
            _mockParser.Setup(x => x.GetCoverageSummary()).Returns(summary);
            
            processor.ParseAndPublishCoverage(_config, token, _mockParser.Object).Wait();

            _mockPublisher.Verify(x => x.PublishCoverageSummary(
                It.Is<CoverageSummary>(a => a == summary),
                It.Is<CancellationToken>(b => b == token)));

           Assert.IsNotNull(summary.CodeCoverageData);
        }
    }
}
