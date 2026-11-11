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
        private readonly IReadOnlyDictionary<string, ApprovedSource> _sourcesByPath;

        internal ApprovedSourceFileReader(IReadOnlyDictionary<string, ApprovedSource> sourcesByPath)
        {
            _sourcesByPath = sourcesByPath;
        }

        public string[] LoadFile(string path, out string error)
        {
            return LoadFile(path, out _, out error);
        }

        public string[] LoadFile(string path, out string mappedPath, out string error)
        {
            if (path != null && _sourcesByPath.TryGetValue(path, out var source))
            {
                mappedPath = source.MappedPath;
                error = null;
                return source.Lines;
            }

            mappedPath = path;
            error = $"The source file '{path}' was not approved for reading.";
            return null;
        }
    }

    /// <summary>
    /// The approved lines of a source file in a coverage report, and the file they were read from.
    /// </summary>
    internal sealed class ApprovedSource
    {
        /// <param name="mappedPath">
        /// The file the lines were read from, as ReportGenerator's LocalFileReader maps the path in the coverage report;
        /// the path itself if there is no file
        /// </param>
        /// <param name="lines">The lines of the file</param>
        internal ApprovedSource(string mappedPath, string[] lines)
        {
            MappedPath = mappedPath;
            Lines = lines;
        }

        internal string MappedPath { get; }

        internal string[] Lines { get; }
    }
}
