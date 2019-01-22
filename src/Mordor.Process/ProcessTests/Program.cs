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
                    var current = Process.CurrentProcess;
                    //var startup = Process.CurrentProcessStartup;
                    //var info = Process.AllProcesses.First(p => p.Pid == current.Pid);

                    var startup = new ProcessStartup(
                        Assembly.GetExecutingAssembly().Location,
                        "Testing output redirection");

                    var proc = Process.Factory.Create(startup);

                    //var info = proc.GetProcessInfo();

                    await proc;
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
                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            return 12345;
        }
    }
}
