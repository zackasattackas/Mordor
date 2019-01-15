

using System;
using Mordor.Process;

namespace ProcessTests
{
    class Program
    {
        static void Main(string[] args)
        {
            var p = Process.Factory.Create("C:\\temp\\program.exe", "Hello world!");            

            Console.Read();
        }
    }
}
