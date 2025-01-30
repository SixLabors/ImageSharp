// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <content/>
public abstract partial class ExifTag
{
    /// <summary>
    /// Gets the title tag used by Windows (encoded in UCS2).
    /// </summary>
    public static ExifTag<string> XPTitle => new(ExifTagValue.XPTitle);

    /// <summary>
    /// Gets the comment tag used by Windows (encoded in UCS2).
    /// </summary>
    public static ExifTag<string> XPComment => new(ExifTagValue.XPComment);

    /// <summary>
    /// Gets the author tag used by Windows (encoded in UCS2).
    /// </summary>
    public static ExifTag<string> XPAuthor => new(ExifTagValue.XPAuthor);

    /// <summary>
    /// Gets the keywords tag used by Windows (encoded in UCS2).
    /// </summary>
    public static ExifTag<string> XPKeywords => new(ExifTagValue.XPKeywords);

    /// <summary>
    /// Gets the subject tag used by Windows (encoded in UCS2).
    /// </summary>
    public static ExifTag<string> XPSubject => new(ExifTagValue.XPSubject);
}
