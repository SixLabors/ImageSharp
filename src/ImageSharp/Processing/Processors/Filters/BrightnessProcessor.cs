// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies a brightness filter matrix using the given amount.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BrightnessProcessor<TPixel> : FilterProcessor<TPixel>
         where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrightnessProcessor{TPixel}"/> class.
        /// </summary>
        /// <remarks>
        /// A value of <value>0</value> will create an image that is completely black. A value of <value>1</value> leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of an amount over 1 are allowed, providing brighter results.
        /// </remarks>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        public BrightnessProcessor(float amount)
            : base(KnownFilterMatrices.CreateBrightnessFilter(amount))
        {
            this.Amount = amount;
        }

        /// <summary>
        /// Gets the proportion of the conversion
        /// </summary>
        public float Amount { get; }
    }
}