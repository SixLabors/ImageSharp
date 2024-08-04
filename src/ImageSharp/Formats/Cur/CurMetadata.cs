// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Cur;

/// <summary>
/// Provides Cur specific metadata information for the image.
/// </summary>
public class CurMetadata : IFormatMetadata<CurMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CurMetadata"/> class.
    /// </summary>
    public CurMetadata()
    {
    }

    private CurMetadata(CurMetadata other)
    {
        this.Compression = other.Compression;
        this.HotspotX = other.HotspotX;
        this.HotspotY = other.HotspotY;
        this.EncodingWidth = other.EncodingWidth;
        this.EncodingHeight = other.EncodingHeight;
        this.BmpBitsPerPixel = other.BmpBitsPerPixel;

        if (other.ColorTable?.Length > 0)
        {
            this.ColorTable = other.ColorTable.Value.ToArray();
        }
    }

    /// <summary>
    /// Gets or sets the frame compressions format. Derived from the root frame.
    /// </summary>
    public IconFrameCompression Compression { get; set; }

    /// <summary>
    /// Gets or sets the horizontal coordinates of the hotspot in number of pixels from the left. Derived from the root frame.
    /// </summary>
    public ushort HotspotX { get; set; }

    /// <summary>
    /// Gets or sets the vertical coordinates of the hotspot in number of pixels from the top. Derived from the root frame.
    /// </summary>
    public ushort HotspotY { get; set; }

    /// <summary>
    /// Gets or sets the encoding width. <br />
    /// Can be any number between 0 and 255. Value 0 means a frame height of 256 pixels or greater. Derived from the root frame.
    /// </summary>
    public byte EncodingWidth { get; set; }

    /// <summary>
    /// Gets or sets the encoding height. <br />
    /// Can be any number between 0 and 255. Value 0 means a frame height of 256 pixels or greater. Derived from the root frame.
    /// </summary>
    public byte EncodingHeight { get; set; }

    /// <summary>
    /// Gets or sets the number of bits per pixel.<br/>
    /// Used when <see cref="Compression"/> is <see cref="IconFrameCompression.Bmp"/>
    /// </summary>
    public BmpBitsPerPixel BmpBitsPerPixel { get; set; } = BmpBitsPerPixel.Bit32;

    /// <summary>
    /// Gets or sets the color table, if any. Derived from the root frame.<br/>
    /// The underlying pixel format is represented by <see cref="Bgr24"/>.
    /// </summary>
    public ReadOnlyMemory<Color>? ColorTable { get; set; }

    /// <inheritdoc/>
    public static CurMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
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

        return new CurMetadata
        {
            BmpBitsPerPixel = bbpp,
            Compression = compression,
            ColorTable = compression == IconFrameCompression.Bmp ? metadata.ColorTable : null
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
            PixelTypeInfo = this.GetPixelTypeInfo(),
            ColorTable = this.ColorTable
        };

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public CurMetadata DeepClone() => new(this);
}
