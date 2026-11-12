// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Provides JPEG XL image metadata.
/// </summary>
public class JxlMetadata : IFormatMetadata<JxlMetadata>
{
    private JxlBitDepth bitDepth = new(8, 0);
    private JxlColorType colorType;
    private CieXyChromaticityCoordinates? whitePoint;
    private RgbPrimariesChromaticityCoordinates? primaries;
    private CicpTransferCharacteristics? transferCharacteristics;
    private Rational? gamma;
    private IccRenderingIntent? renderingIntent;
    private float? intensityTarget;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlMetadata"/> class.
    /// </summary>
    public JxlMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlMetadata"/> class by copying another instance.
    /// </summary>
    /// <param name="other">The metadata to copy.</param>
    private JxlMetadata(JxlMetadata other)
    {
        this.bitDepth = other.bitDepth;
        this.colorType = other.colorType;
        this.WhitePoint = other.WhitePoint;
        this.Primaries = other.Primaries;
        this.TransferCharacteristics = other.TransferCharacteristics;
        this.Gamma = other.Gamma;
        this.RenderingIntent = other.RenderingIntent;
        this.IntensityTarget = other.IntensityTarget;
        this.RepeatCount = other.RepeatCount;

        // Each channel description is immutable. Copy the collection so callers can edit channel lists independently.
        foreach (JxlExtraChannelMetadata channel in other.ExtraChannels)
        {
            this.ExtraChannels.Add(channel);
        }
    }

    /// <summary>
    /// Gets or sets the color sample precision. The default is unsigned 8-bit samples.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is an uninitialized sample layout.</exception>
    public JxlBitDepth BitDepth
    {
        get => this.bitDepth;
        set
        {
            // A constructed JxlBitDepth is validated. A struct's default value still has zero precision;
            // reject that value at this public boundary rather than checking it during pixel conversion.
            Guard.MustBeGreaterThan(value.BitsPerSample, (byte)0, nameof(value));

            this.bitDepth = value;
        }
    }

    /// <summary>
    /// Gets or sets the encoded color model. The default is <see cref="JxlColorType.Rgb"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a JPEG XL color type.</exception>
    public JxlColorType ColorType
    {
        get => this.colorType;
        set
        {
            Guard.MustBeBetweenOrEqualTo((byte)value, (byte)JxlColorType.Rgb, (byte)JxlColorType.Other, nameof(value));

            this.colorType = value;
        }
    }

    /// <summary>
    /// Gets or sets the compact color encoding's reference white chromaticity.
    /// Null uses the image's CICP profile or the default D65 white point. An ICC profile supplies its own white point.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate exceeds the signed millionths stored by JPEG XL.</exception>
    public CieXyChromaticityCoordinates? WhitePoint
    {
        get => this.whitePoint;
        set
        {
            if (value.HasValue)
            {
                ValidateChromaticity(value.Value);
            }

            this.whitePoint = value;
        }
    }

    /// <summary>
    /// Gets or sets the compact RGB encoding's primary chromaticities.
    /// Null uses the image's CICP profile or sRGB primaries. Grayscale and ICC encodings do not use this value.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate exceeds the signed millionths stored by JPEG XL.</exception>
    public RgbPrimariesChromaticityCoordinates? Primaries
    {
        get => this.primaries;
        set
        {
            if (value.HasValue)
            {
                ValidateChromaticity(value.Value.R);
                ValidateChromaticity(value.Value.G);
                ValidateChromaticity(value.Value.B);
            }

            this.primaries = value;
        }
    }

