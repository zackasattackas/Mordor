using System.Threading;
using System.Threading.Tasks;
using static Mordor.Process.Internal.NativeHelpers;
using static Mordor.Process.Internal.NativeMethods;

namespace Mordor.Process
{
    public sealed class ProcessFactory
    {
        public Process Create(params string[] commandline)
        {
            return Create(new StartupInfo(commandline));
        }

        public unsafe Process Create(StartupInfo startupInfo)
        {
            var cmd = startupInfo.CommandLineString;
            var procSec = startupInfo.GetProcessAttributes();
            var threadSec = startupInfo.GetThreadAttributes();
            var inherit = startupInfo.InheritHandles;
            var creationFlags = startupInfo.CreationFlags;
            var env = startupInfo.Environment;
            var working = startupInfo.WorkingDirectory.FullName;
            var startup = startupInfo.GetNativeStruct();

            if (!CreateProcess(null, cmd, procSec, threadSec, inherit, creationFlags, env, working, &startup, out var pi))
                ThrowLastWin32Exception();

            return new Process(pi);
        }

        public async Task<Process> CreateAsync(
            string[] commandLine, 
            ProcessCreationFlags flags = ProcessCreationFlags.None,
            CancellationToken cancellationToken = default)
        {
            return await CreateAsync(new StartupInfo(commandLine), cancellationToken);
        }

        public async Task<Process> CreateAsync(StartupInfo startupInfo, CancellationToken cancellationToken)
        {
            return await Task.Run(() => Create(startupInfo), cancellationToken);
        }
    }
}