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
    /// The segment integrity mode of the decode. It selects what happens with damaged data.
    /// </summary>
    private readonly SegmentIntegrityHandling integrityHandling;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackBitsTiffCompression" /> class.
    /// </summary>
    /// <param name="memoryAllocator">The memoryAllocator to use for buffer allocations.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="bitsPerPixel">The number of bits per pixel.</param>
    /// <param name="integrityHandling">The segment integrity mode of the decode.</param>
    public PackBitsTiffCompression(MemoryAllocator memoryAllocator, int width, int bitsPerPixel, SegmentIntegrityHandling integrityHandling)
            : base(memoryAllocator, width, bitsPerPixel)
        => this.integrityHandling = integrityHandling;

    /// <inheritdoc/>
    protected override void Decompress<TPixel>(
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

        // The memory is reused, so only the bytes that the stream gives are data. The stream can end before the
        // byte count.
        int length = stream.ReadAtLeast(this.compressedDataMemory.GetSpan()[..byteCount], byteCount, false);
        ReadOnlySpan<byte> compressedData = this.compressedDataMemory.GetSpan()[..length];
        int compressedOffset = 0;
        int decompressedOffset = 0;
        bool damaged = length < byteCount;

        while (compressedOffset < length)
        {
            byte headerByte = compressedData[compressedOffset];

            if (headerByte <= 127)
            {
                // A literal run of n + 1 bytes. The run must be in the data and fit the strip or tile.
                int literalOffset = compressedOffset + 1;
                int literalLength = headerByte + 1;

                if (literalOffset + literalLength > length || literalLength > buffer.Length - decompressedOffset)
                {
                    damaged = true;
                    break;
                }

                compressedData.Slice(literalOffset, literalLength).CopyTo(buffer[decompressedOffset..]);

                compressedOffset += literalLength + 1;
                decompressedOffset += literalLength;
            }
            else if (headerByte == 0x80)
            {
                compressedOffset += 1;
            }
            else
            {
                // A repeat run of 257 - n copies of the next byte. The byte must be in the data, and the run must fit
                // the strip or tile.
                int repeatLength = 257 - headerByte;
                if (compressedOffset + 1 >= length || repeatLength > buffer.Length - decompressedOffset)
                {
                    damaged = true;
                    break;
                }

                buffer.Slice(decompressedOffset, repeatLength).Fill(compressedData[compressedOffset + 1]);

                compressedOffset += 2;
                decompressedOffset += repeatLength;
            }
        }

        // The buffer holds the samples of this strip or tile only, so data that ends before it fills the buffer is
        // damaged. In the IgnoreImageData mode, the strip or tile keeps the decoded bytes. The buffer is reused, so the
        // bytes after them are cleared. In the other modes, damaged data throws.
        if (damaged || decompressedOffset < buffer.Length)
        {
            this.integrityHandling.ThrowOrIgnoreImageDataError(
                "The TIFF PackBits data is damaged or ends before the strip or tile is complete.");
            buffer[decompressedOffset..].Clear();
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => this.compressedDataMemory?.Dispose();
}
