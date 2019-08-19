// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class TelemetryDataCollectorTests
    {

        [TestMethod]
        public void AddOrUpdateWithDupsWorksFine()
        {
            var telemetryDataCollector = new TestableTelemetryDataCollector(true);

            telemetryDataCollector.AddOrUpdate("Property", "Value");
            Assert.IsTrue(telemetryDataCollector.Properties["Property"].Equals("Value"));

            telemetryDataCollector.AddOrUpdate("Property", () => "Someothervalue");
            Assert.IsTrue(telemetryDataCollector.Properties["Property"].Equals("Someothervalue"));

        }

        [TestMethod]
        public void AddAndAggregateWithDupsWorksFine()
        {
            var telemetryDataCollector = new TestableTelemetryDataCollector(true);

            telemetryDataCollector.AddAndAggregate("Property", "Value");
            telemetryDataCollector.AddAndAggregate("Property", "Someothervalue");

            Assert.IsTrue(telemetryDataCollector.Properties["Property"].Equals("Someothervalue"));

            telemetryDataCollector.AddAndAggregate("Property1", (new[] { "Value" }).ToList());
            telemetryDataCollector.AddAndAggregate("Property1", (new[] { "Someother" }).ToList());

            Assert.IsTrue(((List<string>)telemetryDataCollector.Properties["Property1"]).Count == 2);

            telemetryDataCollector.AddAndAggregate("Property1", "Someother1");
            Assert.IsTrue(((List<string>)telemetryDataCollector.Properties["Property1"]).Count == 3);

            telemetryDataCollector.AddAndAggregate("Property1", () => "Someother2");
            Assert.IsTrue(((List<string>)telemetryDataCollector.Properties["Property1"]).Count == 4);
        }

        [TestMethod]
        public void AddAndAggregateWithDupsWorksFineWithInt()
        {
            var telemetryDataCollector = new TestableTelemetryDataCollector(true);

            telemetryDataCollector.AddAndAggregate("Property", 1);
            telemetryDataCollector.AddAndAggregate("Property", 1);

            Assert.IsTrue((int)telemetryDataCollector.Properties["Property"] == 2);
        }

        [TestMethod]
        public void AddAndAggregateWithDupsWorksFineWithDouble()
        {
            var telemetryDataCollector = new TestableTelemetryDataCollector(true);

            telemetryDataCollector.AddAndAggregate("Property", 1.1);
            telemetryDataCollector.AddAndAggregate("Property", 1.1);

            Assert.IsTrue((double)telemetryDataCollector.Properties["Property"] == 2.2);
        }


        [TestMethod]
        public void AddFailureWorksFine()
        {
            var telemetryDataCollector = new TestableTelemetryDataCollector(true);

            Assert.AreEqual(((ConcurrentBag<Exception>)telemetryDataCollector.Properties["Failures"]).Count, 0);

            var error = new Exception();

            telemetryDataCollector.AddFailure(error);
            telemetryDataCollector.AddFailure(error);

            var enumerator = ((ConcurrentBag<Exception>)telemetryDataCollector.Properties["Failures"]).GetEnumerator();
            enumerator.MoveNext();

            Assert.AreEqual(((ConcurrentBag<Exception>)telemetryDataCollector.Properties["Failures"]).Count, 2);
            Assert.AreEqual(enumerator.Current, error);
        }

        [TestMethod]
        public void WillNotCollectTelemetryWhenDisabled()
        {
            var telemetryDataCollector = new TestableTelemetryDataCollector(false);

            var error = new Exception();

            telemetryDataCollector.AddFailure(error);
            Assert.AreEqual(((ConcurrentBag<Exception>)telemetryDataCollector.Properties["Failures"]).Count, 0);

            telemetryDataCollector.AddAndAggregate("Property", 1.1);
            telemetryDataCollector.AddAndAggregate("Property", () => 1.1);

            telemetryDataCollector.AddOrUpdate("Property", 1.1);
            telemetryDataCollector.AddOrUpdate("Property", () => 1.1);

            Assert.ThrowsException<KeyNotFoundException>(() => telemetryDataCollector.Properties["Property"]);
        }
    }

    public class TestableTelemetryDataCollector: TelemetryDataCollector
    {
        public TestableTelemetryDataCollector(bool enabled): base(enabled) {}

        public override Task PublishCumulativeTelemetryAsync()
        {
            return Task.CompletedTask;
        }

        public override Task PublishTelemetryAsync(string feature, Dictionary<string, object> properties)
        {
            return Task.CompletedTask;
        }
    }
}
