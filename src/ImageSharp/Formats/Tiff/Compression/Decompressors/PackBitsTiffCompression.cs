// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Class to handle cases where TIFF image data is compressed using PackBits compression.
    /// </summary>
    internal sealed class PackBitsTiffCompression : TiffBaseDecompressor
    {
        private IMemoryOwner<byte> compressedDataMemory;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackBitsTiffCompression" /> class.
        /// </summary>
        /// <param name="memoryAllocator">The memoryAllocator to use for buffer allocations.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="bitsPerPixel">The number of bits per pixel.</param>
        public PackBitsTiffCompression(MemoryAllocator memoryAllocator, int width, int bitsPerPixel)
                : base(memoryAllocator, width, bitsPerPixel)
        {
        }

        /// <inheritdoc/>
        protected override void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer)
        {
            if (this.compressedDataMemory == null)
            {
                this.compressedDataMemory = this.Allocator.Allocate<byte>(byteCount);
            }
            else if (this.compressedDataMemory.Length() < byteCount)
            {
                this.compressedDataMemory.Dispose();
                this.compressedDataMemory = this.Allocator.Allocate<byte>(byteCount);
            }

            Span<byte> compressedData = this.compressedDataMemory.GetSpan();

            stream.Read(compressedData, 0, byteCount);
            int compressedOffset = 0;
            int decompressedOffset = 0;

            while (compressedOffset < byteCount)
            {
                byte headerByte = compressedData[compressedOffset];

                if (headerByte <= 127)
                {
                    int literalOffset = compressedOffset + 1;
                    int literalLength = compressedData[compressedOffset] + 1;

                    if ((literalOffset + literalLength) > compressedData.Length)
                    {
                        TiffThrowHelper.ThrowImageFormatException("Tiff packbits compression error: not enough data.");
                    }

                    compressedData.Slice(literalOffset, literalLength).CopyTo(buffer.Slice(decompressedOffset));

                    compressedOffset += literalLength + 1;
                    decompressedOffset += literalLength;
                }
                else if (headerByte == 0x80)
                {
                    compressedOffset += 1;
                }
                else
                {
                    byte repeatData = compressedData[compressedOffset + 1];
                    int repeatLength = 257 - headerByte;

                    buffer.Slice(decompressedOffset, repeatLength).Fill(repeatData);

                    compressedOffset += 2;
                    decompressedOffset += repeatLength;
                }
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing) => this.compressedDataMemory?.Dispose();
    }
}
