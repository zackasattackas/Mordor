using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Mordor.Process.Internal;
using Mordor.Process.Internal.Win32;
using Mordor.Process.Linq;
using static Mordor.Process.Internal.Win32.NativeHelpers;
using static Mordor.Process.Internal.Win32.NativeMethods;

namespace Mordor.Process
{
    public class Process : Component, IProcess<Process>
    {
        #region Fields
        
        private int? _exitCode;
        private readonly CancellationToken _cancellation;
        private readonly uint _pid;
        private readonly SafeProcessHandle _safeProcessHandle;
        private readonly SafeThreadHandle _safeThreadHandle;
        private bool _disposed;

        #endregion

        #region Properties

        public SafeProcessHandle SafeProcessHandle
        {
            get
            {
                ThrowIfDisposed();
                return _safeProcessHandle;
            }
        }

        public SafeThreadHandle SafeThreadHandle
        {
            get
            {
                ThrowIfDisposed();
                return _safeThreadHandle;
            }
        }

        public uint Pid
        {
            get
            {
                ThrowIfDisposed();
                return _pid;
            }
        }

        public int ExitCode
        {
            get
            {
                ThrowIfDisposed();
                if (_exitCode == default)
                {
                    if (!GetExitCodeProcess(SafeProcessHandle, out var exitCode))
                        ThrowLastWin32Exception();

                    _exitCode = exitCode;
                }

                return _exitCode.Value;
            }
        }

        public WaitHandle WaitHandle
        {
            get
            {
                ThrowIfDisposed();
                return SafeProcessHandle.GetWaitHandle<ManualResetEvent>(true);
            }
        }

        public int SuspendCount { get; private set; }

        private bool IsSuspended => SuspendCount > 0;

        #endregion

        #region Static properties

        public static int CurrentPid => (int) GetCurrentProcessId();

        public static Process CurrentProcess => new Process(GetCurrentProcessId(), GetCurrentProcess(), GetCurrentThread());

        public static ProcessStartup CurrentProcessStartup
        {
            get
            {
                GetStartupInfo(out var startup);
                return new ProcessStartup(startup);
            }
        }

        public static ProcessFactory Factory => new ProcessFactory();

        //public static ProcessInfoQueryable AllProcesses { get; } = new ProcessInfoQueryable();

        #endregion

        #region Ctor        

        internal Process(PROCESS_INFORMATION info, ProcessStartup startup, CancellationToken cancellationToken = default)
        {
            _cancellation = cancellationToken;
            _pid = info.Pid;
            _safeProcessHandle = new SafeProcessHandle(info.Process, true);
            _safeThreadHandle = new SafeThreadHandle(info.Thread, true);
        }

        internal Process(uint pid, SafeProcessHandle handle, SafeThreadHandle thread, CancellationToken cancellationToken = default)

        {
            _cancellation = cancellationToken;
            _pid = pid;
            _safeProcessHandle = handle;
            _safeThreadHandle = thread;
        }

        #endregion

        #region Protected methods

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SafeProcessHandle.Dispose();
                SafeThreadHandle.Dispose();
            }

            base.Dispose(disposing);
            _disposed = true;
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Waits for the process to exit.
        /// </summary>
        /// <returns>Returns the process exit code.</returns>
        public int Wait() => Wait(Timeout.InfiniteTimeSpan);

        /// <summary>
        /// Waits for the process to exit or for the timeout limit to be reached.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for the process to exit.</param>
        /// <returns>Returns the process exit code.</returns>
        public int Wait(TimeSpan timeout)
        {
            ThrowIfDisposed();
#if DEBUG
            WaitForSafeHandle(SafeProcessHandle, timeout);
#elif SAFEPROCESSHANDLE
            WaitHandle.WaitOne(timeout);
#endif

            return ExitCode;
        }

        /// <inheritdoc />
        /// <summary>
        /// Get a <see cref="T:System.Runtime.CompilerServices.TaskAwaiter`1" /> that waits for the process to exit or for the <see cref="T:System.Threading.CancellationToken" /> passed to the constructor is signaled.
        /// </summary>
        /// <returns></returns>
        public TaskAwaiter<int> GetAwaiter()
        {
            ThrowIfDisposed();

            if (IsSuspended)
                Resume();

            return Task.Run(Wait, _cancellation).GetAwaiter();
        }

