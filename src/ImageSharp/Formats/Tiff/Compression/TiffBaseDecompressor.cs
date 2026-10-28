// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

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
    /// <param name="width">The encoded strip or tile width.</param>
    /// <param name="bitsPerPixel">The total number of bits in each TIFF source pixel.</param>
    /// <param name="predictor">The TIFF predictor.</param>
    protected TiffBaseDecompressor(MemoryAllocator memoryAllocator, int width, int bitsPerPixel, TiffPredictor predictor = TiffPredictor.None)
        : base(memoryAllocator, width, bitsPerPixel, predictor)
    {
    }

    /// <summary>
    /// Gets a value indicating whether this codec writes final pixels instead of raw TIFF samples.
    /// </summary>
    public virtual bool DecodesPixels => false;

    /// <summary>
    /// Decompresses a TIFF strip or tile into raw samples or final pixels.
    /// </summary>
    /// <typeparam name="TPixel">The requested image pixel format.</typeparam>
    /// <param name="stream">The <see cref="Stream" /> to read image data from.</param>
    /// <param name="offset">The data offset within the stream.</param>
    /// <param name="count">The number of bytes to read from the input stream.</param>
    /// <param name="height">The encoded strip or tile height.</param>
    /// <param name="buffer">The raw sample destination when <see cref="DecodesPixels"/> is false; otherwise, an empty span.</param>
    /// <param name="pixels">The final pixel destination when <see cref="DecodesPixels"/> is true; otherwise, unused.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    public void Decompress<TPixel>(
        BufferedReadStream stream, ulong offset, ulong count, int height, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DebugGuard.MustBeLessThanOrEqualTo(offset, (ulong)long.MaxValue, nameof(offset));
        DebugGuard.MustBeLessThanOrEqualTo(count, (ulong)int.MaxValue, nameof(count));

        stream.Seek((long)offset, SeekOrigin.Begin);
        this.Decompress(stream, (int)count, height, buffer, pixels, cancellationToken);

        if ((long)offset + (long)count < stream.Position)
        {
            TiffThrowHelper.ThrowImageFormatException("Out of range when reading a strip.");
        }
    }

    /// <summary>
    /// Decompresses a TIFF strip or tile into raw samples or final pixels.
    /// </summary>
    /// <typeparam name="TPixel">The requested image pixel format.</typeparam>
    /// <param name="stream">The <see cref="Stream" /> to read image data from.</param>
    /// <param name="byteCount">The number of bytes to read from the input stream.</param>
    /// <param name="height">The encoded strip or tile height.</param>
    /// <param name="buffer">The raw sample destination when <see cref="DecodesPixels"/> is false; otherwise, an empty span.</param>
    /// <param name="pixels">The final pixel destination when <see cref="DecodesPixels"/> is true; otherwise, unused.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    protected abstract void Decompress<TPixel>(
        BufferedReadStream stream, int byteCount, int height, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Copies nested-decoder output without changing its pixel format or alpha representation.
    /// </summary>
    /// <typeparam name="TPixel">The requested image pixel format.</typeparam>
    /// <param name="pixels">The decoded strip or tile.</param>
    /// <param name="height">The encoded height, including tile edge padding.</param>
    /// <param name="destination">The visible destination region.</param>
    protected void CopyPixels<TPixel>(Buffer2D<TPixel> pixels, int height, Buffer2DRegion<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The nested header is an external boundary. Its dimensions must match the IFD before copying into the TIFF region.
        if (pixels.Width != this.Width || pixels.Height != height)
        {
            TiffThrowHelper.ThrowImageFormatException("Nested image dimensions do not match the TIFF strip or tile.");
        }

        // Edge tiles include padding outside the TIFF image. Copy only the visible TPixel values; no color conversion follows.
        for (int y = 0; y < destination.Height; y++)
        {
            pixels.DangerousGetRowSpan(y)[..destination.Width].CopyTo(destination.DangerousGetRowSpan(y));
        }
    }
}
