// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

// TODO: Add color table information.
namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Provides Bmp specific metadata information for the image.
/// </summary>
public class BmpMetadata : IFormatMetadata<BmpMetadata>
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

        if (other.ColorTable?.Length > 0)
        {
            this.ColorTable = other.ColorTable.Value.ToArray();
        }
    }

    /// <summary>
    /// Gets or sets the bitmap info header type.
    /// </summary>
    public BmpInfoHeaderType InfoHeaderType { get; set; }

    /// <summary>
    /// Gets or sets the number of bits per pixel.
    /// </summary>
    public BmpBitsPerPixel BitsPerPixel { get; set; } = BmpBitsPerPixel.Bit24;

    /// <summary>
    /// Gets or sets the color table, if any.
    /// </summary>
    public ReadOnlyMemory<Color>? ColorTable { get; set; }

    /// <inheritdoc/>
    public static BmpMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
    {
        int bpp = metadata.PixelTypeInfo.BitsPerPixel;
        return bpp switch
        {
            1 => new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Bit1 },
            2 => new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Bit2 },
            <= 4 => new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Bit4 },
            <= 8 => new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Bit8 },
            <= 16 => new BmpMetadata
            {
                BitsPerPixel = BmpBitsPerPixel.Bit16,
                InfoHeaderType = BmpInfoHeaderType.WinVersion3
            },
            <= 24 => new BmpMetadata
            {
                BitsPerPixel = BmpBitsPerPixel.Bit24,
                InfoHeaderType = BmpInfoHeaderType.WinVersion4
            },
            _ => new BmpMetadata
            {
                BitsPerPixel = BmpBitsPerPixel.Bit32,
                InfoHeaderType = BmpInfoHeaderType.WinVersion5
            }
        };
    }

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
            case BmpBitsPerPixel.Bit1:
                info = PixelComponentInfo.Create(1, bpp, 1);
                color = PixelColorType.Indexed;
                break;
            case BmpBitsPerPixel.Bit2:
                info = PixelComponentInfo.Create(1, bpp, 2);
                color = PixelColorType.Indexed;
                break;
            case BmpBitsPerPixel.Bit4:
                info = PixelComponentInfo.Create(1, bpp, 4);
                color = PixelColorType.Indexed;
                break;
            case BmpBitsPerPixel.Bit8:
                info = PixelComponentInfo.Create(1, bpp, 8);
                color = PixelColorType.Indexed;
                break;

            // Could be 555 with padding but 565 is more common in newer bitmaps and offers
            // greater accuracy due to extra green precision.
            case BmpBitsPerPixel.Bit16:
                info = PixelComponentInfo.Create(3, bpp, 5, 6, 5);
                color = PixelColorType.RGB;
                break;
            case BmpBitsPerPixel.Bit24:
                info = PixelComponentInfo.Create(3, bpp, 8, 8, 8);
                color = PixelColorType.RGB;
                break;
            case BmpBitsPerPixel.Bit32 or _:
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
            EncodingType = this.BitsPerPixel <= BmpBitsPerPixel.Bit8
                ? EncodingType.Lossy
                : EncodingType.Lossless,
            PixelTypeInfo = this.GetPixelTypeInfo()
        };

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public BmpMetadata DeepClone() => new(this);

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
        => this.ColorTable = null;
}
