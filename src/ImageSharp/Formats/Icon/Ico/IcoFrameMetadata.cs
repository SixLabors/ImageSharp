// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon.Ico;

/// <summary>
/// IcoFrameMetadata. TODO: Remove base class and merge into this class.
/// </summary>
public class IcoFrameMetadata : IDeepCloneable<IcoFrameMetadata>, IDeepCloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IcoFrameMetadata"/> class.
    /// </summary>
    public IcoFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IcoFrameMetadata"/> class.
    /// </summary>
    /// <param name="width">width</param>
    /// <param name="height">height</param>
    /// <param name="colorCount">colorCount</param>
    public IcoFrameMetadata(byte width, byte height, byte colorCount)
    {
        this.EncodingWidth = width;
        this.EncodingHeight = height;
        this.ColorCount = colorCount;
    }

    /// <inheritdoc cref="IcoFrameMetadata()"/>
    public IcoFrameMetadata(IcoFrameMetadata metadata)
    {
        this.EncodingWidth = metadata.EncodingWidth;
        this.EncodingHeight = metadata.EncodingHeight;
        this.ColorCount = metadata.ColorCount;
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
    public IcoFrameMetadata DeepClone() => new(this);

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    internal void FromIconDirEntry(in IconDirEntry entry)
    {
        this.EncodingWidth = entry.Width;
        this.EncodingHeight = entry.Height;
        this.ColorCount = entry.ColorCount;
    }

    internal IconDirEntry ToIconDirEntry() => new()
    {
        Width = this.EncodingWidth,
        Height = this.EncodingHeight,
        ColorCount = this.ColorCount,
        Planes = 1,
        BitCount = this.Compression switch
        {
            IconFrameCompression.Bmp => (ushort)this.BitsPerPixel,
            _ => 0,
        },
    };
}
