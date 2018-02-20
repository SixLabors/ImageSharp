// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    /// <summary>
    /// Bytes is a byte buffer, similar to a stream, except that it
    /// has to be able to unread more than 1 byte, due to byte stuffing.
    /// Byte stuffing is specified in section F.1.2.3.
    /// TODO: Optimize buffer management inside this class!
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
        public IManagedByteBuffer Buffer;

        /// <summary>
        /// Values of <see cref="Buffer"/> converted to <see cref="int"/>-s
        /// </summary>
        public IBuffer<int> BufferAsInt;

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

        /// <summary>
        /// Creates a new instance of the <see cref="Bytes"/>, and initializes it's buffer.
        /// </summary>
        /// <param name="memoryManager">The <see cref="MemoryManager"/> to use for buffer allocations.</param>
        /// <returns>The bytes created</returns>
        public static Bytes Create(MemoryManager memoryManager)
        {
            return new Bytes
            {
                Buffer = memoryManager.AllocateManagedByteBuffer(BufferSize),
                BufferAsInt = memoryManager.Allocate<int>(BufferSize)
            };
        }

        /// <summary>
        /// Disposes of the underlying buffer
        /// </summary>
        public void Dispose()
        {
            this.Buffer?.Dispose();
            this.BufferAsInt?.Dispose();

            this.Buffer = null;
            this.BufferAsInt = null;
        }

        /// <summary>
        /// ReadByteStuffedByte is like ReadByte but is for byte-stuffed Huffman data.
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        /// <param name="x">The result byte as <see cref="int"/></param>
        /// <returns>The <see cref="OrigDecoderErrorCode"/></returns>
        public OrigDecoderErrorCode ReadByteStuffedByteUnsafe(Stream inputStream, out int x)
        {
            // Take the fast path if bytes.buf contains at least two bytes.
            if (this.I + 2 <= this.J)
            {
                Span<int> bufferSpan = this.BufferAsInt.Span;
                x = bufferSpan[this.I];
                this.I++;
                this.UnreadableBytes = 1;
                if (x != OrigJpegConstants.Markers.XFFInt)
                {
                    return OrigDecoderErrorCode.NoError;
                }

                if (bufferSpan[this.I] != 0x00)
                {
                    return OrigDecoderErrorCode.MissingFF00;
                }

                this.I++;
                this.UnreadableBytes = 2;
                x = OrigJpegConstants.Markers.XFF;
                return OrigDecoderErrorCode.NoError;
            }

            this.UnreadableBytes = 0;

            OrigDecoderErrorCode errorCode = this.ReadByteAsIntUnsafe(inputStream, out x);
            this.UnreadableBytes = 1;
            if (errorCode != OrigDecoderErrorCode.NoError)
            {
                return errorCode;
            }

            if (x != OrigJpegConstants.Markers.XFF)
            {
                return OrigDecoderErrorCode.NoError;
            }

            errorCode = this.ReadByteAsIntUnsafe(inputStream, out x);
            this.UnreadableBytes = 2;
            if (errorCode != OrigDecoderErrorCode.NoError)
            {
                return errorCode;
            }

            if (x != 0x00)
            {
                return OrigDecoderErrorCode.MissingFF00;
            }

            x = OrigJpegConstants.Markers.XFF;
            return OrigDecoderErrorCode.NoError;
        }

        /// <summary>
        /// Returns the next byte, whether buffered or not buffered. It does not care about byte stuffing.
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        /// <returns>The <see cref="byte"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte(Stream inputStream)
        {
            OrigDecoderErrorCode errorCode = this.ReadByteUnsafe(inputStream, out byte result);
            errorCode.EnsureNoError();
            return result;
        }

        /// <summary>
        /// Extracts the next byte, whether buffered or not buffered into the result out parameter. It does not care about byte stuffing.
        /// This method does not throw on format error, it returns a <see cref="OrigDecoderErrorCode"/> instead.
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        /// <param name="result">The result <see cref="byte"/> as out parameter</param>
        /// <returns>The <see cref="OrigDecoderErrorCode"/></returns>
        public OrigDecoderErrorCode ReadByteUnsafe(Stream inputStream, out byte result)
        {
            OrigDecoderErrorCode errorCode = OrigDecoderErrorCode.NoError;
            while (this.I == this.J)
            {
                errorCode = this.FillUnsafe(inputStream);
                if (errorCode != OrigDecoderErrorCode.NoError)
                {
                    result = 0;
                    return errorCode;
                }
            }

            result = this.Buffer.Span[this.I];
            this.I++;
            this.UnreadableBytes = 0;
            return errorCode;
        }

        /// <summary>
        /// Same as <see cref="ReadByteUnsafe"/> but the result is an <see cref="int"/>
        /// </summary>
        /// <param name="inputStream">The input stream</param>
        /// <param name="result">The result <see cref="int"/></param>
        /// <returns>A <see cref="OrigDecoderErrorCode"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrigDecoderErrorCode ReadByteAsIntUnsafe(Stream inputStream, out int result)
        {
            OrigDecoderErrorCode errorCode = OrigDecoderErrorCode.NoError;
            while (this.I == this.J)
            {
                errorCode = this.FillUnsafe(inputStream);
                if (errorCode != OrigDecoderErrorCode.NoError)
                {
                    result = 0;
                    return errorCode;
                }
            }

            result = this.BufferAsInt.Span[this.I];
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
            OrigDecoderErrorCode errorCode = this.FillUnsafe(inputStream);
            errorCode.EnsureNoError();
        }

        /// <summary>
        /// Fills up the bytes buffer from the underlying stream.
        /// It should only be called when there are no unread bytes in bytes.
        /// This method does not throw <see cref="EOFException"/>, returns a <see cref="OrigDecoderErrorCode"/> instead!
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        /// <returns>The <see cref="OrigDecoderErrorCode"/></returns>
        public OrigDecoderErrorCode FillUnsafe(Stream inputStream)
        {
            if (this.I != this.J)
            {
                // Unrecoverable error in the input, throwing!
                DecoderThrowHelper.ThrowImageFormatException.FillCalledWhenUnreadBytesExist();
            }

            Span<byte> byteSpan = this.Buffer.Span;

            // Move the last 2 bytes to the start of the buffer, in case we need
            // to call UnreadByteStuffedByte.
            if (this.J > 2)
            {
                byteSpan[0] = byteSpan[this.J - 2];
                byteSpan[1] = byteSpan[this.J - 1];
                this.I = 2;
                this.J = 2;
            }

            // Fill in the rest of the buffer.
            int n = inputStream.Read(this.Buffer.Array, this.J, byteSpan.Length - this.J);
            if (n == 0)
            {
                return OrigDecoderErrorCode.UnexpectedEndOfStream;
            }

            this.J += n;

            Span<int> intSpan = this.BufferAsInt.Span;
            for (int i = 0; i < byteSpan.Length; i++)
            {
                intSpan[i] = byteSpan[i];
            }

            return OrigDecoderErrorCode.NoError;
        }
    }
}