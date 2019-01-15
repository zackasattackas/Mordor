namespace Mordor.Process.Internal
{
    internal sealed unsafe partial class NativeMethods
    {
        public const int MAX_COMMAND_LINE = short.MaxValue + 1;
        public const int MAX_PATH = 256;
        public const uint INFINITE = 0xFFFFFFFF;
    }
}
