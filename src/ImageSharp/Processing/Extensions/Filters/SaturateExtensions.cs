// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the alteration of the saturation component of an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class SaturateExtensions
    {
        /// <summary>
        /// Alters the saturation component of the image.
        /// </summary>
        /// <remarks>
        /// A value of 0 is completely un-saturated. A value of 1 leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of amount over 1 are allowed, providing super-saturated results
        /// </remarks>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Saturate(this IImageProcessingContext source, float amount)
            => source.ApplyProcessor(new SaturateProcessor(amount));

        /// <summary>
        /// Alters the saturation component of the image.
        /// </summary>
        /// <remarks>
        /// A value of 0 is completely un-saturated. A value of 1 leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of amount over 1 are allowed, providing super-saturated results
        /// </remarks>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Saturate(this IImageProcessingContext source, float amount, Rectangle rectangle)
            => source.ApplyProcessor(new SaturateProcessor(amount), rectangle);
    }
}