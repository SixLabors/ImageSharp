// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;

namespace SixLabors.ImageSharp.Formats.Cur;

/// <summary>
/// IcoFrameMetadata.
/// </summary>
public class CurFrameMetadata : IDeepCloneable<CurFrameMetadata>, IDeepCloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CurFrameMetadata"/> class.
    /// </summary>
    public CurFrameMetadata()
    {
    }

    private CurFrameMetadata(CurFrameMetadata metadata)
    {
        this.Compression = metadata.Compression;
        this.HotspotX = metadata.HotspotX;
        this.HotspotY = metadata.HotspotY;
        this.EncodingWidth = metadata.EncodingWidth;
        this.EncodingHeight = metadata.EncodingHeight;
        this.BmpBitsPerPixel = metadata.BmpBitsPerPixel;
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
    /// Can be any number between 0 and 255. Value 0 means a frame height of 256 pixels.
    /// </summary>
    public byte EncodingWidth { get; set; }

    /// <summary>
    /// Gets or sets the encoding height. <br />
    /// Can be any number between 0 and 255. Value 0 means a frame height of 256 pixels.
    /// </summary>
    public byte EncodingHeight { get; set; }

    /// <summary>
    /// Gets or sets the number of bits per pixel.<br/>
    /// Used when <see cref="Compression"/> is <see cref="IconFrameCompression.Bmp"/>
    /// </summary>
    public BmpBitsPerPixel BmpBitsPerPixel { get; set; } = BmpBitsPerPixel.Pixel32;

    /// <inheritdoc/>
    public CurFrameMetadata DeepClone() => new(this);

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    internal void FromIconDirEntry(IconDirEntry entry)
    {
        this.EncodingWidth = entry.Width;
        this.EncodingHeight = entry.Height;
        this.HotspotX = entry.Planes;
        this.HotspotY = entry.BitCount;
    }

    internal IconDirEntry ToIconDirEntry()
    {
        byte colorCount = this.Compression == IconFrameCompression.Png || this.BmpBitsPerPixel > BmpBitsPerPixel.Pixel8
            ? (byte)0
            : (byte)ColorNumerics.GetColorCountForBitDepth((int)this.BmpBitsPerPixel);

        return new()
        {
            Width = this.EncodingWidth,
            Height = this.EncodingHeight,
            Planes = this.HotspotX,
            BitCount = this.HotspotY,
            ColorCount = colorCount
        };
    }
}
