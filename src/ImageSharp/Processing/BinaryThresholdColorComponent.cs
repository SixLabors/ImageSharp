// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// The color component to be compared to threshold.
    /// </summary>
    public enum BinaryThresholdColorComponent : int
    {
        /// <summary>
        /// Luminance color component according to ITU-R Recommendation BT.709.
        /// </summary>
        Luminance = 0,

        /// <summary>
        /// HSL saturation color component.
        /// </summary>
        Saturation = 1,

        /// <summary>
        /// Maximum of YCbCr chroma value, i.e. Cb and Cr distance from achromatic value.
        /// </summary>
        MaxChroma = 2,
    }
}
