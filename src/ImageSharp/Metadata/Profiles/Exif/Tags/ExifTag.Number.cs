// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the ImageWidth exif tag.
    /// </summary>
    public static ExifTag<Number> ImageWidth { get; } = new(ExifTagValue.ImageWidth);

    /// <summary>
    /// Gets the ImageLength exif tag.
    /// </summary>
    public static ExifTag<Number> ImageLength { get; } = new(ExifTagValue.ImageLength);

    /// <summary>
    /// Gets the RowsPerStrip exif tag.
    /// </summary>
    public static ExifTag<Number> RowsPerStrip { get; } = new(ExifTagValue.RowsPerStrip);

    /// <summary>
    /// Gets the TileWidth exif tag.
    /// </summary>
    public static ExifTag<Number> TileWidth { get; } = new(ExifTagValue.TileWidth);

    /// <summary>
    /// Gets the TileLength exif tag.
    /// </summary>
    public static ExifTag<Number> TileLength { get; } = new(ExifTagValue.TileLength);

    /// <summary>
    /// Gets the BadFaxLines exif tag.
    /// </summary>
    public static ExifTag<Number> BadFaxLines { get; } = new(ExifTagValue.BadFaxLines);

    /// <summary>
    /// Gets the ConsecutiveBadFaxLines exif tag.
    /// </summary>
    public static ExifTag<Number> ConsecutiveBadFaxLines { get; } = new(ExifTagValue.ConsecutiveBadFaxLines);

    /// <summary>
    /// Gets the PixelXDimension exif tag.
    /// </summary>
    public static ExifTag<Number> PixelXDimension { get; } = new(ExifTagValue.PixelXDimension);

    /// <summary>
    /// Gets the PixelYDimension exif tag.
    /// </summary>
    public static ExifTag<Number> PixelYDimension { get; } = new(ExifTagValue.PixelYDimension);
}
