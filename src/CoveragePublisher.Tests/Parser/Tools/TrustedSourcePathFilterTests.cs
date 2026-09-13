// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class TrustedSourcePathFilterTests
    {
        private string _testDirectory;
        private string _trustedDirectory;

        [TestInitialize]
        public void Initialize()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _trustedDirectory = Path.Combine(_testDirectory, "trusted");
            Directory.CreateDirectory(_trustedDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [TestMethod]
        public void AllowsFilesInsideTrustedDirectory()
        {
            string sourceFile = Path.Combine(_trustedDirectory, "source.cs");
            File.WriteAllText(sourceFile, "class Source {}");
            var filter = new TrustedSourcePathFilter(new[] { _trustedDirectory });

            Assert.IsTrue(filter.IsElementIncludedInReport(sourceFile));
            Assert.IsTrue(filter.IsSafeForRead(sourceFile, new[] { _trustedDirectory }));
        }

        [TestMethod]
        public void RejectsExistingFilesOutsideTrustedDirectory()
        {
            string sourceFile = Path.Combine(_testDirectory, "secret.txt");
            File.WriteAllText(sourceFile, "secret");
            var filter = new TrustedSourcePathFilter(new[] { _trustedDirectory });

            Assert.IsFalse(filter.IsElementIncludedInReport(sourceFile));
            Assert.IsFalse(filter.IsSafeForRead(sourceFile, new[] { _trustedDirectory }));
        }

        [TestMethod]
        public void RejectsTraversalAndRemotePaths()
        {
            var filter = new TrustedSourcePathFilter(new[] { _trustedDirectory });

            Assert.IsFalse(filter.IsElementIncludedInReport(@"..\secret.txt"));
            Assert.IsFalse(filter.IsElementIncludedInReport("https://example.test/source.cs"));
            Assert.IsFalse(filter.IsElementIncludedInReport(@"\\server\share\source.cs"));
        }

        [TestMethod]
        public void DoesNotUsePathPrefixAsContainment()
        {
            string siblingDirectory = _trustedDirectory + "-secrets";
            Directory.CreateDirectory(siblingDirectory);
            string sourceFile = Path.Combine(siblingDirectory, "secret.txt");
            File.WriteAllText(sourceFile, "secret");
            var filter = new TrustedSourcePathFilter(new[] { _trustedDirectory });

            Assert.IsFalse(filter.IsSafeForRead(sourceFile, new[] { _trustedDirectory }));
        }
    }
}
