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
        [TestMethod]
        public void SimpleTimeWhenWithinThreshold()
        {
            var verboseLogCalled = false;
            var logger = new Mock<TraceLogger>(new TextWriterTraceListener());
            var clientFactory = new Mock<ClientFactory>(null);
            var telemetryDataCollector = new Mock<TelemetryDataCollector>(clientFactory.Object, logger.Object);

            clientFactory
                .Setup(x => x.GetClient<CustomerIntelligenceHttpClient>())
                .Returns((CustomerIntelligenceHttpClient)null);
            logger.Setup(x => x.Verbose(It.IsAny<string>())).Callback((() => verboseLogCalled = true));
            telemetryDataCollector.Setup(x => x.PublishTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Callback((() => verboseLogCalled = true));

            using (new SimpleTimer("testTimer", "Test", "test",
                logger.Object, telemetryDataCollector.Object, TimeSpan.FromHours(1), true)) ;

            Assert.IsTrue(verboseLogCalled);
        }

        [TestMethod]
        public void SimpleTimeWhenOutSideThreshold()
        {
            var warningLogCalled = false;
            var logger = new Mock<TraceLogger>(new TextWriterTraceListener());
            var clientFactory = new Mock<ClientFactory>(null);
            var telemetryDataCollector = new Mock<TelemetryDataCollector>(clientFactory.Object, logger.Object);

            clientFactory
                .Setup(x => x.GetClient<CustomerIntelligenceHttpClient>())
                .Returns((CustomerIntelligenceHttpClient)null);
            logger.Setup(x => x.Warning(It.IsAny<string>())).Callback((() => warningLogCalled = true));
            telemetryDataCollector.Setup(x =>
                x.PublishTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()));

            using (new SimpleTimer("testTimer", "Test", "test",
                logger.Object, telemetryDataCollector.Object, TimeSpan.FromMilliseconds(0), true)) ;

            Assert.IsTrue(warningLogCalled);
        }

        [TestMethod]
        public void SimpleTimeWorksWhenDisposedCalledTwice()
        {
            var verboseLogCalled = false;
            var logger = new Mock<TraceLogger>(new TextWriterTraceListener());
            var clientFactory = new Mock<ClientFactory>(null);
            var telemetryDataCollector = new Mock<TelemetryDataCollector>(clientFactory.Object, logger.Object);

            clientFactory
                .Setup(x => x.GetClient<CustomerIntelligenceHttpClient>())
                .Returns((CustomerIntelligenceHttpClient)null);
            logger.Setup(x => x.Verbose(It.IsAny<string>())).Callback((() => verboseLogCalled = true));
            telemetryDataCollector.Setup(x => x.PublishTelemetryAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Callback((() => verboseLogCalled = true));

            var timer = new SimpleTimer("testTimer", "Test", "test",
                logger.Object, telemetryDataCollector.Object, TimeSpan.FromMilliseconds(0), true);

            timer.Dispose();

            timer.Dispose();
        }
    }
}
