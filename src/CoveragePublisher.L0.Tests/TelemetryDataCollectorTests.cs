using System;
using System.Collections.Generic;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Edm.Csdl;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.CustomerIntelligence.WebApi;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Microsoft.VisualStudio.Services.WebPlatform;

namespace CoveragePublisher.L0.Tests
{
    [TestClass]
    public class TelemetryDataCollectorTests
    {
        [TestMethod]
        public void PublishTelemetryAsyncTest()
        {
            var logger = new Mock<TraceLogger>(new TextWriterTraceListener());
            var clientFactory = new Mock<ClientFactory>(null);
            var telemetryDataCollector = new TelemetryDataCollector(clientFactory.Object, logger.Object);
            var ciHttpClient =
                new Mock<CustomerIntelligenceHttpClient>(new Uri("https://somename.Visualstudio.com"), new VssCredentials());

            clientFactory
                .Setup(x => x.GetClient<CustomerIntelligenceHttpClient>())
                .Returns(ciHttpClient.Object);

            telemetryDataCollector.PublishTelemetryAsync("Feature", new Dictionary<string, object>());
        }

        [TestMethod]
        public void AddOrUpdateWithDupsWorksFine()
        {
            var logger = new Mock<TraceLogger>(new TextWriterTraceListener());
            var clientFactory = new Mock<ClientFactory>(null);
            var telemetryDataCollector = new TelemetryDataCollector(clientFactory.Object, logger.Object);

            telemetryDataCollector.AddOrUpdate("Property", "Value");
            telemetryDataCollector.AddOrUpdate("Property", "Someothervalue");
            telemetryDataCollector.PublishCumulativeTelemetryAsync();
        }

        [TestMethod]
        public void AddAndAggregateWithDupsWorksFine()
        {
            var logger = new Mock<TraceLogger>(new TextWriterTraceListener());
            var clientFactory = new Mock<ClientFactory>(null);
            var telemetryDataCollector = new TelemetryDataCollector(clientFactory.Object, logger.Object);

            telemetryDataCollector.AddAndAggregate("Property", "Value");
            telemetryDataCollector.AddAndAggregate("Property", "Someothervalue");

            telemetryDataCollector.AddAndAggregate("Property", new string[] { "Value" });
            telemetryDataCollector.AddAndAggregate("Property", new string[] { "Someother" });
        }
    }
}
