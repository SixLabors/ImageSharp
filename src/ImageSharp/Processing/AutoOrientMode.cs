// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Provides an enumeration over how an image should be auto oriented.
    /// </summary>
    public enum AutoOrientMode
    {
        /// <summary>
        /// Reset the EXIF orientation flag to horizontal (normal).
        /// </summary>
        ResetExifValue,

        /// <summary>
        /// Keep the existing EXIF orientation flag.
        /// </summary>
        KeepExifValue,

        /// <summary>
        /// Remove the EXIF orientation flag.
        /// </summary>
        RemoveExifValue
    }
}
