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
        public void WillReturnApprovedLines()
        {
            var lines = new[] { "approved" };
            var reader = new ApprovedSourceFileReader(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "Source.cs", lines }
            });

            Assert.AreSame(lines, reader.LoadFile("Source.cs", out string error));
            Assert.IsNull(error);
        }

        [TestMethod]
        public void WillNotReadUnapprovedPathFromDisk()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "on disk");
                var reader = new ApprovedSourceFileReader(new Dictionary<string, string[]>(StringComparer.Ordinal));

                Assert.IsNull(reader.LoadFile(path, out string error));
                Assert.IsNotNull(error);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
