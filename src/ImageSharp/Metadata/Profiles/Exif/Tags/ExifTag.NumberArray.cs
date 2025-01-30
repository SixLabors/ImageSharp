// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the StripOffsets exif tag.
    /// </summary>
    public static ExifTag<Number[]> StripOffsets { get; } = new(ExifTagValue.StripOffsets);

    /// <summary>
    /// Gets the StripByteCounts exif tag.
    /// </summary>
    public static ExifTag<Number[]> StripByteCounts { get; } = new(ExifTagValue.StripByteCounts);

    /// <summary>
    /// Gets the TileByteCounts exif tag.
    /// </summary>
    public static ExifTag<Number[]> TileByteCounts { get; } = new(ExifTagValue.TileByteCounts);

    /// <summary>
    /// Gets the TileOffsets exif tag.
    /// </summary>
    public static ExifTag<Number[]> TileOffsets { get; } = new(ExifTagValue.TileOffsets);

    /// <summary>
    /// Gets the ImageLayer exif tag.
    /// </summary>
    public static ExifTag<Number[]> ImageLayer { get; } = new(ExifTagValue.ImageLayer);
}
