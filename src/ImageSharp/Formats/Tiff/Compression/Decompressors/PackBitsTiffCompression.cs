// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

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
    /// <param name="integrityHandling">The segment integrity mode of the decode.</param>
    public PackBitsTiffCompression(MemoryAllocator memoryAllocator, int width, int bitsPerPixel, SegmentIntegrityHandling integrityHandling)
            : base(memoryAllocator, width, bitsPerPixel, integrityHandling)
    {
    }

    /// <inheritdoc/>
    protected override int Decompress<TPixel>(
        BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
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

        // The stream has the bytes of the byte count. The decoder uses the same memory for each strip or tile. Thus only the first byteCount bytes are data.
        Span<byte> compressedData = this.compressedDataMemory.GetSpan()[..byteCount];
        stream.ReadExactly(compressedData);
        int compressedOffset = 0;
        int decompressedOffset = 0;

        // A run that is longer than the rest of the strip or tile writes the part that fits. The decode stops when the buffer is full. If the data of a run
        // ends before all its bytes, the decode stops. Only missing output is damage. The caller reports it.
        while (compressedOffset < byteCount && decompressedOffset < buffer.Length)
        {
            byte headerByte = compressedData[compressedOffset];

            if (headerByte <= 127)
            {
                // A literal run of n + 1 bytes.
                int literalOffset = compressedOffset + 1;
                int literalLength = Math.Min(headerByte + 1, buffer.Length - decompressedOffset);
                if (literalOffset + literalLength > byteCount)
                {
                    break;
                }

                compressedData.Slice(literalOffset, literalLength).CopyTo(buffer[decompressedOffset..]);

                compressedOffset = literalOffset + headerByte + 1;
                decompressedOffset += literalLength;
            }
            else if (headerByte == 0x80)
            {
                compressedOffset += 1;
            }
            else
            {
                // A repeat run of 257 - n copies of the next byte.
                int repeatLength = Math.Min(257 - headerByte, buffer.Length - decompressedOffset);
                if (compressedOffset + 1 >= byteCount)
                {
                    break;
                }

                buffer.Slice(decompressedOffset, repeatLength).Fill(compressedData[compressedOffset + 1]);

                compressedOffset += 2;
                decompressedOffset += repeatLength;
            }
        }

        return decompressedOffset;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => this.compressedDataMemory?.Dispose();
}
