using System.ComponentModel;

namespace Mordor.Process
{
    /// <inheritdoc />
    /// <summary>
    /// Represents an operating system process.
    /// </summary>
    public interface IProcess : IComponent
    {        
        /// <summary>
        /// The operating system-assigned identifier of the process.
        /// </summary>
        uint Pid { get; }
    }
}