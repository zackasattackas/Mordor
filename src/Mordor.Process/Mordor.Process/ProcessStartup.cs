using Mordor.Process.Internal;
using System;
using System.IO;
using static Mordor.Process.Internal.NativeMethods;

namespace Mordor.Process
{
    public class ProcessStartup
    {
        #region Fields

        private StartupFlags _startupFlags;
        private ShowWindowOptions _swOptions;            
        private DirectoryInfo _workingDir;

        public static readonly Lazy<DirectoryInfo> DefaultWorkingDirectory =
            new Lazy<DirectoryInfo>(() => new DirectoryInfo(Directory.GetCurrentDirectory()));

        #endregion

        #region Properties

        public string[] CommandLine { get; set; }
        public string CommandLineString => string.Join(" ", CommandLine);

        public DirectoryInfo WorkingDirectory
        {
            get => _workingDir ?? DefaultWorkingDirectory.Value;
            set => _workingDir = value;
        }

        public Stream StdInput { get; set; }
        public Stream StdOutput { get; set; }
        public Stream StdError { get; set; }
        public bool InheritHandles { get; set; }
        public unsafe void* Environment { get; set; }
        public ProcessCreationFlags CreationFlags { get; set; }
        public string Title { get; set; }

        #endregion

        #region Ctor

        public ProcessStartup(params string[] commandLine)
        {
            CommandLine = commandLine;
        }

        #endregion

        #region Public methods        

        public ProcessStartup SetStdInput(FileStream stream)
        {
            StdInput = stream;
            return this;
        }

        public ProcessStartup SetStdOutput(FileStream stream)
        {
            StdOutput = stream;
            return this;
        }

        public ProcessStartup SetStdError(FileStream stream)
        {
            StdError = stream;
            return this;
        }

        #endregion

        #region Public static methods

        public static string Escape(string value)
        {
            return '"' + value + '"';
        }

        #endregion

        #region Internal methods

        internal unsafe STARTUP_INFO GetNativeStruct()
        {
            fixed (char* pTitle = Title)
                return new STARTUP_INFO
                {
                    cb = typeof(STARTUP_INFO).GetSize(),
                    dwFillAttribute = 0,
                    dwFlags = _startupFlags,
                    wShowWindow = _swOptions,
                    lpTitle = pTitle,
                };
        }

        internal unsafe SECURITY_ATTRIBUTES* GetProcessAttributes()
        {
            return null;
        }

        internal unsafe SECURITY_ATTRIBUTES* GetThreadAttributes()
        {
            return null;
        }

        #endregion
    }
}