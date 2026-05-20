using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol
{
    public class IdentifiedStream : Stream
    {
        public Guid Identifier { get; }

        public Stream ReadStream { get; }
        public Stream WriteStream { get; }

        public IdentifiedStream(Guid identifier, Stream readStream, Stream writeStream) { Identifier = identifier; ReadStream = readStream; WriteStream = writeStream; }
        public IdentifiedStream(Guid identifier, Stream readWriteStream) : this(identifier, readWriteStream, readWriteStream) { }
        public IdentifiedStream(Stream readStream, Stream writeStream) : this(GuidExtensions.GenerateRandom(), readStream, writeStream) { }
        public IdentifiedStream(Stream readWriteStream) : this(GuidExtensions.GenerateRandom(), readWriteStream, readWriteStream) { }

        public override bool CanRead => ReadStream.CanRead;
        public override bool CanSeek => ReadStream.CanSeek && WriteStream.CanSeek;       
        public override bool CanWrite => WriteStream.CanWrite;
        public override long Length => ReadStream.Length;
        public override long Position { get => ReadStream.Position; set { ReadStream.Position = value; WriteStream.Position = value; } }

        public override void Flush() => WriteStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => ReadStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin)
        {
            long readSeek = ReadStream.Seek(offset, origin);
            long writeSeek = WriteStream.Seek(offset, origin);

            return Math.Min(readSeek, writeSeek);
        }
        public override void SetLength(long value) => WriteStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => WriteStream.Write(buffer, offset, count);

        public override void Close()
        {
            base.Close();

            ReadStream.Close();
            WriteStream.Close();
        }
    }
}
