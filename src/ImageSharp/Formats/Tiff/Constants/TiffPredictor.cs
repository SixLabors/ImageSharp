// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// A mathematical operator that is applied to the image data before an encoding scheme is applied.
    /// </summary>
    public enum TiffPredictor : ushort
    {
        /// <summary>
        /// No prediction.
        /// </summary>
        None = 1,

        /// <summary>
        /// Horizontal differencing.
        /// </summary>
        Horizontal = 2,

        /// <summary>
        /// Floating point horizontal differencing.
        ///
        /// Note: The Tiff Encoder does not yet support this. If this is chosen, the encoder will fallback to none.
        /// </summary>
        FloatingPoint = 3
    }
}
