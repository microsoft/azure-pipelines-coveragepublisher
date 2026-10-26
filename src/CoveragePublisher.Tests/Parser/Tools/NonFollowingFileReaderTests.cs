// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class NonFollowingFileReaderTests
    {
        [TestMethod]
        public void UsesLinuxOpenNumbersOnLinux()
        {
            NonFollowingFileReader.UnixOpenFlagValues flags = NonFollowingFileReader.GetUnixOpenFlags(
                NonFollowingFileReader.UnixOpenPlatform.Linux);

            Assert.AreEqual(0x20000, flags.NoFollow);
            Assert.AreEqual(0x10000, flags.Directory);
            Assert.AreEqual(0x80000, flags.CloseOnExec);
        }

        [TestMethod]
        public void UsesMacOpenNumbersOnMac()
        {
            NonFollowingFileReader.UnixOpenFlagValues flags = NonFollowingFileReader.GetUnixOpenFlags(
                NonFollowingFileReader.UnixOpenPlatform.MacOs);

            Assert.AreEqual(0x100, flags.NoFollow);
            Assert.AreEqual(0x100000, flags.Directory);
            Assert.AreEqual(0x1000000, flags.CloseOnExec);
        }

        [TestMethod]
        public void DoesNotUseLinuxNoFollowNumberOnMac()
        {
            NonFollowingFileReader.UnixOpenFlagValues linuxFlags = NonFollowingFileReader.GetUnixOpenFlags(
                NonFollowingFileReader.UnixOpenPlatform.Linux);
            NonFollowingFileReader.UnixOpenFlagValues macFlags = NonFollowingFileReader.GetUnixOpenFlags(
                NonFollowingFileReader.UnixOpenPlatform.MacOs);

            Assert.AreNotEqual(linuxFlags.NoFollow, macFlags.NoFollow);
            Assert.AreNotEqual(linuxFlags.Directory, macFlags.Directory);
            Assert.AreNotEqual(linuxFlags.CloseOnExec, macFlags.CloseOnExec);
        }

        [TestMethod]
        public void CurrentPlatformGetsItsOwnOpenNumbers()
        {
            NonFollowingFileReader.UnixOpenPlatform platform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? NonFollowingFileReader.UnixOpenPlatform.MacOs
                : NonFollowingFileReader.UnixOpenPlatform.Linux;
            NonFollowingFileReader.UnixOpenFlagValues expected = NonFollowingFileReader.GetUnixOpenFlags(platform);
            NonFollowingFileReader.UnixOpenFlagValues actual = NonFollowingFileReader.GetUnixOpenFlags(
                NonFollowingFileReader.TEST_CurrentUnixOpenPlatform());

            Assert.AreEqual(expected.NoFollow, actual.NoFollow);
            Assert.AreEqual(expected.Directory, actual.Directory);
            Assert.AreEqual(expected.CloseOnExec, actual.CloseOnExec);
        }

        [TestMethod]
        public void ReadsEmptyFile()
        {
            string[] lines = ReadTrustedLines(string.Empty, new UTF8Encoding(false));

            CollectionAssert.AreEqual(Array.Empty<string>(), lines);
        }

        [TestMethod]
        public void ReadsUtf8WithoutEncodingMarker()
        {
            string[] expected = { "class Source { }", "\u00E9 \u03A9 \u4E2D \uD83D\uDE00" };
            string[] lines = ReadTrustedLines(string.Join("\n", expected), new UTF8Encoding(false));

            CollectionAssert.AreEqual(expected, lines);
        }

        [TestMethod]
        [DataRow("utf-8")]
        [DataRow("utf-16")]
        [DataRow("utf-16BE")]
        [DataRow("utf-32")]
        [DataRow("utf-32BE")]
        public void ReadsTextWithEncodingMarker(string encodingName)
        {
            string[] expected = { "class Source { }", "\u00E9 \u03A9 \u4E2D \uD83D\uDE00" };
            string[] lines = ReadTrustedLines(string.Join("\n", expected), Encoding.GetEncoding(encodingName));

            CollectionAssert.AreEqual(expected, lines);
        }

        [TestMethod]
        [DataRow("\n")]
        [DataRow("\r")]
        [DataRow("\r\n")]
        public void ReadsEmptyLinesWithDifferentLineEndings(string lineEnding)
        {
            string[] expected = { "first", string.Empty, "third" };
            string contents = string.Join(lineEnding, expected) + lineEnding;
            string[] lines = ReadTrustedLines(contents, new UTF8Encoding(false));

            CollectionAssert.AreEqual(expected, lines);
        }

        [TestMethod]
        public void ReadsMultibyteTextAcrossBufferBoundaries()
        {
            string longLine = new string('x', 8191) + "\u00E9" + new string('y', 16384);
            string[] expected = { longLine, "\u4E2D" };
            string[] lines = ReadTrustedLines(string.Join("\r\n", expected), new UTF8Encoding(false));

            CollectionAssert.AreEqual(expected, lines);
        }

        private static string[] ReadTrustedLines(string contents, Encoding encoding)
        {
            string trustedDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(trustedDirectory);
            try
            {
                string sourceFile = Path.Combine(trustedDirectory, "Source.cs");
                File.WriteAllText(sourceFile, contents, encoding);

                bool read = NonFollowingFileReader.TryReadLines(
                    trustedDirectory,
                    sourceFile,
                    out string[] lines,
                    out bool blocked);

                Assert.IsTrue(read, "The trusted source file could not be read.");
                Assert.IsFalse(blocked, "The trusted source file was blocked.");

                return lines;
            }
            finally
            {
                Directory.Delete(trustedDirectory, true);
            }
        }
    }
}
