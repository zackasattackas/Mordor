using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Mordor.Process.Internal
{
    internal static class Extensions
    {
        //public static SafeHandle GetSafeHandle(this IntPtr ptr, bool ownsHandle = false, Func<IntPtr, bool> disposer = default)
        //{
        //    return new SafePtr(ptr, ownsHandle, disposer);
        //}

        public static uint GetSize(this Type type)
        {
            return (uint) Marshal.SizeOf(type);
        }

        public static T GetWaitHandle<T>(this SafeHandle handle, bool ownsHandle) where T : WaitHandle
        {
            return GetWaitHandle<T>(handle.DangerousGetHandle(), ownsHandle);
        }

        public static T GetWaitHandle<T>(this IntPtr handle, bool ownsHandle) where T : WaitHandle
        {
            var waitHandle = (T) Activator.CreateInstance(typeof(T), false);
            waitHandle.SetSafeWaitHandle(new SafeWaitHandle(handle, ownsHandle));

            return waitHandle;
        }
    }
}