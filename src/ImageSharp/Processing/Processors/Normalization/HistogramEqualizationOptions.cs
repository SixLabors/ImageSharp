﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Data container providing the different options for the histogram equalization.
    /// </summary>
    public class HistogramEqualizationOptions
    {
        /// <summary>
        /// Gets or sets the histogram equalization method to use. Defaults to global histogram equalization.
        /// </summary>
        public HistogramEqualizationMethod Method { get; set; } = HistogramEqualizationMethod.Global;

        /// <summary>
        /// Gets or sets the number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images. Defaults to 256.
        /// </summary>
        public int LuminanceLevels { get; set; } = 256;

        /// <summary>
        /// Gets or sets a value indicating whether to clip the histogram bins at a specific value. Defaults to true.
        /// </summary>
        public bool ClipHistogram { get; set; } = true;

        /// <summary>
        /// Gets or sets the histogram clip limit. Histogram bins which exceed this limit, will be capped at this value.
        /// Defaults to 80.
        /// </summary>
        public int ClipLimit { get; set; } = 80;

        /// <summary>
        /// Gets or sets the size of the grid for the adaptive histogram equalization. Defaults to 32.
        /// </summary>
        public int GridSize { get; set; } = 32;
    }
}
