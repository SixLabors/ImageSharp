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
        if (bpp == 1)
        {
            return new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel1 };
        }

        if (bpp == 2)
        {
            return new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel2 };
        }

        if (bpp <= 4)
        {
            return new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel4 };
        }

        if (bpp <= 8)
        {
            return new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel8 };
        }

        if (bpp <= 16)
        {
            return new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel16, InfoHeaderType = BmpInfoHeaderType.WinVersion3 };
        }

        if (bpp <= 24)
        {
            return new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel24, InfoHeaderType = BmpInfoHeaderType.WinVersion4 };
        }

        return new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Pixel32, InfoHeaderType = BmpInfoHeaderType.WinVersion5 };
    }

    /// <inheritdoc/>
    public static BmpMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
        => new();

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
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

        PixelComponentInfo info = this.BitsPerPixel switch
        {
            BmpBitsPerPixel.Pixel1 => PixelComponentInfo.Create(1, bpp, 1),
            BmpBitsPerPixel.Pixel2 => PixelComponentInfo.Create(1, bpp, 2),
            BmpBitsPerPixel.Pixel4 => PixelComponentInfo.Create(1, bpp, 4),
            BmpBitsPerPixel.Pixel8 => PixelComponentInfo.Create(1, bpp, 8),

            // Could be 555 with padding but 565 is more common in newer bitmaps and offers
            // greater accuracy due to extra green precision.
            BmpBitsPerPixel.Pixel16 => PixelComponentInfo.Create(3, bpp, 5, 6, 5),
            BmpBitsPerPixel.Pixel24 => PixelComponentInfo.Create(3, bpp, 8, 8, 8),
            BmpBitsPerPixel.Pixel32 or _ => PixelComponentInfo.Create(4, bpp, 8, 8, 8, 8),
        };

        PixelColorType color = this.BitsPerPixel switch
        {
            BmpBitsPerPixel.Pixel1 or
            BmpBitsPerPixel.Pixel2 or
            BmpBitsPerPixel.Pixel4 or
            BmpBitsPerPixel.Pixel8 => PixelColorType.Indexed,
            BmpBitsPerPixel.Pixel16 or
            BmpBitsPerPixel.Pixel24 => PixelColorType.RGB,
            BmpBitsPerPixel.Pixel32 or _ => PixelColorType.RGB | PixelColorType.Alpha,
        };

        return new()
        {
            PixelTypeInfo = new PixelTypeInfo(bpp)
            {
                AlphaRepresentation = alpha,
                ComponentInfo = info,
                ColorType = color
            }
        };
    }

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
        => new();

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public BmpMetadata DeepClone() => new(this);
}
