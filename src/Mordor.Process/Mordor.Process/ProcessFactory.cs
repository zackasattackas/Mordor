using System.Threading;
using static Mordor.Process.Internal.Win32.NativeHelpers;
using static Mordor.Process.Internal.Win32.NativeMethods;

namespace Mordor.Process
{
    public sealed class ProcessFactory
    {
        public Process Create(
            string filePath,
            string arguments = default,
            ProcessCreationFlags creationFlags = ProcessCreationFlags.None)
        {
            return Create(new ProcessStartup(filePath, arguments), creationFlags);
        }

        public unsafe Process Create(
            ProcessStartup startup,
            ProcessCreationFlags creationFlags = ProcessCreationFlags.None,
            CancellationToken cancellationToken = default)
        {
            var native = startup.GetNativeStruct();

            if (!CreateProcess(
                startup.FilePath,
                startup.Arguments,
                startup.GetProcessAttributes(),
                startup.GetThreadAttributes(),
                startup.InheritHandles,
                creationFlags,
                startup.Environment,
                startup.WorkingDirectory.FullName, &native, out var pi))
                ThrowLastWin32Exception();

            return new Process(pi, startup, cancellationToken);
        }
    }
}