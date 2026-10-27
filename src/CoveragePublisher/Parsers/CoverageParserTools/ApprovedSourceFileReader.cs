// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Palmmedia.ReportGenerator.Core.Parser.FileReading;
using System.Collections.Generic;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers
{
    /// <summary>
    /// Gives the report library the lines already read from a safe open.
    /// The library otherwise opens the file name again while drawing the page.
    /// </summary>
    /// <remarks>
    /// A path without approved lines is never read from disk: the report shows an error for it instead.
    /// </remarks>
    internal sealed class ApprovedSourceFileReader : IFileReader
    {
        private readonly IReadOnlyDictionary<string, string[]> _linesByPath;

        internal ApprovedSourceFileReader(IReadOnlyDictionary<string, string[]> linesByPath)
        {
            _linesByPath = linesByPath;
        }

        public string[] LoadFile(string path, out string error)
        {
            if (path != null && _linesByPath.TryGetValue(path, out string[] lines))
            {
                error = null;
                return lines;
            }

            error = $"The source file '{path}' was not approved for reading.";
            return null;
        }
    }
}
