// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies a saturation filter matrix using the given amount.
    /// </summary>
    public sealed class SaturateProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SaturateProcessor"/> class.
        /// </summary>
        /// <remarks>
        /// A value of <value>0</value> is completely un-saturated. A value of <value>1</value> leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of amount over 1 are allowed, providing super-saturated results
        /// </remarks>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        public SaturateProcessor(float amount)
            : base(KnownFilterMatrices.CreateSaturateFilter(amount))
        {
            this.Amount = amount;
        }

        /// <summary>
        /// Gets the proportion of the conversion
        /// </summary>
        public float Amount { get; }
    }
}