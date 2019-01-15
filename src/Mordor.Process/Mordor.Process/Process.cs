using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Mordor.Process.Internal;

namespace Mordor.Process
{
    public class Process : Component, ISafeDisposable
    {
        #region Properties

        public bool IsDisposed { get; private set; }
        public SafeProcessHandle SafeProcessHandle { get; set; }
        public SafeProcessHandle SafeThreadHandle { get; set; }
        public uint Pid { get; }

        public WaitHandle WaitHandle
        {
            get
            {
                var waitHandle = new ManualResetEvent(false);
                waitHandle.SetSafeWaitHandle(new SafeWaitHandle(SafeProcessHandle.DangerousGetHandle(), true));
                return waitHandle;
            }
        }

        #endregion

        #region Static properties

        public static ProcessFactory Factory => new ProcessFactory();

        #endregion

        #region Ctor

        internal Process(NativeMethods.PROCESS_INFORMATION info)
        {
            Pid = info.Pid;
            SafeProcessHandle = new SafeProcessHandle(info.Process, true);
            SafeThreadHandle = new SafeProcessHandle(info.Thread, true);            
        }

        public Process(SafeProcessHandle handle)
            
        {
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

        public void WaitOne(TimeSpan timeout = default)
        {
            WaitHandle.WaitOne(timeout);
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