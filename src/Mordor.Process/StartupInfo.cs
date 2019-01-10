using System;

namespace Mordor.Process
{
    public class StartupInfo
    {
        public string FilePath { get; set; }
        public string Arguments { get; set; }

        public StartupInfo CreateNewConsole() => CreateNewConsole(ConsoleCreationOptions.Default);

        public StartupInfo CreateNewConsole(ConsoleCreationOptions options)
        {
            throw new NotImplementedException();
        }
    }
}