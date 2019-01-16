using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Mordor.Process.Internal;
using Mordor.Process.Linq;
using static Mordor.Process.Internal.NativeHelpers;
using static Mordor.Process.Internal.NativeMethods;

namespace Mordor.Process
{
    public class Process : Component, ISafeDisposable
    {
        #region Fields

        private int? _exitCode;
        private readonly CancellationToken _cancellation;
        private readonly ProcessStartup _startup;

        #endregion

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
        public static CimInstanceQuery<ProcessInfo> AllProcesses { get; }

        #endregion

        #region Ctor        

        internal Process(PROCESS_INFORMATION info, ProcessStartup startup, CancellationToken cancellationToken = default)
        {
            _cancellation = cancellationToken;
            _startup = startup;

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
            if ((_startup.CreationFlags & ProcessCreationFlags.Suspended) != 0)
                ResumeProcess(this);
            return Task.Run(() => (this, Wait()), _cancellation).GetAwaiter();
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

        public static SafeProcessHandle OpenProcess(uint pid, ProcessAccess access, bool inherit)
        {
            return NativeMethods.OpenProcess(access, inherit, pid);
        }

        public static SafeProcessHandle[] OpenProcesses(uint[] pids, ProcessAccess access, bool inherit)
        {
            var processes = new SafeProcessHandle[pids.Length];

            for (var i = 0; i < pids.Length; i++)
                processes[i] = OpenProcess(pids[i], access, inherit);

            return processes;
        }

        public static void TerminateProcess(uint pid)
        {
            throw new NotImplementedException();
        }

        public static void ResumeProcess(Process process)
        {
            var result = NativeMethods.ResumeThread(process.SafeThreadHandle);

            if (result == unchecked((uint) -1))
                ThrowLastWin32Exception();
        }

        //public static unsafe string GetProcessFileName(SafeProcessHandle handle)
        //{
        //    var fileName = stackalloc char[MAX_PATH];

        //    if (!GetProcessInformation(handle, ProcessInfo.ImageFileName, (void*)fileName, sizeof(char) * MAX_PATH))
        //        ThrowLastWin32Exception();

        //    return new string(fileName);
        //}


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