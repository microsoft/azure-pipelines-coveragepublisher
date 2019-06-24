using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using TraceLogger = Microsoft.Azure.Pipelines.CoveragePublisher.TraceLogger;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class TraceLoggerTest
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
        public void TestInfo()
        {
            TraceLogger.Info("something");
            Assert.AreEqual("info: something", _logger.Log.Trim());
        }

        [TestMethod]
        public void TestVerbose()
        {
            TraceLogger.Verbose("something");
            Assert.AreEqual("verbose: something", _logger.Log.Trim());
        }

        [TestMethod]
        public void TestWarning()
        {
            TraceLogger.Warning("something");
            Assert.AreEqual("warning: something", _logger.Log.Trim());
        }

        [TestMethod]
        public void TestError()
        {
            TraceLogger.Error("something");
            Assert.AreEqual("error: something", _logger.Log.Trim());
        }

        [TestMethod]
        public void TestDebug()
        {
            TraceLogger.Debug("something");
            Assert.AreEqual("debug: something", _logger.Log.Trim());
        }
    }
}
