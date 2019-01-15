using System;

namespace Mordor.Process 
{
    public sealed class ConsoleCreationOptions
    {
        public string Title { get; set; }
        public ConsoleColor TextColor { get; set; }
        public ConsoleColor BackgroundColor { get; set; }

        public static ConsoleCreationOptions Default { get; }
    }
}