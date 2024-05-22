// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Provides Jpeg specific metadata information for the image.
/// </summary>
public class JpegMetadata : IFormatMetadata<JpegMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JpegMetadata"/> class.
    /// </summary>
    public JpegMetadata() => this.Comments = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="JpegMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private JpegMetadata(JpegMetadata other)
    {
        this.ColorType = other.ColorType;

        this.Comments = other.Comments;
        this.LuminanceQuality = other.LuminanceQuality;
        this.ChrominanceQuality = other.ChrominanceQuality;
    }

    /// <summary>
    /// Gets or sets the jpeg luminance quality.
    /// </summary>
    /// <remarks>
    /// This value might not be accurate if it was calculated during jpeg decoding
    /// with non-compliant ITU quantization tables.
    /// </remarks>
    internal int? LuminanceQuality { get; set; }

    /// <summary>
    /// Gets or sets the jpeg chrominance quality.
    /// </summary>
    /// <remarks>
    /// This value might not be accurate if it was calculated during jpeg decoding
    /// with non-compliant ITU quantization tables.
    /// </remarks>
    internal int? ChrominanceQuality { get; set; }

    /// <summary>
    /// Gets the encoded quality.
    /// </summary>
    /// <remarks>
    /// Note that jpeg image can have different quality for luminance and chrominance components.
    /// This property returns maximum value of luma/chroma qualities if both are present.
    /// </remarks>
    public int Quality
    {
        get
        {
            if (this.LuminanceQuality.HasValue)
            {
                if (this.ChrominanceQuality.HasValue)
                {
                    return Math.Max(this.LuminanceQuality.Value, this.ChrominanceQuality.Value);
                }

                return this.LuminanceQuality.Value;
            }

            return this.ChrominanceQuality ?? Quantization.DefaultQualityFactor;
        }
    }

    /// <summary>
    /// Gets the color type.
    /// </summary>
    public JpegEncodingColor? ColorType { get; internal set; }

    /// <summary>
    /// Gets the component encoding mode.
    /// </summary>
    /// <remarks>
    /// Interleaved encoding mode encodes all color components in a single scan.
    /// Non-interleaved encoding mode encodes each color component in a separate scan.
    /// </remarks>
    public bool? Interleaved { get; internal set; }

    /// <summary>
    /// Gets the scan encoding mode.
    /// </summary>
    /// <remarks>
    /// Progressive jpeg images encode component data across multiple scans.
    /// </remarks>
    public bool? Progressive { get; internal set; }

    /// <summary>
    /// Gets the comments.
    /// </summary>
    public IList<JpegComData> Comments { get; }

    /// <inheritdoc/>
    public static JpegMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
    {
        JpegEncodingColor color;
        PixelColorType colorType = metadata.PixelTypeInfo.ColorType ?? PixelColorType.YCbCr;
        switch (colorType)
        {
            case PixelColorType.Luminance:
                color = JpegEncodingColor.Luminance;
                break;
            case PixelColorType.CMYK:
                color = JpegEncodingColor.Cmyk;
                break;
            case PixelColorType.YCCK:
                color = JpegEncodingColor.Ycck;
                break;
            default:
                if (colorType.HasFlag(PixelColorType.RGB) || colorType.HasFlag(PixelColorType.BGR))
                {
                    color = JpegEncodingColor.Rgb;
                }
                else
                {
                    color = metadata.Quality <= Quantization.DefaultQualityFactor
                        ? JpegEncodingColor.YCbCrRatio420
                        : JpegEncodingColor.YCbCrRatio444;
                }

                break;
        }

        return new JpegMetadata
        {
            ColorType = color,
            ChrominanceQuality = metadata.Quality,
            LuminanceQuality = metadata.Quality,
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
    {
        int bpp;
        PixelColorType colorType;
        PixelComponentInfo info;
        switch (this.ColorType)
        {
            case JpegEncodingColor.Luminance:
                bpp = 8;
                colorType = PixelColorType.Luminance;
                info = PixelComponentInfo.Create(1, bpp, 8);
                break;
            case JpegEncodingColor.Cmyk:
                bpp = 32;
                colorType = PixelColorType.CMYK;
                info = PixelComponentInfo.Create(4, bpp, 8, 8, 8, 8);
                break;
            case JpegEncodingColor.Ycck:
                bpp = 32;
                colorType = PixelColorType.YCCK;
                info = PixelComponentInfo.Create(4, bpp, 8, 8, 8, 8);
                break;
            case JpegEncodingColor.Rgb:
                bpp = 24;
                colorType = PixelColorType.RGB;
                info = PixelComponentInfo.Create(3, bpp, 8, 8, 8);
                break;
            default:
                bpp = 24;
                colorType = PixelColorType.YCbCr;
                info = PixelComponentInfo.Create(3, bpp, 8, 8, 8);
                break;
        }

        return new FormatConnectingMetadata
        {
            PixelTypeInfo = new PixelTypeInfo(bpp)
            {
                AlphaRepresentation = PixelAlphaRepresentation.None,
                ColorType = colorType,
                ComponentInfo = info,
            },
            Quality = this.Quality,
        };
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public JpegMetadata DeepClone() => new(this);
}
