using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Mordor.Process.Internal.Win32
{
    internal static unsafe partial class NativeMethods
    {
        [DllImport("Psapi.dll", SetLastError = true)]
        public static extern bool EnumProcesses(uint* lpidProcess, uint cb, uint* lpcbNeeded);

        [DllImport("Psapi.dll", SetLastError = true)]
        public static extern bool EnumProcessModules(SafeProcessHandle handle, IntPtr* modules, uint cb, out uint needed);
    }
}
