using System;
using System.Runtime.InteropServices;

namespace Mordor.Process.Internal
{
    internal static class Extensions
    {
        public static SafeHandle GetSafeHandle(this IntPtr ptr, bool ownsHandle = false, Func<IntPtr, bool> disposer = default)
        {
            return new SafePtr(ptr, ownsHandle, disposer);
        }

        internal static uint GetSize(this Type type)
        {
            return (uint) Marshal.SizeOf(type);
        }
    }
}