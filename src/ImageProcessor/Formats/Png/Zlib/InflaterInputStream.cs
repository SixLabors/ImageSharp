namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    //using ICSharpCode.SharpZipLib;
    //using ICSharpCode.SharpZipLib.Zip;
    //using ICSharpCode.SharpZipLib.Zip.Compression;

    /// <summary>
    /// This filter stream is used to decompress data compressed using the "deflate"
    /// format. The "deflate" format is described in RFC 1951.
    ///
    /// This stream may form the basis for other decompression filters, such
    /// as the <see cref="ICSharpCode.SharpZipLib.GZip.GZipInputStream">GZipInputStream</see>.
    ///
    /// Author of the original java version : John Leuner.
    /// </summary>
    public class InflaterInputStream : Stream
    {
        #region Constructors
        /// <summary>
        /// Create an InflaterInputStream with the default decompressor
        /// and a default buffer size of 4KB.
        /// </summary>
        /// <param name = "baseInputStream">
        /// The InputStream to read bytes from
        /// </param>
        public InflaterInputStream(Stream baseInputStream)
            : this(baseInputStream, new Inflater(), 4096)
        {
        }

        /// <summary>
        /// Create an InflaterInputStream with the specified decompressor
        /// and a default buffer size of 4KB.
        /// </summary>
        /// <param name = "baseInputStream">
        /// The source of input data
        /// </param>
        /// <param name = "inf">
        /// The decompressor used to decompress data read from baseInputStream
        /// </param>
        public InflaterInputStream(Stream baseInputStream, Inflater inf)
            : this(baseInputStream, inf, 4096)
        {
        }

        /// <summary>
        /// Create an InflaterInputStream with the specified decompressor and the specified buffer size.
        /// </summary>
        /// <param name = "baseInputStream">
        /// The InputStream to read bytes from
        /// </param>
        /// <param name = "inflater">
        /// The decompressor to use
        /// </param>
        /// <param name = "bufferSize">
        /// Size of the buffer to use
        /// </param>
        public InflaterInputStream(Stream baseInputStream, Inflater inflater, int bufferSize)
        {
            if (baseInputStream == null)
            {
                throw new ArgumentNullException("baseInputStream");
            }

            if (inflater == null)
            {
                throw new ArgumentNullException("inflater");
            }

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("bufferSize");
            }

            this.baseInputStream = baseInputStream;
            this.inf = inflater;

            inputBuffer = new InflaterInputBuffer(baseInputStream, bufferSize);
        }

        #endregion

        /// <summary>
        /// Get/set flag indicating ownership of underlying stream.
        /// When the flag is true <see cref="Dispose"/> will close the underlying stream also.
        /// </summary>
        /// <remarks>
        /// The default value is true.
        /// </remarks>
        public bool IsStreamOwner
        {
            get { return isStreamOwner; }
            set { isStreamOwner = value; }
        }

        /// <summary>
        /// Skip specified number of bytes of uncompressed data
        /// </summary>
        /// <param name ="count">
        /// Number of bytes to skip
        /// </param>
        /// <returns>
        /// The number of bytes skipped, zero if the end of 
        /// stream has been reached
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="count">The number of bytes</paramref> to skip is less than or equal to zero.
        /// </exception>
        public long Skip(long count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            // v0.80 Skip by seeking if underlying stream supports it...
            if (baseInputStream.CanSeek)
            {
                baseInputStream.Seek(count, SeekOrigin.Current);
                return count;
            }
            else
            {
                int length = 2048;
                if (count < length)
                {
                    length = (int)count;
                }

                byte[] tmp = new byte[length];
                int readCount = 1;
                long toSkip = count;

                while ((toSkip > 0) && (readCount > 0))
                {
                    if (toSkip < length)
                    {
                        length = (int)toSkip;
                    }

                    readCount = baseInputStream.Read(tmp, 0, length);
                    toSkip -= readCount;
                }

                return count - toSkip;
            }
        }

        /// <summary>
        /// Clear any cryptographic state.
        /// </summary>
        protected void StopDecrypting()
        {
#if !NETCF_1_0 && !NOCRYPTO
            inputBuffer.CryptoTransform = null;
#endif
        }

        /// <summary>
        /// Returns 0 once the end of the stream (EOF) has been reached.
        /// Otherwise returns 1.
        /// </summary>
        public virtual int Available
        {
            get
            {
                return inf.IsFinished ? 0 : 1;
            }
        }

        /// <summary>
        /// Fills the buffer with more data to decompress.
        /// </summary>
        /// <exception cref="SharpZipBaseException">
        /// Stream ends early
        /// </exception>
        protected void Fill()
        {
            // Protect against redundant calls
            if (inputBuffer.Available <= 0)
            {
                inputBuffer.Fill();
                if (inputBuffer.Available <= 0)
                {
                    throw new ImageFormatException("Unexpected EOF");
                }
            }
            inputBuffer.SetInflaterInput(inf);
        }

        #region Stream Overrides
        /// <summary>
        /// Gets a value indicating whether the current stream supports reading
        /// </summary>
        public override bool CanRead
        {
            get
            {
                return baseInputStream.CanRead;
            }
        }

        /// <summary>
        /// Gets a value of false indicating seeking is not supported for this stream.
        /// </summary>
        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value of false indicating that this stream is not writeable.
        /// </summary>
        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// A value representing the length of the stream in bytes.
        /// </summary>
        public override long Length
        {
            get
            {
                return inputBuffer.RawLength;
            }
        }

        /// <summary>
        /// The current position within the stream.
        /// Throws a NotSupportedException when attempting to set the position
        /// </summary>
        /// <exception cref="NotSupportedException">Attempting to set the position</exception>
        public override long Position
        {
            get
            {
                return baseInputStream.Position;
            }
            set
            {
                throw new NotSupportedException("InflaterInputStream Position not supported");
            }
        }

        /// <summary>
        /// Flushes the baseInputStream
        /// </summary>
        public override void Flush()
        {
            baseInputStream.Flush();
        }

        /// <summary>
        /// Sets the position within the current stream
        /// Always throws a NotSupportedException
        /// </summary>
        /// <param name="offset">The relative offset to seek to.</param>
        /// <param name="origin">The <see cref="SeekOrigin"/> defining where to seek from.</param>
        /// <returns>The new position in the stream.</returns>
        /// <exception cref="NotSupportedException">Any access</exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Seek not supported");
        }

        /// <summary>
        /// Set the length of the current stream
        /// Always throws a NotSupportedException
        /// </summary>
        /// <param name="value">The new length value for the stream.</param>
        /// <exception cref="NotSupportedException">Any access</exception>
        public override void SetLength(long value)
        {
            throw new NotSupportedException("InflaterInputStream SetLength not supported");
        }

        /// <summary>
        /// Writes a sequence of bytes to stream and advances the current position
        /// This method always throws a NotSupportedException
        /// </summary>
        /// <param name="buffer">Thew buffer containing data to write.</param>
        /// <param name="offset">The offset of the first byte to write.</param>
        /// <param name="count">The number of bytes to write.</param>
        /// <exception cref="NotSupportedException">Any access</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("InflaterInputStream Write not supported");
        }

        /// <summary>
        /// Writes one byte to the current stream and advances the current position
        /// Always throws a NotSupportedException
        /// </summary>
        /// <param name="value">The byte to write.</param>
        /// <exception cref="NotSupportedException">Any access</exception>
        public override void WriteByte(byte value)
        {
            throw new NotSupportedException("InflaterInputStream WriteByte not supported");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && !isClosed)
            {
                isClosed = true;
                if (isStreamOwner)
                {
                    baseInputStream.Dispose();
                }
            }
        }

        /// <summary>
        /// Reads decompressed data into the provided buffer byte array
        /// </summary>
        /// <param name ="buffer">
        /// The array to read and decompress data into
        /// </param>
        /// <param name ="offset">
        /// The offset indicating where the data should be placed
        /// </param>
        /// <param name ="count">
        /// The number of bytes to decompress
        /// </param>
        /// <returns>The number of bytes read.  Zero signals the end of stream</returns>
        /// <exception cref="SharpZipBaseException">
        /// Inflater needs a dictionary
        /// </exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (inf.IsNeedingDictionary)
            {
                throw new ImageFormatException("Need a dictionary");
            }

            int remainingBytes = count;
            while (true)
            {
                int bytesRead = inf.Inflate(buffer, offset, remainingBytes);
                offset += bytesRead;
                remainingBytes -= bytesRead;

                if (remainingBytes == 0 || inf.IsFinished)
                {
                    break;
                }

                if (inf.IsNeedingInput)
                {
                    Fill();
                }
                else if (bytesRead == 0)
                {
                    throw new ImageFormatException("Dont know what to do");
                }
            }
            return count - remainingBytes;
        }
        #endregion

        #region Instance Fields
        /// <summary>
        /// Decompressor for this stream
        /// </summary>
        protected Inflater inf;

        /// <summary>
        /// <see cref="InflaterInputBuffer">Input buffer</see> for this stream.
        /// </summary>
        protected InflaterInputBuffer inputBuffer;

        /// <summary>
        /// Base stream the inflater reads from.
        /// </summary>
        private Stream baseInputStream;

        /// <summary>
        /// The compressed size
        /// </summary>
        protected long csize;

        /// <summary>
        /// Flag indicating wether this instance has been closed or not.
        /// </summary>
        bool isClosed;

        /// <summary>
        /// Flag indicating wether this instance is designated the stream owner.
        /// When closing if this flag is true the underlying stream is closed.
        /// </summary>
        bool isStreamOwner = true;
        #endregion
    }
}

