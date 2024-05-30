// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tga;

/// <summary>
/// Provides TGA specific metadata information for the image.
/// </summary>
public class TgaMetadata : IFormatMetadata<TgaMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TgaMetadata"/> class.
    /// </summary>
    public TgaMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TgaMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private TgaMetadata(TgaMetadata other)
        => this.BitsPerPixel = other.BitsPerPixel;

    /// <summary>
    /// Gets or sets the number of bits per pixel.
    /// </summary>
    public TgaBitsPerPixel BitsPerPixel { get; set; } = TgaBitsPerPixel.Pixel24;

    /// <summary>
    /// Gets or sets the number of alpha bits per pixel.
    /// </summary>
    public byte AlphaChannelBits { get; set; }

    /// <inheritdoc/>
    public static TgaMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
    {
        // TODO: AlphaChannelBits is not used during encoding.
        int bpp = metadata.PixelTypeInfo.BitsPerPixel;
        return bpp switch
        {
            <= 8 => new TgaMetadata { BitsPerPixel = TgaBitsPerPixel.Pixel8 },
            <= 16 => new TgaMetadata { BitsPerPixel = TgaBitsPerPixel.Pixel16 },
            <= 24 => new TgaMetadata { BitsPerPixel = TgaBitsPerPixel.Pixel24 },
            _ => new TgaMetadata { BitsPerPixel = TgaBitsPerPixel.Pixel32 }
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
    {
        int bpp = (int)this.BitsPerPixel;

        PixelComponentInfo info;
        PixelColorType color;
        PixelAlphaRepresentation alpha;
        switch (this.BitsPerPixel)
        {
            case TgaBitsPerPixel.Pixel8:
                info = PixelComponentInfo.Create(1, bpp, 8);
                color = PixelColorType.Luminance;
                alpha = PixelAlphaRepresentation.None;
                break;
            case TgaBitsPerPixel.Pixel16:
                info = PixelComponentInfo.Create(1, bpp, 5, 5, 5, 1);
                color = PixelColorType.BGR | PixelColorType.Alpha;
                alpha = PixelAlphaRepresentation.Unassociated;
                break;
            case TgaBitsPerPixel.Pixel24:
                info = PixelComponentInfo.Create(3, bpp, 8, 8, 8);
                color = PixelColorType.RGB;
                alpha = PixelAlphaRepresentation.None;
                break;
            case TgaBitsPerPixel.Pixel32 or _:
                info = PixelComponentInfo.Create(4, bpp, 8, 8, 8, 8);
                color = PixelColorType.RGB | PixelColorType.Alpha;
                alpha = PixelAlphaRepresentation.Unassociated;
                break;
        }

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
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public TgaMetadata DeepClone() => new(this);
}
