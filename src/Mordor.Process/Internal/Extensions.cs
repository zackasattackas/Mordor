using System;
using Mordor.Process.Internal;

namespace Mordor.Internal
{
    internal static class Extensions
    {
        public static uint ToMillisecondTimeout(this TimeSpan timeSpan)
        {
            if (timeSpan.Milliseconds == -1)
                return NativeMethods.INFINITE;

            return (uint) timeSpan.TotalMilliseconds;
        }
    }
}