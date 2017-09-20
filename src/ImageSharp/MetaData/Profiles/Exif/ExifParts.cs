// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.MetaData.Profiles.Exif
{
    using System;

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
        ExifTags = 4,

        /// <summary>
        /// GPSTags
        /// </summary>
        GPSTags = 8,

        /// <summary>
        /// All
        /// </summary>
        All = IfdTags | ExifTags | GPSTags
    }
}