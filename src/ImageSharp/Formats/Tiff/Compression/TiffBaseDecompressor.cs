// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
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
    private readonly TiffColorType colorType;

    private readonly bool isBigEndian;

    private readonly bool isTiled;

    private readonly int tileWidth;

    private readonly int tileHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffBaseDecompressor"/> class for a compression with no predictor.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="width">The encoded strip or tile width.</param>
    /// <param name="bitsPerPixel">The total number of bits in each TIFF source pixel.</param>
    /// <param name="integrityHandling">The segment integrity mode of the decode.</param>
    protected TiffBaseDecompressor(MemoryAllocator memoryAllocator, int width, int bitsPerPixel, SegmentIntegrityHandling integrityHandling)
        : base(memoryAllocator, width, bitsPerPixel)
        => this.IntegrityHandling = integrityHandling;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffBaseDecompressor"/> class for a compression that can use a predictor.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="width">The encoded strip or tile width.</param>
    /// <param name="bitsPerPixel">The total number of bits in each TIFF source pixel.</param>
    /// <param name="integrityHandling">The segment integrity mode of the decode.</param>
    /// <param name="predictor">The TIFF predictor.</param>
    /// <param name="colorType">The color type of the samples. It gives the sample width of the predictor.</param>
    /// <param name="isBigEndian">Whether the samples are big-endian.</param>
    /// <param name="isTiled">Whether the image uses tiles.</param>
    /// <param name="tileWidth">The pixel count of a tile row.</param>
    /// <param name="tileHeight">The row count of a tile.</param>
    protected TiffBaseDecompressor(
        MemoryAllocator memoryAllocator,
        int width,
        int bitsPerPixel,
        SegmentIntegrityHandling integrityHandling,
        TiffPredictor predictor,
        TiffColorType colorType,
        bool isBigEndian,
        bool isTiled,
        int tileWidth,
        int tileHeight)
        : base(memoryAllocator, width, bitsPerPixel, predictor)
    {
        this.IntegrityHandling = integrityHandling;
        this.colorType = colorType;
        this.isBigEndian = isBigEndian;
        this.isTiled = isTiled;
        this.tileWidth = tileWidth;
        this.tileHeight = tileHeight;
    }

    /// <summary>
    /// Gets the segment integrity mode of the decode. This mode controls what the decoder does with damaged data.
    /// </summary>
    protected SegmentIntegrityHandling IntegrityHandling { get; }

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
    /// <remarks>
    /// In the <see cref="SegmentIntegrityHandling.Strict"/> and <see cref="SegmentIntegrityHandling.IgnoreAncillary"/> modes, the decompressor throws an
    /// exception for damaged data. In the <see cref="SegmentIntegrityHandling.IgnoreImageData"/> mode, the decompressor keeps the data that it decoded.
    /// </remarks>
    public void Decompress<TPixel>(
        BufferedReadStream stream, ulong offset, ulong count, int height, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DebugGuard.MustBeLessThanOrEqualTo(offset, (ulong)long.MaxValue, nameof(offset));

        // The byte count can be more than the bytes that remain in the stream. This decoder gives the decompressors only the bytes that the stream has. Thus
        // they decode the data that is present. The check after the decode finds the missing samples. The clamp also keeps the count in the int range.
        long start = (long)offset;
        int byteCount = (int)Math.Min(count, (ulong)Math.Clamp(stream.Length - start, 0, int.MaxValue));

        stream.Seek(start, SeekOrigin.Begin);
        int decoded = this.Decompress(stream, byteCount, height, buffer, pixels, cancellationToken);

        // This is the only damage report for the samples of each compression. The strip or tile is damaged if the data ends before it fills the buffer. The
        // strip or tile is also damaged if a decompressor reads after the end of the strip or tile. In the IgnoreImageData mode, the strip or tile keeps the
        // decoded samples. The decoder uses the buffer again for each strip or tile. Thus this method clears the samples after the decoded samples. The CCITT
        // row decoders report damage in a row where they find it. The nested decoders report damage through their own decoders. These decompressors write each
        // byte of the buffer.
        if (decoded < buffer.Length || stream.Position > start + byteCount)
        {
            this.IntegrityHandling.ThrowOrIgnoreImageDataError("The TIFF strip or tile data is damaged or ends before the strip or tile is complete.");
            buffer[decoded..].Clear();
        }

        // The predictor undo runs after the decode of each compression.
        if (this.Predictor == TiffPredictor.Horizontal)
        {
            if (this.isTiled)
            {
                // If the image is tiled, undo the horizontal predictor for each tile row.
                HorizontalPredictor.UndoTile(buffer, this.tileWidth, this.tileHeight, this.colorType, this.isBigEndian);
            }
            else
            {
                HorizontalPredictor.Undo(buffer, this.Width, this.colorType, this.isBigEndian);
            }
        }
    }

    /// <summary>
    /// Decompresses a TIFF strip or tile into raw samples or final pixels.
    /// </summary>
    /// <typeparam name="TPixel">The requested image pixel format.</typeparam>
    /// <param name="stream">The <see cref="Stream" /> to read image data from.</param>
    /// <param name="byteCount">The number of bytes to read from the input stream. The stream has these bytes.</param>
    /// <param name="height">The encoded strip or tile height.</param>
    /// <param name="buffer">The raw sample destination when <see cref="DecodesPixels"/> is false; otherwise, an empty span.</param>
    /// <param name="pixels">The final pixel destination when <see cref="DecodesPixels"/> is true; otherwise, unused.</param>
    /// <param name="cancellationToken">The token to monitor cancellation.</param>
    /// <returns>
    /// The count of the bytes at the start of <paramref name="buffer"/> that hold decoded samples. A count less than the buffer length shows damaged data, or
    /// data that ends early. The caller then clears the other bytes.
    /// </returns>
    protected abstract int Decompress<TPixel>(
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
        // The nested header is an external boundary. Its dimensions must agree with the IFD before the method copies pixels into the TIFF region. Other
        // dimensions are damaged data. In the IgnoreImageData mode, the region keeps its cleared pixels.
        if (pixels.Width != this.Width || pixels.Height != height)
        {
            this.IntegrityHandling.ThrowOrIgnoreImageDataError("Nested image dimensions do not match the TIFF strip or tile.");
            return;
        }

        // Edge tiles include padding outside the TIFF image. Copy only the visible TPixel values; no color conversion follows.
        for (int y = 0; y < destination.Height; y++)
        {
            pixels.DangerousGetRowSpan(y)[..destination.Width].CopyTo(destination.DangerousGetRowSpan(y));
        }
    }
}
