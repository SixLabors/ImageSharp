// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <summary>
    /// Enumerates the available orientation values supplied by EXIF metadata.
    /// </summary>
    public static class ExifOrientationMode
    {
        /// <summary>
        /// Unknown rotation.
        /// </summary>
        public const ushort Unknown = 0;

        /// <summary>
        /// The 0th row at the top, the 0th column on the left.
        /// </summary>
        public const ushort TopLeft = 1;

        /// <summary>
        /// The 0th row at the top, the 0th column on the right.
        /// </summary>
        public const ushort TopRight = 2;

        /// <summary>
        /// The 0th row at the bottom, the 0th column on the right.
        /// </summary>
        public const ushort BottomRight = 3;

        /// <summary>
        /// The 0th row at the bottom, the 0th column on the left.
        /// </summary>
        public const ushort BottomLeft = 4;

        /// <summary>
        /// The 0th row on the left, the 0th column at the top.
        /// </summary>
        public const ushort LeftTop = 5;

        /// <summary>
        /// The 0th row at the right, the 0th column at the top.
        /// </summary>
        public const ushort RightTop = 6;

        /// <summary>
        /// The 0th row on the right, the 0th column at the bottom.
        /// </summary>
        public const ushort RightBottom = 7;

        /// <summary>
        /// The 0th row on the left, the 0th column at the bottom.
        /// </summary>
        public const ushort LeftBottom = 8;
    }
}
