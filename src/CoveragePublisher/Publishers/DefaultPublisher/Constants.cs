// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
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

            public static class ProxyConfiguration
            {
                public const string ProxyUrl = "proxyurl";
                public const string ProxyUserName = "proxyusername";
                public const string ProxyPassword = "proxypassword";
                public const string ProxyByPassList = "proxybypasslist";
            }
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
            public const string UploadNativeCoverageFilesToLogStore = "TestManagement.Server.UploadNativeCoverageFilesToLogStore";
            public const string TestLogStoreOnTCMService = "TestManagement.Server.TestLogStoreOnTCMService";
        }

        internal static class CoverageConstants
        {
            public const string CoverageFileExtension = ".coverage";
            public const string CoverageBufferFileExtension = ".coveragebuffer";
            public const string CoverageXFileExtension = ".covx";
            public const string CoverageBFileExtension = ".covb";
            public const string CoverageJsonFileExtension = ".cjson";
        }
    }
}
