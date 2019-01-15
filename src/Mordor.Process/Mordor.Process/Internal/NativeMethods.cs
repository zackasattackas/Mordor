using Microsoft.Win32.SafeHandles;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Mordor.Process.Internal
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal sealed partial class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SECURITY_ATTRIBUTES
        {
            
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern WaitResult WaitForInputIdle(SafeProcessHandle handle, uint milliseconds);       
    }
}