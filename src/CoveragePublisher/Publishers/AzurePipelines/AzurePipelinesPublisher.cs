// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.TeamFoundation.TestClient.PublishTestResults;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    internal class AzurePipelinesPublisher : ICoveragePublisher
    {
        private IClientFactory _clientFactory;
        private IFeatureFlagHelper _featureFlagHelper;
        private IHtmlReportPublisher _htmlReportPublisher;
        private ILogStoreHelper _logStoreHelper;

        private static IPipelinesExecutionContext _executionContext;

        public static IPipelinesExecutionContext ExecutionContext
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

        public AzurePipelinesPublisher(IPipelinesExecutionContext executionContext, IClientFactory clientFactory, IFeatureFlagHelper featureFlagHelper, IHtmlReportPublisher htmlReportPublisher, ILogStoreHelper logStoreHelper)
        {
            _executionContext = executionContext;
            _clientFactory = clientFactory;
            _featureFlagHelper = featureFlagHelper;
            _htmlReportPublisher = htmlReportPublisher;
            _logStoreHelper = logStoreHelper;
        }

        public AzurePipelinesPublisher()
        {
            _executionContext = ExecutionContext;
            _clientFactory = new ClientFactory(new VssConnection(new Uri(_executionContext.CollectionUri), new VssBasicCredential("", _executionContext.AccessToken)));
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
                    // Upload to tcm/tfs based on feature flag
                    if (_featureFlagHelper.GetFeatureFlagState(Constants.FeatureFlags.EnablePublishToTcmServiceDirectlyFromTaskFF, false))
                    {
                        TestResultsHttpClient tcmClient = _clientFactory.GetClient<TestResultsHttpClient>();
                        await tcmClient.UpdateCodeCoverageSummaryAsync(coverageData, _executionContext.ProjectId, _executionContext.BuildId, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        TestManagementHttpClient tfsClient = _clientFactory.GetClient<TestManagementHttpClient>();
                        await tfsClient.UpdateCodeCoverageSummaryAsync(coverageData, _executionContext.ProjectId, _executionContext.BuildId, cancellationToken: cancellationToken);
                    }
                }
                catch(Exception ex)
                {
                    TraceLogger.Error(string.Format(Resources.FailedtoUploadCoverageSummary, ex.ToString()));
                }
            }
        }

        public async Task PublishFileCoverage(IList<FileCoverageInfo> coverageInfos, CancellationToken cancellationToken)
        {
            TraceLogger.Info(Resources.PublishingFileCoverage);

            var maxParallelism = Math.Min(Math.Max(Environment.ProcessorCount / 2, 1), coverageInfos.Count);
            var queue = new ConcurrentQueue<FileCoverageInfo>(coverageInfos);
            var tasks = new List<Task>();

            for(var i = 0; i < maxParallelism; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    while (queue.TryDequeue(out FileCoverageInfo file) && !cancellationToken.IsCancellationRequested)
                    {
                        var jsonFile = Path.GetTempFileName();

                        try
                        {
                            File.WriteAllText(jsonFile, JsonUtility.ToString(file));

                            Dictionary<string, string> metaData = new Dictionary<string, string>();
                            metaData.Add("ModuleName", Path.GetFileName(file.FilePath));
                            await _logStoreHelper.UploadTestBuildLogAsync(_executionContext.ProjectId, _executionContext.BuildId, TestLogType.Intermediate, jsonFile, metaData, null, true, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            TraceLogger.Error(string.Format(Resources.FailedToUploadFileCoverage, file.FilePath, ex.ToString()));
                        }

                        try
                        {
                            // Delete the generated json file
                            if (File.Exists(jsonFile))
                            {
                                File.Delete(jsonFile);
                            }
                        }
                        catch (Exception) {
                            TraceLogger.Debug(string.Format("Failed to delete temporary file: {0}", jsonFile));
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }

        public async Task PublishHTMLReport(string reportDirectory, CancellationToken token)
        {
            await _htmlReportPublisher.PublishHTMLReportAsync(reportDirectory, token);
        }
    }
}
