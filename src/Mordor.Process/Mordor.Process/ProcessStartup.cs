using System;
using System.IO;
using Microsoft.Win32.SafeHandles;
using Mordor.Process.Internal;
using static Mordor.Process.Internal.NativeMethods;

namespace Mordor.Process
{
    public class ProcessStartup : IDisposable
    {
        #region Fields

        private STARTUP_INFO _native = new STARTUP_INFO();
        private StartupFlags _startupFlags;
        private ShowWindowOptions _swOptions;            
        private DirectoryInfo _workingDir;
        private bool _disposed;

        #endregion

        #region Properties

        public string FilePath { get; set; }
        public string Arguments { get; set; }
        public DirectoryInfo WorkingDirectory
        {
            get => _workingDir ?? DefaultWorkingDirectory.Value;
            set => _workingDir = value;
        }
        public FileStream StdInput
        {
            get => GetStdStream(_native.hStdInput);
            set => SetStdHandle(value, ref _native.hStdInput);
        }
        public FileStream StdOutput
        {
            get => GetStdStream(_native.hStdOutput);
            set => SetStdHandle(value, ref _native.hStdOutput);
        }
        public FileStream StdError
        {
            get => GetStdStream(_native.hStdError);
            set => SetStdHandle(value, ref _native.hStdError);
        }
        public bool InheritHandles { get; set; }
        public unsafe void* Environment { get; set; }
        public ProcessCreationFlags CreationFlags { get; set; }
        public string Title { get; set; }

        public static readonly Lazy<DirectoryInfo> DefaultWorkingDirectory =
            new Lazy<DirectoryInfo>(() => new DirectoryInfo(Directory.GetCurrentDirectory()));

        #endregion

        #region Ctor

        internal ProcessStartup(STARTUP_INFO native)
        {
            _native = native;           
        }

        public ProcessStartup(string filePath, string arguments = default, ProcessCreationFlags flags = ProcessCreationFlags.None)
        {
            FilePath = filePath;
            Arguments = arguments;
            CreationFlags = flags;
        }

        #endregion

        #region Protected methods

        public void Dispose()
        {
            StdInput?.Dispose();
            StdError?.Dispose();
            StdOutput?.Dispose();

            _disposed = true;
        }

        #endregion

        #region Internal methods

        internal unsafe STARTUP_INFO GetNativeStruct()
        {
            ThrowIfDisposed();
            fixed (char* pTitle = Title)
                return new STARTUP_INFO
                {
                    cb = typeof(STARTUP_INFO).GetSize(),
                    dwFillAttribute = 0,
                    dwFlags = _startupFlags,
                    wShowWindow = _swOptions,
                    lpTitle = pTitle
                };
        }

        internal unsafe SECURITY_ATTRIBUTES* GetProcessAttributes()
        {
            ThrowIfDisposed();
            return null;
        }

        internal unsafe SECURITY_ATTRIBUTES* GetThreadAttributes()
        {
            ThrowIfDisposed();
            return null;
        }

        #endregion

        #region Private methods

        private FileStream GetStdStream(int ptr)
        {
            ThrowIfDisposed();
            var safeHandle = new SafeFileHandle(new IntPtr(ptr), true);

            NativeHelpers.ThrowInvalidHandleException(safeHandle);

            return new FileStream(safeHandle, FileAccess.Write);
        }

        private void SetStdHandle(FileStream stream, ref int handle)
        {
            ThrowIfDisposed();
            CloseHandle(new IntPtr(handle));
            handle = (int)GetStdPtrValue(stream.SafeFileHandle);
        }

        private static long GetStdPtrValue(SafeFileHandle handle)
        {
            var intptr = handle.DangerousGetHandle();

            return IntPtr.Size == 8 ? intptr.ToInt64() : intptr.ToInt32();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ProcessStartup));
        }

        #endregion
    }
}