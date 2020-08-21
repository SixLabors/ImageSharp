// Copyright (c) Six Labors.
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
        /// <param name="clipLimit">The histogram clip limit. Histogram bins which exceed this limit, will be capped at this value.</param>
        public GlobalHistogramEqualizationProcessor(int luminanceLevels, bool clipHistogram, int clipLimit)
            : base(luminanceLevels, clipHistogram, clipLimit)
        {
        }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            => new GlobalHistogramEqualizationProcessor<TPixel>(
                configuration,
                this.LuminanceLevels,
                this.ClipHistogram,
                this.ClipLimit,
                source,
                sourceRectangle);
    }
}
