// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the FreeOffsets exif tag.
    /// </summary>
    public static ExifTag<uint[]> FreeOffsets { get; } = new(ExifTagValue.FreeOffsets);

    /// <summary>
    /// Gets the FreeByteCounts exif tag.
    /// </summary>
    public static ExifTag<uint[]> FreeByteCounts { get; } = new(ExifTagValue.FreeByteCounts);

    /// <summary>
    /// Gets the ColorResponseUnit exif tag.
    /// </summary>
    public static ExifTag<uint[]> ColorResponseUnit { get; } = new(ExifTagValue.ColorResponseUnit);

    /// <summary>
    /// Gets the SMinSampleValue exif tag.
    /// </summary>
    public static ExifTag<uint[]> SMinSampleValue { get; } = new(ExifTagValue.SMinSampleValue);

    /// <summary>
    /// Gets the SMaxSampleValue exif tag.
    /// </summary>
    public static ExifTag<uint[]> SMaxSampleValue { get; } = new(ExifTagValue.SMaxSampleValue);

    /// <summary>
    /// Gets the JPEGQTables exif tag.
    /// </summary>
    public static ExifTag<uint[]> JPEGQTables { get; } = new(ExifTagValue.JPEGQTables);

    /// <summary>
    /// Gets the JPEGDCTables exif tag.
    /// </summary>
    public static ExifTag<uint[]> JPEGDCTables { get; } = new(ExifTagValue.JPEGDCTables);

    /// <summary>
    /// Gets the JPEGACTables exif tag.
    /// </summary>
    public static ExifTag<uint[]> JPEGACTables { get; } = new(ExifTagValue.JPEGACTables);

    /// <summary>
    /// Gets the StripRowCounts exif tag.
    /// </summary>
    public static ExifTag<uint[]> StripRowCounts { get; } = new(ExifTagValue.StripRowCounts);

    /// <summary>
    /// Gets the IntergraphRegisters exif tag.
    /// </summary>
    public static ExifTag<uint[]> IntergraphRegisters { get; } = new(ExifTagValue.IntergraphRegisters);

    /// <summary>
    /// Gets the offset to child IFDs exif tag.
    /// </summary>
    public static ExifTag<uint[]> SubIFDs { get; } = new(ExifTagValue.SubIFDs);
}
