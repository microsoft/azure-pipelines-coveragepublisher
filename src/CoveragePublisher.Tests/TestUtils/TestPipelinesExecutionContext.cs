// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Moq;

namespace CoveragePublisher.Tests
{
    public class TestPipelinesExecutionContext : IPipelinesExecutionContext
    {
        private Guid _projectId;
        private ITelemetryDataCollector _mockTelemetry = new Mock<ITelemetryDataCollector>().Object;
        public TestPipelinesExecutionContext(ILogger consoleLogger)
        {
            Logger = consoleLogger;
            _projectId = Guid.NewGuid();
        }

        public int BuildId => 1234;

        public long ContainerId => 12345;

        public string AccessToken => "accesstoken";

        public string CollectionUri => "collectionuri";

        public Guid ProjectId => _projectId;

        public ILogger Logger { get; private set; }

        public string TempPath => Path.GetTempPath();

        public ITelemetryDataCollector TelemetryDataCollector
        {
            get
            {
                return _mockTelemetry;
            }
            set
            {
                _mockTelemetry = value;
            }
        }
    }
}