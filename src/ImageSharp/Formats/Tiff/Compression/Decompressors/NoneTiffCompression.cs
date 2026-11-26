// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Class to handle cases where TIFF image data is not compressed.
/// </summary>
internal sealed class NoneTiffCompression : TiffBaseDecompressor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NoneTiffCompression" /> class.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    /// <param name="integrityHandling">The policy for incomplete image data.</param>
    public NoneTiffCompression(MemoryAllocator memoryAllocator, int width, int bitsPerPixel, SegmentIntegrityHandling integrityHandling)
        : base(memoryAllocator, width, bitsPerPixel, integrityHandling)
    {
    }

    /// <inheritdoc/>
    protected override int Decompress<TPixel>(
        BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
    {
        // The caller supplies only the rows that this read needs. The stream has the bytes of the byte count. Thus the read fills the buffer if the byte count
        // is not less than the buffer length.
        int count = Math.Min(buffer.Length, byteCount);
        stream.ReadExactly(buffer[..count]);
        return count;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
