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
    /// Gets a value indicating whether <see cref="SegmentIntegrityHandling.IgnoreImageData"/> can discard a block after an
    /// <see cref="ImageFormatException"/>. A codec returns true only when it clears its sample buffer before each block and
    /// never writes outside that buffer, so a failed block cannot leave data that a later block uses.
    /// </summary>
    protected virtual bool CanDiscardFailedBlock => false;

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
        => this.TryDecompress(stream, offset, count, height, buffer, pixels, SegmentIntegrityHandling.Strict, cancellationToken);

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
    /// <param name="integrityHandling">The policy for recoverable image data errors.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    /// <returns>Whether decompression succeeded. A false result discards the entire block.</returns>
    public bool TryDecompress<TPixel>(
        BufferedReadStream stream,
        ulong offset,
        ulong count,
        int height,
        Span<byte> buffer,
        Buffer2DRegion<TPixel> pixels,
        SegmentIntegrityHandling integrityHandling,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DebugGuard.MustBeLessThanOrEqualTo(offset, (ulong)long.MaxValue, nameof(offset));
        DebugGuard.MustBeLessThanOrEqualTo(count, (ulong)int.MaxValue, nameof(count));

        stream.Seek((long)offset, SeekOrigin.Begin);
        try
        {
            this.Decompress(stream, (int)count, height, buffer, pixels, cancellationToken);
        }
        catch (ImageFormatException) when (integrityHandling == SegmentIntegrityHandling.IgnoreImageData && this.CanDiscardFailedBlock)
        {
            // Recover only inside a valid file range. A read past the declared block remains fatal.
            if (count > int.MaxValue || !stream.IsReadRangeValid((long)offset, count) || stream.Position > (long)(offset + count))
            {
                throw;
            }

            // Skip pixel conversion so the failed block keeps its initialized pixels.
            // The next block seeks to its own offset, and the codec clears the sample buffer before decoding.
            return false;
        }

        if ((long)offset + (long)count < stream.Position)
        {
            TiffThrowHelper.ThrowImageFormatException("Out of range when reading a strip.");
        }

        return true;
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
