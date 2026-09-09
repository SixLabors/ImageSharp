// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Image encoder for writing image data to a stream in a HEIF container.
/// </summary>
public sealed class HeifEncoder : AnimatedImageEncoder
{
    /// <summary>
    /// Backing field for <see cref="Quality"/>.
    /// </summary>
    private int? quality;

    /// <summary>
    /// Backing field for <see cref="AlphaQuality"/>.
    /// </summary>
    private int? alphaQuality;

    /// <summary>
    /// Backing field for <see cref="Effort"/>.
    /// </summary>
    private int effort = 5;

    /// <summary>
    /// The AV1 encoding speed.
    /// </summary>
    private HeifEncodingSpeed speed;

    /// <summary>
    /// Gets the lossy compression quality, or <see langword="null"/> to use the compression method's default quality.
    /// Valid values range from 0 for the lowest quality to 100 for the highest quality. A value of 100 does not
    /// enable <see cref="Lossless"/> encoding.
    /// </summary>
    /// <exception cref="ArgumentException">The quality is outside the range 0 to 100.</exception>
    public int? Quality
    {
        get => this.quality;
        init
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentException("Quality must be in the range [0..100].");
            }

            this.quality = value;
        }
    }

    /// <summary>
    /// Gets the lossy compression quality for the auxiliary alpha image, or <see langword="null"/> to use the
    /// effective <see cref="Quality"/>. Valid values range from 0 for the lowest quality to 100 for the highest
    /// quality. This option has no effect when the encoded image does not require an auxiliary alpha image.
    /// </summary>
    /// <exception cref="ArgumentException">The alpha quality is outside the range 0 to 100.</exception>
    public int? AlphaQuality
    {
        get => this.alphaQuality;
        init
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentException("Alpha quality must be in the range [0..100].");
            }

            this.alphaQuality = value;
        }
    }

    /// <summary>
    /// Gets the encoding effort in the range 0 to 10. A value of 0 selects the fastest encoding and 10 selects the
    /// slowest encoding with the greatest compression effort. The default is 5.
    /// </summary>
    /// <exception cref="ArgumentException">The effort is outside the range 0 to 10.</exception>
    public int Effort
    {
        get => this.effort;
        init
        {
            if (value is < 0 or > 10)
            {
                throw new ArgumentException("Effort must be in the range [0..10].");
            }

            this.effort = value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the primary and auxiliary alpha images are encoded without loss. When
    /// <see langword="true"/>, <see cref="Quality"/> and <see cref="AlphaQuality"/> do not affect the encoded image.
    /// The default is <see langword="false"/>.
    /// </summary>
    public bool Lossless { get; init; }

    /// <summary>
    /// Gets the AV1 encoding speed. Higher levels prioritize speed over compression efficiency.
    /// The default is <see cref="HeifEncodingSpeed.Level0"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The speed is outside the range 0 to 9.</exception>
    public HeifEncodingSpeed Speed
    {
        get => this.speed;
        init
        {
            if (value is < HeifEncodingSpeed.Level0 or > HeifEncodingSpeed.Level9)
            {
                throw new ArgumentException("Speed must be in the range [0..9].");
            }

            this.speed = value;
        }
    }

    /// <summary>
    /// Gets the encoded precision of each image component, or <see langword="null"/> to use the HEIF metadata bit
    /// depth. Metadata that does not specify a bit depth defaults to <see cref="HeifBitDepth.Bit8"/>.
    /// </summary>
    public HeifBitDepth? BitDepth { get; init; }

    /// <summary>
    /// Gets the encoded chroma sampling, or <see langword="null"/> to use <see cref="HeifChromaSubsampling.Yuv420"/>
    /// for lossy encoding and <see cref="HeifChromaSubsampling.Yuv444"/> for lossless encoding. Oversized still
    /// images use <see cref="HeifChromaSubsampling.Yuv444"/> when a subsampled AVIF grid cannot represent an odd
    /// output dimension.
    /// </summary>
    public HeifChromaSubsampling? ChromaSubsampling { get; init; }

    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        HeifEncoderCore encoder = new(image.Configuration, this);
        encoder.Encode(image, stream, cancellationToken);
    }
}
