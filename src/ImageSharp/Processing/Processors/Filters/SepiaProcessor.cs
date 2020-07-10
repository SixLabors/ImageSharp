// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies a sepia filter matrix using the given amount.
    /// </summary>
    public sealed class SepiaProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SepiaProcessor"/> class.
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        public SepiaProcessor(float amount)
            : base(KnownFilterMatrices.CreateSepiaFilter(amount))
        {
            this.Amount = amount;
        }

        /// <summary>
        /// Gets the proportion of the conversion
        /// </summary>
        public float Amount { get; }
    }
}