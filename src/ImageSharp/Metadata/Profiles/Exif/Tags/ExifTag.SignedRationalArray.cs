// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the Decode exif tag.
    /// </summary>
    public static ExifTag<SignedRational[]> Decode { get; } = new(ExifTagValue.Decode);
}
