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
    public class PipelinesLoggerTests
    {
        private static StringWriter ConsoleWriter = new StringWriter();

        [TestInitialize]
        public void TestInitialize()
        {
            ConsoleWriter.GetStringBuilder().Clear();
            Console.SetOut(ConsoleWriter);
            Console.SetError(ConsoleWriter);
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            ConsoleWriter.Dispose();
        }

        [TestMethod]
        public void LogTests()
        {
            var logger = new PipelinesLogger();
            var ConsoleWriter = new StringWriter();

            Console.SetOut(ConsoleWriter);
            Console.SetError(ConsoleWriter);

            logger.Info("message");
            logger.Verbose("message");
            logger.Warning("message");
            logger.Error("message");

            var log = ConsoleWriter.ToString();
            var expectedLog = "message" + Environment.NewLine +
                "message" + Environment.NewLine +
                "##vso[task.logissue type=warning]message" + Environment.NewLine +
                "##vso[task.logissue type=error]message" + Environment.NewLine;

            Assert.AreEqual(expectedLog, log);
        }

    }
}
