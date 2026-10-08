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
    public NoneTiffCompression(MemoryAllocator memoryAllocator, int width, int bitsPerPixel)
        : base(memoryAllocator, width, bitsPerPixel)
    {
    }

    /// <inheritdoc/>
    protected override void Decompress<TPixel>(
        BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
        => _ = stream.Read(buffer, 0, Math.Min(buffer.Length, byteCount));

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
