// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the ClipPath exif tag.
    /// </summary>
    public static ExifTag<byte[]> ClipPath => new(ExifTagValue.ClipPath);

    /// <summary>
    /// Gets the VersionYear exif tag.
    /// </summary>
    public static ExifTag<byte[]> VersionYear => new(ExifTagValue.VersionYear);

    /// <summary>
    /// Gets the XMP exif tag.
    /// </summary>
    public static ExifTag<byte[]> XMP => new(ExifTagValue.XMP);

    /// <summary>
    /// Gets the IPTC exif tag.
    /// </summary>
    public static ExifTag<byte[]> IPTC => new(ExifTagValue.IPTC);

    /// <summary>
    /// Gets the IccProfile exif tag.
    /// </summary>
    public static ExifTag<byte[]> IccProfile => new(ExifTagValue.IccProfile);

    /// <summary>
    /// Gets the CFAPattern2 exif tag.
    /// </summary>
    public static ExifTag<byte[]> CFAPattern2 => new(ExifTagValue.CFAPattern2);

    /// <summary>
    /// Gets the TIFFEPStandardID exif tag.
    /// </summary>
    public static ExifTag<byte[]> TIFFEPStandardID => new(ExifTagValue.TIFFEPStandardID);

    /// <summary>
    /// Gets the GPSVersionID exif tag.
    /// </summary>
    public static ExifTag<byte[]> GPSVersionID => new(ExifTagValue.GPSVersionID);
}
