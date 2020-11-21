// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression
{
    /// <summary>
    /// Class to handle cases where TIFF image data is compressed using CCITT T4 compression.
    /// </summary>
    internal class T4TiffCompression : TiffBaseCompression
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T4TiffCompression" /> class.
        /// </summary>
        /// <param name="allocator">The memory allocator.</param>
        public T4TiffCompression(MemoryAllocator allocator)
            : base(allocator)
        {
        }

        /// <inheritdoc/>
        public override void Decompress(Stream stream, int byteCount, Span<byte> buffer)
        {
            // TODO: handle case when white is not zero.
            bool isWhiteZero = true;
            int whiteValue = isWhiteZero ? 0 : 1;
            int blackValue = isWhiteZero ? 1 : 0;

            var bitReader = new T4BitReader(stream, byteCount);

            uint bitsWritten = 0;
            uint pixels = 0;
            while (bitReader.HasMoreData)
            {
                bitReader.ReadNextRun();

                if (bitReader.IsEndOfScanLine)
                {
                    // Write padding bytes, if necessary.
                    uint pad = 8 - (bitsWritten % 8);
                    if (pad != 8)
                    {
                        this.WriteBits(buffer, (int)bitsWritten, pad, 0);
                        bitsWritten += pad;
                    }

                    continue;
                }

                if (bitReader.IsWhiteRun)
                {
                    this.WriteBits(buffer, (int)bitsWritten, bitReader.RunLength, whiteValue);
                    bitsWritten += bitReader.RunLength;
                    pixels += bitReader.RunLength;
                }
                else
                {
                    this.WriteBits(buffer, (int)bitsWritten, bitReader.RunLength, blackValue);
                    bitsWritten += bitReader.RunLength;
                    pixels += bitReader.RunLength;
                }
            }

            int foo = 0;
        }

        private void WriteBits(Span<byte> buffer, int pos, uint count, int value)
        {
            int bitPos = pos % 8;
            int bufferPos = pos / 8;
            int startIdx = bufferPos + bitPos;
            int endIdx = (int)(startIdx + count);

            for (int i = startIdx; i < endIdx; i++)
            {
                this.WriteBit(buffer, bufferPos, bitPos, value);

                bitPos++;
                if (bitPos >= 8)
                {
                    bitPos = 0;
                    bufferPos++;
                }
            }
        }

        private void WriteBit(Span<byte> buffer, int bufferPos, int bitPos, int value)
        {
            buffer[bufferPos] |= (byte)(value << (7 - bitPos));
        }
    }
}
