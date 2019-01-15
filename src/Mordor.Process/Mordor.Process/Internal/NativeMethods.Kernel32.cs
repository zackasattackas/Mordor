using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
// ReSharper disable InconsistentNaming

namespace Mordor.Process.Internal
{
    internal sealed unsafe partial class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr Process;
            public IntPtr Thread;
            public uint Pid;
            public uint ThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint GetModuleFileName(IntPtr module, StringBuilder fileName, uint size);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcess(
            [In] string applicationName,
            [In, MarshalAs(UnmanagedType.LPStr)] string commandLine,
            [In] SECURITY_ATTRIBUTES* processAttributes,
            [In] SECURITY_ATTRIBUTES* threadAttributes,
            [In] bool inherit,
            [In] ProcessCreationFlags creationFlags,
            [In] void* environment,
            [In] string workingDirectory,
            [In] STARTUP_INFO* startupInfo,
            [Out] out PROCESS_INFORMATION processInfo);


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeProcessHandle OpenProcess(ProcessAccess dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern WaitResult WaitForSingleObject(SafeHandle handle, uint milliseconds);

        public const int MAXIMUM_WAIT_OBJECTS = 64;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForMultipleObjects(int count, IntPtr* handles, bool waitAll, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);
    }
}
