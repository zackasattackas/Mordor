using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mordor.Process
{
    public sealed class ProcessFactory
    {
        public static Mordor.Process Create(string fileName, params string[] arguments)
        {
            return CreateAsync(fileName, string.Join(" ", arguments), CancellationToken.None).Result;
        }

        public static async Task<Mordor.Process> CreateAsync(string fileName, string arguments, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public static Mordor.Process CreateAsync(StartupInfo startupInfo)
        {
            return CreateAsync(startupInfo, CancellationToken.None).Result;
        }

        public static async Task<Mordor.Process> CreateAsync(StartupInfo startupInfo, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}