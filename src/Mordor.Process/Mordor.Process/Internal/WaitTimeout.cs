using System;

namespace Mordor.Process.Internal
{
    internal struct WaitTimeout
    {
        public uint Milliseconds;

        private WaitTimeout(TimeSpan timeSpan)
        {
            Milliseconds = timeSpan.Milliseconds == -1 ? NativeMethods.INFINITE : (uint) timeSpan.TotalMilliseconds;
        }

        public static explicit operator WaitTimeout(TimeSpan timeSpan)
        {
            return new WaitTimeout(timeSpan);
        }

        public static implicit operator uint(WaitTimeout timeout)
        {
            return timeout.Milliseconds;
        }
    }
}