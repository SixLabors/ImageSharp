// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the TimeZoneOffset exif tag.
    /// </summary>
    public static ExifTag<short[]> TimeZoneOffset { get; } = new(ExifTagValue.TimeZoneOffset);
}
