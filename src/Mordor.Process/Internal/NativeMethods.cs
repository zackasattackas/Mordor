using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Mordor.Process.Internal
{


    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal sealed unsafe partial class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SECURITY_ATTRIBUTES
        {
            
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUP_INFO
        {
            
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct PROCESS_INFORMATION
        {
            public SafeProcessHandle Process;
            public SafeHandle Thread;
            public uint Pid;
            public uint ThreadId;
        }




        [DllImport("user32.dll", SetLastError = true)]
        public static extern WaitResult WaitForInputIdle(SafeProcessHandle handle, uint milliseconds);

    }
}