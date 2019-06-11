using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoveragePublisher.L0.Tests
{
    [TestClass]
    public class PipelinesExecutionContextTests
    {
        [TestMethod]
        public void WillInitializeProperties()
        {
            var context = new PipelinesExecutionContext();
            Assert.IsNotNull(context.ConsoleLogger);

            var guid = Guid.NewGuid().ToString();

            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.AccessToken, "token");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.BuildContainerId, "1234");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.BuildId, "1234");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.ProjectId, guid);

            Assert.AreEqual("token", context.AccessToken);
            Assert.AreEqual((long)1234, context.ContainerId);
            Assert.AreEqual((int)1234, context.BuildId);
            Assert.AreEqual(guid, context.ProjectId.ToString());
        }

        [TestMethod]
        public void WillInitializePropertiesWithEmptyValues()
        {
            var context = new PipelinesExecutionContext();
            Assert.IsNotNull(context.ConsoleLogger);

            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.BuildContainerId, "");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.BuildId, "");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.ProjectId, "");

            Assert.AreEqual((long)0, context.ContainerId);
            Assert.AreEqual((int)0, context.BuildId);
            Assert.AreEqual(Guid.Empty, context.ProjectId.ToString());
        }

        [TestMethod]
        public void WillThrowIfAccessTokenIsEmpty()
        {
            var context = new PipelinesExecutionContext();
            Assert.IsNotNull(context.ConsoleLogger);

            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.AccessToken, "");

            Assert.ThrowsException<ArgumentNullException>(() => { var a = context.AccessToken; });
        }
    }
}
