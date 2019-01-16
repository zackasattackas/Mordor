namespace Mordor.Process.Internal
{
    internal enum WaitResult
    {
        Signaled = 0x0,
        Abandoned = 0x80,        
        TimedOut = 0x102,
        Failed = unchecked((int)0xFFFFFFFF)
    }
}