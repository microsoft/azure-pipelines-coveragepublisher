﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.CustomerIntelligence.WebApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CoveragePublisher.L0.Tests
{
    [TestClass]
    public class TelemetryDataCollectorTests
    {
        [TestMethod]
        public void PublishTelemetryAsyncTest()
        {
            var logger = TraceLogger.Instance;
            var clientFactory = new Mock<IClientFactory>();
            var telemetryDataCollector = new TelemetryDataCollector(clientFactory.Object, logger);
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
            var logger = TraceLogger.Instance;
            var clientFactory = new Mock<IClientFactory>();
            var telemetryDataCollector = new TelemetryDataCollector(clientFactory.Object, logger);

            telemetryDataCollector.AddOrUpdate("Property", "Value");
            telemetryDataCollector.AddOrUpdate("Property", "Someothervalue");

            Assert.IsTrue(telemetryDataCollector.Properties["Property"].Equals("Someothervalue"));
        }

        [TestMethod]
        public void AddAndAggregateWithDupsWorksFine()
        {
            var logger = TraceLogger.Instance;
            var clientFactory = new Mock<IClientFactory>();
            var telemetryDataCollector = new TelemetryDataCollector(clientFactory.Object, logger);

            telemetryDataCollector.AddAndAggregate("Property", "Value");
            telemetryDataCollector.AddAndAggregate("Property", "Someothervalue");

            Assert.IsTrue(telemetryDataCollector.Properties["Property"].Equals("Someothervalue"));

            telemetryDataCollector.AddAndAggregate("Property1", (new[] { "Value" }).ToList());
            telemetryDataCollector.AddAndAggregate("Property1", (new[] { "Someother" }).ToList());

            Assert.IsTrue(((List<string>) telemetryDataCollector.Properties["Property1"]).Count == 2);

            telemetryDataCollector.AddAndAggregate("Property1", "Someother1" );

            Assert.IsTrue(((List<string>) telemetryDataCollector.Properties["Property1"]).Count == 3);
        }

        [TestMethod]
        public void AddAndAggregateWithDupsWorksFineWithInt()
        {
            var logger = TraceLogger.Instance;
            var clientFactory = new Mock<IClientFactory>();
            var telemetryDataCollector = new TelemetryDataCollector(clientFactory.Object, logger);

            telemetryDataCollector.AddAndAggregate("Property", 1);
            telemetryDataCollector.AddAndAggregate("Property", 1);

            Assert.IsTrue((int)telemetryDataCollector.Properties["Property"] == 2);
        }

        [TestMethod]
        public void AddAndAggregateWithDupsWorksFineWithDouble()
        {
            var logger = TraceLogger.Instance;
            var clientFactory = new Mock<IClientFactory>();
            var telemetryDataCollector = new TelemetryDataCollector(clientFactory.Object, logger);

            telemetryDataCollector.AddAndAggregate("Property", 1.1);
            telemetryDataCollector.AddAndAggregate("Property", 1.1);

            Assert.IsTrue((double)telemetryDataCollector.Properties["Property"] == 2.2);
        }

        [TestMethod]
        public void PublishCumulativeTelemetryAsyncTest()
        {
            var logger = TraceLogger.Instance;
            var clientFactory = new Mock<IClientFactory>();
            var telemetryDataCollector = new TelemetryDataCollector(clientFactory.Object, logger);

            telemetryDataCollector.AddAndAggregate("Property", 1.1);
            telemetryDataCollector.AddAndAggregate("Property", 1.1);

            Assert.IsTrue((double)telemetryDataCollector.Properties["Property"] == 2.2);

            telemetryDataCollector.PublishCumulativeTelemetryAsync();

            Assert.IsTrue(telemetryDataCollector.Properties.Count == 0);
        }
    }
}
