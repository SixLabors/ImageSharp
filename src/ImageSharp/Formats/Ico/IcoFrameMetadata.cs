// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;

namespace SixLabors.ImageSharp.Formats.Ico;

/// <summary>
/// Provides Ico specific metadata information for the image frame.
/// </summary>
public class IcoFrameMetadata : IDeepCloneable<IcoFrameMetadata>, IDeepCloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IcoFrameMetadata"/> class.
    /// </summary>
    public IcoFrameMetadata()
    {
    }

    private IcoFrameMetadata(IcoFrameMetadata metadata)
    {
        this.Compression = metadata.Compression;
        this.EncodingWidth = metadata.EncodingWidth;
        this.EncodingHeight = metadata.EncodingHeight;
        this.BmpBitsPerPixel = metadata.BmpBitsPerPixel;
    }

    /// <summary>
    /// Gets or sets the frame compressions format.
    /// </summary>
    public IconFrameCompression Compression { get; set; }

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
    public IcoFrameMetadata DeepClone() => new(this);

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    internal void FromIconDirEntry(IconDirEntry entry)
    {
        this.EncodingWidth = entry.Width;
        this.EncodingHeight = entry.Height;
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
            Planes = 1,
            ColorCount = colorCount,
            BitCount = this.Compression switch
            {
                IconFrameCompression.Bmp => (ushort)this.BmpBitsPerPixel,
                IconFrameCompression.Png or _ => 32,
            },
        };
    }
}
