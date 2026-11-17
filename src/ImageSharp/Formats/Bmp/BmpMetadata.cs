// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
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
        this.ColorType = other.ColorType;
        this.InfoHeaderType = other.InfoHeaderType;
        this.Compression = other.Compression;

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
    /// Gets or sets the color type.
    /// </summary>
    public BmpColorType ColorType { get; set; } = BmpColorType.Bgr;

    /// <summary>
    /// Gets or sets the compression of the pixel data.
    /// </summary>
    public BmpCompression Compression { get; set; } = BmpCompression.None;

    /// <summary>
    /// Gets or sets the color table, if any.
    /// </summary>
    public ReadOnlyMemory<Color>? ColorTable { get; set; }

    /// <inheritdoc/>
    public static BmpMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
    {
        int bpp = metadata.PixelTypeInfo.BitsPerPixel;
        bool alpha = metadata.PixelTypeInfo.AlphaRepresentation is not PixelAlphaRepresentation.None;

        // A green channel of 6 bits gives the 5-6-5 color type. Other 16-bit sources use 5-5-5, with 1 bit of alpha
        // when the source has alpha.
        PixelComponentInfo? components = metadata.PixelTypeInfo.ComponentInfo;
        bool green6 = components.HasValue && components.Value.ComponentCount >= 3 && components.Value.GetComponentPrecision(1) == 6;

        return bpp switch
        {
            1 => new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Bit1, ColorType = BmpColorType.Palette },
            2 => new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Bit2, ColorType = BmpColorType.Palette },
            <= 4 => new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Bit4, ColorType = BmpColorType.Palette },
            <= 8 => new BmpMetadata { BitsPerPixel = BmpBitsPerPixel.Bit8, ColorType = BmpColorType.Palette },
            <= 16 => new BmpMetadata
            {
                BitsPerPixel = BmpBitsPerPixel.Bit16,
                ColorType = green6 ? BmpColorType.Bgr565 : alpha ? BmpColorType.Bgra5551 : BmpColorType.Bgr,
                InfoHeaderType = BmpInfoHeaderType.WinVersion3
            },
            <= 24 => new BmpMetadata
            {
                BitsPerPixel = BmpBitsPerPixel.Bit24,
                ColorType = BmpColorType.Bgr,
                InfoHeaderType = BmpInfoHeaderType.WinVersion4
            },
            _ => new BmpMetadata
            {
                BitsPerPixel = BmpBitsPerPixel.Bit32,
                ColorType = alpha ? BmpColorType.Bgra : BmpColorType.Bgr,
                InfoHeaderType = BmpInfoHeaderType.WinVersion5
            }
        };
    }

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        // Describe the output of the encoder, which corrects a bit depth that does not fit the color type.
        int bpp = (int)BmpConstants.GetBitsPerPixel(this.ColorType, this.BitsPerPixel);

        // A bitmap stores blue first: in the first byte, or in the least significant bits of a 16-bit pixel. The color
        // type is BGR, and the component precisions follow the same order.
        PixelComponentInfo info;
        PixelColorType color = PixelColorType.BGR;
        PixelAlphaRepresentation alpha = PixelAlphaRepresentation.None;
        switch (this.ColorType)
        {
            case BmpColorType.Palette:
                info = PixelComponentInfo.Create(1, bpp, bpp);
                color = PixelColorType.Indexed;
                break;
            case BmpColorType.Bgra:
                info = PixelComponentInfo.Create(4, bpp, 8, 8, 8, 8);
                color = PixelColorType.BGR | PixelColorType.Alpha;
                alpha = PixelAlphaRepresentation.Unassociated;
                break;
            case BmpColorType.Bgr565:
                info = PixelComponentInfo.Create(3, bpp, 5, 6, 5);
                break;
            case BmpColorType.Bgra5551:
                info = PixelComponentInfo.Create(4, bpp, 5, 5, 5, 1);
                color = PixelColorType.BGR | PixelColorType.Alpha;
                alpha = PixelAlphaRepresentation.Unassociated;
                break;
            case BmpColorType.Bgr or _:
                info = bpp == 16 ? PixelComponentInfo.Create(3, bpp, 5, 5, 5) : PixelComponentInfo.Create(3, bpp, 8, 8, 8);
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
            EncodingType = this.ColorType is BmpColorType.Palette
                ? EncodingType.Lossy
                : EncodingType.Lossless,
            PixelTypeInfo = this.GetPixelTypeInfo()
        };

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public BmpMetadata DeepClone() => new(this);

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
        => this.ColorTable = null;
}
