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

        [TestMethod]
        public void AllowsDeterministicPathOfFileInsideTrustedDirectory()
        {
            // Deterministic builds report paths from the virtual root /_/, which ReportGenerator reads from the source directories
            Directory.CreateDirectory(Path.Combine(_trustedDirectory, "src"));
            File.WriteAllText(Path.Combine(_trustedDirectory, "src", "Source.cs"), "class Source {}");
            var filter = new TrustedSourcePathFilter(new[] { _trustedDirectory }, new[] { _trustedDirectory });

            Assert.IsTrue(filter.IsElementIncludedInReport("/_/src/Source.cs"));
            Assert.IsTrue(filter.IsSafeForRead("/_/src/Source.cs", new[] { _trustedDirectory }));
            Assert.IsTrue(filter.IsElementIncludedInReport("/_1/src/Source.cs"));
            Assert.IsTrue(filter.IsSafeForRead("/_1/src/Source.cs", new[] { _trustedDirectory }));
        }

        [TestMethod]
        public void AllowsPathFromAnotherAgentOfFileInsideTrustedDirectory()
        {
            // Coverage collected on another agent, e.g. tests run on Linux and published on Windows
            Directory.CreateDirectory(Path.Combine(_trustedDirectory, "src"));
            File.WriteAllText(Path.Combine(_trustedDirectory, "src", "Source.cs"), "class Source {}");
            string otherAgentPath = $"/{Guid.NewGuid()}/work/1/s/src/Source.cs";
            var filter = new TrustedSourcePathFilter(new[] { _trustedDirectory }, new[] { _trustedDirectory });

            Assert.IsTrue(filter.IsElementIncludedInReport(otherAgentPath));
            Assert.IsTrue(filter.IsSafeForRead(otherAgentPath, new[] { _trustedDirectory }));
        }

        [TestMethod]
        public void RejectsDeterministicPathOfFileOutsideTrustedDirectory()
        {
            string untrustedDirectory = Path.Combine(_testDirectory, "untrusted");
            Directory.CreateDirectory(Path.Combine(untrustedDirectory, "src"));
            File.WriteAllText(Path.Combine(untrustedDirectory, "src", "Secret.cs"), "secret");
            var filter = new TrustedSourcePathFilter(new[] { _trustedDirectory }, new[] { untrustedDirectory });

            Assert.IsFalse(filter.IsElementIncludedInReport("/_/src/Secret.cs"));
            Assert.IsFalse(filter.IsSafeForRead("/_/src/Secret.cs", new[] { untrustedDirectory }));
        }

        [TestMethod]
        public void RejectsDeterministicPathOfMissingFile()
        {
            var filter = new TrustedSourcePathFilter(new[] { _trustedDirectory }, new[] { _trustedDirectory });

            Assert.IsFalse(filter.IsElementIncludedInReport("/_/src/Missing.cs"));
            Assert.IsFalse(filter.IsSafeForRead("/_/src/Missing.cs", new[] { _trustedDirectory }));
        }

        [TestMethod]
        public void RejectsExistingFileOutsideTrustedDirectoryEvenIfSourceDirectoryHasOne()
        {
            // ReportGenerator reads a file at the path itself before searching the source directories
            string secretFile = Path.Combine(_testDirectory, "Source.cs");
            File.WriteAllText(secretFile, "secret");
            File.WriteAllText(Path.Combine(_trustedDirectory, "Source.cs"), "class Source {}");
            var filter = new TrustedSourcePathFilter(new[] { _trustedDirectory }, new[] { _trustedDirectory });

            Assert.IsFalse(filter.IsElementIncludedInReport(secretFile));
            Assert.IsFalse(filter.IsSafeForRead(secretFile, new[] { _trustedDirectory }));
        }

        [TestMethod]
        public void RejectsTrustedDirectoryThatIsSymbolicLink()
        {
            string targetDirectory = Path.Combine(_testDirectory, "target");
            string linkedDirectory = Path.Combine(_testDirectory, "linked");
            Directory.CreateDirectory(targetDirectory);

            try
            {
                Directory.CreateSymbolicLink(linkedDirectory, targetDirectory);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException
                || ex is PlatformNotSupportedException
                || ex is IOException)
            {
                Assert.Inconclusive($"Symbolic links are not available in this test environment: {ex.Message}");
            }

            string sourceFile = Path.Combine(linkedDirectory, "secret.txt");
            File.WriteAllText(Path.Combine(targetDirectory, "secret.txt"), "secret");
            var filter = new TrustedSourcePathFilter(new[] { linkedDirectory });

            Assert.IsFalse(filter.IsSafeForRead(sourceFile, new[] { linkedDirectory }));
        }

        [TestMethod]
        public void ReadsApprovedTextFromTrustedFile()
        {
            string sourceFile = Path.Combine(_trustedDirectory, "source.cs");
            File.WriteAllText(sourceFile, "TRUSTED_MARKER");
            var filter = new TrustedSourcePathFilter(new[] { _trustedDirectory });

            ApprovedSourceStatus status = filter.TryReadApprovedLines(sourceFile, new[] { _trustedDirectory }, out string[] lines);

            Assert.AreEqual(ApprovedSourceStatus.Approved, status);
            Assert.AreEqual("TRUSTED_MARKER", lines[0]);
        }

        [TestMethod]
        public void DoesNotReadThroughSymbolicLink()
        {
            string outsideDirectory = Path.Combine(_testDirectory, "outside");
            Directory.CreateDirectory(outsideDirectory);
            string outsideFile = Path.Combine(outsideDirectory, "secret.txt");
            File.WriteAllText(outsideFile, "SWAPPED_MARKER");
            string linkedFile = Path.Combine(_trustedDirectory, "source.cs");

            try
            {
                File.CreateSymbolicLink(linkedFile, outsideFile);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException
                || ex is PlatformNotSupportedException
                || ex is IOException)
            {
                Assert.Inconclusive($"Symbolic links are not available in this test environment: {ex.Message}");
            }

            var filter = new TrustedSourcePathFilter(new[] { _trustedDirectory });
            ApprovedSourceStatus status = filter.TryReadApprovedLines(linkedFile, new[] { _trustedDirectory }, out string[] lines);

            Assert.AreEqual(ApprovedSourceStatus.Unsafe, status);
            Assert.IsNull(lines);
        }

        [TestMethod]
        public void ReadsApprovedTextForDeterministicAndOtherAgentPaths()
        {
            Directory.CreateDirectory(Path.Combine(_trustedDirectory, "src"));
            File.WriteAllText(Path.Combine(_trustedDirectory, "src", "Source.cs"), "TRUSTED_MARKER");
            var filter = new TrustedSourcePathFilter(new[] { _trustedDirectory }, new[] { _trustedDirectory });

            foreach (string path in new[] { "/_/src/Source.cs", $"/{Guid.NewGuid()}/work/1/s/src/Source.cs" })
            {
                ApprovedSourceStatus status = filter.TryReadApprovedLines(path, new[] { _trustedDirectory }, out string[] lines);

                Assert.AreEqual(ApprovedSourceStatus.Approved, status, path);
                Assert.AreEqual("TRUSTED_MARKER", lines[0], path);
            }
        }

        [TestMethod]
        public void DoesNotReadApprovedTextForDeterministicPathOfFileOutsideTrustedDirectory()
        {
            string untrustedDirectory = Path.Combine(_testDirectory, "untrusted");
            Directory.CreateDirectory(Path.Combine(untrustedDirectory, "src"));
            File.WriteAllText(Path.Combine(untrustedDirectory, "src", "Secret.cs"), "secret");
            var filter = new TrustedSourcePathFilter(new[] { _trustedDirectory }, new[] { untrustedDirectory });

            ApprovedSourceStatus status = filter.TryReadApprovedLines("/_/src/Secret.cs", new[] { untrustedDirectory }, out string[] lines);

            Assert.AreEqual(ApprovedSourceStatus.Unsafe, status);
            Assert.IsNull(lines);
        }
    }
}
