// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
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
    }
}
