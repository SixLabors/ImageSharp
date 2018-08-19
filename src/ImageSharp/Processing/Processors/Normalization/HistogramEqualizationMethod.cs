﻿// Copyright (c) Six Labors and contributors.
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
        /// Adaptive histogram equalization.
        /// </summary>
        Adaptive,

        /// <summary>
        /// Adaptive sliding window histogram equalization.
        /// </summary>
        AdaptiveSlidingWindow,
    }
}
