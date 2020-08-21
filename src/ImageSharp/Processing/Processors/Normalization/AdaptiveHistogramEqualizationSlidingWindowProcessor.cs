// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Applies an adaptive histogram equalization to the image using an sliding window approach.
    /// </summary>
    public class AdaptiveHistogramEqualizationSlidingWindowProcessor : HistogramEqualizationProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveHistogramEqualizationSlidingWindowProcessor"/> class.
        /// </summary>
        /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.</param>
        /// <param name="clipHistogram">Indicating whether to clip the histogram bins at a specific value.</param>
        /// <param name="clipLimit">The histogram clip limit. Histogram bins which exceed this limit, will be capped at this value.</param>
        /// <param name="numberOfTiles">The number of tiles the image is split into (horizontal and vertically). Minimum value is 2. Maximum value is 100.</param>
        public AdaptiveHistogramEqualizationSlidingWindowProcessor(
            int luminanceLevels,
            bool clipHistogram,
            int clipLimit,
            int numberOfTiles)
            : base(luminanceLevels, clipHistogram, clipLimit)
        {
            this.NumberOfTiles = numberOfTiles;
        }

        /// <summary>
        /// Gets the number of tiles the image is split into (horizontal and vertically) for the adaptive histogram equalization.
        /// </summary>
        public int NumberOfTiles { get; }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            => new AdaptiveHistogramEqualizationSlidingWindowProcessor<TPixel>(
                configuration,
                this.LuminanceLevels,
                this.ClipHistogram,
                this.ClipLimit,
                this.NumberOfTiles,
                source,
                sourceRectangle);
    }
}
