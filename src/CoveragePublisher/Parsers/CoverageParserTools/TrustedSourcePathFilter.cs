// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Palmmedia.ReportGenerator.Core.Parser.Filtering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers
{
    internal sealed class TrustedSourcePathFilter : IFilter
    {
        private readonly IReadOnlyList<string> _trustedRoots;
        private readonly StringComparison _pathComparison;

        public TrustedSourcePathFilter(IEnumerable<string> trustedRoots)
        {
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

            string directPath = NormalizePath(path);
            if (directPath == null)
            {
                return false;
            }

            if (File.Exists(directPath))
            {
                return IsTrustedExistingPath(directPath);
            }

            if (IsAbsolutePath(path))
            {
                return IsWithinTrustedRoot(directPath);
            }

            foreach (string candidate in GetSourceDirectoryCandidates(path, sourceDirectories))
            {
                if (File.Exists(candidate))
                {
                    return IsTrustedExistingPath(candidate);
                }
            }

            // A missing relative path can only be resolved from the process working directory
            // or one of the explicitly configured source directories.
            return IsWithinTrustedRoot(directPath);
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
            return normalizedPath != null && IsWithinTrustedRoot(normalizedPath);
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
