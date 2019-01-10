using System.IO;

namespace Mordor.Process
{
    public sealed class ConsoleReader : TextReader
    {
        private StreamReader _reader;

        public ConsoleReader(Stream stream)
        {
            _reader = new StreamReader(stream);
        }
    }
}