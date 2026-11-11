// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class ApprovedSourceFileReaderTests
    {
        [TestMethod]
        public void WillReturnApprovedLinesAndMappedPath()
        {
            var lines = new[] { "approved" };
            var mappedPath = Path.Combine(Path.GetTempPath(), "src", "Source.cs");
            var reader = new ApprovedSourceFileReader(new Dictionary<string, ApprovedSource>(StringComparer.Ordinal)
            {
                { "/_/src/Source.cs", new ApprovedSource(mappedPath, lines) }
            });

            Assert.AreSame(lines, reader.LoadFile("/_/src/Source.cs", out string actualMappedPath, out string error));
            Assert.AreEqual(mappedPath, actualMappedPath);
            Assert.IsNull(error);

            Assert.AreSame(lines, reader.LoadFile("/_/src/Source.cs", out error));
            Assert.IsNull(error);
        }

        [TestMethod]
        public void WillNotReadUnapprovedPathFromDisk()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "on disk");
                var reader = new ApprovedSourceFileReader(new Dictionary<string, ApprovedSource>(StringComparer.Ordinal));

                Assert.IsNull(reader.LoadFile(path, out string mappedPath, out string error));
                Assert.AreEqual(path, mappedPath);
                Assert.IsNotNull(error);

                Assert.IsNull(reader.LoadFile(path, out error));
                Assert.IsNotNull(error);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
