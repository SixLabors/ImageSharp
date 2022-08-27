// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <summary>
    /// Specifies which parts will be written when the profile is added to an image.
    /// </summary>
    [Flags]
    public enum ExifParts
    {
        /// <summary>
        /// None
        /// </summary>
        None = 0,

        /// <summary>
        /// IfdTags
        /// </summary>
        IfdTags = 1,

        /// <summary>
        /// ExifTags
        /// </summary>
        ExifTags = 2,

        /// <summary>
        /// GPSTags
        /// </summary>
        GpsTags = 4,

        /// <summary>
        /// All
        /// </summary>
        All = IfdTags | ExifTags | GpsTags
    }
}
