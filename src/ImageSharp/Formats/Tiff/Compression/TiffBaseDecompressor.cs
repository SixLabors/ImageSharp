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
    /// Gets a value indicating whether <see cref="SegmentIntegrityHandling.IgnoreImageData"/> can discard a block after an
    /// <see cref="ImageFormatException"/>. A codec returns true only when it clears its sample buffer before each block and
    /// never writes outside that buffer, so a failed block cannot leave data that a later block uses.
    /// </summary>
    protected virtual bool CanDiscardFailedBlock => false;

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
        => this.TryDecompress(stream, offset, count, stripHeight, buffer, SegmentIntegrityHandling.Strict, cancellationToken);

    /// <summary>
    /// Decompresses image data into the supplied buffer.
    /// </summary>
    /// <param name="stream">The <see cref="Stream" /> to read image data from.</param>
    /// <param name="offset">The data offset within the stream.</param>
    /// <param name="count">The number of bytes to read from the input stream.</param>
    /// <param name="stripHeight">The height of the strip.</param>
    /// <param name="buffer">The output buffer for uncompressed data.</param>
    /// <param name="integrityHandling">The policy for recoverable image data errors.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    /// <returns>Whether decompression succeeded. A false result discards the entire block.</returns>
    public bool TryDecompress(BufferedReadStream stream, ulong offset, ulong count, int stripHeight, Span<byte> buffer, SegmentIntegrityHandling integrityHandling, CancellationToken cancellationToken)
    {
        DebugGuard.MustBeLessThanOrEqualTo(offset, (ulong)long.MaxValue, nameof(offset));
        DebugGuard.MustBeLessThanOrEqualTo(count, (ulong)int.MaxValue, nameof(count));

        stream.Seek((long)offset, SeekOrigin.Begin);
        try
        {
            this.Decompress(stream, (int)count, stripHeight, buffer, cancellationToken);
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
    /// Decompresses image data into the supplied buffer.
    /// </summary>
    /// <param name="stream">The <see cref="Stream" /> to read image data from.</param>
    /// <param name="byteCount">The number of bytes to read from the input stream.</param>
    /// <param name="stripHeight">The height of the strip.</param>
    /// <param name="buffer">The output buffer for uncompressed data.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    protected abstract void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, CancellationToken cancellationToken);
}
