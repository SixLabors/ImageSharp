// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies a grayscale filter matrix using the given amount and the formula as specified by ITU-R Recommendation BT.709
    /// </summary>
    public sealed class GrayscaleBt709Processor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GrayscaleBt709Processor"/> class.
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        public GrayscaleBt709Processor(float amount)
            : base(KnownFilterMatrices.CreateGrayscaleBt709Filter(amount))
        {
            this.Amount = amount;
        }

        /// <summary>
        /// Gets the proportion of the conversion.
        /// </summary>
        public float Amount { get; }
    }
}