using Microsoft.Azure.Pipelines.CoveragePublisher.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.VisualStudio.Services.CustomerIntelligence.WebApi;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

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
            var telemetryDataCollector = new Mock<ITelemetryDataCollector>();

            clientFactory
                .Setup(x => x.GetClient<CustomerIntelligenceHttpClient>())
                .Returns((CustomerIntelligenceHttpClient)null);
            telemetryDataCollector.Setup(x => x.PublishTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()));

            using (new SimpleTimer("Test", "test", telemetryDataCollector.Object)) { }

            Assert.IsTrue(_logger.Log.Contains("debug: PERF : Test.test : took "));
        }

        [TestMethod]
        public void SimpleTimeWhenOutSideThreshold()
        {
            var clientFactory = new Mock<IClientFactory>();
            var telemetryDataCollector = new Mock<ITelemetryDataCollector>();

            clientFactory
                .Setup(x => x.GetClient<CustomerIntelligenceHttpClient>())
                .Returns((CustomerIntelligenceHttpClient)null);
            telemetryDataCollector.Setup(x =>
                x.PublishTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()));

            using (new SimpleTimer("Test", "test", telemetryDataCollector.Object)) { }

            Assert.IsTrue(_logger.Log.Contains("debug: PERF : Test.test : took "));
        }

        [TestMethod]
        public void SimpleTimeWorksWhenDisposedCalledTwice()
        {
            var clientFactory = new Mock<IClientFactory>();
            var telemetryDataCollector = new Mock<ITelemetryDataCollector>();

            clientFactory
                .Setup(x => x.GetClient<CustomerIntelligenceHttpClient>())
                .Returns((CustomerIntelligenceHttpClient)null);
            telemetryDataCollector.Setup(x => x.PublishTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()));

            var timer = new SimpleTimer("Test", "test", telemetryDataCollector.Object);

            timer.Dispose();
            timer.Dispose();
        }
    }
}
