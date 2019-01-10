using Microsoft.Win32.SafeHandles;
using Mordor.Process.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using static Mordor.Process.Internal.NativeHelpers;
using static Mordor.Process.Internal.NativeMethods;

namespace Mordor.Process
{
    public class Process : Component, ISafeDisposable
    {
        #region Properties

        public bool IsDisposed { get; private set; }
        public SafeProcessHandle SafeHandle { get; set; }
        public uint Pid { get; }

        public static ProcessFactory Factory => new ProcessFactory();

        #endregion

        internal Process(PROCESS_INFORMATION info)
        {
            ThrowInvalidHandleException(info.Process);

            SafeHandle = info.Process;
            Pid = info.Pid;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SafeHandle?.Dispose();
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }

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

        private void ThrowIfDisposed()
        {
            if (!IsDisposed)
                return;

            throw new ObjectDisposedException(nameof(Process));
        }
    }
}

namespace Mordor.Process.Extensions
{
    public static class Extensions
    {
        public static Process GetParent(this Process parent)
        {
            throw new NotImplementedException();
        }

        public static IEnumerable<Process> EnumerateChildProcesses(this Process parent)
        {
            throw new NotImplementedException();
        }
    }
}