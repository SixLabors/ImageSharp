// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ico;

/// <summary>
/// Provides ICO-specific metadata for an image frame.
/// </summary>
public class IcoFrameMetadata : IFormatFrameMetadata<IcoFrameMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IcoFrameMetadata"/> class.
    /// </summary>
    public IcoFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IcoFrameMetadata"/> class by copying another instance.
    /// </summary>
    /// <param name="other">The metadata to copy.</param>
    private IcoFrameMetadata(IcoFrameMetadata other)
    {
        this.Compression = other.Compression;
        this.EncodingWidth = other.EncodingWidth;
        this.EncodingHeight = other.EncodingHeight;
        this.BmpBitsPerPixel = other.BmpBitsPerPixel;

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
    /// Gets or sets the color table, if any.
    /// The underlying pixel format is represented by <see cref="Bgr24"/>.
    /// </summary>
    public ReadOnlyMemory<Color>? ColorTable { get; set; }

    /// <inheritdoc/>
    public static IcoFrameMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
    {
        if (!metadata.PixelTypeInfo.HasValue)
        {
            return new IcoFrameMetadata
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

        return new IcoFrameMetadata
        {
            BmpBitsPerPixel = bbpp,
            Compression = compression,
            EncodingWidth = ClampEncodingDimension(metadata.EncodingWidth),
            EncodingHeight = ClampEncodingDimension(metadata.EncodingHeight)
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
        this.EncodingWidth = ScaleEncodingDimension(this.EncodingWidth, destination.Width, ratioX);
        this.EncodingHeight = ScaleEncodingDimension(this.EncodingHeight, destination.Height, ratioY);
        this.ColorTable = null;
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public IcoFrameMetadata DeepClone() => new(this);

    /// <summary>
    /// Copies the observable ICO directory values from an entry.
    /// </summary>
    /// <param name="entry">The source directory entry.</param>
    internal void FromIconDirEntry(IconDirEntry entry)
    {
        this.EncodingWidth = entry.Width;
        this.EncodingHeight = entry.Height;
    }

    /// <summary>
    /// Creates an ICO directory entry from this metadata.
    /// </summary>
    /// <param name="size">The source frame size.</param>
    /// <returns>The ICO directory entry.</returns>
    internal IconDirEntry ToIconDirEntry(Size size)
    {
        byte colorCount = this.Compression == IconFrameCompression.Png || this.BmpBitsPerPixel > BmpBitsPerPixel.Bit8
            ? (byte)0
            : (byte)ColorNumerics.GetColorCountForBitDepth((int)this.BmpBitsPerPixel);

        return new IconDirEntry
        {
            Width = ClampEncodingDimension(this.EncodingWidth ?? size.Width),
            Height = ClampEncodingDimension(this.EncodingHeight ?? size.Height),
            Planes = 1,
            ColorCount = colorCount,
            BitCount = this.Compression switch
            {
                IconFrameCompression.Bmp => (ushort)this.BmpBitsPerPixel,
                IconFrameCompression.Png or _ => 32
            }
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

    /// <summary>
    /// Scales an encoded dimension after an image transform.
    /// </summary>
    /// <param name="value">The encoded source dimension.</param>
    /// <param name="destination">The full destination dimension.</param>
    /// <param name="ratio">The destination-to-source scale ratio.</param>
    /// <returns>The encoded destination dimension.</returns>
    private static byte ScaleEncodingDimension(byte? value, int destination, float ratio)
    {
        if (value is null)
        {
            return ClampEncodingDimension(destination);
        }

        // A stored zero represents 256 pixels, so scaling must expand it before applying the transform ratio.
        int source = value.Value is 0 ? 256 : value.Value;
        return ClampEncodingDimension(MathF.Ceiling(source * ratio));
    }

    /// <summary>
    /// Converts a pixel dimension to the one-byte ICO representation.
    /// </summary>
    /// <param name="dimension">The pixel dimension.</param>
    /// <returns>The encoded dimension.</returns>
    private static byte ClampEncodingDimension(float? dimension)
        => dimension switch
        {
            // Encoding dimensions can be between 0-256 where 0 means 256 or greater.
            > 255 => 0,
            <= 255 and >= 1 => (byte)dimension,
            _ => 0
        };
}
