using System.Threading;
using static Mordor.Process.Internal.NativeHelpers;
using static Mordor.Process.Internal.NativeMethods;

namespace Mordor.Process
{
    public sealed class ProcessFactory
    {
        public Process Create(string filePath, string arguments = default, ProcessCreationFlags flags = ProcessCreationFlags.None)
        {
            return Create(new ProcessStartup(filePath, arguments, flags));
        }

        public unsafe Process Create(ProcessStartup startup, CancellationToken cancellationToken = default)
        {
            var native = startup.GetNativeStruct();

            if (!CreateProcess(
                startup.FilePath,
                startup.Arguments,
                startup.GetProcessAttributes(),
                startup.GetThreadAttributes(),
                startup.InheritHandles,
                startup.CreationFlags,
                startup.Environment,
                startup.WorkingDirectory.FullName, &native, out var pi))
                ThrowLastWin32Exception();

            return new Process(pi, startup, cancellationToken);
        }
    }
}