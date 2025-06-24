// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Provides Webp specific metadata information for the image.
/// </summary>
public class WebpMetadata : IFormatMetadata<WebpMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebpMetadata"/> class.
    /// </summary>
    public WebpMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebpMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private WebpMetadata(WebpMetadata other)
    {
        this.BitsPerPixel = other.BitsPerPixel;
        this.ColorType = other.ColorType;
        this.FileFormat = other.FileFormat;
        this.RepeatCount = other.RepeatCount;
        this.BackgroundColor = other.BackgroundColor;
    }

    /// <summary>
    /// Gets or sets the number of bits per pixel.
    /// </summary>
    public WebpBitsPerPixel BitsPerPixel { get; set; } = WebpBitsPerPixel.Bit32;

    /// <summary>
    /// Gets or sets the color type.
    /// </summary>
    public WebpColorType ColorType { get; set; } = WebpColorType.Rgba;

    /// <summary>
    /// Gets or sets the webp file format used. Either lossless or lossy.
    /// </summary>
    public WebpFileFormatType FileFormat { get; set; } = WebpFileFormatType.Lossy;

    /// <summary>
    /// Gets or sets the loop count. The number of times to loop the animation. 0 means infinitely.
    /// </summary>
    public ushort RepeatCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the default background color of the canvas when animating.
    /// This color may be used to fill the unused space on the canvas around the frames,
    /// as well as the transparent pixels of the first frame.
    /// The background color is also used when the Disposal method is <see cref="FrameDisposalMode.RestoreToBackground"/>.
    /// </summary>
    public Color BackgroundColor { get; set; }

    /// <inheritdoc/>
    public static WebpMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
    {
        WebpBitsPerPixel bitsPerPixel;
        WebpColorType color;
        PixelColorType colorType = metadata.PixelTypeInfo.ColorType;
        switch (colorType)
        {
            case PixelColorType.RGB:
            case PixelColorType.BGR:
                color = WebpColorType.Rgb;
                bitsPerPixel = WebpBitsPerPixel.Bit24;
                break;
            case PixelColorType.YCbCr:
                color = WebpColorType.Yuv;
                bitsPerPixel = WebpBitsPerPixel.Bit24;
                break;
            default:
                if (colorType.HasFlag(PixelColorType.Alpha))
                {
                    color = WebpColorType.Rgba;
                    bitsPerPixel = WebpBitsPerPixel.Bit32;
                    break;
                }

                color = WebpColorType.Rgb;
                bitsPerPixel = WebpBitsPerPixel.Bit24;
                break;
        }

        return new()
        {
            BitsPerPixel = bitsPerPixel,
            ColorType = color,
            BackgroundColor = metadata.BackgroundColor,
            RepeatCount = metadata.RepeatCount,
            FileFormat = metadata.EncodingType == EncodingType.Lossless ? WebpFileFormatType.Lossless : WebpFileFormatType.Lossy
        };
    }

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        int bpp;
        PixelColorType colorType;
        PixelAlphaRepresentation alpha = PixelAlphaRepresentation.None;
        PixelComponentInfo info;
        switch (this.ColorType)
        {
            case WebpColorType.Yuv:
                bpp = 24;
                colorType = PixelColorType.YCbCr;
                info = PixelComponentInfo.Create(3, bpp, 8, 8, 8);
                break;
            case WebpColorType.Rgb:
                bpp = 24;
                colorType = PixelColorType.RGB;
                info = PixelComponentInfo.Create(3, bpp, 8, 8, 8);
                break;
            case WebpColorType.Rgba:
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
            EncodingType = this.FileFormat == WebpFileFormatType.Lossless ? EncodingType.Lossless : EncodingType.Lossy,
            PixelTypeInfo = this.GetPixelTypeInfo(),
            ColorTableMode = FrameColorTableMode.Global,
            RepeatCount = this.RepeatCount,
            BackgroundColor = this.BackgroundColor
        };

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public WebpMetadata DeepClone() => new(this);
}
