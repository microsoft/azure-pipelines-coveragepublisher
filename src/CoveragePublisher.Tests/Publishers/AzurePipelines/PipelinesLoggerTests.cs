using System;
using System.IO;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoveragePublisher.Tests
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

            logger.Info("info");
            logger.Verbose("verbose");
            logger.Warning("warning");
            logger.Error("error");
            logger.Debug("debug");

            var log = ConsoleWriter.ToString();
            var expectedLog = "info" + Environment.NewLine +
                "##vso[task.debug]Verbose: verbose" + Environment.NewLine +
                "##vso[task.logissue type=warning]warning" + Environment.NewLine +
                "##vso[task.logissue type=error]error" + Environment.NewLine +
                "##vso[task.debug]debug" + Environment.NewLine;

            Assert.AreEqual(expectedLog, log);
        }
    }
}