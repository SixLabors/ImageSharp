// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Enumerates the possible dithering algorithm transform behaviors.
    /// </summary>
    public enum DitherType
    {
        /// <summary>
        /// Error diffusion. Spreads the difference between source and quanized color values as distributed error.
        /// </summary>
        ErrorDiffusion,

        /// <summary>
        /// Ordered dithering. Applies thresholding matrices agains the source to determine the quantized color.
        /// </summary>
        OrderedDither
    }
}
