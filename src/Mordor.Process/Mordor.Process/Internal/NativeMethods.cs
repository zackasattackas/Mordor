using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Mordor.Process.Internal
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static partial class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SECURITY_ATTRIBUTES
        {
            
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern WaitResult WaitForInputIdle(SafeProcessHandle handle, uint milliseconds);       
    }
}