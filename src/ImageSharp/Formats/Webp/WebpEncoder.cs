// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Image encoder for writing an image to a stream in the Webp format.
/// </summary>
public sealed class WebpEncoder : AnimatedImageEncoder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebpEncoder"/> class.
    /// </summary>
    public WebpEncoder()

        // Match the default behavior of the native reference encoder.
        => this.TransparentColorMode = TransparentColorMode.Clear;

    /// <summary>
    /// Gets the webp file format used. Either lossless or lossy.
    /// Defaults to lossy.
    /// </summary>
    public WebpFileFormatType? FileFormat { get; init; }

    /// <summary>
    /// Gets the compression quality. Between 0 and 100.
    /// For lossy, 0 gives the smallest size and 100 the largest. For lossless,
    /// this parameter is the amount of effort put into the compression: 0 is the fastest but gives larger
    /// files compared to the slowest, but best, 100.
    /// Defaults to 75.
    /// </summary>
    public int Quality { get; init; } = 75;

    /// <summary>
    /// Gets the encoding method to use. Its a quality/speed trade-off (0=fast, 6=slower-better).
    /// Defaults to 4.
    /// </summary>
    public WebpEncodingMethod Method { get; init; } = WebpEncodingMethod.Default;

    /// <summary>
    /// Gets a value indicating whether the alpha plane should be compressed with Webp lossless format.
    /// Defaults to true.
    /// </summary>
    public bool UseAlphaCompression { get; init; } = true;

    /// <summary>
    /// Gets the number of entropy-analysis passes (in [1..10]).
    /// Defaults to 1.
    /// </summary>
    public int EntropyPasses { get; init; } = 1;

    /// <summary>
    /// Gets the amplitude of the spatial noise shaping. Spatial noise shaping (or sns for short) refers to a general collection of built-in algorithms
    /// used to decide which area of the picture should use relatively less bits, and where else to better transfer these bits.
    /// The possible range goes from 0 (algorithm is off) to 100 (the maximal effect).
    /// Defaults to 50.
    /// </summary>
    public int SpatialNoiseShaping { get; init; } = 50;

    /// <summary>
    /// Gets the strength of the deblocking filter, between 0 (no filtering) and 100 (maximum filtering).
    /// A value of 0 will turn off any filtering. Higher value will increase the strength of the filtering process applied after decoding the picture.
    /// The higher the value the smoother the picture will appear.
    /// Typical values are usually in the range of 20 to 50.
    /// Defaults to 60.
    /// </summary>
    public int FilterStrength { get; init; } = 60;

    /// <summary>
    /// Gets a value indicating whether near lossless mode should be used.
    /// This option adjusts pixel values to help compressibility, but has minimal impact on the visual quality.
    /// </summary>
    public bool NearLossless { get; init; }

    /// <summary>
    /// Gets the quality of near-lossless image preprocessing. The range is 0 (maximum preprocessing) to 100 (no preprocessing, the default).
    /// The typical value is around 60. Note that lossy with -q 100 can at times yield better results.
    /// </summary>
    public int NearLosslessQuality { get; init; } = 100;

    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        WebpEncoderCore encoder = new(this, image.Configuration);
        encoder.Encode(image, stream, cancellationToken);
    }
}
