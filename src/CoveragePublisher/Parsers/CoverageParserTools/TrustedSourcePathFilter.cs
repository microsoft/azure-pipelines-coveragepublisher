// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Palmmedia.ReportGenerator.Core.Parser.Filtering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers
{
    internal sealed class TrustedSourcePathFilter : IFilter
    {
        /// <summary>
        /// A deterministic path, as ReportGenerator recognizes it, e.g. <c>/_/src/File.cs</c>
        /// </summary>
        private static readonly Regex DeterministicPathRegex = new Regex("\\/_\\d?\\/", RegexOptions.Compiled);

        /// <summary>
        /// The directories ReportGenerator searches for deterministic paths when no source directories are given
        /// </summary>
        private static readonly IReadOnlyList<string> DeterministicSourceDirectories = GetDeterministicSourceDirectories();

        private readonly IReadOnlyList<string> _trustedRoots;
        private readonly IReadOnlyList<string> _sourceDirectories;
        private readonly StringComparison _pathComparison;

        public TrustedSourcePathFilter(IEnumerable<string> trustedRoots)
            : this(trustedRoots, null)
        {
        }

        /// <param name="trustedRoots">The directories source files may be read from</param>
        /// <param name="sourceDirectories">
        /// The source directories ReportGenerator searches for source files that are not at their path in the coverage report
        /// </param>
        public TrustedSourcePathFilter(IEnumerable<string> trustedRoots, IEnumerable<string> sourceDirectories)
        {
            _sourceDirectories = (sourceDirectories ?? Enumerable.Empty<string>())
                .Where(directory => !string.IsNullOrWhiteSpace(directory))
                .ToArray();

            _pathComparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            _trustedRoots = (trustedRoots ?? Enumerable.Empty<string>())
                .Where(root => !string.IsNullOrWhiteSpace(root))
                .Select(NormalizePath)
                .Where(root => root != null)
                .Distinct(GetPathComparer())
                .ToArray();
        }

        public bool HasCustomFilters => true;

        public int ExcludedPathCount { get; private set; }

        public bool HasTrustedRoots => _trustedRoots.Count > 0;

        public bool IsElementIncludedInReport(string path)
        {
            bool isAllowed = IsPotentiallyTrustedPath(path);
            if (!isAllowed)
            {
                ExcludedPathCount++;
            }

            return isAllowed;
        }

        public bool IsSafeForRead(string path, IEnumerable<string> sourceDirectories)
        {
            if (!HasTrustedRoots || string.IsNullOrWhiteSpace(path) || IsRemoteOrDevicePath(path))
            {
                return false;
            }

            string resolvedPath = ResolveSourceFile(path, sourceDirectories);
            if (resolvedPath != null)
            {
                return IsTrustedExistingPath(resolvedPath);
            }

            // ReportGenerator finds no file for the path: only one created at the path itself could be read later
            string directPath = NormalizePath(path);
            return directPath != null && IsWithinTrustedRoot(directPath);
        }

        internal ApprovedSourceStatus TryReadApprovedLines(string path, IEnumerable<string> sourceDirectories, out string[] lines)
        {
            lines = null;
            if (!HasTrustedRoots || string.IsNullOrWhiteSpace(path) || IsRemoteOrDevicePath(path))
            {
                return ApprovedSourceStatus.Unsafe;
            }

            if (GetPathSegments(path).Any(segment => segment == ".."))
            {
                return ApprovedSourceStatus.Unsafe;
            }

            string directPath = NormalizePath(path);
            if (directPath == null)
            {
                return ApprovedSourceStatus.Unsafe;
            }

            // The file ReportGenerator would read, e.g. for a deterministic path (/_/src/File.cs)
            // or a path on the agent that ran the tests
            string resolvedPath = ResolveSourceFile(path, sourceDirectories);
            if (resolvedPath != null)
            {
                return ReadIfTrusted(resolvedPath, out lines);
            }

            return IsWithinTrustedRoot(directPath)
                ? ApprovedSourceStatus.Missing
                : ApprovedSourceStatus.Unsafe;
        }

        private ApprovedSourceStatus ReadIfTrusted(string path, out string[] lines)
        {
            lines = null;
            string trustedRoot = _trustedRoots
                .Where(root => IsWithinRoot(path, root))
                .OrderByDescending(root => root.Length)
                .FirstOrDefault();
            if (trustedRoot == null)
            {
                return ApprovedSourceStatus.Unsafe;
            }

            bool read = NonFollowingFileReader.TryReadLines(trustedRoot, path, out lines, out bool blocked);
            if (read)
            {
                return ApprovedSourceStatus.Approved;
            }

            return blocked ? ApprovedSourceStatus.Unsafe : ApprovedSourceStatus.Missing;
        }

        private bool IsPotentiallyTrustedPath(string path)
        {
            if (!HasTrustedRoots || string.IsNullOrWhiteSpace(path) || IsRemoteOrDevicePath(path))
            {
                return false;
            }

            if (GetPathSegments(path).Any(segment => segment == ".."))
            {
                return false;
            }

            if (!IsAbsolutePath(path))
            {
                return true;
            }

            string normalizedPath = NormalizePath(path);
            if (normalizedPath != null && IsWithinTrustedRoot(normalizedPath))
            {
                return true;
            }

            // An absolute path outside the trusted roots that does not exist, e.g. a deterministic path (/_/src/File.cs)
            // or a path on the agent that ran the tests: ReportGenerator reads it from under the source directories.
            // Trusted if the file it would read is.
            string resolvedPath = ResolveSourceFile(path, _sourceDirectories);
            return resolvedPath != null && IsTrustedExistingPath(resolvedPath);
        }

        /// <summary>
        /// Gets the file ReportGenerator reads for a path in a coverage report, or null if there is none.
        /// </summary>
        /// <remarks>
        /// The same search as ReportGenerator's LocalFileReader, so that the file checked is the file read:
        /// the path itself; then, for a deterministic path, the path after the /_/ segment, from the working directory;
        /// then the path under each source directory, without each of its leading segments in turn.
        /// For a deterministic path with no source directories, ReportGenerator searches its own deterministic source directories.
        /// </remarks>
        private static string ResolveSourceFile(string path, IEnumerable<string> sourceDirectories)
        {
            IEnumerable<string> directories = sourceDirectories ?? Enumerable.Empty<string>();

            string directPath = NormalizePath(path);
            if (directPath != null && File.Exists(directPath))
            {
                return directPath;
            }

            if (path.StartsWith("/_", StringComparison.Ordinal) && DeterministicPathRegex.IsMatch(path))
            {
                path = path.Substring(path.IndexOf('/', 2) + 1);

                string relativePath = NormalizePath(path);
                if (relativePath != null && File.Exists(relativePath))
                {
                    return relativePath;
                }

                if (!directories.Any())
                {
                    directories = DeterministicSourceDirectories;
                }
            }

            return GetSourceDirectoryCandidates(path, directories).FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// As ReportGenerator's LocalFileReader
        /// </summary>
        private static IReadOnlyList<string> GetDeterministicSourceDirectories()
        {
            var directories = new List<string>();

            if ("true".Equals(Environment.GetEnvironmentVariable("TF_BUILD"), StringComparison.OrdinalIgnoreCase)
                && Environment.GetEnvironmentVariable("Build.SourcesDirectory") != null)
            {
                directories.Add(Environment.GetEnvironmentVariable("Build.SourcesDirectory"));
            }

            if ("true".Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), StringComparison.OrdinalIgnoreCase)
                && Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") != null)
            {
                directories.Add(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE"));
            }

            return directories;
        }

        private bool IsTrustedExistingPath(string path)
        {
            string trustedRoot = _trustedRoots
                .Where(root => IsWithinRoot(path, root))
                .OrderByDescending(root => root.Length)
                .FirstOrDefault();

            return trustedRoot != null && !ContainsReparsePoint(path, trustedRoot);
        }

        private bool IsWithinTrustedRoot(string path)
        {
            return _trustedRoots.Any(root => IsWithinRoot(path, root));
        }

        private bool IsWithinRoot(string path, string root)
        {
            if (string.Equals(path, root, _pathComparison))
            {
                return true;
            }

            string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar.ToString(), _pathComparison)
                ? root
                : root + Path.DirectorySeparatorChar;

            return path.StartsWith(rootWithSeparator, _pathComparison);
        }

        private static bool ContainsReparsePoint(string path, string trustedRoot)
        {
            if (IsExistingReparsePoint(trustedRoot))
            {
                return true;
            }

            string relativePath = path.Substring(trustedRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string currentPath = trustedRoot;

            foreach (string segment in GetPathSegments(relativePath))
            {
                currentPath = Path.Combine(currentPath, segment);
                if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
                {
                    break;
                }

                if (IsExistingReparsePoint(currentPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExistingReparsePoint(string path)
        {
            try
            {
                return (File.Exists(path) || Directory.Exists(path))
                    && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return true;
            }
        }

        private static IEnumerable<string> GetSourceDirectoryCandidates(string path, IEnumerable<string> sourceDirectories)
        {
            string[] pathSegments = GetPathSegments(path).ToArray();

            foreach (string sourceDirectory in sourceDirectories ?? Enumerable.Empty<string>())
            {
                string normalizedSourceDirectory = NormalizePath(sourceDirectory);
                if (normalizedSourceDirectory == null)
                {
                    continue;
                }

                for (int index = 0; index < pathSegments.Length; index++)
                {
                    string candidate = normalizedSourceDirectory;
                    for (int segmentIndex = index; segmentIndex < pathSegments.Length; segmentIndex++)
                    {
                        candidate = Path.Combine(candidate, pathSegments[segmentIndex]);
                    }

                    string normalizedCandidate = NormalizePath(candidate);
                    if (normalizedCandidate != null)
                    {
                        yield return normalizedCandidate;
                    }
                }
            }
        }

        private static IEnumerable<string> GetPathSegments(string path)
        {
            return path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool IsAbsolutePath(string path)
        {
            return Path.IsPathRooted(path)
                || (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/'));
        }

        private static bool IsRemoteOrDevicePath(string path)
        {
            if (path.StartsWith(@"\\", StringComparison.Ordinal)
                || path.StartsWith("//", StringComparison.Ordinal)
                || path.StartsWith(@"\\?\", StringComparison.Ordinal)
                || path.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                return true;
            }

            return Uri.TryCreate(path, UriKind.Absolute, out Uri uri) && !uri.IsFile;
        }

        private static string NormalizePath(string path)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                string pathRoot = Path.GetPathRoot(fullPath);

                return string.Equals(fullPath, pathRoot, GetPathComparison())
                    ? fullPath
                    : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return null;
            }
        }

        private static StringComparer GetPathComparer()
        {
            return Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }

        private static StringComparison GetPathComparison()
        {
            return Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }
    }
}
