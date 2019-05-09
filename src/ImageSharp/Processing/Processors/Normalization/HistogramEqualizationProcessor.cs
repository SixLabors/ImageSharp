// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Defines a processor that normalizes the histogram of an image.
    /// </summary>
    public abstract class HistogramEqualizationProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HistogramEqualizationProcessor"/> class.
        /// </summary>
        /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.</param>
        /// <param name="clipHistogram">Indicates, if histogram bins should be clipped.</param>
        /// <param name="clipLimitPercentage">Histogram clip limit in percent of the total pixels in the tile. Histogram bins which exceed this limit, will be capped at this value.</param>
        protected HistogramEqualizationProcessor(int luminanceLevels, bool clipHistogram, float clipLimitPercentage)
        {
            this.LuminanceLevels = luminanceLevels;
            this.ClipHistogram = clipHistogram;
            this.ClipLimitPercentage = clipLimitPercentage;
        }

        /// <summary>
        /// Gets the number of luminance levels.
        /// </summary>
        public int LuminanceLevels { get; }

        /// <summary>
        /// Gets a value indicating whether to clip the histogram bins at a specific value.
        /// </summary>
        public bool ClipHistogram { get; }

        /// <summary>
        /// Gets the histogram clip limit in percent of the total pixels in the tile. Histogram bins which exceed this limit, will be capped at this value.
        /// </summary>
        public float ClipLimitPercentage { get; }

        /// <inheritdoc />
        public abstract IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>;

        /// <summary>
        /// Creates the <see cref="HistogramEqualizationProcessor"/> that implements the algorithm
        /// defined by the given <see cref="HistogramEqualizationOptions"/>.
        /// </summary>
        /// <param name="options">The <see cref="HistogramEqualizationOptions"/>.</param>
        /// <returns>The <see cref="HistogramEqualizationProcessor"/>.</returns>
        public static HistogramEqualizationProcessor FromOptions(HistogramEqualizationOptions options)
        {
            HistogramEqualizationProcessor processor;

            switch (options.Method)
            {
                case HistogramEqualizationMethod.Global:
                    processor = new GlobalHistogramEqualizationProcessor(
                        options.LuminanceLevels,
                        options.ClipHistogram,
                        options.ClipLimitPercentage);
                    break;

                case HistogramEqualizationMethod.AdaptiveTileInterpolation:
                    processor = new AdaptiveHistogramEqualizationProcessor(
                        options.LuminanceLevels,
                        options.ClipHistogram,
                        options.ClipLimitPercentage,
                        options.NumberOfTiles);
                    break;

                case HistogramEqualizationMethod.AdaptiveSlidingWindow:
                    processor = new AdaptiveHistogramEqualizationSlidingWindowProcessor(
                        options.LuminanceLevels,
                        options.ClipHistogram,
                        options.ClipLimitPercentage,
                        options.NumberOfTiles);
                    break;

                default:
                    processor = new GlobalHistogramEqualizationProcessor(
                        options.LuminanceLevels,
                        options.ClipHistogram,
                        options.ClipLimitPercentage);
                    break;
            }

            return processor;
        }
    }
}