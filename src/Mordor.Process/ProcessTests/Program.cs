using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Mordor.Process;
using Process = Mordor.Process.Process;

namespace ProcessTests
{
    internal abstract class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (!args.Any())
            {
                try
                {
                    var file = Assembly.GetExecutingAssembly().Location;
                    var flags = ProcessCreationFlags.NewConsole | ProcessCreationFlags.Suspended;                    
                    var startup = new ProcessStartup(file, "Hello world!", flags);
                    var process = Process.Factory.Create(startup);
                    var info = Process.AllProcesses.First(p => p.Pid == process.Pid);

                    var (_, exitCode) = await process;

                    Console.WriteLine($"Process {info.ModuleName} ({process.Pid}) exit with code {exitCode}.");                    
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                Debugger.Break();
            }
            else
            {
                Console.WriteLine(Environment.CommandLine);
                await Task.Delay(TimeSpan.FromSeconds(30));
            }

            return 12345;
        }
    }
}
