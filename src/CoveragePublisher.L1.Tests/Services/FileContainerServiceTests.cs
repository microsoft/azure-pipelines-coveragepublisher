using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.FileContainer.Client;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CoveragePublisher.L1.Tests
{
    [TestClass]
    public class FileContainerServiceTests
    {
        private Guid _projectId = Guid.Empty;
        private long _container = 1234;
        private string _containerPath = "path";
        private Mock<IClientFactory> _mockFactory;
        private VssConnection _connection = new VssConnection(new Uri("http://localhost"), new VssCredentials());
        private TestLogger _logger = new TestLogger();
        private Mock<IFileContainerClientHelper> _mockClientHelper;

        private static string _uploadDirectory;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _uploadDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_uploadDirectory);

            Directory.CreateDirectory(Path.Combine(_uploadDirectory, "summary"));

            for (var i = 0; i < 2; i++)
            {
                // file0 & file1
                File.WriteAllText(Path.Join(_uploadDirectory, "file" + i), "file" + i);

                // file2 & file3
                File.WriteAllText(Path.Join(_uploadDirectory, "summary", "file" + (i+2)), "file" + (i+2));
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
            _mockFactory = new Mock<IClientFactory>();
            _mockFactory.SetupGet(x => x.VssConnection).Returns(_connection);
            _mockFactory.Setup((x) => x.GetClient<FileContainerHttpClient>(It.IsAny<VssClientHttpRequestSettings>())).Returns(new FileContainerHttpClient(_connection.Uri, _connection.Credentials));

            _mockClientHelper = new Mock<IFileContainerClientHelper>();

            _logger.Log = "";
        }

        [TestMethod]
        public void WillInitializeClientWithSpecificTimeout()
        {
            var service = new FileContainerService(_mockFactory.Object, _projectId, _container, _containerPath);

            _mockFactory.Verify(x => x.GetClient<FileContainerHttpClient>(It.Is<VssClientHttpRequestSettings>(y => TimeSpan.Compare(y.SendTimeout, TimeSpan.FromSeconds(600)) >= 0)));
        }

        [TestMethod]
        public void UploadFilesTest()
        {
            _mockClientHelper.Setup(x => x.UploadFileAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<FileStream>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>()
            ))
            .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)));

            var service = new FileContainerService(_mockClientHelper.Object, _projectId, _container, _containerPath);

            service.CopyToContainerAsync(_logger, _uploadDirectory, new CancellationToken()).Wait();

            _mockClientHelper.Verify(y => y.UploadFileAsync(
                It.Is<long>(x => x == _container),
                It.Is<string>(x => x == "path/file0" || x == "path/file1" || x == "path/summary/file2" || x == "path/summary/file3"),
                It.IsAny<FileStream>(),
                It.Is<Guid>(x => x.Equals(Guid.Empty)),
                It.IsAny<CancellationToken>(),
                It.Is<int>(x => x == 4 * 1024 * 1024)), Times.Exactly(4));
        }

        [TestMethod]
        public void WillRetryAndThrowIfFilesFailToUpload()
        {
            _mockClientHelper.Setup(x => x.UploadFileAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<FileStream>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>()
            ))
            .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)));

            var service = new FileContainerService(_mockClientHelper.Object, _projectId, _container, _containerPath);

            var ex = Assert.ThrowsException<AggregateException>(() => service.CopyToContainerAsync(_logger, _uploadDirectory, new CancellationToken(), false).Wait());
            Assert.IsTrue(string.Equals(ex.InnerExceptions[0].Message, Resources.FileUploadFailedAfterRetry));

            _mockClientHelper.Verify(y => y.UploadFileAsync(
                It.Is<long>(x => x == _container),
                It.Is<string>(x => x == "path/file0" || x == "path/file1" || x == "path/summary/file2" || x == "path/summary/file3"),
                It.IsAny<FileStream>(),
                It.Is<Guid>(x => x.Equals(Guid.Empty)),
                It.IsAny<CancellationToken>(),
                It.Is<int>(x => x == 4 * 1024 * 1024)), Times.Exactly(8));
        }

        [TestMethod]
        public void WillRetryAndPassIfFilesUploadDuringRetry()
        {
            var files = new List<string>();
            var response = new HttpResponseMessage(HttpStatusCode.Conflict);
            var calls = 0;

            _mockClientHelper.Setup(x => x.UploadFileAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<FileStream>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>()
            ))
            .Returns(() => Task.FromResult(response))
            .Callback<long, string, FileStream, Guid, CancellationToken, int>((a, b, c, d, e, f) => {
                calls++;
                if(calls == 4)
                {
                    response = new HttpResponseMessage(HttpStatusCode.Created);
                }
            });

            var service = new FileContainerService(_mockClientHelper.Object, _projectId, _container, _containerPath);

            service.CopyToContainerAsync(_logger, _uploadDirectory, new CancellationToken(), false).Wait();

            _mockClientHelper.Verify(y => y.UploadFileAsync(
                It.Is<long>(x => x == _container),
                It.Is<string>(x => x == "path/file0" || x == "path/file1" || x == "path/summary/file2" || x == "path/summary/file3"),
                It.IsAny<FileStream>(),
                It.Is<Guid>(x => x.Equals(Guid.Empty)),
                It.IsAny<CancellationToken>(),
                It.Is<int>(x => x == 4 * 1024 * 1024)), Times.Exactly(8));
        }

        [TestMethod]
        public void WillThrowIfCancelRequested()
        {
            var files = new List<string>();

            _mockClientHelper.Setup(x => x.UploadFileAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<FileStream>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>()
            )).Throws(new OperationCanceledException());

            var service = new FileContainerService(_mockClientHelper.Object, _projectId, _container, _containerPath);


            var cancellationToken = new CancellationTokenSource();
            cancellationToken.Cancel();

            var ex = Assert.ThrowsException<AggregateException>(() => service.CopyToContainerAsync(_logger, _uploadDirectory, cancellationToken.Token).Wait());
        }

        [TestMethod]
        public void WillThrowIfCancelRequestedDuringUpload()
        {
            var cancellationToken = new CancellationTokenSource();

            _mockClientHelper.Setup(x => x.UploadFileAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<FileStream>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>()
            )).Returns(() =>
            {
                cancellationToken.Cancel();
                throw new OperationCanceledException();
            });

            var service = new FileContainerService(_mockClientHelper.Object, _projectId, _container, _containerPath);

            Assert.ThrowsException<AggregateException>(() => service.CopyToContainerAsync(_logger, _uploadDirectory, cancellationToken.Token).Wait());

            Assert.IsTrue(_logger.Log.Contains("File upload has been cancelled"));
        }

        [TestMethod]
        public void WillLogIfExceptionOccuredWhileUpload()
        {
            var cancellationToken = new CancellationTokenSource();

            _mockClientHelper.Setup(x => x.UploadFileAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<FileStream>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>()
            )).Returns(() =>
            {
                cancellationToken.Cancel();
                throw new Exception();
            });

            var service = new FileContainerService(_mockClientHelper.Object, _projectId, _container, _containerPath);

            Assert.ThrowsException<AggregateException>(() => service.CopyToContainerAsync(_logger, _uploadDirectory, cancellationToken.Token).Wait());

            Assert.IsTrue(_logger.Log.Contains("Fail to upload"));
        }
        
        [TestMethod]
        public void WillLogFileProgress()
        {
            var cancellationToken = new CancellationTokenSource();

            _mockClientHelper.Setup(x => x.UploadFileAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<FileStream>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>()
            ))
            .Callback<long, string, Stream, Guid, CancellationToken, int>((a, b, c, d, e, f) =>
            {
                _mockClientHelper.Raise(x => x.UploadFileReportProgress += null, new ReportProgressEventArgs(b, 50, 100));
                _mockClientHelper.Raise(x => x.UploadFileReportTrace += null, new ReportTraceEventArgs(b, "completed"));
            })
            .ReturnsAsync(() => {
                return new HttpResponseMessage(HttpStatusCode.Created);
            });

            var service = new FileContainerService(_mockClientHelper.Object, _projectId, _container, _containerPath);

            service.CopyToContainerAsync(_logger, _uploadDirectory, cancellationToken.Token).Wait();

            Assert.IsTrue(_logger.Log.Contains("Uploading 4 files."));
            Assert.IsTrue(_logger.Log.Contains(string.Format(@"File: '{0}\file1' took", _uploadDirectory)));
        }
    }
}
   
