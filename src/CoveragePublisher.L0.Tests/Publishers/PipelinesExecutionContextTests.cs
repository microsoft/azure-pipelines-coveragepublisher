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

            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.AccessToken, "token");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.BuildContainerId, "1234");

            Assert.AreEqual("token", context.AccessToken);
            Assert.AreEqual(1234, context.ContainerId);
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
