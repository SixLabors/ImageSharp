// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Enumerates the available orientation values supplied by EXIF metadata.
    /// </summary>
    internal enum OrientationMode : ushort
    {
        /// <summary>
        /// Unknown rotation.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The 0th row at the top, the 0th column on the left.
        /// </summary>
        TopLeft = 1,

        /// <summary>
        /// The 0th row at the top, the 0th column on the right.
        /// </summary>
        TopRight = 2,

        /// <summary>
        /// The 0th row at the bottom, the 0th column on the right.
        /// </summary>
        BottomRight = 3,

        /// <summary>
        /// The 0th row at the bottom, the 0th column on the left.
        /// </summary>
        BottomLeft = 4,

        /// <summary>
        /// The 0th row on the left, the 0th column at the top.
        /// </summary>
        LeftTop = 5,

        /// <summary>
        /// The 0th row at the right, the 0th column at the top.
        /// </summary>
        RightTop = 6,

        /// <summary>
        /// The 0th row on the right, the 0th column at the bottom.
        /// </summary>
        RightBottom = 7,

        /// <summary>
        /// The 0th row on the left, the 0th column at the bottom.
        /// </summary>
        LeftBottom = 8
    }
}
