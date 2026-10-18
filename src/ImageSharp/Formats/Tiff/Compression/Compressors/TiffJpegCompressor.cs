// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;

/// <summary>
/// Passes source pixels to JPEG for color conversion, component encoding, and compression.
/// </summary>
internal class TiffJpegCompressor : TiffBaseCompressor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TiffJpegCompressor"/> class.
    /// </summary>
    /// <param name="output">The output stream.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="width">The strip width.</param>
    /// <param name="bitsPerPixel">The encoded pixel depth.</param>
    /// <param name="predictor">The sample predictor.</param>
    public TiffJpegCompressor(Stream output, MemoryAllocator memoryAllocator, int width, int bitsPerPixel, TiffPredictor predictor = TiffPredictor.None)
        : base(output, memoryAllocator, width, bitsPerPixel, predictor)
    {
    }

    /// <inheritdoc/>
    public override TiffCompression Method => TiffCompression.Jpeg;

    /// <inheritdoc/>
    public override void Initialize(int rowsPerStrip)
    {
    }

    /// <inheritdoc/>
    public override void CompressStrip(Span<byte> rows, int height)
    {
        int width = rows.Length / (3 * height);
        using Image<Rgb24> image = Image.LoadPixelData<Rgb24>(rows, width, height);
        this.CompressStrip(image.Configuration, image.Frames.RootFrame.PixelBuffer.GetRegion(), null, JpegColorType.Rgb, false, default);
    }

    /// <summary>
    /// Encodes source pixels as a JPEG strip without an intermediate byte image.
    /// </summary>
    /// <remarks>
    /// TIFF selects colorType from its resolved photometric interpretation and supplies the compatible frame profile.
    /// JPEG applies the color conversion itself. TIFF writes that same profile in its IccProfile directory entry.
    /// </remarks>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="configuration">The source configuration.</param>
    /// <param name="pixels">The source region, which remains unchanged.</param>
    /// <param name="profile">The frame's retained ICC profile, or null.</param>
    /// <param name="colorType">RGB, grayscale, or CMYK, as selected by TIFF's resolved sample layout.</param>
    /// <param name="clearTransparentPixels">Whether JPEG sets RGB to zero for fully transparent input pixels in its reusable row buffer.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void CompressStrip<TPixel>(
        Configuration configuration,
        Buffer2DRegion<TPixel> pixels,
        IccProfile? profile,
        JpegColorType colorType,
        bool clearTransparentPixels,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // TIFF supplies the JPEG color type explicitly. Empty JPEG metadata cannot change that choice.
        // SkipMetadata omits optional JPEG metadata. The separately supplied ICC profile still controls conversion and embedding.
        JpegEncoderCore encoder = new(new JpegEncoder { ColorType = colorType, SkipMetadata = true });
        encoder.Encode(configuration, pixels, new ImageMetadata(), profile, clearTransparentPixels, this.Output, cancellationToken);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
