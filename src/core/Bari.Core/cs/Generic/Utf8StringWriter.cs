using System.IO;
using System.Text;

namespace Bari.Core.Generic
{
    /// <summary>
    /// A <see cref="StringWriter"/> that reports UTF-8 without a byte order mark, the encoding of
    /// <see cref="IFileSystemDirectory.CreateTextFile"/>, so an XML declaration written to it names
    /// the encoding the text is stored with.
    /// </summary>
    public class Utf8StringWriter: StringWriter
    {
        private static readonly Encoding utf8 = new UTF8Encoding(false);

        /// <summary>
        /// Gets the encoding the written text is going to be stored with
        /// </summary>
        public override Encoding Encoding
        {
            get { return utf8; }
        }
    }
}
