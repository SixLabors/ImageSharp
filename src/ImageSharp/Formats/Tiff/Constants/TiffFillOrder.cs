// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// Enumeration representing the fill orders defined by the Tiff file-format.
    /// </summary>
    internal enum TiffFillOrder : ushort
    {
        /// <summary>
        /// Pixels with lower column values are stored in the higher-order bits of the byte.
        /// </summary>
        MostSignificantBitFirst = 1,

        /// <summary>
        /// Pixels with lower column values are stored in the lower-order bits of the byte.
        /// </summary>
        LeastSignificantBitFirst = 2
    }
}
