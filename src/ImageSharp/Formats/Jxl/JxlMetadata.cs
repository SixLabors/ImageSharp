// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Provides JPEG XL image metadata.
/// </summary>
public class JxlMetadata : IFormatMetadata<JxlMetadata>
{
    private JxlBitDepth bitDepth = new(8, 0);
    private JxlColorType colorType;

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
        int precision = pixelInfo.ComponentInfo?.GetMaximumComponentPrecision() ?? 8;

        // Connecting metadata describes precision and range, but not the floating-point exponent layout.
        // Use single precision for extended values or integer formats beyond JPEG XL's 31-bit unsigned limit.
        bool useFloat = pixelInfo.ColorRange == PixelColorRange.Extended || precision > 31;
        JxlBitDepth bitDepth = useFloat ? new JxlBitDepth(32, 8) : new JxlBitDepth((byte)precision, 0);
        PixelColorType color = pixelInfo.ColorType & ~PixelColorType.Alpha;
        JxlMetadata result = new()
        {
            BitDepth = bitDepth,
            ColorType = color switch
            {
                PixelColorType.Luminance => JxlColorType.Luminance,
                PixelColorType.RGB or PixelColorType.BGR or PixelColorType.CMYK => JxlColorType.Rgb,
                _ => JxlColorType.Other
            },
            RepeatCount = metadata.RepeatCount
        };

        // JPEG XL's RGB color type also represents the three CMY components. A black extra channel identifies CMYK.
        // Preserve that component instead of labelling the four source components as RGB with alpha.
        if (color == PixelColorType.CMYK)
        {
            result.ExtraChannels.Add(new JxlExtraChannelMetadata(JxlExtraChannelType.Black, bitDepth, PixelAlphaRepresentation.None));
        }

        if (pixelInfo.AlphaRepresentation != PixelAlphaRepresentation.None)
        {
            result.ExtraChannels.Add(new JxlExtraChannelMetadata(JxlExtraChannelType.Alpha, bitDepth, pixelInfo.AlphaRepresentation));
        }

        return result;
    }

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
}
