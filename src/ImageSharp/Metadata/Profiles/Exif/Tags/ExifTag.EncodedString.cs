// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the UserComment exif tag.
    /// </summary>
    public static ExifTag<EncodedString> UserComment { get; } = new(ExifTagValue.UserComment);

    /// <summary>
    /// Gets the GPSProcessingMethod exif tag.
    /// </summary>
    public static ExifTag<EncodedString> GPSProcessingMethod { get; } = new(ExifTagValue.GPSProcessingMethod);

    /// <summary>
    /// Gets the GPSAreaInformation exif tag.
    /// </summary>
    public static ExifTag<EncodedString> GPSAreaInformation { get; } = new(ExifTagValue.GPSAreaInformation);
}
