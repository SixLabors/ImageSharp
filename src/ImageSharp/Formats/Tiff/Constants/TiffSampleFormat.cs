// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// Specifies how to interpret each data sample in a pixel.
    /// </summary>
    public enum TiffSampleFormat : ushort
    {
        /// <summary>
        /// Unsigned integer data. Default value.
        /// </summary>
        UnsignedInteger = 1,

        /// <summary>
        /// Signed integer data.
        /// </summary>
        SignedInteger = 2,

        /// <summary>
        /// IEEE floating point data.
        /// </summary>
        Float = 3,

        /// <summary>
        /// Undefined data format.
        /// </summary>
        Undefined = 4,

        /// <summary>
        /// The complex int.
        /// </summary>
        ComplexInt = 5,

        /// <summary>
        /// The complex float.
        /// </summary>
        ComplexFloat = 6
    }
}
