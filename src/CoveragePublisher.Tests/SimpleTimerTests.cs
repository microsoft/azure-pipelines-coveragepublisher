using Microsoft.Azure.Pipelines.CoveragePublisher.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.VisualStudio.Services.CustomerIntelligence.WebApi;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class SimpleTimerTests
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
        public void SimpleTimeWhenWithinThreshold()
        {

            var clientFactory = new Mock<IClientFactory>();
            var telemetryDataCollector = new Mock<TelemetryDataCollector>(clientFactory.Object);

            clientFactory
                .Setup(x => x.GetClient<CustomerIntelligenceHttpClient>())
                .Returns((CustomerIntelligenceHttpClient)null);
            telemetryDataCollector.Setup(x => x.PublishTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()));

            using (new SimpleTimer("testTimer", "Test", "test", telemetryDataCollector.Object, TimeSpan.FromHours(1), true)) { }

            Assert.IsTrue(_logger.Log.Contains("debug: PERF : testTimer : took "));
        }

        [TestMethod]
        public void SimpleTimeWhenOutSideThreshold()
        {
            var clientFactory = new Mock<IClientFactory>();
            var telemetryDataCollector = new Mock<TelemetryDataCollector>(clientFactory.Object);

            clientFactory
                .Setup(x => x.GetClient<CustomerIntelligenceHttpClient>())
                .Returns((CustomerIntelligenceHttpClient)null);
            telemetryDataCollector.Setup(x =>
                x.PublishTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()));

            using (new SimpleTimer("testTimer", "Test", "test", telemetryDataCollector.Object, TimeSpan.FromMilliseconds(0), true)) { }

            Assert.IsTrue(_logger.Log.Contains("debug: PERF : testTimer : took "));
        }

        [TestMethod]
        public void SimpleTimeWorksWhenDisposedCalledTwice()
        {
            var clientFactory = new Mock<IClientFactory>();
            var telemetryDataCollector = new Mock<TelemetryDataCollector>(clientFactory.Object);

            clientFactory
                .Setup(x => x.GetClient<CustomerIntelligenceHttpClient>())
                .Returns((CustomerIntelligenceHttpClient)null);
            telemetryDataCollector.Setup(x => x.PublishTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()));

            var timer = new SimpleTimer("testTimer", "Test", "test", telemetryDataCollector.Object, TimeSpan.FromMilliseconds(10), true);

            timer.Dispose();
            timer.Dispose();
        }
    }
}
