using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Mordor.Process.Internal;
using static Mordor.Process.Internal.NativeMethods;
using static Mordor.Process.Internal.NativeHelpers;

namespace Mordor.Process
{
    public class Process : Component, ISafeDisposable
    {
        private int? _exitCode;
        private CancellationToken _cancellation;

        #region Properties

        public bool IsDisposed { get; private set; }
        public SafeProcessHandle SafeProcessHandle { get; }
        public SafeProcessHandle SafeThreadHandle { get; }
        public uint Pid { get; }

        public int ExitCode
        {
            get
            {
                if (_exitCode == default)
                {
                    if (!GetExitCodeProcess(SafeProcessHandle, out var exitCode))
                        ThrowLastWin32Exception();

                    _exitCode = exitCode;
                }

                return _exitCode.Value;
            }
        }

        public WaitHandle WaitHandle => SafeProcessHandle.GetWaitHandle<ManualResetEvent>(true);

        #endregion

        #region Static properties

        public static ProcessFactory Factory => new ProcessFactory();

        #endregion

        #region Ctor        

        internal Process(PROCESS_INFORMATION info, CancellationToken cancellationToken = default)
        {
            _cancellation = cancellationToken;
            Pid = info.Pid;
            SafeProcessHandle = new SafeProcessHandle(info.Process, true);
            SafeThreadHandle = new SafeProcessHandle(info.Thread, true);
        }

        public Process(SafeProcessHandle handle, CancellationToken cancellationToken = default)

        {
            _cancellation = cancellationToken;
            SafeProcessHandle = handle;
            throw new NotImplementedException();
        }

        #endregion

        #region Protected methods

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SafeProcessHandle?.Dispose();
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }

        #endregion

        #region Public methods

        public ConsoleReader OpenStdIn()
        {
            ThrowIfDisposed();
            throw new NotImplementedException();
        }

        public ConsoleWriter OpenStdOut()
        {
            ThrowIfDisposed();
            throw new NotImplementedException();
        }

        public ConsoleWriter OpenStdError()
        {
            ThrowIfDisposed();
            throw new NotImplementedException();
        }

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
#if DEBUG
            NativeHelpers.WaitOne(SafeProcessHandle, timeout);
#elif SAFEPROCESSHANDLE
            WaitHandle.WaitOne(timeout);
#endif

            return ExitCode;
        }

        public TaskAwaiter<(Process Process, int ExitCode)> GetAwaiter()
        {
            return Task.Run(() => (this, Wait())).GetAwaiter();
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

        #endregion

        #region Private methods

        private void ThrowIfDisposed()
        {
            if (!IsDisposed)
                return;

            throw new ObjectDisposedException(nameof(Process));
        }        

        #endregion
    }
}