// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Enumerates the different types of defined histogram equalization methods.
    /// </summary>
    public enum HistogramEqualizationMethod : int
    {
        /// <summary>
        /// A global histogram equalization.
        /// </summary>
        Global,

        /// <summary>
        /// Adaptive histogram equalization using a tile interpolation approach.
        /// </summary>
        AdaptiveTileInterpolation,

        /// <summary>
        /// Adaptive histogram equalization using sliding window. Slower then the tile interpolation mode, but can yield to better results.
        /// </summary>
        AdaptiveSlidingWindow,
    }
}
