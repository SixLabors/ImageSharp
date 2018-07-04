// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Provides enumeration over how the image should be rotated.
    /// </summary>
    public enum RotateMode
    {
        /// <summary>
        /// Do not rotate the image.
        /// </summary>
        None,

        /// <summary>
        /// Rotate the image by 90 degrees clockwise.
        /// </summary>
        Rotate90 = 90,

        /// <summary>
        /// Rotate the image by 180 degrees clockwise.
        /// </summary>
        Rotate180 = 180,

        /// <summary>
        /// Rotate the image by 270 degrees clockwise.
        /// </summary>
        Rotate270 = 270
    }
}