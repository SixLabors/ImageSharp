// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies a grayscale filter matrix using the given amount and the formula as specified by ITU-R Recommendation BT.601
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class GrayscaleBt601Processor<TPixel> : FilterProcessor<TPixel>
         where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GrayscaleBt601Processor{TPixel}"/> class.
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        public GrayscaleBt601Processor(float amount)
            : base(KnownFilterMatrices.CreateGrayscaleBt601Filter(amount))
        {
            this.Amount = amount;
        }

        /// <summary>
        /// Gets the proportion of the conversion
        /// </summary>
        public float Amount { get; }
    }
}