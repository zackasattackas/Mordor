using System;
using Microsoft.Win32.SafeHandles;

namespace Mordor.Process.Internal
{
    internal sealed class SafePtr : SafeHandleZeroOrMinusOneIsInvalid
    {
        private readonly Func<IntPtr, bool> _disposer;

        public SafePtr(IntPtr value, bool ownsHandle = false, Func<IntPtr, bool> disposer = default)
            :base(ownsHandle)
        {
            _disposer = disposer;
            handle = value;
        }

        protected override bool ReleaseHandle()
        {
            return _disposer?.Invoke(handle) ?? true;
        }
    }
}