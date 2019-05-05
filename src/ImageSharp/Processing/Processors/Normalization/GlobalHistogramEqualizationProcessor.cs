// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Defines a global histogram equalization applicable to an <see cref="Image"/>.
    /// </summary>
    internal class GlobalHistogramEqualizationProcessor : HistogramEqualizationProcessor
    {
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