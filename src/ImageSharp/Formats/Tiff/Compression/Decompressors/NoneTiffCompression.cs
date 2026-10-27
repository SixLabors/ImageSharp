// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Class to handle cases where TIFF image data is not compressed.
/// </summary>
internal sealed class NoneTiffCompression : TiffBaseDecompressor
{
    private readonly SegmentIntegrityHandling integrityHandling;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoneTiffCompression" /> class.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    /// <param name="integrityHandling">The policy for incomplete image data.</param>
    public NoneTiffCompression(MemoryAllocator memoryAllocator, int width, int bitsPerPixel, SegmentIntegrityHandling integrityHandling)
        : base(memoryAllocator, width, bitsPerPixel)
    {
        this.integrityHandling = integrityHandling;
    }

    /// <inheritdoc/>
    protected override void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, CancellationToken cancellationToken)
    {
        // The caller supplies only the rows needed for this read. Respect both the declared byte count and end of file.
        int bytesRead = stream.Read(buffer, 0, Math.Min(buffer.Length, byteCount));
        if (bytesRead < buffer.Length)
        {
            if (this.integrityHandling != SegmentIntegrityHandling.IgnoreImageData)
            {
                throw new InvalidImageContentException("TIFF strip or tile contains incomplete image data.");
            }

            // The buffer is reused. Missing samples must not retain bytes from a previous strip, row, or pooled allocation.
            buffer[bytesRead..].Clear();
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
