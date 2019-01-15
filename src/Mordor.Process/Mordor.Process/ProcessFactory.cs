using System.Threading;
using System.Threading.Tasks;
using static Mordor.Process.Internal.NativeHelpers;
using static Mordor.Process.Internal.NativeMethods;

// ReSharper disable MemberCanBeMadeStatic.Global
// ReSharper disable MemberCanBePrivate.Global

namespace Mordor.Process
{
    public sealed class ProcessFactory
    {
        public Process Create(params string[] commandline)
        {
            return Create(new ProcessStartup(commandline));
        }

        public unsafe Process Create(ProcessStartup startup)
        {
            var cmd = startup.CommandLineString;
            var procSec = startup.GetProcessAttributes();
            var threadSec = startup.GetThreadAttributes();
            var inherit = startup.InheritHandles;
            var creationFlags = startup.CreationFlags;
            var env = startup.Environment;
            var working = startup.WorkingDirectory.FullName;
            var native = startup.GetNativeStruct();

            if (!CreateProcess(null, cmd, procSec, threadSec, inherit, creationFlags, env, working, &native, out var pi))
                ThrowLastWin32Exception();

            return new Process(pi);
        }

        public async Task<Process> CreateAsync(
            string[] commandLine, 
            ProcessCreationFlags flags = ProcessCreationFlags.None,
            CancellationToken cancellationToken = default)
        {
            return await CreateAsync(new ProcessStartup(commandLine), cancellationToken);
        }

        public async Task<Process> CreateAsync(ProcessStartup startup, CancellationToken cancellationToken)
        {
            return await Task.Run(() => Create(startup), cancellationToken);
        }
    }
}