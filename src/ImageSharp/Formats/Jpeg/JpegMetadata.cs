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
    public JpegMetadata()
    {
    }

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
    /// Gets or sets the encoded quality.
    /// </summary>
    /// <remarks>
    /// Note that jpeg image can have different quality for luminance and chrominance components.
    /// This property returns maximum value of luma/chroma qualities if both are present.
    /// Setting the quality will update both values.
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

        set
        {
            this.LuminanceQuality = value;
            this.ChrominanceQuality = value;
        }
    }

    /// <summary>
    /// Gets or sets the color type.
    /// </summary>
    public JpegColorType ColorType { get; set; } = JpegColorType.YCbCrRatio420;

    /// <summary>
    /// Gets or sets a value indicating whether the component encoding mode should be interleaved.
    /// </summary>
    /// <remarks>
    /// Interleaved encoding mode encodes all color components in a single scan.
    /// Non-interleaved encoding mode encodes each color component in a separate scan.
    /// </remarks>
    public bool Interleaved { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the scan encoding mode is progressive.
    /// </summary>
    /// <remarks>
    /// Progressive jpeg images encode component data across multiple scans.
    /// </remarks>
    public bool Progressive { get; set; }

    /// <summary>
    /// Gets or sets collection of comments.
    /// </summary>
    public IList<JpegComData> Comments { get; set; } = [];

    /// <inheritdoc/>
    public static JpegMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
    {
        JpegColorType color;
        PixelColorType colorType = metadata.PixelTypeInfo.ColorType;
        switch (colorType)
        {
            case PixelColorType.Luminance:
                color = JpegColorType.Luminance;
                break;
            case PixelColorType.CMYK:
                color = JpegColorType.Cmyk;
                break;
            case PixelColorType.YCCK:
                color = JpegColorType.Ycck;
                break;
            default:
                if (colorType.HasFlag(PixelColorType.RGB) || colorType.HasFlag(PixelColorType.BGR))
                {
                    color = JpegColorType.Rgb;
                }
                else
                {
                    color = metadata.Quality <= Quantization.DefaultQualityFactor
                        ? JpegColorType.YCbCrRatio420
                        : JpegColorType.YCbCrRatio444;
                }

                break;
        }

        return new()
        {
            ColorType = color,
            ChrominanceQuality = metadata.Quality,
            LuminanceQuality = metadata.Quality,
        };
    }

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        int bpp;
        PixelColorType colorType;
        PixelComponentInfo info;
        switch (this.ColorType)
        {
            case JpegColorType.Luminance:
                bpp = 8;
                colorType = PixelColorType.Luminance;
                info = PixelComponentInfo.Create(1, bpp, 8);
                break;
            case JpegColorType.Cmyk:
                bpp = 32;
                colorType = PixelColorType.CMYK;
                info = PixelComponentInfo.Create(4, bpp, 8, 8, 8, 8);
                break;
            case JpegColorType.Ycck:
                bpp = 32;
                colorType = PixelColorType.YCCK;
                info = PixelComponentInfo.Create(4, bpp, 8, 8, 8, 8);
                break;
            case JpegColorType.Rgb:
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

        return new(bpp)
        {
            AlphaRepresentation = PixelAlphaRepresentation.None,
            ColorType = colorType,
            ComponentInfo = info,
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
        => new()
        {
            EncodingType = EncodingType.Lossy,
            PixelTypeInfo = this.GetPixelTypeInfo(),
            Quality = this.Quality,
        };

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public JpegMetadata DeepClone() => new(this);
}
