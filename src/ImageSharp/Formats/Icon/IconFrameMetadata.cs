// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon;

/// <summary>
/// IconFrameMetadata
/// </summary>
public abstract class IconFrameMetadata : IDeepCloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IconFrameMetadata"/> class.
    /// </summary>
    protected IconFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IconFrameMetadata"/> class.
    /// </summary>
    /// <param name="width">width</param>
    /// <param name="height">height</param>
    /// <param name="colorCount">colorCount</param>
    /// <param name="field1">field1</param>
    /// <param name="field2">field2</param>
    protected IconFrameMetadata(byte width, byte height, byte colorCount, ushort field1, ushort field2)
    {
        this.EncodingWidth = width;
        this.EncodingHeight = height;
        this.ColorCount = colorCount;
        this.Field1 = field1;
        this.Field2 = field2;
    }

    /// <inheritdoc cref="IconFrameMetadata()"/>
    protected IconFrameMetadata(IconFrameMetadata metadata)
    {
        this.EncodingWidth = metadata.EncodingWidth;
        this.EncodingHeight = metadata.EncodingHeight;
        this.ColorCount = metadata.ColorCount;
        this.Field1 = metadata.Field1;
        this.Field2 = metadata.Field2;
    }

    /// <summary>
    /// Gets or sets icoFrameCompression.
    /// </summary>
    public IconFrameCompression Compression { get; protected set; }

    /// <summary>
    /// Gets or sets ColorCount field. <br />
    /// Specifies number of colors in the color palette. Should be 0 if the image does not use a color palette.
    /// </summary>
    // TODO: BmpMetadata does not supported palette yet.
    public byte ColorCount { get; set; }

    /// <summary>
    /// Gets or sets Planes field. <br />
    /// In ICO format: Specifies color planes. Should be 0 or 1. <br />
    /// In CUR format: Specifies the horizontal coordinates of the hotspot in number of pixels from the left.
    /// </summary>
    protected ushort Field1 { get; set; }

    /// <summary>
    /// Gets or sets BitCount field. <br />
    /// In ICO format: Specifies bits per pixel. <br />
    /// In CUR format: Specifies the vertical coordinates of the hotspot in number of pixels from the top.
    /// </summary>
    protected ushort Field2 { get; set; }

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

    /// <inheritdoc/>
    public abstract IDeepCloneable DeepClone();

    internal void FromIconDirEntry(in IconDirEntry metadata)
    {
        this.EncodingWidth = metadata.Width;
        this.EncodingHeight = metadata.Height;
        this.ColorCount = metadata.ColorCount;
        this.Field1 = metadata.Planes;
        this.Field2 = metadata.BitCount;
    }

    internal IconDirEntry ToIconDirEntry() => new()
    {
        Width = this.EncodingWidth,
        Height = this.EncodingHeight,
        ColorCount = this.ColorCount,
        Planes = this.Field1,
        BitCount = this.Field2,
    };
}
