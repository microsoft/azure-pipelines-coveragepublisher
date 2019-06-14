using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CoveragePublisher.Tests.Services
{
    [TestClass]
    public class BuildServiceTests
    {
        private Mock<IClientFactory> _mockClientFactory;
        private Mock<BuildHttpClient> _mockBuildClient;

        [TestInitialize]
        public void TestInitialize()
        {
            _mockBuildClient = new Mock<BuildHttpClient>(new Uri("http://localhost"), new VssCredentials());
            _mockClientFactory = new Mock<IClientFactory>();
            _mockClientFactory.Setup(x => x.GetClient<BuildHttpClient>()).Returns(_mockBuildClient.Object);
        }

        [TestMethod]
        public void AssociateArtifactTest()
        {
            var returnArtifact = new BuildArtifact();
            var token = new CancellationToken();
            var guid = Guid.NewGuid();
            var service = new BuildService(_mockClientFactory.Object, guid);

            _mockBuildClient.Setup(x => x.CreateArtifactAsync(
                It.Is<BuildArtifact>(a => a.Name == "name" && a.Resource.Data == "data" && a.Resource.Type == "type" && a.Resource.Properties["key"] == "value"),
                It.Is<Guid>(b => b == guid),
                It.Is<int>(c => c == 1234),
                It.IsAny<object>(),
                It.Is<CancellationToken>(d => d == token)))
                .Returns(Task.FromResult(returnArtifact));

            var artifact = service.AssociateArtifact(1234, "name", "type", "data", new Dictionary<string, string>() { { "key", "value" } }, token).Result;

            Assert.AreEqual(artifact, returnArtifact);
        }

        [TestMethod]
        public void UpdateBuildNumberTest()
        {
            var returnBuild = new Build();
            var token = new CancellationToken();
            var guid = Guid.NewGuid();
            var service = new BuildService(_mockClientFactory.Object, guid);

            _mockBuildClient.Setup(x => x.UpdateBuildAsync(
                It.Is<Build>(a => a.Id == 1234 && a.BuildNumber == "number" && a.Project.Id == guid),
                It.IsAny<bool?>(),
                It.IsAny<object>(),
                It.Is<CancellationToken>(b => b == token)))
                .Returns(Task.FromResult(returnBuild));

            var build = service.UpdateBuildNumber(1234, "number", token).Result;

            Assert.AreEqual(build, returnBuild);
        }

        [TestMethod]
        public void AddBuildTagTest()
        {
            var guid = Guid.NewGuid();
            var service = new BuildService(_mockClientFactory.Object, guid);
            var token = new CancellationToken();
            var returnArray = new List<string>() { "value" };

            _mockBuildClient.Setup(x => x.AddBuildTagAsync(
                It.Is<Guid>(a => a == guid),
                It.Is<int>(b => b == 1234),
                It.Is<string>(c => c == "tag"),
                It.IsAny<object>(),
                It.Is<CancellationToken>(d => d == token)))
                .Returns(Task.FromResult(returnArray));

            var array = service.AddBuildTag(1234, "tag", token).Result;

            Assert.AreEqual(array, returnArray);
        }
    }
}
