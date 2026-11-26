// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Cur;

/// <summary>
/// Provides CUR-specific metadata for an image frame.
/// </summary>
public class CurFrameMetadata : IFormatFrameMetadata<CurFrameMetadata>
{
    private ReadOnlyMemory<Color>? colorTable;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurFrameMetadata"/> class.
    /// </summary>
    public CurFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CurFrameMetadata"/> class by copying another instance.
    /// </summary>
    /// <param name="other">The metadata to copy.</param>
    private CurFrameMetadata(CurFrameMetadata other)
    {
        this.Compression = other.Compression;
        this.HotspotX = other.HotspotX;
        this.HotspotY = other.HotspotY;
        this.EncodingWidth = other.EncodingWidth;
        this.EncodingHeight = other.EncodingHeight;
        this.BmpBitsPerPixel = other.BmpBitsPerPixel;
        this.BmpColorType = other.BmpColorType;

        if (other.ColorTable?.Length > 0)
        {
            this.ColorTable = other.ColorTable.Value.ToArray();
        }
    }

    /// <summary>
    /// Gets or sets the frame compression format.
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
    /// Gets or sets the encoded width.
    /// A value of zero represents 256 pixels or greater.
    /// </summary>
    public byte? EncodingWidth { get; set; }

    /// <summary>
    /// Gets or sets the encoded height.
    /// A value of zero represents 256 pixels or greater.
    /// </summary>
    public byte? EncodingHeight { get; set; }

    /// <summary>
    /// Gets or sets the number of bits per pixel.<br/>
    /// Used when <see cref="Compression"/> is <see cref="IconFrameCompression.Bmp"/>
    /// </summary>
    public BmpBitsPerPixel BmpBitsPerPixel { get; set; } = BmpBitsPerPixel.Bit32;

    /// <summary>
    /// Gets or sets the color type of the embedded bitmap.
    /// </summary>
    /// <remarks>
    /// The decoder sets the color type of the decoded bitmap. The encoder does not read this value. The icon format
    /// permits only BI_RGB. Thus the encoder writes a palette at 8 bits per pixel or fewer, 5-5-5 BGR at 16, BGR at 24,
    /// and BGRA at 32.
    /// </remarks>
    public BmpColorType BmpColorType { get; set; } = BmpColorType.Bgra;

    /// <summary>
    /// Gets or sets the color table, if any.
    /// The underlying pixel format is represented by <see cref="Bgr24"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The color table has more than 256 entries.</exception>
    public ReadOnlyMemory<Color>? ColorTable
    {
        get => this.colorTable;
        set
        {
            IconDirEntry.GuardColorTable(value, nameof(value));
            this.colorTable = value;
        }
    }

    /// <inheritdoc/>
    public static CurFrameMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
    {
        if (!metadata.PixelTypeInfo.HasValue)
        {
            return new CurFrameMetadata
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

        return new CurFrameMetadata
        {
            BmpBitsPerPixel = bbpp,
            Compression = compression,
            EncodingWidth = IconDirEntry.ClampEncodingDimension(metadata.EncodingWidth),
            EncodingHeight = IconDirEntry.ClampEncodingDimension(metadata.EncodingHeight)
        };
    }

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
        => new()
        {
            PixelTypeInfo = this.GetPixelTypeInfo(),
            EncodingWidth = this.EncodingWidth,
            EncodingHeight = this.EncodingHeight
        };

    /// <inheritdoc/>
    public void AfterFrameApply<TPixel>(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        float ratioX = destination.Width / (float)source.Width;
        float ratioY = destination.Height / (float)source.Height;
        this.EncodingWidth = IconDirEntry.ScaleEncodingDimension(this.EncodingWidth, destination.Width, ratioX);
        this.EncodingHeight = IconDirEntry.ScaleEncodingDimension(this.EncodingHeight, destination.Height, ratioY);
        this.ColorTable = null;
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public CurFrameMetadata DeepClone() => new(this);

    /// <summary>
    /// Copies the observable CUR directory values from an entry.
    /// </summary>
    /// <param name="entry">The source directory entry.</param>
    internal void FromIconDirEntry(IconDirEntry entry)
    {
        this.EncodingWidth = entry.Width;
        this.EncodingHeight = entry.Height;
        this.HotspotX = entry.Planes;
        this.HotspotY = entry.BitCount;
    }

    /// <summary>
    /// Creates a CUR directory entry from this metadata.
    /// </summary>
    /// <param name="size">The source frame size.</param>
    /// <returns>The CUR directory entry.</returns>
    internal IconDirEntry ToIconDirEntry(Size size)
    {
        byte colorCount = this.Compression == IconFrameCompression.Png || this.BmpBitsPerPixel > BmpBitsPerPixel.Bit8
            ? (byte)0
            : (byte)ColorNumerics.GetColorCountForBitDepth((int)this.BmpBitsPerPixel);

        return new IconDirEntry
        {
            Width = IconDirEntry.ClampEncodingDimension(this.EncodingWidth ?? size.Width),
            Height = IconDirEntry.ClampEncodingDimension(this.EncodingHeight ?? size.Height),
            Planes = this.HotspotX,
            BitCount = this.HotspotY,
            ColorCount = colorCount
        };
    }

    /// <summary>
    /// Gets the pixel layout represented by this metadata.
    /// </summary>
    /// <returns>The represented pixel layout.</returns>
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

                case BmpBitsPerPixel.Bit16:
                    // The icon encoder writes BI_RGB bitmaps, and a 16-bit BI_RGB bitmap has the 5-5-5 layout.
                    info = PixelComponentInfo.Create(3, bpp, 5, 5, 5);

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
}
