using Microsoft.Azure.Pipelines.CoveragePublisher.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.VisualStudio.Services.CustomerIntelligence.WebApi;
using TraceLogger = Microsoft.Azure.Pipelines.CoveragePublisher.TraceLogger;

namespace CoveragePublisher.L0.Tests
{
    [TestClass]
    public class SimpleTimerTests
    {
        private TestTraceListener trace;

        [TestInitialize]
        public void TestInitialize()
        {
            trace = new TestTraceListener();
            TraceLogger.ResetLogger();
            TraceLogger.Instance.AddListener(trace);
        }

        [TestMethod]
        public void SimpleTimeWhenWithinThreshold()
        {
            var logger = TraceLogger.Instance;

            var clientFactory = new Mock<ClientFactory>(null);
            var telemetryDataCollector = new Mock<TelemetryDataCollector>(clientFactory.Object, logger);

            clientFactory
                .Setup(x => x.GetClient<CustomerIntelligenceHttpClient>())
                .Returns((CustomerIntelligenceHttpClient)null);
            telemetryDataCollector.Setup(x => x.PublishTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()));

            using (new SimpleTimer("testTimer", "Test", "test", logger, telemetryDataCollector.Object, TimeSpan.FromHours(1), true)) { }

            Assert.IsTrue(trace.Log.Contains("Verbose"));
        }

        [TestMethod]
        public void SimpleTimeWhenOutSideThreshold()
        {
            var logger = TraceLogger.Instance;

            var clientFactory = new Mock<ClientFactory>(null);
            var telemetryDataCollector = new Mock<TelemetryDataCollector>(clientFactory.Object, logger);

            clientFactory
                .Setup(x => x.GetClient<CustomerIntelligenceHttpClient>())
                .Returns((CustomerIntelligenceHttpClient)null);
            telemetryDataCollector.Setup(x =>
                x.PublishTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()));

            using (new SimpleTimer("testTimer", "Test", "test", logger, telemetryDataCollector.Object, TimeSpan.FromMilliseconds(0), true)) { }

            Assert.IsTrue(trace.Log.Contains("Warning"));
        }

        [TestMethod]
        public void SimpleTimeWorksWhenDisposedCalledTwice()
        {
            var logger = TraceLogger.Instance;

            var clientFactory = new Mock<ClientFactory>(null);
            var telemetryDataCollector = new Mock<TelemetryDataCollector>(clientFactory.Object, logger);

            clientFactory
                .Setup(x => x.GetClient<CustomerIntelligenceHttpClient>())
                .Returns((CustomerIntelligenceHttpClient)null);
            telemetryDataCollector.Setup(x => x.PublishTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()));

            var timer = new SimpleTimer("testTimer", "Test", "test",
                logger, telemetryDataCollector.Object, TimeSpan.FromMilliseconds(10), true);

            timer.Dispose();
            timer.Dispose();
        }
    }
}
