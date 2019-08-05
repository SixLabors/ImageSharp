// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// An <see langword="enum"/> that indicates execution options for the <see cref="BokehBlurProcessor"/>.
    /// </summary>
    public enum BokehBlurExecutionMode
    {
        /// <summary>
        /// Indicates that the maximum performance should be prioritized over memory usage.
        /// </summary>
        PreferMaximumPerformance,

        /// <summary>
        /// Indicates that the memory usage should be prioritized over raw performance.
        /// </summary>
        PreferLowMemoryUsage
    }
}
