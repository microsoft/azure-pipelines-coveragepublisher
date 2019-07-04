using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class LogStoreTraceListenerTests
    {
        [TestMethod]
        public void LogStoreTraceListenerTest()
        {
            var logger = new TestLogger();
            TraceLogger.Initialize(logger);

            var listener = new LogStoreTraceListener();
            listener.Write("message1");
            listener.WriteLine("message2");

            Assert.IsTrue(logger.Log.Contains(@"
debug: message1
debug: message2
".Trim()));
        }
    }
}
