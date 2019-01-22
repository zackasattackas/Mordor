using System;

namespace Mordor.Process.Fluent
{
    public interface IProcessConfigurator
    {
        IProcessConfigurator Startup(Func<IProcessStartup> startup);
        IProcessConfigurator PipeTo(IProcessConfigurator other);
        IProcess Create(string arguments);
    }
}