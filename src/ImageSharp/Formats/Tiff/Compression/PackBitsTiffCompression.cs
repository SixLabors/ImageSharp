// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;

using SixLabors.ImageSharp.Formats.Experimental.Tiff.Utils;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression
{
    /// <summary>
    /// Class to handle cases where TIFF image data is compressed using PackBits compression.
    /// </summary>
    internal class PackBitsTiffCompression : TiffBaseCompression
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PackBitsTiffCompression" /> class.
        /// </summary>
        /// <param name="memoryAllocator">The memoryAllocator to use for buffer allocations.</param>
        public PackBitsTiffCompression(MemoryAllocator memoryAllocator)
            : base(memoryAllocator)
        {
        }

        /// <inheritdoc/>
        public override void Decompress(Stream stream, int byteCount, Span<byte> buffer)
        {
            using IMemoryOwner<byte> compressedDataMemory = this.Allocator.Allocate<byte>(byteCount);

            Span<byte> compressedData = compressedDataMemory.GetSpan();

            stream.Read(compressedData, 0, byteCount);
            int compressedOffset = 0;
            int decompressedOffset = 0;

            while (compressedOffset < byteCount)
            {
                byte headerByte = compressedData[compressedOffset];

                if (headerByte <= (byte)127)
                {
                    int literalOffset = compressedOffset + 1;
                    int literalLength = compressedData[compressedOffset] + 1;

                    compressedData.Slice(literalOffset, literalLength).CopyTo(buffer.Slice(decompressedOffset));

                    compressedOffset += literalLength + 1;
                    decompressedOffset += literalLength;
                }
                else if (headerByte == (byte)0x80)
                {
                    compressedOffset += 1;
                }
                else
                {
                    byte repeatData = compressedData[compressedOffset + 1];
                    int repeatLength = 257 - headerByte;

                    ArrayCopyRepeat(repeatData, buffer, decompressedOffset, repeatLength);

                    compressedOffset += 2;
                    decompressedOffset += repeatLength;
                }
            }
        }

        private static void ArrayCopyRepeat(byte value, Span<byte> destinationArray, int destinationIndex, int length)
        {
            for (int i = 0; i < length; i++)
            {
                destinationArray[i + destinationIndex] = value;
            }
        }
    }
}
