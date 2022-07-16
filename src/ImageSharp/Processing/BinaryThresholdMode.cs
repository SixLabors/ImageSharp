// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Selects the value to be compared to threshold.
    /// </summary>
    public enum BinaryThresholdMode
    {
        /// <summary>
        /// Compare the color luminance (according to ITU-R Recommendation BT.709).
        /// </summary>
        Luminance = 0,

        /// <summary>
        /// Compare the HSL saturation of the color.
        /// </summary>
        Saturation = 1,

        /// <summary>
        /// Compare the maximum of YCbCr chroma value, i.e. Cb and Cr distance from achromatic value.
        /// </summary>
        MaxChroma = 2,
    }
}
