// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Defines a global histogram equalization applicable to an <see cref="Image"/>.
    /// </summary>
    public class GlobalHistogramEqualizationProcessor : HistogramEqualizationProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalHistogramEqualizationProcessor"/> class.
        /// </summary>
        /// <param name="luminanceLevels">The number of luminance levels.</param>
        /// <param name="clipHistogram">A value indicating whether to clip the histogram bins at a specific value.</param>
        /// <param name="clipLimitPercentage">The histogram clip limit in percent of the total pixels in the tile. Histogram bins which exceed this limit, will be capped at this value.</param>
        public GlobalHistogramEqualizationProcessor(int luminanceLevels, bool clipHistogram, float clipLimitPercentage)
            : base(luminanceLevels, clipHistogram, clipLimitPercentage)
        {
        }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
        {
            return new GlobalHistogramEqualizationProcessor<TPixel>(
                this.LuminanceLevels,
                this.ClipHistogram,
                this.ClipLimitPercentage);
        }
    }
}