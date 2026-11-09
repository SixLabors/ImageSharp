// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Encodes images in JPEG XL format with modular compression.
/// </summary>
public sealed class JxlEncoder : AlphaAwareImageEncoder
{
    /// <summary>
    /// The optional sample precision selected by the caller.
    /// </summary>
    private JxlBitDepth? bitDepth;

    /// <summary>
    /// The optional color model selected by the caller.
    /// </summary>
    private JxlColorType? colorType;

    /// <summary>
    /// The selected lossy reconstruction distance, or zero for lossless modular coding.
    /// </summary>
    private float distance;

    /// <summary>
    /// The supplied extra channels in encoded order.
    /// </summary>
    private IReadOnlyList<JxlExtraChannel> extraChannels = [];

    /// <summary>
    /// Gets the output color model. When null, the encoder uses the image's JPEG XL metadata.
    /// XYB metadata selects RGB output for a lossless replacement. Explicit XYB selects the coding transform
    /// and retains the original compact color description or ICC profile. It requires a positive <see cref="Distance"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a JPEG XL color type.</exception>
    public JxlColorType? ColorType
    {
        get => this.colorType;
        init
        {
            if (value.HasValue)
            {
                Guard.MustBeBetweenOrEqualTo((byte)value.Value, (byte)JxlColorType.Rgb, (byte)JxlColorType.Other, nameof(value));
            }

            this.colorType = value;
        }
    }

    /// <summary>
    /// Gets the reconstruction distance from zero through 25. Zero uses lossless modular coding.
    /// Positive values use quantized XYB components. The default is zero.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The distance is outside the supported interval.</exception>
    public float Distance
    {
        get => this.distance;
        init
        {
            Guard.MustBeBetweenOrEqualTo(value, 0F, 25F, nameof(value));

            this.distance = value;
        }
    }

    /// <summary>
    /// Gets the number of sequence plays. Zero repeats indefinitely. When null, the encoder uses the image metadata.
    /// </summary>
    public uint? RepeatCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether to write two progressive passes.
    /// Integer color samples with sufficient arithmetic precision use a half-resolution first pass.
    /// </summary>
    public bool Progressive { get; init; }

    /// <summary>
    /// Gets the main color sample precision. When null, the encoder uses the image's JPEG XL metadata.
    /// Extra channels retain the precision in their channel metadata.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is an uninitialized sample layout.</exception>
    public JxlBitDepth? BitDepth
    {
        get => this.bitDepth;
        init
        {
            // Constructed layouts are validated by JxlBitDepth. Its default struct value still has zero precision.
            if (value.HasValue)
            {
                Guard.MustBeGreaterThan(value.Value.BitsPerSample, (byte)0, nameof(value));
            }

            this.bitDepth = value;
        }
    }

    /// <summary>
    /// Gets the supplied extra channels in encoded order. The default is an empty list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first supplied alpha channel replaces pixel-derived transparency.
    /// The first supplied black channel replaces the black component derived through the image's CMYK profile.
    /// Other supplied channels retain their list order. Pixel-derived alpha and black channels follow when no supplied channel replaces them.
    /// </para>
    /// <para>
    /// A spot-color channel applies its contribution to the supplied image color pixels during decoding.
    /// The caller owns all sample buffers and must keep them available until encoding completes.
    /// Later changes to the list supplied to this property do not change the encoder's channel list.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">The channel list is null.</exception>
    /// <exception cref="ArgumentException">The channel list contains a null channel.</exception>
    public IReadOnlyList<JxlExtraChannel> ExtraChannels
    {
        get => this.extraChannels;
        init
        {
            Guard.NotNull(value, nameof(value));

            // Keep the configured channel order stable, as with HEIF's layer list. Channels retain
            // references to caller-owned sample buffers; this setup copy does not copy their samples.
            JxlExtraChannel[] copy = new JxlExtraChannel[value.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = value[i] ?? throw new ArgumentException("The extra channels cannot contain a null channel.", nameof(value));
            }

            this.extraChannels = Array.AsReadOnly(copy);
        }
    }

    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using JxlEncoderCore encoder = new(this, image, stream);
        encoder.Encode(image, cancellationToken);
    }
}
