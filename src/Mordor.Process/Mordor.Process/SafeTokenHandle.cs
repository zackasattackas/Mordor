using System;
using Microsoft.Win32.SafeHandles;
using Mordor.Process.Internal.Win32;

namespace Mordor.Process
{
    public class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeTokenHandle()
            : base(true)
        {
        }
        public SafeTokenHandle(IntPtr existing, bool ownsHandle)
            : base(ownsHandle)
        {
            handle = existing;
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }
}