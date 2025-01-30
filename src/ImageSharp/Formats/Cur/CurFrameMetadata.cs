// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Cur;

/// <summary>
/// IcoFrameMetadata.
/// </summary>
public class CurFrameMetadata : IFormatFrameMetadata<CurFrameMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CurFrameMetadata"/> class.
    /// </summary>
    public CurFrameMetadata()
    {
    }

    private CurFrameMetadata(CurFrameMetadata other)
    {
        this.Compression = other.Compression;
        this.HotspotX = other.HotspotX;
        this.HotspotY = other.HotspotY;
        this.EncodingWidth = other.EncodingWidth;
        this.EncodingHeight = other.EncodingHeight;
        this.BmpBitsPerPixel = other.BmpBitsPerPixel;
    }

    /// <summary>
    /// Gets or sets the frame compressions format.
    /// </summary>
    public IconFrameCompression Compression { get; set; }

    /// <summary>
    /// Gets or sets the horizontal coordinates of the hotspot in number of pixels from the left.
    /// </summary>
    public ushort HotspotX { get; set; }

    /// <summary>
    /// Gets or sets the vertical coordinates of the hotspot in number of pixels from the top.
    /// </summary>
    public ushort HotspotY { get; set; }

    /// <summary>
    /// Gets or sets the encoding width. <br />
    /// Can be any number between 0 and 255. Value 0 means a frame height of 256 pixels or greater.
    /// </summary>
    public byte? EncodingWidth { get; set; }

    /// <summary>
    /// Gets or sets the encoding height. <br />
    /// Can be any number between 0 and 255. Value 0 means a frame height of 256 pixels or greater.
    /// </summary>
    public byte? EncodingHeight { get; set; }

    /// <summary>
    /// Gets or sets the number of bits per pixel.<br/>
    /// Used when <see cref="Compression"/> is <see cref="IconFrameCompression.Bmp"/>
    /// </summary>
    public BmpBitsPerPixel BmpBitsPerPixel { get; set; } = BmpBitsPerPixel.Bit32;

    /// <summary>
    /// Gets or sets the color table, if any.
    /// The underlying pixel format is represented by <see cref="Bgr24"/>.
    /// </summary>
    public ReadOnlyMemory<Color>? ColorTable { get; set; }

    /// <inheritdoc/>
    public static CurFrameMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
    {
        if (!metadata.PixelTypeInfo.HasValue)
        {
            return new()
            {
                BmpBitsPerPixel = BmpBitsPerPixel.Bit32,
                Compression = IconFrameCompression.Png
            };
        }

        int bpp = metadata.PixelTypeInfo.Value.BitsPerPixel;
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

        return new()
        {
            BmpBitsPerPixel = bbpp,
            Compression = compression,
            EncodingWidth = ClampEncodingDimension(metadata.EncodingWidth),
            EncodingHeight = ClampEncodingDimension(metadata.EncodingHeight),
            ColorTable = compression == IconFrameCompression.Bmp ? metadata.ColorTable : null
        };
    }

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
        => new()
        {
            PixelTypeInfo = this.GetPixelTypeInfo(),
            ColorTable = this.ColorTable,
            EncodingWidth = this.EncodingWidth,
            EncodingHeight = this.EncodingHeight
        };

    /// <inheritdoc/>
    public void AfterFrameApply<TPixel>(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        float ratioX = destination.Width / (float)source.Width;
        float ratioY = destination.Height / (float)source.Height;
        this.EncodingWidth = ScaleEncodingDimension(this.EncodingWidth, destination.Width, ratioX);
        this.EncodingHeight = ScaleEncodingDimension(this.EncodingHeight, destination.Height, ratioY);
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public CurFrameMetadata DeepClone() => new(this);

    internal void FromIconDirEntry(IconDirEntry entry)
    {
        this.EncodingWidth = entry.Width;
        this.EncodingHeight = entry.Height;
        this.HotspotX = entry.Planes;
        this.HotspotY = entry.BitCount;
    }

    internal IconDirEntry ToIconDirEntry(Size size)
    {
        byte colorCount = this.Compression == IconFrameCompression.Png || this.BmpBitsPerPixel > BmpBitsPerPixel.Bit8
            ? (byte)0
            : (byte)ColorNumerics.GetColorCountForBitDepth((int)this.BmpBitsPerPixel);

        return new()
        {
            Width = ClampEncodingDimension(this.EncodingWidth ?? size.Width),
            Height = ClampEncodingDimension(this.EncodingHeight ?? size.Height),
            Planes = this.HotspotX,
            BitCount = this.HotspotY,
            ColorCount = colorCount
        };
    }

    private PixelTypeInfo GetPixelTypeInfo()
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

        return new(bpp)
        {
            AlphaRepresentation = alpha,
            ComponentInfo = info,
            ColorType = color
        };
    }

    private static byte ScaleEncodingDimension(byte? value, int destination, float ratio)
    {
        if (value is null)
        {
            return ClampEncodingDimension(destination);
        }

        return ClampEncodingDimension(MathF.Ceiling(value.Value * ratio));
    }

    private static byte ClampEncodingDimension(float? dimension)
        => dimension switch
        {
            // Encoding dimensions can be between 0-256 where 0 means 256 or greater.
            > 255 => 0,
            <= 255 and >= 1 => (byte)dimension,
            _ => 0
        };
}
