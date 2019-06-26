namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    internal static class Constants
    {
        public const string ReportDirectory = "Code Coverage Report";
        public const string DefaultIndexFile = "index.html";
        public const string HtmIndexFile = "index.htm";

        internal static class EnvironmentVariables
        {
            public const string BuildId = "BUILD_BUILDID";
            public const string BuildContainerId = "BUILD_CONTAINERID";
            public const string AccessToken = "SYSTEM_ACCESSTOKEN";
            public const string ProjectId = "SYSTEM_TEAMPROJECTID";
            public const string CollectionUri = "SYSTEM_TEAMFOUNDATIONCOLLECTIONURI";
            public const string AgentTempPath = "AGENT_TEMPPATH";
        }

        internal static class ArtifactUploadEventProperties
        {
            public const string ContainerFolder = "containerfolder";
            public const string ArtifactName = "artifactname";
            public const string ArtifactType = "artifacttype";
            public const string Browsable = "Browsable";
        }

        internal static class FeatureFlags
        {
            public const string EnablePublishToTcmServiceDirectlyFromTaskFF = "TestManagement.Server.EnablePublishToTcmServiceDirectlyFromTask";
            public const string TestLogStoreOnTCMService = "TestManagement.Server.TestLogStoreOnTCMService";
        }
    }
}
