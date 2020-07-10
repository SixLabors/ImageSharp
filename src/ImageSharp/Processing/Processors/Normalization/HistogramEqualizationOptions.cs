// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Data container providing the different options for the histogram equalization.
    /// </summary>
    public class HistogramEqualizationOptions
    {
        /// <summary>
        /// Gets the default <see cref="HistogramEqualizationOptions"/> instance.
        /// </summary>
        public static HistogramEqualizationOptions Default { get; } = new HistogramEqualizationOptions();

        /// <summary>
        /// Gets or sets the histogram equalization method to use. Defaults to global histogram equalization.
        /// </summary>
        public HistogramEqualizationMethod Method { get; set; } = HistogramEqualizationMethod.Global;

        /// <summary>
        /// Gets or sets the number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.
        /// Defaults to 256.
        /// </summary>
        public int LuminanceLevels { get; set; } = 256;

        /// <summary>
        /// Gets or sets a value indicating whether to clip the histogram bins at a specific value.
        /// It is recommended to use clipping when the AdaptiveTileInterpolation method is used, to suppress artifacts which can occur on the borders of the tiles.
        /// Defaults to false.
        /// </summary>
        public bool ClipHistogram { get; set; } = false;

        /// <summary>
        /// Gets or sets the histogram clip limit. Adaptive histogram equalization may cause noise to be amplified in near constant
        /// regions. To reduce this problem, histogram bins which exceed a given limit will be capped at this value. The exceeding values
        /// will be redistributed equally to all other bins. The clipLimit depends on the size of the tiles the image is split into
        /// and therefore the image size itself.
        /// Defaults to 350.
        /// </summary>
        /// <remarks>For more information, see also: https://en.wikipedia.org/wiki/Adaptive_histogram_equalization#Contrast_Limited_AHE</remarks>
        public int ClipLimit { get; set; } = 350;

        /// <summary>
        /// Gets or sets the number of tiles the image is split into (horizontal and vertically) for the adaptive histogram equalization.
        /// Defaults to 8.
        /// </summary>
        public int NumberOfTiles { get; set; } = 8;
    }
}
