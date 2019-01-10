using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Mordor;
using Process = System.Diagnostics.Process;

namespace ConsoleApp1
{
    class Program
    {
        static unsafe void Main(string[] args)
        {
            var perf = Stopwatch.StartNew();
            var processes = Process.GetProcesses();
            
            perf.Stop();
            Console.WriteLine(perf.Elapsed);
            perf.Restart();
            var ids = ProcessManager.GetProcesses();
            perf.Stop();
            Console.WriteLine(perf.Elapsed);

            Debugger.Break();
        }
    }
}
