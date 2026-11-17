// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ico;

/// <summary>
/// Provides ICO-specific metadata for an image.
/// </summary>
public class IcoMetadata : IFormatMetadata<IcoMetadata>
{
    private ReadOnlyMemory<Color>? colorTable;

    /// <summary>
    /// Initializes a new instance of the <see cref="IcoMetadata"/> class.
    /// </summary>
    public IcoMetadata()
    {
    }

    private IcoMetadata(IcoMetadata other)
    {
        this.Compression = other.Compression;
        this.BmpBitsPerPixel = other.BmpBitsPerPixel;
        this.BmpColorType = other.BmpColorType;

        if (other.ColorTable?.Length > 0)
        {
            this.ColorTable = other.ColorTable.Value.ToArray();
        }
    }

    /// <summary>
    /// Gets or sets the root frame compression format.
    /// </summary>
    public IconFrameCompression Compression { get; set; }

    /// <summary>
    /// Gets or sets the number of bits per pixel.<br/>
    /// Used when <see cref="Compression"/> is <see cref="IconFrameCompression.Bmp"/>
    /// </summary>
    public BmpBitsPerPixel BmpBitsPerPixel { get; set; } = BmpBitsPerPixel.Bit32;

    /// <summary>
    /// Gets or sets the color type of the embedded bitmap.
    /// </summary>
    /// <remarks>
    /// The decoder sets the color type of the decoded bitmap. The icon format permits only BI_RGB, so the encoder writes
    /// a palette for 8 bits per pixel or fewer, and BGR without masks for more bits per pixel. At 16 bits per pixel, the
    /// layout is 5-5-5.
    /// </remarks>
    public BmpColorType BmpColorType { get; set; } = BmpColorType.Bgra;

    /// <summary>
    /// Gets or sets the root frame color table, if any.<br/>
    /// The underlying pixel format is represented by <see cref="Bgr24"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The color table has more than 256 entries.</exception>
    public ReadOnlyMemory<Color>? ColorTable
    {
        get => this.colorTable;
        set
        {
            if (value.HasValue)
            {
                // ICONDIRENTRY stores the color count in one byte, and only bitmaps of 8 bits or fewer have a
                // color table: https://learn.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)
                Guard.MustBeLessThanOrEqualTo(value.Value.Length, 256, nameof(value));
            }

            this.colorTable = value;
        }
    }

    /// <inheritdoc/>
    public static IcoMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
    {
        int bpp = metadata.PixelTypeInfo.BitsPerPixel;
        BmpBitsPerPixel bbpp = bpp switch
        {
            1 => BmpBitsPerPixel.Bit1,
            2 => BmpBitsPerPixel.Bit2,
            <= 4 => BmpBitsPerPixel.Bit4,
            <= 8 => BmpBitsPerPixel.Bit8,
            <= 16 => BmpBitsPerPixel.Bit16,
            <= 24 => BmpBitsPerPixel.Bit24,
            _ => BmpBitsPerPixel.Bit32
        };

        IconFrameCompression compression = IconFrameCompression.Bmp;
        if (bbpp is BmpBitsPerPixel.Bit32)
        {
            compression = IconFrameCompression.Png;
        }

        return new IcoMetadata
        {
            BmpBitsPerPixel = bbpp,
            Compression = compression
        };
    }

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        int bpp = (int)this.BmpBitsPerPixel;
        PixelComponentInfo info;
        PixelColorType color;
        PixelAlphaRepresentation alpha = PixelAlphaRepresentation.None;

        if (this.Compression is IconFrameCompression.Png)
        {
            bpp = 32;
            info = PixelComponentInfo.Create(4, bpp, 8, 8, 8, 8);
            color = PixelColorType.RGB | PixelColorType.Alpha;
            alpha = PixelAlphaRepresentation.Unassociated;
        }
        else
        {
            switch (this.BmpBitsPerPixel)
            {
                case BmpBitsPerPixel.Bit1:
                    info = PixelComponentInfo.Create(1, bpp, 1);
                    color = PixelColorType.Binary;
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

                case BmpBitsPerPixel.Bit16:
                    info = this.BmpColorType is BmpColorType.Bgr565
                        ? PixelComponentInfo.Create(3, bpp, 5, 6, 5)
                        : PixelComponentInfo.Create(3, bpp, 5, 5, 5);

                    color = PixelColorType.RGB;
                    break;
                case BmpBitsPerPixel.Bit24:
                    info = PixelComponentInfo.Create(3, bpp, 8, 8, 8);
                    color = PixelColorType.RGB;
                    break;
                case BmpBitsPerPixel.Bit32 or _:
                    info = PixelComponentInfo.Create(4, bpp, 8, 8, 8, 8);
                    color = PixelColorType.RGB | PixelColorType.Alpha;
                    alpha = PixelAlphaRepresentation.Unassociated;
                    break;
            }
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
            EncodingType = this.Compression == IconFrameCompression.Bmp && this.BmpBitsPerPixel <= BmpBitsPerPixel.Bit8
                ? EncodingType.Lossy
                : EncodingType.Lossless,
            PixelTypeInfo = this.GetPixelTypeInfo()
        };

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
        => this.ColorTable = null;

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public IcoMetadata DeepClone() => new(this);
}
