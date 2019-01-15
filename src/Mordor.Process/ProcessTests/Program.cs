using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Mordor.Process;

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
                    var exe = ProcessStartup.Escape(Assembly.GetExecutingAssembly().Location);
                    var startup = new ProcessStartup(exe, "Hello world!")
                    {
                        CreationFlags = ProcessCreationFlags.NewConsole
                    };
                    var (process, exitCode) = await Process.Factory.Create(startup);
                    var wow64 = ProcessManager.IsWow64Process(process.SafeProcessHandle);

                    Console.WriteLine($"Process {process.Pid} exit with code {exitCode}.");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                Console.Write("\r\nPress any key to exit...");
                Console.Read();
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
