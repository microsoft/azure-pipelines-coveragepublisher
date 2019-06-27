
using System;
using System.IO;
using Microsoft.Azure.Pipelines.CoveragePublisher.DefaultPublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace CoveragePublisher.Tests
{
    public class TestPipelinesExecutionContext : IPipelinesExecutionContext
    {
        private Guid _projectId;
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
    }
}