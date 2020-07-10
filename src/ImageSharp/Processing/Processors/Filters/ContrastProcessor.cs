// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies a contrast filter matrix using the given amount.
    /// </summary>
    public sealed class ContrastProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContrastProcessor"/> class.
        /// </summary>
        /// <remarks>
        /// A value of <value>0</value> will create an image that is completely gray. A value of <value>1</value> leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of an amount over 1 are allowed, providing results with more contrast.
        /// </remarks>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        public ContrastProcessor(float amount)
            : base(KnownFilterMatrices.CreateContrastFilter(amount))
        {
            this.Amount = amount;
        }

        /// <summary>
        /// Gets the proportion of the conversion.
        /// </summary>
        public float Amount { get; }
    }
}