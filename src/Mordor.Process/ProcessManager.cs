using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Mordor.Process.Internal;

namespace Mordor.Process
{
    public sealed class ProcessManager
    {
        public static unsafe uint[] GetProcesses()
        {
            var buffer = new uint[1024];
            var bytesNeeded = 0U;
            uint count;

            fixed (uint* pBuff = &buffer[0])
            {
                if (!NativeMethods.EnumProcesses(pBuff, (uint) (sizeof(uint) * buffer.Length), &bytesNeeded))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                count = bytesNeeded / sizeof(uint);
            }

            var processes = new uint[count];

            for (var i = 0; i < count; i++)
                processes[i] = buffer[i];

            return processes;
        }

        public static Mordor.Process OpenProcess(uint pid, ProcessAccess access)
        {
            return new Mordor.Process(NativeMethods.OpenProcess(access, false, pid));
        }

        public static Mordor.Process[] OpenProcesses(uint[] pids, ProcessAccess access)
        {
            var processes = new Mordor.Process[pids.Length];

            for (var i = 0; i < pids.Length; i++)
                processes[i] = OpenProcess(pids[i], access);

            return processes;
        }

        public static void TerminateProcess(Mordor.Process process)
        {
            TerminateProcess(process.Pid);
        }

        public static void TerminateProcess(uint pid)
        {
            throw new NotImplementedException();
        }

        public static void WaitForInputIdle(Mordor.Process process, TimeSpan timeout)
        {
            var result = NativeMethods.WaitForInputIdle(process.SafeHandle, (uint) timeout.TotalMilliseconds);
        }

        public static void WaitForExit(Mordor.Process process, TimeSpan timeout)
        {
            NativeHelpers.WaitOne(process.SafeHandle, timeout);
        }

        public static void WaitAll(TimeSpan timeout, params Mordor.Process[] processes)
        {
            NativeHelpers.WaitAll(timeout, processes.Select(p => p.SafeHandle).Cast<SafeHandle>().ToArray());
        }

        public static int WaitAny(TimeSpan timeout, params Mordor.Process[] processes)
        {
            return NativeHelpers.WaitAny(timeout, processes.Select(p => p.SafeHandle).Cast<SafeHandle>().ToArray());
        }
    }
}