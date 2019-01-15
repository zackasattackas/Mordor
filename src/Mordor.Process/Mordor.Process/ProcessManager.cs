using System;
using Mordor.Process.Internal;
using static Mordor.Process.Internal.NativeHelpers;
using static Mordor.Process.Internal.NativeMethods;

namespace Mordor.Process
{
    public sealed class ProcessManager
    {
        public static unsafe uint[] GetProcesses()
        {
            var buffer = new uint[1024];
            var bytesNeeded = 0U;
            uint count;

            fixed (uint* pBuff = buffer)
            {
                if (!EnumProcesses(pBuff, (uint) (sizeof(uint) * buffer.Length), &bytesNeeded))
                    ThrowLastWin32Exception();

                count = bytesNeeded / sizeof(uint);
            }

            var processes = new uint[count];

            for (var i = 0; i < count; i++)
                processes[i] = buffer[i];

            return processes;
        }

        public static Process OpenProcess(uint pid, ProcessAccess access, bool inherit)
        {            
            return new Process(NativeMethods.OpenProcess(access, inherit, pid));
        }

        public static Process [] OpenProcesses(uint[] pids, ProcessAccess access, bool inherit)
        {
            var processes = new Process [pids.Length];

            for (var i = 0; i < pids.Length; i++)
                processes[i] = OpenProcess(pids[i], access, inherit);

            return processes;
        }

        public static void TerminateProcess(Process process)
        {
            TerminateProcess(process.Pid);
        }

        public static void TerminateProcess(uint pid)
        {
            throw new NotImplementedException();
        }

        public static void WaitForInputIdle(Process process, TimeSpan timeout)
        {
            throw new NotImplementedException();
        }
    }
}