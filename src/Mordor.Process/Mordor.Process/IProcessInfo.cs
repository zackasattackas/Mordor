using Microsoft.Win32.SafeHandles;

namespace Mordor.Process
{
    public interface IProcessInfo
    {
        uint Pid { get; }
        IProcessInfo Parent { get; }        
        string Name { get; }
        string Location { get; }
        int ExitCode { get; }        
        string CommandLine { get; }
        IProcessInfo Refresh();
        SafeProcessHandle Open();
    }
}