# Mordor.Process

A new C# library for the Win32 Process API's.

Objectives:

* Async Process object with custom `GetAwaiter` implementation
  * Process factory
  * Fluent API extension methods
* Async LINQ API for querying information about running processes.
  * LINQ->WQL provider to wrap the Microsoft.Management.Infrastructure API.

## Examples

The following examples demonstrate how to configure, create and await processes, and how to query information about running processes.

#### Simple process creation
```csharp
using Mordor.Process;

class Program 
{
    static void Main() 
    {
        // No point capturing the Process object because the handles will be invalid one the process exits
        var (_, exit) = await Process.Factory.Create("git", "init");

        Console.WriteLine($"Process exited with error code {exit}");
    }
}
```

#### Advanced process creation
```csharp
using Mordor.Process;
using Mordor.Process.Linq;

class Program 
{
    static void Main()
    {
        // Configure process startup options                   
        var startup = new ProcessStartup(
            Assembly.GetExecutingAssembly().Location, 
            "Hello world!", 
            ProcessCreationFlags.NewConsole | ProcessCreationFlags.Suspended);

        var process = Process.Factory.Create(startup);

        // Get process info
        var info = Process.AllProcesses.First(p => p.Pid == process.Pid);

        // Resume process and wait for it to exit
        var (_, exitCode) = await process;

        Console.WriteLine($"Process {info.ModuleName} ({process.Pid}) exit with code {exitCode}.");          
    }
}
```

#### Find processes by name and terminate them
```csharp
using Mordor.Process;
using Mordor.Process.Linq;

class Program 
{
    static void Main()
    {
          var procs = Process.AllProcesses.Where(p => p.ModuleName == "notepad.exe").ToList();

          procs.ForEach(p =>
          {
              Process.TerminateProcess(p.Pid);
              Console.WriteLine($"Terminated process: " + p.ToString());
          });     
    }
}
```
