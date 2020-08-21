// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies an opacity filter matrix using the given amount.
    /// </summary>
    public sealed class OpacityProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpacityProcessor"/> class.
        /// </summary>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        public OpacityProcessor(float amount)
            : base(KnownFilterMatrices.CreateOpacityFilter(amount))
        {
            this.Amount = amount;
        }

        /// <summary>
        /// Gets the proportion of the conversion.
        /// </summary>
        public float Amount { get; }
    }
}