        /// <summary>
        /// Wait until the process has finished initialization and is waiting for user input. This function should be called before searching for any windows associated
        /// with the process.
        /// <para> See https://docs.microsoft.com/windows/desktop/api/winuser/nf-winuser-waitforinputidle for more info.</para>
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public void WaitForInputIdle(TimeSpan timeout)
        {
            ThrowIfDisposed();
            var result = NativeMethods.WaitForInputIdle(SafeProcessHandle, (uint) timeout.Milliseconds);

            switch (result)
            {
                case WaitResult.Signaled:
                    break;
                case WaitResult.TimedOut:
                    throw new TimeoutException("The WaitForInputIdle function timed out.");
                case WaitResult.Failed:
                    ThrowLastWin32Exception();
                    break;
                default :
                    throw new ArgumentOutOfRangeException(default, "Unxpected result from NativeMathod.WaitForInputIdle: " + (int) result);
            }
        }

        public async Task TerminateAsync(uint exitCode)
        {
            await Task.Run(() => Terminate(exitCode), _cancellation);
        }

        public int Terminate() => Terminate(default);

        public int Terminate(uint exitCode)
        {
            ThrowIfDisposed();
            if (!TerminateProcess(SafeProcessHandle, exitCode))
                ThrowLastWin32Exception();

            return Wait();
        }

        /// <inheritdoc />
        /// <summary>
        /// Suspends the specified thread and increments the suspend count until <see cref="M:Mordor.Process.Process.Resume"/> is called.
        /// <para>This method is intended for use by debuggers and should not be used for thread synchronization.</para>
        /// </summary>
        public void Suspend()
        {
            ThrowIfDisposed();

            if (SuspendThread(SafeThreadHandle) == unchecked((uint)-1))
                ThrowLastWin32Exception();            
        }

        /// <inheritdoc />
        /// <summary>
        /// Decrements the suspend count. If the <see cref="SuspendCount"/> is 1, the process' mail thread will resume execution. If the process was not
        /// previously suspended, this method returns immediately.
        /// </summary>
        /// <returns></returns>
        public Process Resume()
        {
            ThrowIfDisposed();

            if (!IsSuspended)
                return this;

            if (ResumeThread(SafeThreadHandle) == unchecked((uint) -1))
                ThrowLastWin32Exception();

            var result = ResumeThread(SafeThreadHandle);

            if (result == unchecked((uint) -1))
                ThrowLastWin32Exception();
            else
                SuspendCount = result == 0 ? 0 : (int) result - 1;

            return this;
        }

        public ProcessInfo GetProcessInfo()
        {
            ThrowIfDisposed();
            throw new NotImplementedException();
            //return AllProcesses.SingleOrDefault(p => p.Pid == Pid);
        }

        #endregion

        #region Public static methods

        public static void WaitAll(TimeSpan timeout, params Process[] processes)
        {
            NativeHelpers.WaitAll(timeout, processes.Select(p => p.SafeProcessHandle).Cast<SafeHandle>().ToArray());
        }

        public static int WaitAny(TimeSpan timeout, params Process[] processes)
        {
            return NativeHelpers.WaitAny(timeout, processes.Select(p => p.SafeProcessHandle).Cast<SafeHandle>().ToArray());
        }        

        /// <summary>
        /// This method wraps the Win32 EnumProcesses(uint*, uint, uint*) function to get the ID of all running processes.
        /// </summary>
        /// <returns></returns>
        public static unsafe uint[] GetProcesses()
        {
            var buffer = new uint[1024];
            var bytesNeeded = 0U;
            uint count;

            fixed (uint* pBuff = buffer)
            {
                if (!EnumProcesses(pBuff, (uint)(sizeof(uint) * buffer.Length), &bytesNeeded))
                    ThrowLastWin32Exception();

                count = bytesNeeded / sizeof(uint);
            }

            var processes = new uint[count];

            for (var i = 0; i < count; i++)
                processes[i] = buffer[i];

            return processes;
        }        

        /// <summary>
        /// Obtains a disposable handle to the process with the specified ID. The handle permissions can be configures with the <see cref="access"/> parameter.
        /// </summary>
        /// <param name="pid"></param>
        /// <param name="access"></param>
        /// <param name="inherit"></param>
        /// <returns></returns>
        public static SafeProcessHandle OpenProcess(uint pid, ProcessAccess access, bool inherit = true)
        {
            return NativeMethods.OpenProcess(access, inherit, pid);
        }

        /// <summary>
        /// Obtains a disposable handle to the specified processes. The handle permissions can be configures with the <see cref="access"/> parameter.
        /// </summary>
        /// <param name="pids"></param>
        /// <param name="access"></param>
        /// <param name="inherit"></param>
        /// <returns></returns>
        public static IEnumerable<SafeProcessHandle> OpenProcesses(uint[] pids, ProcessAccess access, bool inherit)
        {
            return pids.Select(t => OpenProcess(t, access, inherit));
        }

        public static SafeTokenHandle OpenProcessToken(SafeProcessHandle process)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Private methods

        private void ThrowIfDisposed()
        {
            if (!_disposed)
                return;

            throw new ObjectDisposedException(nameof(Process));
        }        

        #endregion
    }
}