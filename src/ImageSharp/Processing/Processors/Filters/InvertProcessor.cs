// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies a filter matrix that inverts the colors of an image
    /// </summary>
    public sealed class InvertProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvertProcessor"/> class.
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        public InvertProcessor(float amount)
            : base(KnownFilterMatrices.CreateInvertFilter(amount))
        {
            this.Amount = amount;
        }

        /// <summary>
        /// Gets the proportion of the conversion
        /// </summary>
        public float Amount { get; }
    }
}