using System;

namespace Mordor.Process.Internal
{
    [Flags]
    internal enum ProcessCreationFlags : uint
    {
        BreakawayFromJob = 0x01000000,
        DefaultErrorMode = 0x04000000,
        NewConsole = 0x00000010,
        NewProcessGroup = 0x00000200,
        NoWindow = 0x08000000,
        PreserveCodeAuthorizationLevel = 0x02000000,
        Suspended = 0x00000004,
        UnicodeEnvironment = 0x00000400,
        DetachedProcess = 0x00000008,
        ExtendedStartupInfoPresent = 0x00080000,
        InheritParentAffinity = 0x00010000
    }
}