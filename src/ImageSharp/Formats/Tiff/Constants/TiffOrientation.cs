// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// Enumeration representing the image orientations defined by the Tiff file-format.
    /// </summary>
    internal enum TiffOrientation
    {
        /// <summary>
        /// The 0th row and 0th column represent the visual top and left-hand side of the image respectively.
        /// </summary>
        TopLeft = 1,

        /// <summary>
        /// The 0th row and 0th column represent the visual top and right-hand side of the image respectively.
        /// </summary>
        TopRight = 2,

        /// <summary>
        /// The 0th row and 0th column represent the visual bottom and right-hand side of the image respectively.
        /// </summary>
        BottomRight = 3,

        /// <summary>
        /// The 0th row and 0th column represent the visual bottom and left-hand side of the image respectively.
        /// </summary>
        BottomLeft = 4,

        /// <summary>
        /// The 0th row and 0th column represent the visual left-hand side and top of the image respectively.
        /// </summary>
        LeftTop = 5,

        /// <summary>
        /// The 0th row and 0th column represent the visual right-hand side and top of the image respectively.
        /// </summary>
        RightTop = 6,

        /// <summary>
        /// The 0th row and 0th column represent the visual right-hand side and bottom of the image respectively.
        /// </summary>
        RightBottom = 7,

        /// <summary>
        /// The 0th row and 0th column represent the visual left-hand side and bottom of the image respectively.
        /// </summary>
        LeftBottom = 8
    }
}
