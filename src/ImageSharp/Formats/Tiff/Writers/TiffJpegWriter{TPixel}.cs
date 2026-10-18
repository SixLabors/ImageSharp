// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Writes a TIFF JPEG strip from the original pixel region. JPEG owns ICC conversion and final sample packing.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal sealed class TiffJpegWriter<TPixel> : TiffBaseColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly IccProfile? profile;
    private readonly JpegColorType colorType;
    private readonly CancellationToken cancellationToken;
    private readonly bool clearTransparentPixels;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffJpegWriter{TPixel}"/> class.
    /// </summary>
    /// <param name="frame">The source frame.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="configuration">The source configuration.</param>
    /// <param name="collector">The directory entries.</param>
    /// <param name="profile">The frame's compatible retained ICC profile, or null.</param>
    /// <param name="colorType">The JPEG color type selected from TIFF's resolved photometric interpretation.</param>
    /// <param name="clearTransparentPixels">Whether JPEG sets RGB to zero for fully transparent input pixels in its reusable row buffer.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public TiffJpegWriter(
        ImageFrame<TPixel> frame,
        Size encodingSize,
        MemoryAllocator allocator,
        Configuration configuration,
        TiffEncoderEntriesCollector collector,
        IccProfile? profile,
        JpegColorType colorType,
        bool clearTransparentPixels,
        CancellationToken cancellationToken)
        : base(frame, encodingSize, allocator, configuration, collector)
    {
        this.profile = profile;
        this.colorType = colorType;
        this.clearTransparentPixels = clearTransparentPixels;
        this.cancellationToken = cancellationToken;
    }

    /// <inheritdoc />
    public override int BitsPerPixel => this.colorType switch
    {
        JpegColorType.Luminance => 8,
        JpegColorType.Cmyk => 32,
        _ => 24
    };

    /// <inheritdoc />
    protected override void EncodeStrip(int y, int height, TiffBaseCompressor compressor)
    {
        // TIFF writes one full-height JPEG strip. Pass TPixel directly so JPEG applies ICC before it reduces samples to bytes.
        // JPEG owns the color conversion. TIFF does not convert these pixels a second time.
        Buffer2DRegion<TPixel> region = this.Image.PixelBuffer.GetRegion(0, y, this.Width, height);
        TiffJpegCompressor jpegCompressor = (TiffJpegCompressor)compressor;
        jpegCompressor.CompressStrip(
            this.Configuration,
            region,
            this.profile,
            this.colorType,
            this.clearTransparentPixels,
            this.cancellationToken);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }
}
