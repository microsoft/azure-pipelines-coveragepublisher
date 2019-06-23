using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Resources = Microsoft.Azure.Pipelines.CoveragePublisher.Parsers.Resources;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class ParserTests
    {
        private static TestLogger _logger = new TestLogger();

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            TraceLogger.Initialize(_logger);
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _logger.Log = "";
        }

        [TestMethod]
        public void WillGenerateHTMLReportWithSummaryFilesCopied()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var mockTool = new Mock<ICoverageParserTool>();

            Directory.CreateDirectory(tempDir);

            mockTool.Setup(x => x.GenerateHTMLReport());

            var config = new PublisherConfiguration()
            {
                CoverageFiles = new List<string>() { "SampleCoverage/Clover.xml", "SampleCoverage/Cobertura.xml", "SampleCoverage/Jacoco.xml" },
                ReportDirectory = tempDir
            };

            var parser = new TestParser(config);
            parser.GenerateReport(mockTool.Object);

            var tempDirSummary = Directory.EnumerateDirectories(tempDir, "Summary_*").ToList()[0];

            foreach (var summaryFile in config.CoverageFiles)
            {
                var fileName = Path.GetFileName(summaryFile);
                Assert.IsTrue(File.Exists(Path.Combine(tempDirSummary, fileName)));
            }

            //cleanup
            Directory.Delete(tempDir, true);

            Assert.IsTrue(_logger.Log.Contains("debug: Info: Parser.GenerateHTMLReport: Creating summary file directory:"));

            Assert.IsTrue(_logger.Log.Contains(@"
debug: Info: Parser.GenerateHTMLReport: Copying summary file SampleCoverage/Clover.xml
debug: Info: Parser.GenerateHTMLReport: Copying summary file SampleCoverage/Cobertura.xml
debug: Info: Parser.GenerateHTMLReport: Copying summary file SampleCoverage/Jacoco.xml
".Trim()));

        }

        [TestMethod]
        public void WillSafelyLogException()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var mockTool = new Mock<ICoverageParserTool>();

            Directory.CreateDirectory(tempDir);

            mockTool.Setup(x => x.GenerateHTMLReport()).Throws(new Exception("error"));

            var config = new PublisherConfiguration()
            {
                CoverageFiles = new List<string>() { "SampleCoverage/Clover.xml", "SampleCoverage/Cobertura.xml", "SampleCoverage/Jacoco.xml" },
                ReportDirectory = tempDir
            };

            var parser = new TestParser(config);
            parser.GenerateReport(mockTool.Object);

            //cleanup
            Directory.Delete(tempDir, true);

            Assert.IsTrue(_logger.Log.Contains($"error: {string.Format(Resources.HTMLReportError, "error")}".Trim()));
        }

        [TestMethod]
        public void WillLogIfReportDirectoryDoesntExist()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var mockTool = new Mock<ICoverageParserTool>();

            mockTool.Setup(x => x.GenerateHTMLReport());

            var config = new PublisherConfiguration()
            {
                CoverageFiles = new List<string>() { "SampleCoverage/Clover.xml", "SampleCoverage/Cobertura.xml", "SampleCoverage/Jacoco.xml" },
                ReportDirectory = tempDir
            };

            var parser = new TestParser(config);
            parser.GenerateReport(mockTool.Object);
            
            Assert.IsTrue(_logger.Log.Contains("debug: Warning: Parser.GenerateHTMLReport: Directory".Trim()));
            Assert.IsTrue(_logger.Log.Contains("doesn't exist, skipping copying of coverage input files.".Trim()));
        }

        [TestMethod]
        public void WillGenerateReportWhenParsingSummary()
        {
            var mockTool = new Mock<ICoverageParserTool>();

            var config = new PublisherConfiguration();
            var parser = new Mock<TestParser>(config);

            parser.Setup(x => x.GenerateReport(It.IsAny<ICoverageParserTool>()));

            parser.CallBase = true;
            parser.Object.GetCoverageSummary();

            parser.Verify(x => x.GenerateReport(It.IsAny<ICoverageParserTool>()));
        }

        [TestMethod]
        public void WillGenerateReportWhenParsingFileCoverage()
        {
            var mockTool = new Mock<ICoverageParserTool>();

            var config = new PublisherConfiguration();
            var parser = new Mock<TestParser>(config);

            parser.Setup(x => x.GenerateReport(It.IsAny<ICoverageParserTool>()));

            parser.CallBase = true;
            parser.Object.GetFileCoverageInfos();

            parser.Verify(x => x.GenerateReport(It.IsAny<ICoverageParserTool>()));
        }
    }
}
