using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.DefaultPublisher;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class HtmlReportPublisherTests
    {
        private IPipelinesExecutionContext _context;
        private Mock<IClientFactory> _mockClientFactory;
        private TestServiceFactory _serviceFactory;
        private Mock<FileContainerService> _mockFileService;
        private Mock<BuildService> _mockBuildService;

        private static TestLogger _logger = new TestLogger();
        private static string _uploadDirectory;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            TraceLogger.Initialize(_logger);

            _uploadDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_uploadDirectory);

            Directory.CreateDirectory(Path.Combine(_uploadDirectory, "summary"));

            for (var i = 0; i < 2; i++)
            {
                // file0 & file1
                File.WriteAllText(Path.Join(_uploadDirectory, "file" + i), "file" + i);

                // file2 & file3
                File.WriteAllText(Path.Join(_uploadDirectory, "summary", "file" + (i + 2)), "file" + (i + 2));
            }
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            Directory.Delete(_uploadDirectory, true);
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _mockClientFactory = new Mock<IClientFactory>();
            _mockBuildService = new Mock<BuildService>(_mockClientFactory.Object, null);
            _context = new TestPipelinesExecutionContext(_logger);
            _logger.Log = "";
            _mockFileService = new Mock<FileContainerService>(new Mock<IFileContainerClientHelper>().Object, _context);

            _serviceFactory = new TestServiceFactory(_mockFileService.Object, _mockBuildService.Object);

            _mockFileService.Setup(x => x.CopyToContainerAsync(It.IsAny<Tuple<string, string>>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .Returns(Task.CompletedTask);

            _mockBuildService.Setup(x => x.AssociateArtifact(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<BuildArtifact>(null));
        }

        [TestMethod]
        public void PublishHTMLReportTest()
        {
            var token = new CancellationToken();
            var publisher = new HtmlReportPublisher(_context, _mockClientFactory.Object, _serviceFactory);
            var containerPath = Constants.ReportDirectory + "_" + _context.BuildId;

            publisher.PublishHTMLReportAsync(_uploadDirectory, token).Wait();

            _mockFileService.Verify(x => x.CopyToContainerAsync(
                It.Is<Tuple<string, string>>(a => a.Item1 == _uploadDirectory && a.Item2 == containerPath),
                It.Is<CancellationToken>(b => b == token),
                It.Is<bool>(c => c == true)));

            _mockBuildService.Verify(x => x.AssociateArtifact(
                It.Is<int>(a => a == _context.BuildId),
                It.Is<string>(b => b == containerPath),
                It.Is<string>(c => c == ArtifactResourceTypes.Container),
                It.Is<string>(d => d == string.Format($"#/{_context.ContainerId}/{containerPath}")),
                It.Is<Dictionary<string, string>>(e => 
                    e[Constants.ArtifactUploadEventProperties.ContainerFolder] == containerPath &&
                    e[Constants.ArtifactUploadEventProperties.ArtifactName] == containerPath &&
                    e[Constants.ArtifactUploadEventProperties.ArtifactType] == ArtifactResourceTypes.Container &&
                    e[Constants.ArtifactUploadEventProperties.Browsable] == bool.FalseString),
                It.Is<CancellationToken>(f => f == token)));

            Assert.IsFalse(_logger.Log.Contains("error:"));
        }

        [TestMethod]
        public void WillLogErrorIFDirectoryDoesntExist()
        {
            var token = new CancellationToken();
            var publisher = new HtmlReportPublisher(_context, _mockClientFactory.Object, _serviceFactory);
            var containerPath = Constants.ReportDirectory + "_" + _context.BuildId;

            var directory = "D:\\" + Guid.NewGuid();

            publisher.PublishHTMLReportAsync(directory, token).Wait();
            
            Assert.IsTrue(_logger.Log.Contains("error"));
            Assert.IsTrue(_logger.Log.Contains("Directory not found"));
        }

        [TestMethod]
        public void WillRenameExtensionToHtml()
        {
            var token = new CancellationToken();
            var publisher = new HtmlReportPublisher(_context, _mockClientFactory.Object, _serviceFactory);
            var containerPath = Constants.ReportDirectory + "_" + _context.BuildId;

            var htmIndex = Path.Join(_uploadDirectory, Constants.HtmIndexFile);
            var defaultIndex = Path.Join(_uploadDirectory, Constants.DefaultIndexFile);

            File.WriteAllText(htmIndex, "");
            Assert.IsTrue(File.Exists(htmIndex));
            Assert.IsFalse(File.Exists(defaultIndex));

            publisher.PublishHTMLReportAsync(_uploadDirectory, token).Wait();
            
            Assert.IsFalse(File.Exists(htmIndex));
            Assert.IsTrue(File.Exists(defaultIndex));
        }
    }

    public class TestServiceFactory: ServiceFactory
    {
        private FileContainerService _fileService;
        private BuildService _buildService;

        public TestServiceFactory(FileContainerService fileContainerService, BuildService buildService)
        {
            _fileService = fileContainerService;
            _buildService = buildService;
        }

        public override BuildService GetBuildService(IClientFactory clientFactory, IPipelinesExecutionContext executionContext)
        {
            return _buildService;
        }

        public override FileContainerService GetFileContainerService(IClientFactory clientFactory, IPipelinesExecutionContext executionContext)
        {
            return _fileService;
        }
    }
}
