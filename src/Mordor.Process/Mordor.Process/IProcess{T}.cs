using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Mordor.Process.Internal;

namespace Mordor.Process
{
    /// <inheritdoc />
    public interface IProcess<out T> : IProcess where T : IProcess
    { 
        /// <summary>
        /// A disposable handle to the native operating system handle.
        /// </summary>
        SafeProcessHandle SafeProcessHandle { get; }

        /// <summary>
        /// A disposable handle to the process' main thread.
        /// </summary>
        SafeThreadHandle SafeThreadHandle { get; }

        /// <summary>
        /// Gets a <see cref="WaitHandle"/> instance that is signaled when the process exits.
        /// </summary>
        WaitHandle WaitHandle { get; }

        /// <summary>
        /// Gets the value returned by the process when it exits.
        /// </summary>
        int ExitCode { get; }

        /// <summary>
        /// Suspends the process until <see cref="Resume"/> is called.
        /// </summary>
        /// <returns></returns>
        void Suspend();

        /// <summary>
        /// Resumes a process that was previously suspended by a call to <see cref="Suspend"/>. If the process is not suspended this
        /// method returns immediately.
        /// </summary>
        /// <returns></returns>
        T Resume();

        /// <summary>
        /// Stops execution of all threads within the process and requests cancellation of all pending I/O.
        /// </summary>
        /// <returns></returns>
        int Terminate();

        /// <summary>
        /// Returns a <see cref="TaskAwaiter{TResult}"/> instance that returns the process exit code once all threads in the process have finished executing.
        /// </summary>
        /// <returns></returns>
        TaskAwaiter<int> GetAwaiter();
    }
}