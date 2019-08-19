// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.FileContainer.Client;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Resources = Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher.Resources;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class FileContainerServiceTests
    {
        private string _containerPath = "path";
        private Mock<IClientFactory> _mockFactory;
        private VssConnection _connection = new VssConnection(new Uri("http://localhost"), new VssCredentials());
        private Mock<IFileContainerClientHelper> _mockClientHelper;
        private IPipelinesExecutionContext _context;

        private static TestLogger _logger = new TestLogger();
        private static string _uploadDirectory;

        public FileContainerServiceTests()
        {
            _context = new TestPipelinesExecutionContext(_logger);
        }

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
            var service = new FileContainerService(_mockFactory.Object, _context);

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

            var service = new FileContainerService(_mockClientHelper.Object, _context);

            service.CopyToContainerAsync(new Tuple<string, string>(_uploadDirectory, _containerPath), new CancellationToken()).Wait();

            _mockClientHelper.Verify(y => y.UploadFileAsync(
                It.Is<long>(x => x == _context.ContainerId),
                It.Is<string>(x => x == "path/file0" || x == "path/file1" || x == "path/summary/file2" || x == "path/summary/file3"),
                It.IsAny<FileStream>(),
                It.Is<Guid>(x => x.Equals(_context.ProjectId)),
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

            var service = new FileContainerService(_mockClientHelper.Object, _context);

            var ex = Assert.ThrowsException<AggregateException>(() => service.CopyToContainerAsync(new Tuple<string, string>(_uploadDirectory, _containerPath), new CancellationToken(), false).Wait());
            Assert.IsTrue(string.Equals(ex.InnerExceptions[0].Message, Resources.FileUploadFailedAfterRetry));

            _mockClientHelper.Verify(y => y.UploadFileAsync(
                It.Is<long>(x => x == _context.ContainerId),
                It.Is<string>(x => x == "path/file0" || x == "path/file1" || x == "path/summary/file2" || x == "path/summary/file3"),
                It.IsAny<FileStream>(),
                It.Is<Guid>(x => x.Equals(_context.ProjectId)),
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

            var service = new FileContainerService(_mockClientHelper.Object, _context);

            service.CopyToContainerAsync(new Tuple<string, string>(_uploadDirectory, _containerPath), new CancellationToken(), false).Wait();

            _mockClientHelper.Verify(y => y.UploadFileAsync(
                It.Is<long>(x => x == _context.ContainerId),
                It.Is<string>(x => x == "path/file0" || x == "path/file1" || x == "path/summary/file2" || x == "path/summary/file3"),
                It.IsAny<FileStream>(),
                It.Is<Guid>(x => x.Equals(_context.ProjectId)),
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

            var service = new FileContainerService(_mockClientHelper.Object, _context);


            var cancellationToken = new CancellationTokenSource();
            cancellationToken.Cancel();

            var ex = Assert.ThrowsException<AggregateException>(() => service.CopyToContainerAsync(new Tuple<string, string>(_uploadDirectory, _containerPath), cancellationToken.Token).Wait());
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

            var service = new FileContainerService(_mockClientHelper.Object, _context);

            Assert.ThrowsException<AggregateException>(() => service.CopyToContainerAsync(new Tuple<string, string>(_uploadDirectory, _containerPath), cancellationToken.Token).Wait());

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

            var service = new FileContainerService(_mockClientHelper.Object, _context);

            Assert.ThrowsException<AggregateException>(() => service.CopyToContainerAsync(new Tuple<string, string>(_uploadDirectory, _containerPath), cancellationToken.Token).Wait());

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

            var service = new FileContainerService(_mockClientHelper.Object, _context);

            service.CopyToContainerAsync(new Tuple<string, string>(_uploadDirectory, _containerPath), cancellationToken.Token).Wait();

            Assert.IsTrue(_logger.Log.Contains("Uploading 4 files."));
            Assert.IsTrue(_logger.Log.Contains(string.Format(@"File: '{0}\file1' took", _uploadDirectory)));
        }
    }
}
   
