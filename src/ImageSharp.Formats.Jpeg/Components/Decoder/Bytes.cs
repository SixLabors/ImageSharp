// <copyright file="Bytes.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Formats.Jpg
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Bytes is a byte buffer, similar to a stream, except that it
    /// has to be able to unread more than 1 byte, due to byte stuffing.
    /// Byte stuffing is specified in section F.1.2.3.
    /// </summary>
    internal struct Bytes : IDisposable
    {
        /// <summary>
        /// Gets or sets the buffer.
        /// buffer[i:j] are the buffered bytes read from the underlying
        /// stream that haven't yet been passed further on.
        /// </summary>
        public byte[] Buffer;

        /// <summary>
        /// Start of bytes read
        /// </summary>
        public int I;

        /// <summary>
        /// End of bytes read
        /// </summary>
        public int J;

        /// <summary>
        /// Gets or sets the unreadable bytes. The number of bytes to back up i after
        /// overshooting. It can be 0, 1 or 2.
        /// </summary>
        public int UnreadableBytes;

        private static readonly ArrayPool<byte> ArrayPool = ArrayPool<byte>.Create(4096, 50);

        /// <summary>
        /// Creates a new instance of the <see cref="Bytes"/>, and initializes it's buffer.
        /// </summary>
        /// <returns>The bytes created</returns>
        public static Bytes Create()
        {
            return new Bytes { Buffer = ArrayPool.Rent(4096) };
        }

        /// <summary>
        /// Disposes of the underlying buffer
        /// </summary>
        public void Dispose()
        {
            if (this.Buffer != null)
            {
                ArrayPool.Return(this.Buffer);
            }

            this.Buffer = null;
        }

        /// <summary>
        /// ReadByteStuffedByte is like ReadByte but is for byte-stuffed Huffman data.
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        /// <param name="errorCode">Error code</param>
        /// <returns>The <see cref="byte"/></returns>
        internal byte ReadByteStuffedByte(Stream inputStream, out JpegDecoderCore.ErrorCodes errorCode)
        {
            byte x;

            errorCode = JpegDecoderCore.ErrorCodes.NoError;

            // Take the fast path if bytes.buf contains at least two bytes.
            if (this.I + 2 <= this.J)
            {
                x = this.Buffer[this.I];
                this.I++;
                this.UnreadableBytes = 1;
                if (x != JpegConstants.Markers.XFF)
                {
                    return x;
                }

                if (this.Buffer[this.I] != 0x00)
                {
                    errorCode = JpegDecoderCore.ErrorCodes.MissingFF00;
                    return 0;

                    // throw new MissingFF00Exception();
                }

                this.I++;
                this.UnreadableBytes = 2;
                return JpegConstants.Markers.XFF;
            }

            this.UnreadableBytes = 0;

            x = this.ReadByte(inputStream);
            this.UnreadableBytes = 1;
            if (x != JpegConstants.Markers.XFF)
            {
                return x;
            }

            x = this.ReadByte(inputStream);
            this.UnreadableBytes = 2;
            if (x != 0x00)
            {
                errorCode = JpegDecoderCore.ErrorCodes.MissingFF00;
                return 0;

                // throw new MissingFF00Exception();
            }

            return JpegConstants.Markers.XFF;
        }

        /// <summary>
        /// Returns the next byte, whether buffered or not buffered. It does not care about byte stuffing.
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        /// <returns>The <see cref="byte"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte ReadByte(Stream inputStream)
        {
            while (this.I == this.J)
            {
                this.Fill(inputStream);
            }

            byte x = this.Buffer[this.I];
            this.I++;
            this.UnreadableBytes = 0;
            return x;
        }

        /// <summary>
        /// Fills up the bytes buffer from the underlying stream.
        /// It should only be called when there are no unread bytes in bytes.
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Fill(Stream inputStream)
        {
            if (this.I != this.J)
            {
                throw new ImageFormatException("Fill called when unread bytes exist.");
            }

            // Move the last 2 bytes to the start of the buffer, in case we need
            // to call UnreadByteStuffedByte.
            if (this.J > 2)
            {
                this.Buffer[0] = this.Buffer[this.J - 2];
                this.Buffer[1] = this.Buffer[this.J - 1];
                this.I = 2;
                this.J = 2;
            }

            // Fill in the rest of the buffer.
            int n = inputStream.Read(this.Buffer, this.J, this.Buffer.Length - this.J);
            if (n == 0)
            {
                throw new JpegDecoderCore.EOFException();
            }

            this.J += n;
        }
    }
}