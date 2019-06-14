using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using TraceLogger = Microsoft.Azure.Pipelines.CoveragePublisher.TraceLogger;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class TraceLoggerTest
    {
        [TestMethod]
        public void TestInfo()
        {
            var mockListener = new MockTraceListener();
            var logger = TraceLogger.Instance;
            logger.AddListener(mockListener);
            logger.Info("something");
            Assert.AreEqual("something", mockListener.Message);
            Assert.AreEqual("CodeCoveragePublisherTrace Information: 0 : ", mockListener.WriteMessage);
        }

        [TestMethod]
        public void TestVerbose()
        {
            var mockListener = new MockTraceListener();
            var logger = TraceLogger.Instance;
            logger.AddListener(mockListener);
            logger.Verbose("something");
            Assert.AreEqual("something", mockListener.Message);
            Assert.AreEqual("CodeCoveragePublisherTrace Verbose: 0 : ", mockListener.WriteMessage);
        }

        [TestMethod]
        public void TestWarning()
        {
            var mockListener = new MockTraceListener();
            var logger = TraceLogger.Instance;
            logger.AddListener(mockListener);
            logger.Warning("something");
            Assert.AreEqual("something", mockListener.Message);
            Assert.AreEqual("CodeCoveragePublisherTrace Warning: 0 : ", mockListener.WriteMessage);
        }

        [TestMethod]
        public void TestError()
        {
            var mockListener = new MockTraceListener();
            var logger = TraceLogger.Instance;
            logger.AddListener(mockListener);
            logger.Error("something");
            Assert.AreEqual("something", mockListener.Message);
            Assert.AreEqual("CodeCoveragePublisherTrace Error: 0 : ", mockListener.WriteMessage);
        }

        [TestMethod]
        public void TestDebug()
        {
            var mockListener = new MockTraceListener();
            var logger = TraceLogger.Instance;
            logger.AddListener(mockListener);
            logger.Debug("something");
            Assert.AreEqual("something", mockListener.Message);
            Assert.AreEqual("CodeCoveragePublisherTrace Verbose: 0 : ", mockListener.WriteMessage);
        }
    }

    public class MockTraceListener : TraceListener
    {
        public override void Write(string message)
        {
            WriteMessage = message;
        }

        public override void WriteLine(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }

        public string WriteMessage { get; private set; }
    }
}
