using System.IO;

namespace Mordor.Process
{
    public unsafe interface IProcessStartup
    {
        string FilePath { get; set; }
        string Arguments { get; set; }
        Stream Output { get; set; }
        Stream Input { get; set; }
        Stream Error { get; set; }
        string WorkingDirectory { get; set; }
        void *Environment { get; set; }
        IProcess CreateProcess(string arguments);
    }
}