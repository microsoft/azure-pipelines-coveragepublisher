// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers
{
    /// <summary>
    /// Reads a file by walking from an already-opened trusted directory.
    /// A shortcut swapped in after a name check cannot redirect this read.
    /// </summary>
    internal static class NonFollowingFileReader
    {
        private const uint FileReadData = 0x0001;
        private const uint FileReadAttributes = 0x0080;
        private const uint Synchronize = 0x00100000;
        private const uint FileShareAll = 0x00000007;
        private const uint FileOpen = 0x00000001;
        private const uint FileDirectoryFile = 0x00000001;
        private const uint FileSynchronousIoNonalert = 0x00000020;
        private const uint FileNonDirectoryFile = 0x00000040;
        private const uint FileOpenReparsePoint = 0x00200000;
        private const uint ObjCaseInsensitive = 0x00000040;
        private const uint FileAttributeReparsePoint = 0x00000400;
        private const int FileAttributeTagInformationClass = 9;
        private const int StatusObjectNameNotFound = unchecked((int)0xC0000034);
        private const int StatusObjectPathNotFound = unchecked((int)0xC000003A);
        private const int StatusNoSuchFile = unchecked((int)0xC000000F);
        private const int ErrorFileNotFound = 2;

        internal static bool TryReadLines(string trustedRoot, string fullPath, out string[] lines, out bool blocked)
        {
            lines = null;
            blocked = true;

            if (string.IsNullOrWhiteSpace(trustedRoot) || string.IsNullOrWhiteSpace(fullPath))
            {
                return false;
            }

            string relativePath = fullPath.Substring(trustedRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] segments = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return false;
            }

            if (Path.DirectorySeparatorChar == '\\')
            {
                return TryReadLinesWindows(trustedRoot, segments, out lines, out blocked);
            }

            return TryReadLinesUnix(trustedRoot, segments, out lines, out blocked);
        }

        private static bool TryReadLinesWindows(string trustedRoot, string[] segments, out string[] lines, out bool blocked)
        {
            lines = null;
            blocked = true;
            OpenResult root = OpenNtPath(ToNtPath(trustedRoot), directory: true);
            if (!root.Opened)
            {
                blocked = !root.Missing;
                return false;
            }

            using (SafeFileHandle rootHandle = root.Handle)
            {
                if (IsReparsePoint(rootHandle))
                {
                    return false;
                }

                SafeFileHandle current = rootHandle;
                bool ownsCurrent = false;
                try
                {
                    for (int index = 0; index < segments.Length - 1; index++)
                    {
                        OpenResult child = OpenRelative(current, segments[index], directory: true);
                        if (!child.Opened)
                        {
                            blocked = !child.Missing;
                            return false;
                        }

                        if (ownsCurrent)
                        {
                            current.Dispose();
                        }

                        current = child.Handle;
                        ownsCurrent = true;
                        if (IsReparsePoint(current))
                        {
                            return false;
                        }
                    }

                    OpenResult file = OpenRelative(current, segments[segments.Length - 1], directory: false);
                    if (!file.Opened)
                    {
                        blocked = !file.Missing;
                        return false;
                    }

                    using (SafeFileHandle fileHandle = file.Handle)
                    {
                        if (IsReparsePoint(fileHandle))
                        {
                            return false;
                        }

                        lines = ReadLines(fileHandle);
                        blocked = false;
                        return true;
                    }
                }
                finally
                {
                    if (ownsCurrent)
                    {
                        current.Dispose();
                    }
                }
            }
        }

        private static bool TryReadLinesUnix(string trustedRoot, string[] segments, out string[] lines, out bool blocked)
        {
            lines = null;
            blocked = true;
            int rootFd = Open(trustedRoot, UnixReadOnly | UnixDirectory | UnixNoFollow | UnixCloseOnExec);
            if (rootFd < 0)
            {
                blocked = Marshal.GetLastWin32Error() != ErrorFileNotFound;
                return false;
            }

            using (var rootHandle = new SafeFileHandle((IntPtr)rootFd, ownsHandle: true))
            {
                SafeFileHandle current = rootHandle;
                bool ownsCurrent = false;
                try
                {
                    for (int index = 0; index < segments.Length - 1; index++)
                    {
                        int childFd = OpenAt((int)current.DangerousGetHandle(), segments[index], UnixReadOnly | UnixDirectory | UnixNoFollow | UnixCloseOnExec);
                        if (childFd < 0)
                        {
                            blocked = Marshal.GetLastWin32Error() != ErrorFileNotFound;
                            return false;
                        }

                        if (ownsCurrent)
                        {
                            current.Dispose();
                        }

                        current = new SafeFileHandle((IntPtr)childFd, ownsHandle: true);
                        ownsCurrent = true;
                    }

                    int fileFd = OpenAt(
                        (int)current.DangerousGetHandle(),
                        segments[segments.Length - 1],
                        UnixReadOnly | UnixNoFollow | UnixCloseOnExec);
                    if (fileFd < 0)
                    {
                        blocked = Marshal.GetLastWin32Error() != ErrorFileNotFound;
                        return false;
                    }

                    using (var fileHandle = new SafeFileHandle((IntPtr)fileFd, ownsHandle: true))
                    {
                        lines = ReadLines(fileHandle);
                        blocked = false;
                        return true;
                    }
                }
                finally
                {
                    if (ownsCurrent)
                    {
                        current.Dispose();
                    }
                }
            }
        }

        private static string[] ReadLines(SafeFileHandle handle)
        {
            var bytes = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                int read = Read(handle, buffer);
                if (read == 0)
                {
                    break;
                }

                if (read < 0)
                {
                    throw new IOException("Could not read the approved source file.");
                }

                bytes.Write(buffer, 0, read);
            }

            bytes.Position = 0;
            var lines = new List<string>();
            using (var reader = new StreamReader(bytes, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            return lines.ToArray();
        }

        private static int Read(SafeFileHandle handle, byte[] buffer)
        {
            if (Path.DirectorySeparatorChar == '\\')
            {
                int read;
                if (!ReadFile(handle, buffer, buffer.Length, out read, IntPtr.Zero))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 38 || error == 109)
                    {
                        return 0;
                    }

                    return -1;
                }

                return read;
            }

            return UnixRead((int)handle.DangerousGetHandle(), buffer, buffer.Length);
        }

        private static bool IsReparsePoint(SafeFileHandle handle)
        {
            FileAttributeTagInfo info;
            if (!GetFileInformationByHandleEx(handle, FileAttributeTagInformationClass, out info, Marshal.SizeOf<FileAttributeTagInfo>()))
            {
                return true;
            }

            return (info.FileAttributes & FileAttributeReparsePoint) != 0;
        }

        private static OpenResult OpenNtPath(string ntPath, bool directory)
        {
            return OpenCore(IntPtr.Zero, ntPath, directory);
        }

        private static OpenResult OpenRelative(SafeFileHandle root, string name, bool directory)
        {
            return OpenCore(root.DangerousGetHandle(), name, directory);
        }

        private static OpenResult OpenCore(IntPtr root, string name, bool directory)
        {
            byte[] nameBytes = Encoding.Unicode.GetBytes(name);
            IntPtr buffer = Marshal.AllocHGlobal(nameBytes.Length);
            IntPtr unicodePointer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeStringNative>());
            try
            {
                Marshal.Copy(nameBytes, 0, buffer, nameBytes.Length);
                var unicode = new UnicodeStringNative
                {
                    Length = (ushort)nameBytes.Length,
                    MaximumLength = (ushort)nameBytes.Length,
                    Buffer = buffer
                };
                Marshal.StructureToPtr(unicode, unicodePointer, false);

                var attributes = new ObjectAttributesNative
                {
                    Length = Marshal.SizeOf<ObjectAttributesNative>(),
                    RootDirectory = root,
                    ObjectName = unicodePointer,
                    Attributes = ObjCaseInsensitive
                };

                uint access = (directory ? FileReadData : FileReadData) | FileReadAttributes | Synchronize;
                uint options = FileOpenReparsePoint | FileSynchronousIoNonalert
                    | (directory ? FileDirectoryFile : FileNonDirectoryFile);
                int status = NtCreateFile(
                    out IntPtr rawHandle,
                    access,
                    ref attributes,
                    out _,
                    IntPtr.Zero,
                    0,
                    FileShareAll,
                    FileOpen,
                    options,
                    IntPtr.Zero,
                    0);

                if (status < 0 || rawHandle == IntPtr.Zero)
                {
                    if (rawHandle != IntPtr.Zero)
                    {
                        new SafeFileHandle(rawHandle, ownsHandle: true).Dispose();
                    }

                    return new OpenResult(null, IsNotFound(status), blocked: !IsNotFound(status));
                }

                return new OpenResult(new SafeFileHandle(rawHandle, ownsHandle: true), missing: false, blocked: false);
            }
            finally
            {
                Marshal.FreeHGlobal(unicodePointer);
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static bool IsNotFound(int status)
        {
            return status == StatusObjectNameNotFound
                || status == StatusObjectPathNotFound
                || status == StatusNoSuchFile;
        }

        private static string ToNtPath(string fullPath)
        {
            return @"\??\" + fullPath;
        }

        private static int UnixNoFollow
        {
            get { return 0x20000; }
        }

        private static int UnixDirectory
        {
            get { return 0x10000; }
        }

        private static int UnixCloseOnExec
        {
            get { return 0x80000; }
        }

        private const int UnixReadOnly = 0;

        [DllImport("ntdll.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int NtCreateFile(
            out IntPtr fileHandle,
            uint desiredAccess,
            ref ObjectAttributesNative objectAttributes,
            out IoStatusBlockNative ioStatusBlock,
            IntPtr allocationSize,
            uint fileAttributes,
            uint shareAccess,
            uint createDisposition,
            uint createOptions,
            IntPtr eaBuffer,
            uint eaLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool ReadFile(
            SafeFileHandle file,
            byte[] buffer,
            int numberOfBytesToRead,
            out int numberOfBytesRead,
            IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool GetFileInformationByHandleEx(
            SafeFileHandle file,
            int fileInformationClass,
            out FileAttributeTagInfo fileInformation,
            int bufferSize);

        [DllImport("libc", EntryPoint = "open", SetLastError = true)]
        private static extern int Open(string path, int flags);

        [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
        private static extern int OpenAt(int directoryHandle, string path, int flags);

        [DllImport("libc", EntryPoint = "read", SetLastError = true)]
        private static extern int UnixRead(int fileHandle, byte[] buffer, int count);

        [StructLayout(LayoutKind.Sequential)]
        private struct UnicodeStringNative
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ObjectAttributesNative
        {
            public int Length;
            public IntPtr RootDirectory;
            public IntPtr ObjectName;
            public uint Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoStatusBlockNative
        {
            public IntPtr Status;
            public IntPtr Information;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileAttributeTagInfo
        {
            public uint FileAttributes;
            public uint ReparseTag;
        }

        private readonly struct OpenResult
        {
            public OpenResult(SafeFileHandle handle, bool missing, bool blocked)
            {
                Handle = handle;
                Missing = missing;
                Blocked = blocked;
            }

            public SafeFileHandle Handle { get; }

            public bool Missing { get; }

            public bool Blocked { get; }

            public bool Opened
            {
                get { return Handle != null && !Handle.IsInvalid; }
            }
        }
    }
}
