// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Provides ANI-specific metadata for an image frame.
/// </summary>
public class AniFrameMetadata : IFormatFrameMetadata<AniFrameMetadata>
{
    private ReadOnlyMemory<Color>? colorTable;

    /// <summary>
    /// Initializes a new instance of the <see cref="AniFrameMetadata"/> class.
    /// </summary>
    public AniFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AniFrameMetadata"/> class by copying another instance.
    /// </summary>
    /// <param name="other">The metadata to copy.</param>
    private AniFrameMetadata(AniFrameMetadata other)
    {
        this.FrameDelay = other.FrameDelay;
        this.SequenceNumber = other.SequenceNumber;
        this.EncodingWidth = other.EncodingWidth;
        this.EncodingHeight = other.EncodingHeight;
        this.FrameFormat = other.FrameFormat;
        this.Compression = other.Compression;
        this.BmpBitsPerPixel = other.BmpBitsPerPixel;
        this.BmpColorType = other.BmpColorType;
        this.HotspotX = other.HotspotX;
        this.HotspotY = other.HotspotY;

        if (other.ColorTable?.Length > 0)
        {
            this.ColorTable = other.ColorTable.Value.ToArray();
        }
    }

    /// <summary>
    /// Gets or sets the frame display time in sixtieths of a second.
    /// </summary>
    public uint FrameDelay { get; set; }

    /// <summary>
    /// Gets or sets the animation sequence number. Frames with the same positive value play one stored frame. Adjacent
    /// frames with the same value are its resolution variants, and the encoder stores them once. An adjacent frame whose
    /// size and depth are already in the step starts a new step, which plays the same stored frame. A non-positive value
    /// encodes the frame as its own animation step.
    /// </summary>
    /// <remarks>
    /// The decoder gives each frame the number of the stored frame that it plays. Thus a frame that plays in two steps
    /// has the same number in both. For each number, the encoder writes only the frames of the first step, and it
    /// ignores the pixels of later steps. To change the pixels of one step only, give its frames a new number.
    /// </remarks>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// Gets or sets the encoded frame width.
    /// A value of zero represents 256 pixels or greater in ICO and CUR resources.
    /// </summary>
    public byte? EncodingWidth { get; set; }

    /// <summary>
    /// Gets or sets the encoded frame height.
    /// A value of zero represents 256 pixels or greater in ICO and CUR resources.
    /// </summary>
    public byte? EncodingHeight { get; set; }

    /// <summary>
    /// Gets or sets the format used for this frame resource.
    /// </summary>
    public AniFrameFormat FrameFormat { get; set; }

    /// <summary>
    /// Gets or sets the embedded ICO or CUR compression format.
    /// </summary>
    public IconFrameCompression Compression { get; set; } = IconFrameCompression.Png;

    /// <summary>
    /// Gets or sets the embedded bitmap bits per pixel.
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
    /// Gets or sets the embedded bitmap color table.
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

    /// <summary>
    /// Gets or sets the horizontal cursor hotspot in pixels from the left.
    /// </summary>
    public ushort HotspotX { get; set; }

    /// <summary>
    /// Gets or sets the vertical cursor hotspot in pixels from the top.
    /// </summary>
    public ushort HotspotY { get; set; }

    /// <inheritdoc/>
    public static AniFrameMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
    {
        int bitsPerPixel = metadata.PixelTypeInfo?.BitsPerPixel ?? 32;
        BmpBitsPerPixel bmpBitsPerPixel = bitsPerPixel switch
        {
            1 => BmpBitsPerPixel.Bit1,
            2 => BmpBitsPerPixel.Bit2,
            <= 4 => BmpBitsPerPixel.Bit4,
            <= 8 => BmpBitsPerPixel.Bit8,
            <= 16 => BmpBitsPerPixel.Bit16,
            <= 24 => BmpBitsPerPixel.Bit24,
            _ => BmpBitsPerPixel.Bit32
        };

        return new AniFrameMetadata
        {
            FrameDelay = (uint)Math.Round(metadata.Duration.TotalSeconds * 60),
            EncodingWidth = IconDirEntry.ClampEncodingDimension(metadata.EncodingWidth),
            EncodingHeight = IconDirEntry.ClampEncodingDimension(metadata.EncodingHeight),
            Compression = bmpBitsPerPixel is BmpBitsPerPixel.Bit32 ? IconFrameCompression.Png : IconFrameCompression.Bmp,
            BmpBitsPerPixel = bmpBitsPerPixel
        };
    }

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
        => new()
        {
            Duration = TimeSpan.FromSeconds(this.FrameDelay / 60D),
            EncodingWidth = this.EncodingWidth,
            EncodingHeight = this.EncodingHeight,
            PixelTypeInfo = this.GetPixelTypeInfo()
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
    public AniFrameMetadata DeepClone() => new(this);

    /// <summary>
    /// Gets the pixel layout represented by the embedded resource metadata.
    /// </summary>
    /// <returns>The represented pixel layout.</returns>
    private PixelTypeInfo GetPixelTypeInfo()
    {
        int bitsPerPixel = (int)this.BmpBitsPerPixel;
        PixelComponentInfo componentInfo;
        PixelColorType colorType;
        PixelAlphaRepresentation alphaRepresentation = PixelAlphaRepresentation.None;

        if (this.Compression is IconFrameCompression.Png)
        {
            bitsPerPixel = 32;
            componentInfo = PixelComponentInfo.Create(4, bitsPerPixel, 8, 8, 8, 8);
            colorType = PixelColorType.RGB | PixelColorType.Alpha;
            alphaRepresentation = PixelAlphaRepresentation.Unassociated;
        }
        else
        {
            switch (this.BmpBitsPerPixel)
            {
                case BmpBitsPerPixel.Bit1:
                    componentInfo = PixelComponentInfo.Create(1, bitsPerPixel, 1);
                    colorType = PixelColorType.Binary;
                    break;
                case BmpBitsPerPixel.Bit2:
                    componentInfo = PixelComponentInfo.Create(1, bitsPerPixel, 2);
                    colorType = PixelColorType.Indexed;
                    break;
                case BmpBitsPerPixel.Bit4:
                    componentInfo = PixelComponentInfo.Create(1, bitsPerPixel, 4);
                    colorType = PixelColorType.Indexed;
                    break;
                case BmpBitsPerPixel.Bit8:
                    componentInfo = PixelComponentInfo.Create(1, bitsPerPixel, 8);
                    colorType = PixelColorType.Indexed;
                    break;

                case BmpBitsPerPixel.Bit16:
                    // The icon encoder writes BI_RGB bitmaps, and a 16-bit BI_RGB bitmap has the 5-5-5 layout.
                    componentInfo = PixelComponentInfo.Create(3, bitsPerPixel, 5, 5, 5);

                    colorType = PixelColorType.RGB;
                    break;
                case BmpBitsPerPixel.Bit24:
                    componentInfo = PixelComponentInfo.Create(3, bitsPerPixel, 8, 8, 8);
                    colorType = PixelColorType.RGB;
                    break;
                case BmpBitsPerPixel.Bit32 or _:
                    componentInfo = PixelComponentInfo.Create(4, bitsPerPixel, 8, 8, 8, 8);
                    colorType = PixelColorType.RGB | PixelColorType.Alpha;
                    alphaRepresentation = PixelAlphaRepresentation.Unassociated;
                    break;
            }
        }

        return new PixelTypeInfo(bitsPerPixel)
        {
            AlphaRepresentation = alphaRepresentation,
            ComponentInfo = componentInfo,
            ColorType = colorType
        };
    }
}
