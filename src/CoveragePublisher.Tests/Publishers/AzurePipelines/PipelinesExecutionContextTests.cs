using System;
using System.IO;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class PipelinesExecutionContextTests
    {
        [TestMethod]
        public void WillInitializeProperties()
        {
            var context = new PipelinesExecutionContext(new Mock<IClientFactory>().Object);
            Assert.IsNotNull(context.Logger);

            var guid = Guid.NewGuid().ToString();

            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.AccessToken, "token");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.BuildContainerId, "1234");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.BuildId, "1234");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.ProjectId, guid);
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.CollectionUri, "uri");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.AgentTempPath, "D:\\");

            Assert.AreEqual("token", context.AccessToken);
            Assert.AreEqual((long)1234, context.ContainerId);
            Assert.AreEqual((int)1234, context.BuildId);
            Assert.AreEqual(guid, context.ProjectId.ToString());
            Assert.AreEqual("uri", context.CollectionUri);
            Assert.AreEqual("D:\\", context.TempPath);
        }

        [TestMethod]
        public void WillInitializePropertiesWithEmptyValues()
        {
            var context = new PipelinesExecutionContext(new Mock<IClientFactory>().Object);
            Assert.IsNotNull(context.Logger);

            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.BuildContainerId, "");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.BuildId, "");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.ProjectId, "");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.AgentTempPath, "");

            Assert.AreEqual((long)0, context.ContainerId);
            Assert.AreEqual((int)0, context.BuildId);
            Assert.AreEqual(Guid.Empty.ToString(), context.ProjectId.ToString());
            Assert.AreEqual(Path.GetTempPath(), context.TempPath);
        }

        [TestMethod]
        public void WillThrowIfAccessTokenAndCollectionUriAreEmpty()
        {
            var context = new PipelinesExecutionContext(new Mock<IClientFactory>().Object);
            Assert.IsNotNull(context.Logger);

            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.AccessToken, "");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.CollectionUri, "");

            Assert.ThrowsException<Exception>(() => { var a = context.AccessToken; });
            Assert.ThrowsException<Exception>(() => { var a = context.CollectionUri; });
        }
        
    }
}
