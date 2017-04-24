namespace ImageSharp.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Provides methods and properties for deframing streams from PNGs.
    /// </summary>
    internal class DeframeStream : Stream
    {
        /// <summary>
        /// The inner raw memory stream
        /// </summary>
        private readonly Stream innerStream;

        /// <summary>
        /// The compressed stream sitting over the top of the deframer
        /// </summary>
        private ZlibInflateStream compressedStream;

        /// <summary>
        /// The current data remaining to be read
        /// </summary>
        private int currentDataRemaining;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeframeStream"/> class.
        /// </summary>
        /// <param name="innerStream">The inner raw stream</param>
        public DeframeStream(Stream innerStream)
        {
            this.innerStream = innerStream;
        }

        /// <inheritdoc/>
        public override bool CanRead => this.innerStream.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// Gets the compressed stream over the deframed inner stream
        /// </summary>
        public ZlibInflateStream CompressedStream => this.compressedStream;

        /// <summary>
        /// Adds new bytes from a frame found in the original stream
        /// </summary>
        /// <param name="bytes">blabla</param>
        public void AllocateNewBytes(int bytes)
        {
            this.currentDataRemaining = bytes;
            if (this.compressedStream == null)
            {
                this.compressedStream = new ZlibInflateStream(this);
            }
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override int ReadByte()
        {
            this.currentDataRemaining--;
            return this.innerStream.ReadByte();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.currentDataRemaining == 0)
            {
                return 0;
            }

            int bytesToRead = Math.Min(count, this.currentDataRemaining);
            this.currentDataRemaining -= bytesToRead;
            return this.innerStream.Read(buffer, offset, bytesToRead);
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            this.compressedStream.Dispose();
            base.Dispose(disposing);
        }
    }
}
