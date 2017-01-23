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
        /// Specifies the buffer size for <see cref="Buffer"/> and <see cref="BufferAsInt"/>
        /// </summary>
        public const int BufferSize = 4096;

        /// <summary>
        /// Gets or sets the buffer.
        /// buffer[i:j] are the buffered bytes read from the underlying
        /// stream that haven't yet been passed further on.
        /// </summary>
        public byte[] Buffer;

        /// <summary>
        /// Values of <see cref="Buffer"/> converted to <see cref="int"/>-s
        /// </summary>
        public int[] BufferAsInt;

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

        private static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Create(BufferSize, 50);

        private static readonly ArrayPool<int> IntPool = ArrayPool<int>.Create(BufferSize, 50);

        /// <summary>
        /// Creates a new instance of the <see cref="Bytes"/>, and initializes it's buffer.
        /// </summary>
        /// <returns>The bytes created</returns>
        public static Bytes Create()
        {
            return new Bytes
                       {
                           Buffer = BytePool.Rent(BufferSize),
                           BufferAsInt = IntPool.Rent(BufferSize)
                       };
        }

        /// <summary>
        /// Disposes of the underlying buffer
        /// </summary>
        public void Dispose()
        {
            if (this.Buffer != null)
            {
                BytePool.Return(this.Buffer);
                IntPool.Return(this.BufferAsInt);
            }

            this.Buffer = null;
            this.BufferAsInt = null;
        }

        /// <summary>
        /// ReadByteStuffedByte is like ReadByte but is for byte-stuffed Huffman data.
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        /// <param name="x">The result byte as <see cref="int"/></param>
        /// <returns>The <see cref="DecoderErrorCode"/></returns>
        public DecoderErrorCode ReadByteStuffedByteUnsafe(Stream inputStream, out int x)
        {
            // Take the fast path if bytes.buf contains at least two bytes.
            if (this.I + 2 <= this.J)
            {
                x = this.BufferAsInt[this.I];
                this.I++;
                this.UnreadableBytes = 1;
                if (x != JpegConstants.Markers.XFFInt)
                {
                    return DecoderErrorCode.NoError;
                }

                if (this.BufferAsInt[this.I] != 0x00)
                {
                    return DecoderErrorCode.MissingFF00;
                }

                this.I++;
                this.UnreadableBytes = 2;
                x = JpegConstants.Markers.XFF;
                return DecoderErrorCode.NoError;
            }

            this.UnreadableBytes = 0;

            DecoderErrorCode errorCode = this.ReadByteAsIntUnsafe(inputStream, out x);
            this.UnreadableBytes = 1;
            if (errorCode != DecoderErrorCode.NoError)
            {
                return errorCode;
            }

            if (x != JpegConstants.Markers.XFF)
            {
                return DecoderErrorCode.NoError;
            }

            errorCode = this.ReadByteAsIntUnsafe(inputStream, out x);
            this.UnreadableBytes = 2;
            if (errorCode != DecoderErrorCode.NoError)
            {
                return errorCode;
            }

            if (x != 0x00)
            {
                return DecoderErrorCode.MissingFF00;
            }

            x = JpegConstants.Markers.XFF;
            return DecoderErrorCode.NoError;
        }

        /// <summary>
        /// Returns the next byte, whether buffered or not buffered. It does not care about byte stuffing.
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        /// <returns>The <see cref="byte"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte(Stream inputStream)
        {
            byte result;
            DecoderErrorCode errorCode = this.ReadByteUnsafe(inputStream, out result);
            errorCode.EnsureNoError();
            return result;
        }

        /// <summary>
        /// Extracts the next byte, whether buffered or not buffered into the result out parameter. It does not care about byte stuffing.
        /// This method does not throw on format error, it returns a <see cref="DecoderErrorCode"/> instead.
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        /// <param name="result">The result <see cref="byte"/> as out parameter</param>
        /// <returns>The <see cref="DecoderErrorCode"/></returns>
        public DecoderErrorCode ReadByteUnsafe(Stream inputStream, out byte result)
        {
            DecoderErrorCode errorCode = DecoderErrorCode.NoError;
            while (this.I == this.J)
            {
                errorCode = this.FillUnsafe(inputStream);
                if (errorCode != DecoderErrorCode.NoError)
                {
                    result = 0;
                    return errorCode;
                }
            }

            result = this.Buffer[this.I];
            this.I++;
            this.UnreadableBytes = 0;
            return errorCode;
        }

        /// <summary>
        /// Same as <see cref="ReadByteUnsafe"/> but the result is an <see cref="int"/>
        /// </summary>
        /// <param name="inputStream">The input stream</param>
        /// <param name="result">The result <see cref="int"/></param>
        /// <returns>A <see cref="DecoderErrorCode"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecoderErrorCode ReadByteAsIntUnsafe(Stream inputStream, out int result)
        {
            DecoderErrorCode errorCode = DecoderErrorCode.NoError;
            while (this.I == this.J)
            {
                errorCode = this.FillUnsafe(inputStream);
                if (errorCode != DecoderErrorCode.NoError)
                {
                    result = 0;
                    return errorCode;
                }
            }

            result = this.BufferAsInt[this.I];
            this.I++;
            this.UnreadableBytes = 0;
            return errorCode;
        }

        /// <summary>
        /// Fills up the bytes buffer from the underlying stream.
        /// It should only be called when there are no unread bytes in bytes.
        /// </summary>
        /// <exception cref="EOFException">Thrown when reached end of stream unexpectedly.</exception>
        /// <param name="inputStream">Input stream</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Fill(Stream inputStream)
        {
            DecoderErrorCode errorCode = this.FillUnsafe(inputStream);
            errorCode.EnsureNoError();
        }

        /// <summary>
        /// Fills up the bytes buffer from the underlying stream.
        /// It should only be called when there are no unread bytes in bytes.
        /// This method does not throw <see cref="EOFException"/>, returns a <see cref="DecoderErrorCode"/> instead!
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        /// <returns>The <see cref="DecoderErrorCode"/></returns>
        public DecoderErrorCode FillUnsafe(Stream inputStream)
        {
            if (this.I != this.J)
            {
                // Unrecoverable error in the input, throwing!
                DecoderThrowHelper.ThrowImageFormatException.FillCalledWhenUnreadBytesExist();
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
                return DecoderErrorCode.UnexpectedEndOfStream;
            }

            this.J += n;

            for (int i = 0; i < this.Buffer.Length; i++)
            {
                this.BufferAsInt[i] = this.Buffer[i];
            }

            return DecoderErrorCode.NoError;
        }
    }
}