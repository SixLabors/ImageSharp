﻿// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the alteration of the contrast component of an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class ContrastExtensions
    {
        /// <summary>
        /// Alters the contrast component of the image.
        /// </summary>
        /// <remarks>
        /// A value of 0 will create an image that is completely gray. A value of 1 leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of an amount over 1 are allowed, providing results with more contrast.
        /// </remarks>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Contrast(this IImageProcessingContext source, float amount)
            => source.ApplyProcessor(new ContrastProcessor(amount));

        /// <summary>
        /// Alters the contrast component of the image.
        /// </summary>
        /// <remarks>
        /// A value of 0 will create an image that is completely gray. A value of 1 leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of an amount over 1 are allowed, providing results with more contrast.
        /// </remarks>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Contrast(this IImageProcessingContext source, float amount, Rectangle rectangle)
            => source.ApplyProcessor(new ContrastProcessor(amount), rectangle);
    }
}
