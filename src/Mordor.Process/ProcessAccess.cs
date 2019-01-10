using System;

namespace Mordor.Process
{
    [Flags]
    public enum ProcessAccess : uint
    {
        QueryInformation = 0x0400
    }
}