    /// <summary>
    /// Gets or sets the compact encoding's transfer function when <see cref="Gamma"/> is null.
    /// Null uses the image's CICP profile or the sRGB transfer function. An ICC profile supplies its own transfer function.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a compact JPEG XL transfer function.</exception>
    public CicpTransferCharacteristics? TransferCharacteristics
    {
        get => this.transferCharacteristics;
        set
        {
            if (value.HasValue && value.Value is not (CicpTransferCharacteristics.ItuRBt709_6
                or CicpTransferCharacteristics.Linear or CicpTransferCharacteristics.Iec61966_2_1
                or CicpTransferCharacteristics.SmpteSt2084 or CicpTransferCharacteristics.SmpteSt428_1 or CicpTransferCharacteristics.AribStdB67))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The transfer function has no compact JPEG XL identifier.");
            }

            this.transferCharacteristics = value;
        }
    }

    /// <summary>
    /// Gets or sets the compact encoding's compression exponent.
    /// Null uses <see cref="TransferCharacteristics"/>. An ICC profile supplies its own transfer function.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The stored exponent is outside the interval from 1/8192 to one.</exception>
    public Rational? Gamma
    {
        get => this.gamma;
        set
        {
            if (value.HasValue)
            {
                // The file stores integer ten-millionths. Validate the rounded field, including NaN
                // from an uninitialized rational, before the writer converts it to an unsigned integer.
                // The least legal integer is ceil(10000000 / 8192), or 1221.
                double encoded = Math.Round(value.Value.ToDouble() * 10_000_000D);

                if (!(encoded >= 1221D && encoded <= 10_000_000D))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "The stored JPEG XL gamma must be between 1/8192 and one.");
                }
            }

            this.gamma = value;
        }
    }

    /// <summary>
    /// Gets or sets the compact encoding's rendering intent.
    /// Null uses media-relative colorimetric intent. An ICC profile supplies its own rendering intent.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not an ICC rendering intent.</exception>
    public IccRenderingIntent? RenderingIntent
    {
        get => this.renderingIntent;
        set
        {
            if (value.HasValue)
            {
                Guard.MustBeLessThanOrEqualTo((uint)value.Value, 3U, nameof(value));
            }

            this.renderingIntent = value;
        }
    }

    /// <summary>
    /// Gets or sets the reference intensity in nits.
    /// Null uses 10000 nits for PQ, 1000 nits for HLG, or 255 nits for other transfer functions.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value does not round to a positive finite half-precision intensity.</exception>
    public float? IntensityTarget
    {
        get => this.intensityTarget;
        set
        {
            if (value.HasValue)
            {
                float encoded = HalfTypeHelper.Unpack(HalfTypeHelper.Pack(value.Value));
                if (!(encoded > 0F && float.IsFinite(encoded)))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "The intensity must round to a positive finite half-precision value.");
                }
            }

            this.intensityTarget = value;
        }
    }

    /// <summary>
    /// Gets the channels stored in addition to the image color components, in encoded order.
    /// Each channel has its own precision and alpha representation.
    /// </summary>
    public IList<JxlExtraChannelMetadata> ExtraChannels { get; } = [];

    /// <summary>
    /// Gets or sets the number of times the image sequence is played.
    /// Zero repeats indefinitely. The default is one play.
    /// </summary>
    public uint RepeatCount { get; set; } = 1;

    /// <inheritdoc/>
    public static JxlMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
    {
        PixelTypeInfo pixelInfo = metadata.PixelTypeInfo;
        PixelColorType color = pixelInfo.ColorType & ~PixelColorType.Alpha;

        // Palette precision describes an index, not a color component. PNG and GIF palette entries
        // contain eight-bit RGB components, even when one or two index bits select each decoded color.
        int precision = color == PixelColorType.Indexed ? 8 : pixelInfo.ComponentInfo?.GetMaximumComponentPrecision() ?? 8;

        // Connecting metadata describes precision and range, but not the floating-point exponent layout.
        // Use single precision for extended values or integer formats beyond JPEG XL's 31-bit unsigned limit.
        bool useFloat = pixelInfo.ColorRange == PixelColorRange.Extended || precision > 31;
        JxlBitDepth bitDepth = useFloat ? new JxlBitDepth(32, 8) : new JxlBitDepth((byte)precision, 0);
        JxlMetadata result = new()
        {
            BitDepth = bitDepth,
            ColorType = GetColorType(color),
            RepeatCount = metadata.RepeatCount
        };

        // JPEG XL's RGB color type also represents the three CMY components. CMYK and YCCK source
        // descriptions need a separate black component when the original CMYK profile is retained.
        if (color is PixelColorType.CMYK or PixelColorType.YCCK)
        {
            result.ExtraChannels.Add(new JxlExtraChannelMetadata(JxlExtraChannelType.Black, bitDepth, PixelAlphaRepresentation.None));
        }

        if (pixelInfo.AlphaRepresentation != PixelAlphaRepresentation.None)
        {
            result.ExtraChannels.Add(new JxlExtraChannelMetadata(JxlExtraChannelType.Alpha, bitDepth, pixelInfo.AlphaRepresentation));
        }

        return result;
    }

    /// <summary>
    /// Gets the JPEG XL color type for pixel color components or an ICC data space mapped to those components.
    /// </summary>
    /// <param name="colorType">The pixel color type without alpha.</param>
    /// <returns>The corresponding JPEG XL color type, or Other for an ICC-defined model.</returns>
    internal static JxlColorType GetColorType(PixelColorType colorType)
        => colorType switch
        {
            PixelColorType.Luminance => JxlColorType.Luminance,
            PixelColorType.RGB or PixelColorType.BGR or PixelColorType.CMYK
                or PixelColorType.YCbCr or PixelColorType.YCCK or PixelColorType.Indexed => JxlColorType.Rgb,
            _ => JxlColorType.Other
        };

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        JxlExtraChannelMetadata? alpha = null;
        JxlExtraChannelMetadata? black = null;

        // The first alpha channel supplies displayed transparency. The black channel supplies K for CMYK.
        // Depth, selection masks and other extra channels remain metadata; they are not displayed color components.
        foreach (JxlExtraChannelMetadata channel in this.ExtraChannels)
        {
            if (channel.Type == JxlExtraChannelType.Alpha && alpha is null)
            {
                alpha = channel;
            }
            else if (channel.Type == JxlExtraChannelType.Black && black is null)
            {
                black = channel;
            }
        }

        int colorCount = this.ColorType == JxlColorType.Luminance ? 1 : 3;
        PixelColorType colorType = this.ColorType switch
        {
            JxlColorType.Luminance => PixelColorType.Luminance,
            JxlColorType.Rgb => PixelColorType.RGB,
            _ => PixelColorType.Other
        };

        // Up to three base components, one black component and one alpha component describe the displayed pixel.
        // Borrow one stack-backed span for precision construction; metadata conversion does not allocate an array.
        InlineArray8<int> storage = default;
        Span<int> precisions = storage;
        precisions[..colorCount].Fill(this.BitDepth.BitsPerSample);
        int componentCount = colorCount;
        int bitsPerPixel = colorCount * this.BitDepth.BitsPerSample;
        bool extended = this.BitDepth.ExponentBits != 0;
        if (this.ColorType == JxlColorType.Rgb && black is not null)
        {
            precisions[componentCount++] = black.BitDepth.BitsPerSample;
            bitsPerPixel += black.BitDepth.BitsPerSample;
            colorType = PixelColorType.CMYK;
            extended |= black.BitDepth.ExponentBits != 0;
        }

        if (alpha is not null)
        {
            precisions[componentCount++] = alpha.BitDepth.BitsPerSample;
            bitsPerPixel += alpha.BitDepth.BitsPerSample;
            colorType |= PixelColorType.Alpha;
        }

        return new PixelTypeInfo(bitsPerPixel)
        {
            ComponentInfo = PixelComponentInfo.Create(componentCount, bitsPerPixel, precisions[..componentCount]),
            ColorType = colorType,
            ColorRange = extended ? PixelColorRange.Extended : PixelColorRange.Sdr,
            AlphaRepresentation = alpha?.AlphaRepresentation ?? PixelAlphaRepresentation.None
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
        => new()
        {
            // This requests a new lossless encoding. It does not classify the source frame's compression mode.
            EncodingType = EncodingType.Lossless,
            PixelTypeInfo = this.GetPixelTypeInfo(),

            // JPEG XL stores a 32-bit play count. Other formats share a 16-bit count; retain the largest finite
            // count they can represent rather than wrapping it to zero, which would request infinite playback.
            RepeatCount = (ushort)Math.Min(this.RepeatCount, ushort.MaxValue)
        };

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public JxlMetadata DeepClone() => new(this);

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }

    /// <summary>
    /// Checks the signed millionths used by both compact white points and RGB primaries.
    /// </summary>
    /// <param name="value">The chromaticity to encode.</param>
    /// <exception cref="ArgumentOutOfRangeException">A rounded coordinate does not fit the encoded field.</exception>
    private static void ValidateChromaticity(CieXyChromaticityCoordinates value)
    {
        // Zigzag coding stores 22 bits. Its signed range is -2097152 through 2097151 millionths.
        // Match the writer's single-precision rounding before validating that integer range.
        float x = MathF.Round(value.X * 1_000_000F);
        float y = MathF.Round(value.Y * 1_000_000F);

        if (!(x >= -2097152F && x <= 2097151F && y >= -2097152F && y <= 2097151F))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Chromaticity coordinates must fit the signed JPEG XL millionths field.");
        }
    }
}
