using System;
using Microsoft.Win32.SafeHandles;
using Mordor.Process.Internal.Win32;

namespace Mordor.Process
{
    public class SafeThreadHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeThreadHandle()
            :base(true)
        {            
        }
        public SafeThreadHandle(IntPtr existing, bool ownsHandle) 
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