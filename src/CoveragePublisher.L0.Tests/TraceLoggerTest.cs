using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using TraceLogger = Microsoft.Azure.Pipelines.CoveragePublisher.TraceLogger;

namespace CoveragePublisher.L0.Tests
{
    [TestClass]
    public class TraceLoggerTest
    {
        [TestMethod]
        public void TestInfo()
        {
            var mockListener = new MockTraceListener();
            TraceLogger logger = new TraceLogger(mockListener);
            logger.Info("something");
            Assert.AreEqual("something", mockListener.Message);
            Assert.AreEqual("CodeCoveragePublisherTrace Information: 0 : ", mockListener.WriteMessage);
        }

        [TestMethod]
        public void TestDebug()
        {
            var mockListener = new MockTraceListener();
            TraceLogger logger = new TraceLogger(mockListener);
            logger.Verbose("something");
            Assert.AreEqual("something", mockListener.Message);
            Assert.AreEqual("CodeCoveragePublisherTrace Verbose: 0 : ", mockListener.WriteMessage);
        }

        [TestMethod]
        public void TestWarning()
        {
            var mockListener = new MockTraceListener();
            TraceLogger logger = new TraceLogger(mockListener);
            logger.Warning("something");
            Assert.AreEqual("something", mockListener.Message);
            Assert.AreEqual("CodeCoveragePublisherTrace Warning: 0 : ", mockListener.WriteMessage);
        }

        [TestMethod]
        public void TestError()
        {
            var mockListener = new MockTraceListener();
            TraceLogger logger = new TraceLogger(mockListener);
            logger.Error("something");
            Assert.AreEqual("something", mockListener.Message);
            Assert.AreEqual("CodeCoveragePublisherTrace Error: 0 : ", mockListener.WriteMessage);
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
