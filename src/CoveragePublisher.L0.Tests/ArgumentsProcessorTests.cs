using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoveragePublishe.L0.Tests
{
    [TestClass]
    public class ArgumentsProcessorTests
    {
        private static StringWriter ConsoleWriter = new StringWriter();
        private static string UsageText = @"
            --reportDirectory    (Default: ) Path to report directory.

            --sourceDirectory    (Default: ) List of source directories separated by ';'.

            --diag               (Default: false) Enable diagnostics logging.

            --help               Display this help screen.

            --version            Display version information.

            value pos. 0         Required. Set of coverage files to be published.
";

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            UsageText = Regex.Replace(UsageText, @"\s+", "");
        }

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
        public void WillPrintHelpTextForNoArgs()
        {
            var argsProcessor = new ArgumentsProcessor();
            argsProcessor.ProcessCommandLineArgs(new string[] { });

            var helpText = ConsoleWriter.ToString();
            Assert.IsTrue(helpText.Contains("ERROR(S):" + Environment.NewLine + "  A required value not bound to option name is missing."));
            Assert.IsTrue(Regex.Replace(helpText, @"\s+", "").Contains(UsageText), helpText);
        }

        [TestMethod]
        public void WillPrintHelpTextForInvalidArgs()
        {
            var argsProcessor = new ArgumentsProcessor();
            argsProcessor.ProcessCommandLineArgs(new string[] { "asdf", "--abcd", "asdf" });

            var helpText = ConsoleWriter.ToString();
            Assert.IsTrue(helpText.Contains("ERROR(S):" + Environment.NewLine + "  Option 'abcd' is unknown."));
            Assert.IsTrue(Regex.Replace(helpText, @"\s+", "").Contains(UsageText), helpText);
        }


        [TestMethod]
        public void WillPrintHelpTextForInvalidArgs_NoValueForOption()
        {
            var argsProcessor = new ArgumentsProcessor();
            argsProcessor.ProcessCommandLineArgs(new string[] { "asdf", "--reportDirectory", });

            var helpText = ConsoleWriter.ToString();
            Assert.IsTrue(helpText.Contains(@"ERROR(S):" + Environment.NewLine + "  Option 'reportDirectory' has no value."));
            Assert.IsTrue(Regex.Replace(helpText, @"\s+", "").Contains(UsageText), helpText);
        }

        [DataTestMethod]
        [DataRow(new string[] { @"C:\a.txt" }, "")]
        [DataRow(new string[] { @"C:\a.txt" }, @"C:\")]
        [DataRow(new string[] { @"C:\a.txt", @"C:\b.txt"}, @"C:\")]
        public void WillParseMultipleArguments(string [] files, string reportDirectory)
        {
            string[] args = files;
            if(!string.IsNullOrEmpty(reportDirectory))
            {
                args = files.Union(new string[] { "--reportDirectory", reportDirectory }).ToArray();
            }

            var argsProcessor = new ArgumentsProcessor();

            var cliArgs = argsProcessor.ProcessCommandLineArgs(args);

            Assert.AreEqual(cliArgs.ReportDirectory, reportDirectory);

            foreach(var file in files)
            {
                Assert.IsTrue(cliArgs.CoverageFiles.Contains(file));
            }
        }
    }
}
