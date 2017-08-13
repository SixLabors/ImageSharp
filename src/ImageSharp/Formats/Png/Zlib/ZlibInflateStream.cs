namespace ImageSharp.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Text;

    /// <summary>
    /// Provides methods and properties for deframing streams from PNGs.
    /// </summary>
    internal sealed class ZlibInflateStream : Stream
    {
        /// <summary>
        /// The inner raw memory stream
        /// </summary>
        private readonly Stream innerStream;

        /// <summary>
        /// The compressed stream sitting over the top of the deframer
        /// </summary>
        private DeflateStream compressedStream;

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second
        /// time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose()
        /// method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;

        /// <summary>
        /// The read crc data.
        /// </summary>
        private byte[] crcread;

        /// <summary>
        /// The current data remaining to be read
        /// </summary>
        private int currentDataRemaining;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibInflateStream"/> class.
        /// </summary>
        /// <param name="innerStream">The inner raw stream</param>
        public ZlibInflateStream(Stream innerStream)
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
        public DeflateStream CompressedStream => this.compressedStream;

        /// <summary>
        /// Adds new bytes from a frame found in the original stream
        /// </summary>
        /// <param name="bytes">blabla</param>
        public void AllocateNewBytes(int bytes)
        {
            this.currentDataRemaining = bytes;
            if (this.compressedStream == null)
            {
                this.InitializeInflateStream();
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
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                // dispose managed resources
                if (this.compressedStream != null)
                {
                    this.compressedStream.Dispose();
                    this.compressedStream = null;

                    if (this.crcread == null)
                    {
                        // Consume the trailing 4 bytes
                        this.crcread = new byte[4];
                        for (int i = 0; i < 4; i++)
                        {
                            this.crcread[i] = (byte)this.innerStream.ReadByte();
                        }
                    }
                }
            }

            base.Dispose(disposing);

            // Call the appropriate methods to clean up
            // unmanaged resources here.
            // Note disposing is done.
            this.isDisposed = true;
        }

        private void InitializeInflateStream()
        {
            // The DICT dictionary identifier identifying the used dictionary.

            // The preset dictionary.
            bool fdict;

            // Read the zlib header : http://tools.ietf.org/html/rfc1950
            // CMF(Compression Method and flags)
            // This byte is divided into a 4 - bit compression method and a
            // 4-bit information field depending on the compression method.
            // bits 0 to 3  CM Compression method
            // bits 4 to 7  CINFO Compression info
            //
            //   0   1
            // +---+---+
            // |CMF|FLG|
            // +---+---+
            int cmf = this.innerStream.ReadByte();
            int flag = this.innerStream.ReadByte();
            this.currentDataRemaining -= 2;
            if (cmf == -1 || flag == -1)
            {
                return;
            }

            if ((cmf & 0x0f) != 8)
            {
                throw new Exception($"Bad compression method for ZLIB header: cmf={cmf}");
            }

            // CINFO is the base-2 logarithm of the LZ77 window size, minus eight.
            // int cinfo = ((cmf & (0xf0)) >> 8);
            fdict = (flag & 32) != 0;

            if (fdict)
            {
                // The DICT dictionary identifier identifying the used dictionary.
                byte[] dictId = new byte[4];

                for (int i = 0; i < 4; i++)
                {
                    // We consume but don't use this.
                    dictId[i] = (byte)this.innerStream.ReadByte();
                    this.currentDataRemaining--;
                }
            }

            // Initialize the deflate Stream.
            this.compressedStream = new DeflateStream(this, CompressionMode.Decompress, true);
        }
    }
}
