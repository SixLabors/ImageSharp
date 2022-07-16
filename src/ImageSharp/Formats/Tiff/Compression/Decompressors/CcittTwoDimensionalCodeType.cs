// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Enum for the different two dimensional code words for the ccitt fax compression.
    /// </summary>
    internal enum CcittTwoDimensionalCodeType
    {
        /// <summary>
        /// No valid code word was read.
        /// </summary>
        None = 0,

        /// <summary>
        /// Pass mode: This mode is identified when the position of b2 lies to the left of a1.
        /// </summary>
        Pass = 1,

        /// <summary>
        /// Indicates horizontal mode.
        /// </summary>
        Horizontal = 2,

        /// <summary>
        /// Vertical 0 code word: relative distance between a1 and b1 is 0.
        /// </summary>
        Vertical0 = 3,

        /// <summary>
        /// Vertical r1 code word: relative distance between a1 and b1 is 1, a1 is to the right of b1.
        /// </summary>
        VerticalR1 = 4,

        /// <summary>
        /// Vertical r2 code word: relative distance between a1 and b1 is 2, a1 is to the right of b1.
        /// </summary>
        VerticalR2 = 5,

        /// <summary>
        /// Vertical r3 code word: relative distance between a1 and b1 is 3, a1 is to the right of b1.
        /// </summary>
        VerticalR3 = 6,

        /// <summary>
        /// Vertical l1 code word: relative distance between a1 and b1 is 1, a1 is to the left of b1.
        /// </summary>
        VerticalL1 = 7,

        /// <summary>
        /// Vertical l2 code word: relative distance between a1 and b1 is 2, a1 is to the left of b1.
        /// </summary>
        VerticalL2 = 8,

        /// <summary>
        /// Vertical l3 code word: relative distance between a1 and b1 is 3, a1 is to the left of b1.
        /// </summary>
        VerticalL3 = 9,

        /// <summary>
        /// 1d extensions code word, extension code is used to indicate the change from the current mode to another mode, e.g., another coding scheme.
        /// Not supported.
        /// </summary>
        Extensions1D = 10,

        /// <summary>
        /// 2d extensions code word, extension code is used to indicate the change from the current mode to another mode, e.g., another coding scheme.
        /// Not supported.
        /// </summary>
        Extensions2D = 11,
    }
}
