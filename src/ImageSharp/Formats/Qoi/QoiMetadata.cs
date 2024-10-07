// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Qoi;

/// <summary>
/// Provides Qoi specific metadata information for the image.
/// </summary>
public class QoiMetadata : IFormatMetadata<QoiMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QoiMetadata"/> class.
    /// </summary>
    public QoiMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QoiMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private QoiMetadata(QoiMetadata other)
    {
        this.Channels = other.Channels;
        this.ColorSpace = other.ColorSpace;
    }

    /// <summary>
    /// Gets or sets color channels of the image. 3 = RGB, 4 = RGBA.
    /// </summary>
    public QoiChannels Channels { get; set; }

    /// <summary>
    /// Gets or sets color space of the image. 0 = sRGB with linear alpha, 1 = All channels linear
    /// </summary>
    public QoiColorSpace ColorSpace { get; set; }

    /// <inheritdoc/>
    public static QoiMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
    {
        PixelColorType color = metadata.PixelTypeInfo.ColorType;

        if (color.HasFlag(PixelColorType.Alpha))
        {
            return new() { Channels = QoiChannels.Rgba };
        }

        return new() { Channels = QoiChannels.Rgb };
    }

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        int bpp;
        PixelColorType colorType;
        PixelAlphaRepresentation alpha = PixelAlphaRepresentation.None;
        PixelComponentInfo info;

        switch (this.Channels)
        {
            case QoiChannels.Rgb:
                bpp = 24;
                colorType = PixelColorType.RGB;
                info = PixelComponentInfo.Create(3, bpp, 8, 8, 8);
                break;
            default:
                bpp = 32;
                colorType = PixelColorType.RGB | PixelColorType.Alpha;
                info = PixelComponentInfo.Create(4, bpp, 8, 8, 8, 8);
                alpha = PixelAlphaRepresentation.Unassociated;
                break;
        }

        return new(bpp)
        {
            AlphaRepresentation = alpha,
            ColorType = colorType,
            ComponentInfo = info,
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
        => new()
        {
            PixelTypeInfo = this.GetPixelTypeInfo()
        };

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public QoiMetadata DeepClone() => new(this);
}
