// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Utils;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    internal class AzurePipelinesPublisher : ICoveragePublisher
    {
        private IClientFactory _clientFactory;
        private IFeatureFlagHelper _featureFlagHelper;
        private IHtmlReportPublisher _htmlReportPublisher;
        private ILogStoreHelper _logStoreHelper;
        private bool _telemetryEnabled;

        private static IPipelinesExecutionContext _executionContext;

        public IPipelinesExecutionContext ExecutionContext
        {
            get
            {
                if (_executionContext == null)
                {
                    _executionContext = new PipelinesExecutionContext();
                }
                return _executionContext;
            }
        }

        public AzurePipelinesPublisher(IPipelinesExecutionContext executionContext, IClientFactory clientFactory, IFeatureFlagHelper featureFlagHelper, IHtmlReportPublisher htmlReportPublisher, ILogStoreHelper logStoreHelper, bool enableTelemetry)
        {
            _telemetryEnabled = enableTelemetry;
            _clientFactory = clientFactory;
            _executionContext = executionContext;
            _featureFlagHelper = featureFlagHelper;
            _htmlReportPublisher = htmlReportPublisher;
            _logStoreHelper = logStoreHelper;
        }

        public AzurePipelinesPublisher(bool enableTelemetry)
        {
            var context = new PipelinesExecutionContext();

            _telemetryEnabled = enableTelemetry;
            _executionContext = context;
            _clientFactory = new ClientFactory(this.GetVssConnection());

            context.SetTelemetryDataCollector(new PipelinesTelemetry(_clientFactory, _telemetryEnabled));

            _featureFlagHelper = new FeatureFlagHelper(_clientFactory);
            _htmlReportPublisher = new HtmlReportPublisher(_executionContext, _clientFactory);
            _logStoreHelper = new LogStoreHelper(_clientFactory);
        }

        public bool IsFileCoverageJsonSupported()
        {
            return _featureFlagHelper.GetFeatureFlagState(Constants.FeatureFlags.TestLogStoreOnTCMService, true);
        }

        public async Task PublishCoverageSummary(CoverageSummary coverageSummary, CancellationToken cancellationToken)
        {
            var coverageData = coverageSummary.CodeCoverageData;
            var coverageStats = coverageData?.CoverageStats;

            if (coverageData != null && coverageStats != null && coverageStats.Count() > 0)
            {
                // log coverage stats
                TraceLogger.Info(Resources.PublishingCodeCoverageSummary);
                foreach (var coverage in coverageStats)
                {
                    TraceLogger.Info(string.Format(Resources.CoveredStats, coverage.Label, coverage.Covered, coverage.Total));
                }

                try
                {
                    var uploadToTcm = _featureFlagHelper.GetFeatureFlagState(Constants.FeatureFlags.EnablePublishToTcmServiceDirectlyFromTaskFF, false);
                    _executionContext.TelemetryDataCollector.AddOrUpdate("UploadToTcm", uploadToTcm.ToString());

                    // Upload to tcm/tfs based on feature flag
                    if (uploadToTcm)
                    {
                        TestResultsHttpClient tcmClient = _clientFactory.GetClient<TestResultsHttpClient>();
                        using (new SimpleTimer("AzurePipelinesPublisher", "UploadSummary", _executionContext.TelemetryDataCollector))
                        {
                            await tcmClient.UpdateCodeCoverageSummaryAsync(coverageData, _executionContext.ProjectId, _executionContext.BuildId, cancellationToken: cancellationToken);
                        }
                    }
                    else
                    {

                        TestManagementHttpClient tfsClient = _clientFactory.GetClient<TestManagementHttpClient>();
                        using (new SimpleTimer("AzurePipelinesPublisher", "UploadSummary", _executionContext.TelemetryDataCollector))
                        {
                            await tfsClient.UpdateCodeCoverageSummaryAsync(coverageData, _executionContext.ProjectId, _executionContext.BuildId, cancellationToken: cancellationToken);
                        }
                    }
                }
                catch(Exception ex)
                {
                    TraceLogger.Error(string.Format(Resources.FailedtoUploadCoverageSummary, ex.ToString()));
                }
            }
            else{
                TraceLogger.Warning(Resources.FailedtoUploadCoverageSummary);
            }
        }

        public async Task PublishFileCoverage(IList<FileCoverageInfo> coverageInfos, CancellationToken cancellationToken)
        {
            if(coverageInfos.Count == 0)
            {
                return;
            }

            TraceLogger.Info(Resources.PublishingFileCoverage);

            var maxParallelism = Math.Min(Math.Max(Environment.ProcessorCount / 2, 1), coverageInfos.Count);
            var queue = new ConcurrentQueue<FileCoverageInfo>(coverageInfos);
            var tasks = new List<Task>();
            var jsonFile = Path.Combine(_executionContext.TempPath, Guid.NewGuid().ToString() + _executionContext.BuildId.ToString() + ".cjson");

            try
            {
                var fileContent = JsonUtility.ToString(coverageInfos);
                File.WriteAllText(jsonFile, fileContent);

                _executionContext.TelemetryDataCollector.AddOrUpdate("FileCoverageLength", fileContent.Length);

                Dictionary<string, string> metaData = new Dictionary<string, string>();
                using (new SimpleTimer("AzurePipelinesPublisher", "UploadFileCoverage", _executionContext.TelemetryDataCollector))
                {
                    await _logStoreHelper.UploadTestBuildLogAsync(_executionContext.ProjectId, _executionContext.BuildId, TestLogType.Intermediate, jsonFile, metaData, null, true, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                TraceLogger.Error(string.Format(Resources.FailedToUploadFileCoverage, ex));
            }

            try
            {
                // Delete the generated json file
                if (File.Exists(jsonFile))
                {
                    File.Delete(jsonFile);
                }
            }
            catch (Exception)
            {
                TraceLogger.Debug(string.Format("Failed to delete temporary file: {0}", jsonFile));
            }
        }

        public async Task PublishHTMLReport(string reportDirectory, CancellationToken token)
        {
            await _htmlReportPublisher.PublishHTMLReportAsync(reportDirectory, token);
        }

        public bool IsUploadNativeFilesToTCMSupported()
        {
            return _featureFlagHelper.GetFeatureFlagState(Constants.FeatureFlags.UploadNativeCoverageFilesToLogStore, false);
        }

        private VssConnection GetVssConnection()
        {
            var proxy = VssProxyHelper.GetProxy();
            if (proxy != null)
            {
                VssHttpMessageHandler.DefaultWebProxy = proxy;
            }

            var connectionSettings = VssSettingsConfiguration.GetSettings();
            return new VssConnection(new Uri(_executionContext.CollectionUri), new VssBasicCredential("", _executionContext.AccessToken), connectionSettings);
        }
    }
}
