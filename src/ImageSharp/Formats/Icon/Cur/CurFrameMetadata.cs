// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon.Cur;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="CurFrameMetadata"/> class.
    /// </summary>
    /// <param name="width">width</param>
    /// <param name="height">height</param>
    /// <param name="colorCount">colorCount</param>
    /// <param name="hotspotX">hotspotX</param>
    /// <param name="hotspotY">hotspotY</param>
    public CurFrameMetadata(byte width, byte height, byte colorCount, ushort hotspotX, ushort hotspotY)
    {
        this.EncodingWidth = width;
        this.EncodingHeight = height;
        this.ColorCount = colorCount;
        this.HotspotX = hotspotX;
        this.HotspotY = hotspotY;
    }

    /// <inheritdoc cref="CurFrameMetadata()"/>
    public CurFrameMetadata(CurFrameMetadata metadata)
    {
        this.EncodingWidth = metadata.EncodingWidth;
        this.EncodingHeight = metadata.EncodingHeight;
        this.ColorCount = metadata.ColorCount;
        this.HotspotX = metadata.HotspotX;
        this.HotspotY = metadata.HotspotY;
        this.Compression = metadata.Compression;
    }

    /// <summary>
    /// Gets or sets icoFrameCompression.
    /// </summary>
    public IconFrameCompression Compression { get; set; }

    /// <summary>
    /// Gets or sets ColorCount field. <br />
    /// Specifies number of colors in the color palette. Should be 0 if the image does not use a color palette.
    /// </summary>
    // TODO: BmpMetadata does not supported palette yet.
    public byte ColorCount { get; set; }

    /// <summary>
    /// Gets or sets Specifies the horizontal coordinates of the hotspot in number of pixels from the left.
    /// </summary>
    public ushort HotspotX { get; set; }

    /// <summary>
    /// Gets or sets Specifies the vertical coordinates of the hotspot in number of pixels from the top.
    /// </summary>
    public ushort HotspotY { get; set; }

    /// <summary>
    /// Gets or sets Height field. <br />
    /// Specifies image height in pixels. Can be any number between 0 and 255. Value 0 means image height is 256 pixels.
    /// </summary>
    public byte EncodingHeight { get; set; }

    /// <summary>
    /// Gets or sets Width field. <br />
    /// Specifies image width in pixels. Can be any number between 0 and 255. Value 0 means image width is 256 pixels.
    /// </summary>
    public byte EncodingWidth { get; set; }

    /// <inheritdoc cref="Bmp.BmpMetadata.BitsPerPixel" />
    public Bmp.BmpBitsPerPixel BitsPerPixel { get; set; } = Bmp.BmpBitsPerPixel.Pixel24;

    /// <inheritdoc/>
    public CurFrameMetadata DeepClone() => new(this);

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    internal void FromIconDirEntry(in IconDirEntry entry)
    {
        this.EncodingWidth = entry.Width;
        this.EncodingHeight = entry.Height;
        this.ColorCount = entry.ColorCount;
        this.HotspotX = entry.Planes;
        this.HotspotY = entry.BitCount;
    }

    internal IconDirEntry ToIconDirEntry() => new()
    {
        Width = this.EncodingWidth,
        Height = this.EncodingHeight,
        ColorCount = this.ColorCount,
        Planes = this.HotspotX,
        BitCount = this.HotspotY,
    };
}
