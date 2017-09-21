// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    /// <summary>
    /// Encapsulates stream reading and processing data and operations for <see cref="OrigJpegDecoderCore"/>.
    /// It's a value type for imporved data locality, and reduced number of CALLVIRT-s
    /// </summary>
    internal struct InputProcessor : IDisposable
    {
        /// <summary>
        /// Holds the unprocessed bits that have been taken from the byte-stream.
        /// </summary>
        public Bits Bits;

        /// <summary>
        /// The byte buffer
        /// </summary>
        public Bytes Bytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="InputProcessor"/> struct.
        /// </summary>
        /// <param name="inputStream">The input <see cref="Stream"/></param>
        /// <param name="temp">Temporal buffer, same as <see cref="OrigJpegDecoderCore.Temp"/></param>
        public InputProcessor(Stream inputStream, byte[] temp)
        {
            this.Bits = default(Bits);
            this.Bytes = Bytes.Create();
            this.InputStream = inputStream;
            this.Temp = temp;
            this.LastErrorCode = OrigDecoderErrorCode.NoError;
        }

        /// <summary>
        /// Gets the input stream
        /// </summary>
        public Stream InputStream { get; }

        /// <summary>
        /// Gets the temporal buffer, same instance as <see cref="OrigJpegDecoderCore.Temp"/>
        /// </summary>
        public byte[] Temp { get; }

        /// <summary>
        /// Gets a value indicating whether an unexpected EOF reached in <see cref="InputStream"/>.
        /// </summary>
        public bool ReachedEOF => this.LastErrorCode == OrigDecoderErrorCode.UnexpectedEndOfStream;

        public bool HasError => this.LastErrorCode != OrigDecoderErrorCode.NoError;

        public OrigDecoderErrorCode LastErrorCode { get; private set; }

        public void ResetErrorState() => this.LastErrorCode = OrigDecoderErrorCode.NoError;

        /// <summary>
        /// If errorCode indicates unexpected EOF, sets <see cref="ReachedEOF"/> to true and returns false.
        /// Calls <see cref="DecoderThrowHelper.EnsureNoError"/> and returns true otherwise.
        /// </summary>
        /// <returns>A <see cref="bool"/> indicating whether EOF reached</returns>
        public bool CheckEOFEnsureNoError()
        {
            if (this.LastErrorCode == OrigDecoderErrorCode.UnexpectedEndOfStream)
            {
                return false;
            }

            this.LastErrorCode.EnsureNoError();
            return true;
        }

        /// <summary>
        /// If errorCode indicates unexpected EOF, sets <see cref="ReachedEOF"/> to true and returns false.
        /// Returns true otherwise.
        /// </summary>
        /// <returns>A <see cref="bool"/> indicating whether EOF reached</returns>
        public bool CheckEOF()
        {
            if (this.LastErrorCode == OrigDecoderErrorCode.UnexpectedEndOfStream)
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Bytes.Dispose();
        }

        /// <summary>
        /// Returns the next byte, whether buffered or not buffered. It does not care about byte stuffing.
        /// </summary>
        /// <returns>The <see cref="byte" /></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            return this.Bytes.ReadByte(this.InputStream);
        }

        /// <summary>
        /// Decodes a single bit
        /// TODO: This method (and also the usages) could be optimized by batching!
        /// </summary>
        /// <param name="result">The decoded bit as a <see cref="bool"/></param>
        /// <returns>The <see cref="OrigDecoderErrorCode" /></returns>
        public OrigDecoderErrorCode DecodeBitUnsafe(out bool result)
        {
            if (this.Bits.UnreadBits == 0)
            {
                this.LastErrorCode = this.Bits.Ensure1BitUnsafe(ref this);
                if (this.LastErrorCode != OrigDecoderErrorCode.NoError)
                {
                    result = false;
                    return this.LastErrorCode;
                }
            }

            result = (this.Bits.Accumulator & this.Bits.Mask) != 0;
            this.Bits.UnreadBits--;
            this.Bits.Mask >>= 1;
            return this.LastErrorCode = OrigDecoderErrorCode.NoError;
        }

        /// <summary>
        /// Reads exactly length bytes into data. It does not care about byte stuffing.
        /// Does not throw on errors, returns <see cref="OrigJpegDecoderCore"/> instead!
        /// </summary>
        /// <param name="data">The data to write to.</param>
        /// <param name="offset">The offset in the source buffer</param>
        /// <param name="length">The number of bytes to read</param>
        /// <returns>The <see cref="OrigDecoderErrorCode"/></returns>
        public OrigDecoderErrorCode ReadFullUnsafe(byte[] data, int offset, int length)
        {
            // Unread the overshot bytes, if any.
            if (this.Bytes.UnreadableBytes != 0)
            {
                if (this.Bits.UnreadBits >= 8)
                {
                    this.UnreadByteStuffedByte();
                }

                this.Bytes.UnreadableBytes = 0;
            }

            this.LastErrorCode = OrigDecoderErrorCode.NoError;
            while (length > 0 && this.LastErrorCode == OrigDecoderErrorCode.NoError)
            {
                if (this.Bytes.J - this.Bytes.I >= length)
                {
                    Array.Copy(this.Bytes.Buffer, this.Bytes.I, data, offset, length);
                    this.Bytes.I += length;
                    length -= length;
                }
                else
                {
                    Array.Copy(this.Bytes.Buffer, this.Bytes.I, data, offset, this.Bytes.J - this.Bytes.I);
                    offset += this.Bytes.J - this.Bytes.I;
                    length -= this.Bytes.J - this.Bytes.I;
                    this.Bytes.I += this.Bytes.J - this.Bytes.I;

                    this.LastErrorCode = this.Bytes.FillUnsafe(this.InputStream);
                }
            }

            return this.LastErrorCode;
        }

        /// <summary>
        /// Decodes the given number of bits
        /// </summary>
        /// <param name="count">The number of bits to decode.</param>
        /// <param name="result">The <see cref="uint" /> result</param>
        /// <returns>The <see cref="OrigDecoderErrorCode"/></returns>
        public OrigDecoderErrorCode DecodeBitsUnsafe(int count, out int result)
        {
            if (this.Bits.UnreadBits < count)
            {
                this.LastErrorCode = this.Bits.EnsureNBitsUnsafe(count, ref this);
                if (this.LastErrorCode != OrigDecoderErrorCode.NoError)
                {
                    result = 0;
                    return this.LastErrorCode;
                }
            }

            result = this.Bits.Accumulator >> (this.Bits.UnreadBits - count);
            result = result & ((1 << count) - 1);
            this.Bits.UnreadBits -= count;
            this.Bits.Mask >>= count;
            return this.LastErrorCode = OrigDecoderErrorCode.NoError;
        }

        /// <summary>
        /// Extracts the next Huffman-coded value from the bit-stream into result, decoded according to the given value.
        /// </summary>
        /// <param name="huffmanTree">The huffman value</param>
        /// <param name="result">The decoded <see cref="byte" /></param>
        /// <returns>The <see cref="OrigDecoderErrorCode"/></returns>
        public OrigDecoderErrorCode DecodeHuffmanUnsafe(ref OrigHuffmanTree huffmanTree, out int result)
        {
            result = 0;

            if (huffmanTree.Length == 0)
            {
                DecoderThrowHelper.ThrowImageFormatException.UninitializedHuffmanTable();
            }

            if (this.Bits.UnreadBits < 8)
            {
                this.LastErrorCode = this.Bits.Ensure8BitsUnsafe(ref this);

                if (this.LastErrorCode == OrigDecoderErrorCode.NoError)
                {
                    int lutIndex = (this.Bits.Accumulator >> (this.Bits.UnreadBits - OrigHuffmanTree.LutSizeLog2)) & 0xFF;
                    int v = huffmanTree.Lut[lutIndex];

                    if (v != 0)
                    {
                        int n = (v & 0xFF) - 1;
                        this.Bits.UnreadBits -= n;
                        this.Bits.Mask >>= n;
                        result = v >> 8;
                        return this.LastErrorCode;
                    }
                }
                else
                {
                    this.UnreadByteStuffedByte();
                    return this.LastErrorCode;
                }
            }

            int code = 0;
            for (int i = 0; i < OrigHuffmanTree.MaxCodeLength; i++)
            {
                if (this.Bits.UnreadBits == 0)
                {
                    this.LastErrorCode = this.Bits.EnsureNBitsUnsafe(1, ref this);

                    if (this.HasError)
                    {
                        return this.LastErrorCode;
                    }
                }

                if ((this.Bits.Accumulator & this.Bits.Mask) != 0)
                {
                    code |= 1;
                }

                this.Bits.UnreadBits--;
                this.Bits.Mask >>= 1;

                if (code <= huffmanTree.MaxCodes[i])
                {
                    result = huffmanTree.GetValue(code, i);
                    return this.LastErrorCode = OrigDecoderErrorCode.NoError;
                }

                code <<= 1;
            }

            // Unrecoverable error, throwing:
            DecoderThrowHelper.ThrowImageFormatException.BadHuffmanCode();

            // DUMMY RETURN! C# doesn't know we have thrown an exception!
            return OrigDecoderErrorCode.NoError;
        }

        /// <summary>
        /// Skips the next n bytes.
        /// </summary>
        /// <param name="count">The number of bytes to ignore.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Skip(int count)
        {
            this.LastErrorCode = this.SkipUnsafe(count);
            this.LastErrorCode.EnsureNoError();
        }

        /// <summary>
        /// Skips the next n bytes.
        /// Does not throw, returns <see cref="OrigDecoderErrorCode"/> instead!
        /// </summary>
        /// <param name="count">The number of bytes to ignore.</param>
        /// <returns>The <see cref="OrigDecoderErrorCode"/></returns>
        public OrigDecoderErrorCode SkipUnsafe(int count)
        {
            // Unread the overshot bytes, if any.
            if (this.Bytes.UnreadableBytes != 0)
            {
                if (this.Bits.UnreadBits >= 8)
                {
                    this.UnreadByteStuffedByte();
                }

                this.Bytes.UnreadableBytes = 0;
            }

            while (true)
            {
                int m = this.Bytes.J - this.Bytes.I;
                if (m > count)
                {
                    m = count;
                }

                this.Bytes.I += m;
                count -= m;
                if (count == 0)
                {
                    break;
                }

                this.LastErrorCode = this.Bytes.FillUnsafe(this.InputStream);
                if (this.LastErrorCode != OrigDecoderErrorCode.NoError)
                {
                    return this.LastErrorCode;
                }
            }

            return this.LastErrorCode = OrigDecoderErrorCode.NoError;
        }

        /// <summary>
        /// Reads exactly length bytes into data. It does not care about byte stuffing.
        /// </summary>
        /// <param name="data">The data to write to.</param>
        /// <param name="offset">The offset in the source buffer</param>
        /// <param name="length">The number of bytes to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadFull(byte[] data, int offset, int length)
        {
            this.LastErrorCode = this.ReadFullUnsafe(data, offset, length);
            this.LastErrorCode.EnsureNoError();
        }

        /// <summary>
        /// Undoes the most recent ReadByteStuffedByte call,
        /// giving a byte of data back from bits to bytes. The Huffman look-up table
        /// requires at least 8 bits for look-up, which means that Huffman decoding can
        /// sometimes overshoot and read one or two too many bytes. Two-byte overshoot
        /// can happen when expecting to read a 0xff 0x00 byte-stuffed byte.
        /// </summary>
        public void UnreadByteStuffedByte()
        {
            this.Bytes.I -= this.Bytes.UnreadableBytes;
            this.Bytes.UnreadableBytes = 0;
            if (this.Bits.UnreadBits >= 8)
            {
                this.Bits.Accumulator >>= 8;
                this.Bits.UnreadBits -= 8;
                this.Bits.Mask >>= 8;
            }
        }

        /// <summary>
        /// Receive extend
        /// </summary>
        /// <param name="t">Byte</param>
        /// <param name="x">Read bits value</param>
        /// <returns>The <see cref="OrigDecoderErrorCode"/></returns>
        public OrigDecoderErrorCode ReceiveExtendUnsafe(int t, out int x)
        {
            this.LastErrorCode = this.Bits.ReceiveExtendUnsafe(t, ref this, out x);
            return this.LastErrorCode;
        }
    }
}