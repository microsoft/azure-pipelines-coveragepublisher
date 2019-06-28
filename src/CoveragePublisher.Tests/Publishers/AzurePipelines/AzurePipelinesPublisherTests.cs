using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class AzurePipelinesPublisherTests
    {
        private TestLogger _logger = new TestLogger();
        private IPipelinesExecutionContext _context;
        private Mock<IClientFactory> _mockClientFactory = new Mock<IClientFactory>();
        private Mock<IFeatureFlagHelper> _mockFFHelper = new Mock<IFeatureFlagHelper>();
        private Mock<IHtmlReportPublisher> _mockHtmlPublisher = new Mock<IHtmlReportPublisher>();
        private Mock<ILogStoreHelper> _mockLogStoreHelper = new Mock<ILogStoreHelper>();

        [TestInitialize]
        public void TestInitialize()
        {
            _context = new TestPipelinesExecutionContext(_logger);
            _logger.Log = "";

            _mockClientFactory.Reset();
            _mockFFHelper.Reset();
            _mockHtmlPublisher.Reset();
            _mockLogStoreHelper.Reset();
        }

        [TestMethod]
        public void WillPublishHtmlReport()
        {
            var publisher = new AzurePipelinesPublisher(_context, _mockClientFactory.Object, _mockFFHelper.Object, _mockHtmlPublisher.Object, _mockLogStoreHelper.Object);
            var token = new CancellationToken();

            _mockHtmlPublisher.Setup(x => x.PublishHTMLReportAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            publisher.PublishHTMLReport("directory", token).Wait();

            _mockHtmlPublisher.Verify(x => x.PublishHTMLReportAsync(
                It.Is<string>(a => a == "directory"),
                It.Is<CancellationToken>(b => b == token)));
        }

        [TestMethod]
        public void WillPublishCoverageSummaryToTcm()
        {
            var summary = new CoverageSummary("flavor", "platform");
            var token = new CancellationToken();

            summary.AddCoverageStatistics("lines", 10, 5, CoverageSummary.Priority.Line);
            summary.AddCoverageStatistics("blocks", 10, 5, CoverageSummary.Priority.Other);

            var publisher = new AzurePipelinesPublisher(_context, _mockClientFactory.Object, _mockFFHelper.Object, _mockHtmlPublisher.Object, _mockLogStoreHelper.Object);
            var mockClient = new Mock<TestResultsHttpClient>(new Uri("http://localhost"), new VssCredentials());

            _mockClientFactory.Setup(x => x.GetClient<TestResultsHttpClient>()).Returns(mockClient.Object);
            _mockFFHelper.Setup(x => x.GetFeatureFlagState(
                It.Is<string>(a => a == Constants.FeatureFlags.EnablePublishToTcmServiceDirectlyFromTaskFF),
                It.Is<bool>(b => b == false))).Returns(true);

            publisher.PublishCoverageSummary(summary, token).Wait();

            mockClient.Verify(x => x.UpdateCodeCoverageSummaryAsync(
                It.Is<CodeCoverageData>(a => a == summary.CodeCoverageData),
                It.Is<Guid>(b => b == _context.ProjectId),
                It.Is<int>(c => c == _context.BuildId),
                It.IsAny<object>(),
                It.Is<CancellationToken>(d => d == token)));
        }

        [TestMethod]
        public void WillPublishCoverageSummaryToTfs()
        {
            var summary = new CoverageSummary("flavor", "platform");
            summary.AddCoverageStatistics("lines", 10, 5, CoverageSummary.Priority.Line);
            summary.AddCoverageStatistics("blocks", 10, 5, CoverageSummary.Priority.Other);

            var token = new CancellationToken();
            var publisher = new AzurePipelinesPublisher(_context, _mockClientFactory.Object, _mockFFHelper.Object, _mockHtmlPublisher.Object, _mockLogStoreHelper.Object);
            var mockClient = new Mock<TestManagementHttpClient>(new Uri("http://localhost"), new VssCredentials());

            _mockClientFactory.Setup(x => x.GetClient<TestManagementHttpClient>()).Returns(mockClient.Object);
            _mockFFHelper.Setup(x => x.GetFeatureFlagState(
                It.Is<string>(a => a == Constants.FeatureFlags.EnablePublishToTcmServiceDirectlyFromTaskFF),
                It.Is<bool>(b => b == false))).Returns(false);

            publisher.PublishCoverageSummary(summary, token).Wait();

            mockClient.Verify(x => x.UpdateCodeCoverageSummaryAsync(
                It.Is<CodeCoverageData>(a => a == summary.CodeCoverageData),
                It.Is<Guid>(b => b == _context.ProjectId),
                It.Is<int>(c => c == _context.BuildId),
                It.IsAny<object>(),
                It.Is<CancellationToken>(d => d == token)));
        }

        [TestMethod]
        public void WillPublishFileCoverage()
        {
            var token = new CancellationToken();
            var publisher = new AzurePipelinesPublisher(_context, _mockClientFactory.Object, _mockFFHelper.Object, _mockHtmlPublisher.Object, _mockLogStoreHelper.Object);
            var mockClient = new Mock<TestManagementHttpClient>(new Uri("http://localhost"), new VssCredentials());

            var data = new Dictionary<string, string>();

            _mockClientFactory.Setup(x => x.GetClient<TestManagementHttpClient>()).Returns(mockClient.Object);
            _mockFFHelper.Setup(x => x.GetFeatureFlagState(
                It.Is<string>(a => a == Constants.FeatureFlags.EnablePublishToTcmServiceDirectlyFromTaskFF),
                It.Is<bool>(b => b == false))).Returns(false);

            _mockLogStoreHelper.Setup(x => x.UploadTestBuildLogAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<TestLogType>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
                .Callback<Guid, int, TestLogType, string, Dictionary<string, string>, string, bool, CancellationToken>(
                    (a, b, c , d, e, f, g, h) =>
                    {
                        data.Add(d, File.ReadAllText(d));
                    })
                .Returns(Task.FromResult<TestLogStatus>(null));

            var fileCoverage = new List<FileCoverageInfo>()
            {
                new FileCoverageInfo()
                {
                    FilePath = "C:\\a.cs",
                    LineCoverageStatus = new Dictionary<uint, CoverageStatus>()
                    {
                        { 1, CoverageStatus.Covered },
                        { 2, CoverageStatus.Covered },
                    }
                },

                new FileCoverageInfo()
                {
                    FilePath = "C:\\b.cs",
                    LineCoverageStatus = new Dictionary<uint, CoverageStatus>()
                    {
                        { 1, CoverageStatus.Covered },
                        { 2, CoverageStatus.NotCovered },
                    }
                }
            };

            publisher.PublishFileCoverage(fileCoverage, token).Wait();

            _mockLogStoreHelper.Verify(x => x.UploadTestBuildLogAsync(
                It.Is<Guid>(a => a == _context.ProjectId),
                It.Is<int>(b => b == _context.BuildId),
                It.Is<TestLogType>(c => c == TestLogType.Intermediate),
                It.Is<string>(d => data.ContainsKey(d) && data[d] == JsonUtility.ToString(fileCoverage)),
                It.IsAny<Dictionary<string, string>>(),
                It.Is<string>(f => f == null),
                It.Is<bool>(g => g == true),
                It.Is<CancellationToken>(e => e == token)));
        }
    }
}
