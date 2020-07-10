// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the alteration of the opacity component of an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class OpacityExtensions
    {
        /// <summary>
        /// Multiplies the alpha component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Opacity(this IImageProcessingContext source, float amount)
            => source.ApplyProcessor(new OpacityProcessor(amount));

        /// <summary>
        /// Multiplies the alpha component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Opacity(this IImageProcessingContext source, float amount, Rectangle rectangle)
            => source.ApplyProcessor(new OpacityProcessor(amount), rectangle);
    }
}