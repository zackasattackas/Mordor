using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
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
