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
    /// Gets or sets the animation sequence number.
    /// Adjacent frames with the same positive value are grouped as resolution variants in one ANI frame resource.
    /// A non-positive value encodes the frame as its own animation step.
    /// </summary>
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
    /// Gets or sets the embedded bitmap color table.
    /// The underlying pixel format is represented by <see cref="Bgr24"/>.
    /// </summary>
    public ReadOnlyMemory<Color>? ColorTable { get; set; }

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
            EncodingWidth = ClampEncodingDimension(metadata.EncodingWidth),
            EncodingHeight = ClampEncodingDimension(metadata.EncodingHeight),
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

        this.EncodingWidth = ScaleEncodingDimension(this.EncodingWidth, destination.Width, ratioX);
        this.EncodingHeight = ScaleEncodingDimension(this.EncodingHeight, destination.Height, ratioY);
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

                // Windows bitmaps commonly use a 5-6-5 layout for 16-bit color.
                case BmpBitsPerPixel.Bit16:
                    componentInfo = PixelComponentInfo.Create(3, bitsPerPixel, 5, 6, 5);
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

        // ICO and CUR encode dimensions in one byte, where zero represents 256 pixels or greater.
        int source = value.Value is 0 ? 256 : value.Value;
        return ClampEncodingDimension(MathF.Ceiling(source * ratio));
    }

    /// <summary>
    /// Converts a pixel dimension to the one-byte ICO/CUR representation.
    /// </summary>
    /// <param name="dimension">The pixel dimension.</param>
    /// <returns>The encoded dimension.</returns>
    private static byte ClampEncodingDimension(float? dimension)
        => dimension switch
        {
            > 255 => 0,
            >= 1 => (byte)dimension,
            _ => 0
        };
}
