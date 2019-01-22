using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Mordor.Process.Internal.Win32
{
    internal static partial class NativeMethods
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(SafeProcessHandle process, TokenAccessLevels access, out SafeTokenHandle token);
    }
}
