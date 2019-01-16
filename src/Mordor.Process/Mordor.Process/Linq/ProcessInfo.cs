using System;
using System.Linq;
using Microsoft.Management.Infrastructure;
using Mordor.Process.Internal;
using static Mordor.Process.Internal.CimHelpers;

namespace Mordor.Process.Linq
{
    /// <summary>
    /// A managed wrapper for the Win32_Process class.
    /// </summary>
    public sealed class ProcessInfo
    {
        #region Properties

        [CimInstanceProperty("ProcessId")] public uint Pid { get; private set; }
        [CimInstanceProperty] public string Caption { get; private set; }
        [CimInstanceProperty] public string CommandLine { get; private set; }
        [CimInstanceProperty] public DateTime CreationDate { get; private set; }
        [CimInstanceProperty] public string ExecutablePath { get; private set; }
        [CimInstanceProperty] public string Handle { get; private set; }
        [CimInstanceProperty] public uint HandleCount { get; private set; }
        [CimInstanceProperty] public DateTime InstallDate { get; private set; }
        [CimInstanceProperty] public ulong KernelModeTime { get; private set; }
        [CimInstanceProperty] public uint MaximumWorkingSetSize { get; private set; }
        [CimInstanceProperty] public uint MinimumWorkingSetSize { get; private set; }
        [CimInstanceProperty("Name")] public string ModuleName { get; private set; }
        [CimInstanceProperty] public ulong OtherOperationCount { get; private set; }
        [CimInstanceProperty] public ulong OtherTransferCount { get; private set; }
        [CimInstanceProperty] public uint PageFaults { get; private set; }
        [CimInstanceProperty] public uint PageFileUsage { get; private set; }
        [CimInstanceProperty] public uint ParentProcessId { get; private set; }
        [CimInstanceProperty] public uint PeakPageFileUsage { get; private set; }
        [CimInstanceProperty] public ulong PeakVirtualSize { get; private set; }
        [CimInstanceProperty] public uint PeakWorkingSetSize { get; private set; }
        [CimInstanceProperty] public uint? Priority { get; private set; }
        [CimInstanceProperty] public ulong PublicPageCount { get; private set; }
        [CimInstanceProperty] public uint QuotaNonPagedPoolUsage { get; private set; }
        [CimInstanceProperty] public uint QuotaPagedPoolUsage { get; private set; }
        [CimInstanceProperty] public uint QuotaPeakNonPagedPoolUsage { get; private set; }
        [CimInstanceProperty] public uint QuotaPeakPagedPoolUsage { get; private set; }
        [CimInstanceProperty] public ulong ReadOperationCount { get; private set; }
        [CimInstanceProperty] public ulong ReadTransferCount { get; private set; }
        [CimInstanceProperty] public string Status { get; private set; }
        [CimInstanceProperty] public DateTime TerminationDate { get; private set; }
        [CimInstanceProperty] public uint ThreadCount { get; private set; }
        [CimInstanceProperty] public ulong UserModeTime { get; private set; }
        [CimInstanceProperty] public ulong VirtualSize { get; private set; }
        [CimInstanceProperty] public ulong WorkingSetSize { get; private set; }
        [CimInstanceProperty] public ulong WriteOperationCount { get; private set; }
        [CimInstanceProperty] public ulong WriteTransferCount { get; private set; }

        #endregion

        #region Ctor

        [Obsolete("Use the LINQ API to obtain process info")]
        public ProcessInfo(uint pid)
        {
            using (var session = CimSession.Create("."))
                BindCimInstance(this,
                    ExecuteWql(session, new WqlQuery("Win32_Process").Where("ProcessId = " + pid)).First()                    );
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Gets the ID of the process that created the process represented by this instance.
        /// </summary>
        /// <returns></returns>
        public uint GetParentPid()
        {
            throw new NotImplementedException();
        }

        public ProcessInfo Refresh()
        {
            var refresh = new ProcessInfo(Pid);

            foreach (var property in typeof(ProcessInfo).GetProperties())
            {
                if (property.SetMethod is null || property.GetMethod is null)
                    continue;

                property.SetValue(this, property.GetValue(refresh));
            }

            return this;
        }

        public override string ToString()
        {
            return ModuleName + " (Pid: " + Pid + ")";
        }

        #endregion
    }
}
