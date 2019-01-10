using System.IO;
using System.Text;

namespace Mordor.Process
{
    public sealed class ConsoleWriter : TextWriter
    {
        private StreamWriter _writer;

        public override Encoding Encoding { get; } = Encoding.Default;

        public ConsoleWriter(Stream stream)
        {
            _writer = new StreamWriter(stream);
        }
    }
}