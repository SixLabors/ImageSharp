// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

// TODO: Add color table information.
namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Provides Bmp specific metadata information for the image.
/// </summary>
public class BmpMetadata : IFormatMetadata<BmpMetadata>, IFormatFrameMetadata<BmpMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BmpMetadata"/> class.
    /// </summary>
    public BmpMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BmpMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private BmpMetadata(BmpMetadata other)
    {
        this.BitsPerPixel = other.BitsPerPixel;
        this.InfoHeaderType = other.InfoHeaderType;
    }

    /// <summary>
    /// Gets or sets the bitmap info header type.
    /// </summary>
    public BmpInfoHeaderType InfoHeaderType { get; set; }

    /// <summary>
    /// Gets or sets the number of bits per pixel.
    /// </summary>
    public BmpBitsPerPixel BitsPerPixel { get; set; } = BmpBitsPerPixel.Pixel24;

    /// <inheritdoc/>
    public static BmpMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
    {
        int bpp = metadata.PixelTypeInfo.BitsPerPixel;
        return bpp switch
        {
            1 => new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel1 },
            2 => new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel2 },
            <= 4 => new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel4 },
            <= 8 => new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel8 },
            <= 16 => new BmpMetadata
            {
                BitsPerPixel = BmpBitsPerPixel.Pixel16,
                InfoHeaderType = BmpInfoHeaderType.WinVersion3
            },
            <= 24 => new BmpMetadata
            {
                BitsPerPixel = BmpBitsPerPixel.Pixel24,
                InfoHeaderType = BmpInfoHeaderType.WinVersion4
            },
            _ => new BmpMetadata
            {
                BitsPerPixel = BmpBitsPerPixel.Pixel32,
                InfoHeaderType = BmpInfoHeaderType.WinVersion5
            }
        };
    }

    /// <inheritdoc/>
    public static BmpMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
        => new();

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        int bpp = (int)this.BitsPerPixel;

        PixelAlphaRepresentation alpha = this.InfoHeaderType switch
        {
            BmpInfoHeaderType.WinVersion2 or
            BmpInfoHeaderType.Os2Version2Short or
            BmpInfoHeaderType.WinVersion3 or
            BmpInfoHeaderType.AdobeVersion3 or
            BmpInfoHeaderType.Os2Version2 => PixelAlphaRepresentation.None,
            BmpInfoHeaderType.AdobeVersion3WithAlpha or
            BmpInfoHeaderType.WinVersion4 or
            BmpInfoHeaderType.WinVersion5 or
            _ => bpp < 32 ? PixelAlphaRepresentation.None : PixelAlphaRepresentation.Unassociated
        };

        PixelComponentInfo info;
        PixelColorType color;
        switch (this.BitsPerPixel)
        {
            case BmpBitsPerPixel.Pixel1:
                info = PixelComponentInfo.Create(1, bpp, 1);
                color = PixelColorType.Indexed;
                break;
            case BmpBitsPerPixel.Pixel2:
                info = PixelComponentInfo.Create(1, bpp, 2);
                color = PixelColorType.Indexed;
                break;
            case BmpBitsPerPixel.Pixel4:
                info = PixelComponentInfo.Create(1, bpp, 4);
                color = PixelColorType.Indexed;
                break;
            case BmpBitsPerPixel.Pixel8:
                info = PixelComponentInfo.Create(1, bpp, 8);
                color = PixelColorType.Indexed;
                break;

            // Could be 555 with padding but 565 is more common in newer bitmaps and offers
            // greater accuracy due to extra green precision.
            case BmpBitsPerPixel.Pixel16:
                info = PixelComponentInfo.Create(3, bpp, 5, 6, 5);
                color = PixelColorType.RGB;
                break;
            case BmpBitsPerPixel.Pixel24:
                info = PixelComponentInfo.Create(3, bpp, 8, 8, 8);
                color = PixelColorType.RGB;
                break;
            case BmpBitsPerPixel.Pixel32 or _:
                info = PixelComponentInfo.Create(4, bpp, 8, 8, 8, 8);
                color = PixelColorType.RGB | PixelColorType.Alpha;
                break;
        }

        return new PixelTypeInfo(bpp)
        {
            AlphaRepresentation = alpha,
            ComponentInfo = info,
            ColorType = color
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
        => new()
        {
            PixelTypeInfo = this.GetPixelTypeInfo()
        };

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
        => new();

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public BmpMetadata DeepClone() => new(this);
}
