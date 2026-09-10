using System.IO;
using System.Linq;
using Bari.Core.Generic;
using NUnit.Framework;

namespace Bari.Core.Test.Generic
{
    [TestFixture]
    public class StreamOperationsTest
    {
        [Test]
        public void CopyContinuesAfterShortReads()
        {
            var expected = Enumerable.Range(0, 1000).Select(i => (byte)i).ToArray();
            using (var source = new ShortReadStream(expected))
            using (var destination = new MemoryStream())
            {
                StreamOperations.Copy(source, destination);
                Assert.That(destination.ToArray(), Is.EqualTo(expected));
            }
        }

        private sealed class ShortReadStream : Stream
        {
            private readonly MemoryStream inner;
            public ShortReadStream(byte[] data) { inner = new MemoryStream(data); }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => inner.Length;
            public override long Position { get => inner.Position; set => throw new System.NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new System.NotSupportedException();
            public override void SetLength(long value) => throw new System.NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new System.NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count)
            {
                return inner.Read(buffer, offset, System.Math.Min(count, 7));
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    inner.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
