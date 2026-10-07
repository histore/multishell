using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MultiShell.Services;

public sealed partial class ShellSession
{
    #region Win32 ConPTY Interop
    [StructLayout(LayoutKind.Sequential)] private struct Coord(short x, short y) { public short X = x; public short Y = y; }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int nLength; public IntPtr lpSecurityDescriptor; [MarshalAs(UnmanagedType.Bool)] public bool bInheritHandle; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo { public int cb; public IntPtr lpReserved; public IntPtr lpDesktop; public IntPtr lpTitle; public int dwX; public int dwY; public int dwXSize; public int dwYSize; public int dwXCountChars; public int dwYCountChars; public int dwFillAttribute; public int dwFlags; public short wShowWindow; public short cbReserved2; public IntPtr lpReserved2; public IntPtr hStdInput; public IntPtr hStdOutput; public IntPtr hStdError; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr lpAttributeList; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public IntPtr hProcess; public IntPtr hThread; public int dwProcessId; public int dwThreadId; }

    private static partial class NativeMethods
    {
        public const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        public const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        public const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, int nSize);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [LibraryImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CreateProcess(
            string? lpApplicationName,
            IntPtr lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref StartupInfoEx lpStartupInfo,
            out ProcessInformation lpProcessInformation);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        internal static partial int CreatePseudoConsole(Coord size, IntPtr hConsoleInput, IntPtr hConsoleOutput, uint dwFlags, out IntPtr phPC);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        internal static partial int ResizePseudoConsole(IntPtr hPC, Coord size);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        internal static partial void ClosePseudoConsole(IntPtr hPC);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetHandleInformation(IntPtr hObject, int dwMask, int dwFlags);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CloseHandle(IntPtr hObject);

        public static bool CreatePipePair(out SafeFileHandle readPipe, out SafeFileHandle writePipe)
        {
            if (CreatePipe(out IntPtr hRead, out IntPtr hWrite, IntPtr.Zero, 0))
            {
                readPipe = new SafeFileHandle(hRead, true);
                writePipe = new SafeFileHandle(hWrite, true);
                return true;
            }
            readPipe = new SafeFileHandle(IntPtr.Zero, true);
            writePipe = new SafeFileHandle(IntPtr.Zero, true);
            return false;
        }

        public static void ClearHandleInheritance(SafeFileHandle handle)
        {
            SetHandleInformation(handle.DangerousGetHandle(), 1, 0);
        }
    }

    private sealed class WindowsPseudoConsoleSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public WindowsPseudoConsoleSafeHandle(IntPtr preExistingHandle) : base(true) { SetHandle(preExistingHandle); }
        protected override bool ReleaseHandle() { NativeMethods.ClosePseudoConsole(handle); return true; }
    }
    #endregion
}
