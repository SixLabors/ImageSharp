// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;
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
    /// The configuration shared by TIFF and its embedded JPEG images.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffJpegCompressor"/> class.
    /// </summary>
    /// <param name="output">The output stream.</param>
    /// <param name="configuration">The configuration used for allocation and JPEG conversion.</param>
    /// <param name="width">The strip width.</param>
    /// <param name="bitsPerPixel">The encoded pixel depth.</param>
    /// <param name="predictor">The sample predictor.</param>
    public TiffJpegCompressor(Stream output, Configuration configuration, int width, int bitsPerPixel, TiffPredictor predictor = TiffPredictor.None)
        : base(output, configuration.MemoryAllocator, width, bitsPerPixel, predictor)
    {
        this.configuration = configuration;
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

        // The explicit overload retains the caller's allocator and avoids registering unrelated codecs through Configuration.Default.
        using Image<Rgb24> image = Image.LoadPixelData<Rgb24>(this.configuration, rows, width, height);

        this.CompressStrip(image.Configuration, image.Frames.RootFrame.PixelBuffer.GetRegion(), null, JpegColorSpace.RGB, false, default);
    }

    /// <summary>
    /// Encodes source pixels as a JPEG strip without an intermediate byte image.
    /// </summary>
    /// <remarks>
    /// TIFF selects the component model from its resolved photometric interpretation and supplies the compatible frame profile.
    /// JPEG applies the color conversion itself. TIFF writes that same profile in its IccProfile directory entry.
    /// </remarks>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="configuration">The source configuration.</param>
    /// <param name="pixels">The source region, which remains unchanged.</param>
    /// <param name="profile">The frame's retained ICC profile, or null.</param>
    /// <param name="colorSpace">
    /// The internal JPEG component model and stored sample convention. Separated TIFF output uses <see cref="JpegColorSpace.TiffCmyk"/>.
    /// This parameter uses <see cref="JpegColorSpace"/> because the public <see cref="JpegColorType.Cmyk"/> selects inverted Adobe CMYK.
    /// The public enum has no TIFF CMYK value. RGB rows use <see cref="JpegColorSpace.RGB"/> for direct RGB components.
    /// The selected frame configuration retains full resolution for each component; this parameter does not select chroma subsampling.
    /// </param>
    /// <param name="clearTransparentPixels">Whether JPEG sets RGB to zero for fully transparent input pixels in its reusable row buffer.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void CompressStrip<TPixel>(
        Configuration configuration,
        Buffer2DRegion<TPixel> pixels,
        IccProfile? profile,
        JpegColorSpace colorSpace,
        bool clearTransparentPixels,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Select by the internal model so TiffCmyk reaches the conventional-ink converter instead of the standalone Adobe converter.
        // SkipMetadata omits optional JPEG metadata. The separately supplied ICC profile still controls conversion and embedding.
        JpegFrameConfig frameConfig = JpegEncoderCore.GetFrameConfig(colorSpace);
        JpegEncoderCore encoder = new(new JpegEncoder { SkipMetadata = true });
        encoder.Encode(configuration, pixels, frameConfig, new ImageMetadata(), profile, clearTransparentPixels, this.Output, cancellationToken);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
