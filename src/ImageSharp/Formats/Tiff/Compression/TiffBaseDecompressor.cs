// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression;

/// <summary>
/// The base tiff decompressor class.
/// </summary>
internal abstract class TiffBaseDecompressor : TiffBaseCompression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TiffBaseDecompressor"/> class.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    /// <param name="predictor">The predictor.</param>
    protected TiffBaseDecompressor(MemoryAllocator memoryAllocator, int width, int bitsPerPixel, TiffPredictor predictor = TiffPredictor.None)
     : base(memoryAllocator, width, bitsPerPixel, predictor)
    {
    }

    /// <summary>
    /// Decompresses image data into the supplied buffer.
    /// </summary>
    /// <param name="stream">The <see cref="Stream" /> to read image data from.</param>
    /// <param name="offset">The data offset within the stream.</param>
    /// <param name="count">The number of bytes to read from the input stream.</param>
    /// <param name="stripHeight">The height of the strip.</param>
    /// <param name="buffer">The output buffer for uncompressed data.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    public void Decompress(BufferedReadStream stream, ulong offset, ulong count, int stripHeight, Span<byte> buffer, CancellationToken cancellationToken)
    {
        DebugGuard.MustBeLessThanOrEqualTo(offset, (ulong)long.MaxValue, nameof(offset));
        DebugGuard.MustBeLessThanOrEqualTo(count, (ulong)int.MaxValue, nameof(count));

        stream.Seek((long)offset, SeekOrigin.Begin);
        this.Decompress(stream, (int)count, stripHeight, buffer, cancellationToken);

        if ((long)offset + (long)count < stream.Position)
        {
            TiffThrowHelper.ThrowImageFormatException("Out of range when reading a strip.");
        }
    }

    /// <summary>
    /// Decompresses image data into the supplied buffer.
    /// </summary>
    /// <param name="stream">The <see cref="Stream" /> to read image data from.</param>
    /// <param name="byteCount">The number of bytes to read from the input stream.</param>
    /// <param name="stripHeight">The height of the strip.</param>
    /// <param name="buffer">The output buffer for uncompressed data.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    protected abstract void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, CancellationToken cancellationToken);
}
