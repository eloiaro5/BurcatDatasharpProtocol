using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol
{
    /// <summary>
    /// Wraps one or two streams with a stable identifier used by Burcat protocol operations.
    /// </summary>
    public class IdentifiedStream : Stream
    {
        /// <summary>
        /// Gets the protocol identifier for this stream.
        /// </summary>
        public Guid Identifier { get; }

        /// <summary>
        /// Gets the stream used for reads.
        /// </summary>
        public Stream ReadStream { get; }

        /// <summary>
        /// Gets the stream used for writes.
        /// </summary>
        public Stream WriteStream { get; }

        /// <summary>
        /// Initializes an identified stream from separate read and write streams.
        /// </summary>
        /// <param name="identifier">The stream identifier.</param>
        /// <param name="readStream">The stream used for reads.</param>
        /// <param name="writeStream">The stream used for writes.</param>
        public IdentifiedStream(Guid identifier, Stream readStream, Stream writeStream) { Identifier = identifier; ReadStream = readStream; WriteStream = writeStream; }

        /// <summary>
        /// Initializes an identified stream from one bidirectional stream.
        /// </summary>
        /// <param name="identifier">The stream identifier.</param>
        /// <param name="readWriteStream">The stream used for reads and writes.</param>
        public IdentifiedStream(Guid identifier, Stream readWriteStream) : this(identifier, readWriteStream, readWriteStream) { }

        /// <summary>
        /// Initializes an identified stream with a generated identifier.
        /// </summary>
        /// <param name="readStream">The stream used for reads.</param>
        /// <param name="writeStream">The stream used for writes.</param>
        public IdentifiedStream(Stream readStream, Stream writeStream) : this(GuidExtensions.GenerateRandom(), readStream, writeStream) { }

        /// <summary>
        /// Initializes an identified stream with a generated identifier from one bidirectional stream.
        /// </summary>
        /// <param name="readWriteStream">The stream used for reads and writes.</param>
        public IdentifiedStream(Stream readWriteStream) : this(GuidExtensions.GenerateRandom(), readWriteStream, readWriteStream) { }

        /// <inheritdoc/>
        public override bool CanRead => ReadStream.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => ReadStream.CanSeek && WriteStream.CanSeek;       

        /// <inheritdoc/>
        public override bool CanWrite => WriteStream.CanWrite;

        /// <inheritdoc/>
        public override long Length => ReadStream.Length;

        /// <inheritdoc/>
        public override long Position { get => ReadStream.Position; set { ReadStream.Position = value; WriteStream.Position = value; } }

        /// <inheritdoc/>
        public override void Flush() => WriteStream.Flush();

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => ReadStream.Read(buffer, offset, count);

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            long readSeek = ReadStream.Seek(offset, origin);
            long writeSeek = WriteStream.Seek(offset, origin);

            return Math.Min(readSeek, writeSeek);
        }

        /// <inheritdoc/>
        public override void SetLength(long value) => WriteStream.SetLength(value);

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => WriteStream.Write(buffer, offset, count);

        /// <inheritdoc/>
        public override void Close()
        {
            base.Close();

            ReadStream.Close();
            WriteStream.Close();
        }
    }
}
