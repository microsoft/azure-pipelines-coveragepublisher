using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CoveragePublisher.L1.Tests
{
    [TestClass]
    public class ParserTests
    {
        TestTraceListener trace;

        [TestInitialize]
        public void TestInitialize()
        {
            trace = new TestTraceListener();
            TraceLogger.Instance.AddListener(trace);
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

            var parser = new TestableParser(config);
            parser.GenerateReport(mockTool.Object);

            var tempDirSummary = Directory.EnumerateDirectories(tempDir, "Summary_*").ToList()[0];

            foreach (var summaryFile in config.CoverageFiles)
            {
                var fileName = Path.GetFileName(summaryFile);
                Assert.IsTrue(File.Exists(Path.Combine(tempDirSummary, fileName)));
            }

            //cleanup
            Directory.Delete(tempDir, true);

            Assert.IsTrue(trace.Log.Contains("CodeCoveragePublisherTrace Verbose: 0 : Parser.GenerateHTMLReport: Creating summary file directory:"));

            Assert.IsTrue(trace.Log.Contains(@"
CodeCoveragePublisherTrace Verbose: 0 : Parser.GenerateHTMLReport: Copying summary file SampleCoverage/Clover.xml
CodeCoveragePublisherTrace Verbose: 0 : Parser.GenerateHTMLReport: Copying summary file SampleCoverage/Cobertura.xml
CodeCoveragePublisherTrace Verbose: 0 : Parser.GenerateHTMLReport: Copying summary file SampleCoverage/Jacoco.xml
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

            var parser = new TestableParser(config);
            parser.GenerateReport(mockTool.Object);

            //cleanup
            Directory.Delete(tempDir, true);

            Assert.IsTrue(trace.Log.Contains(@"
CodeCoveragePublisherTrace Error: 0 : Parser.GenerateHTMLReport: Error System.Exception: error
".Trim()));
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

            var parser = new TestableParser(config);
            parser.GenerateReport(mockTool.Object);
            
            Assert.IsTrue(trace.Log.Contains("CodeCoveragePublisherTrace Verbose: 0 : Parser.GenerateHTMLReport: Directory".Trim()));
            Assert.IsTrue(trace.Log.Contains("doesn't exist, skipping copying of coverage input files.".Trim()));
        }

    }

    class TestableParser : Parser
    {
        public TestableParser(PublisherConfiguration config) : base(config) { }

        public void GenerateReport(ICoverageParserTool tool)
        {
            GenerateHTMLReport(tool);
        }
    }
}
