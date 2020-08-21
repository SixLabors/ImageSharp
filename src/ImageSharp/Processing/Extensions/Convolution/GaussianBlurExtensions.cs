// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines Gaussian blurring extensions to apply on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class GaussianBlurExtensions
    {
        /// <summary>
        /// Applies a Gaussian blur to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext GaussianBlur(this IImageProcessingContext source)
            => source.ApplyProcessor(new GaussianBlurProcessor());

        /// <summary>
        /// Applies a Gaussian blur to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext GaussianBlur(this IImageProcessingContext source, float sigma)
            => source.ApplyProcessor(new GaussianBlurProcessor(sigma));

        /// <summary>
        /// Applies a Gaussian blur to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext GaussianBlur(this IImageProcessingContext source, float sigma, Rectangle rectangle)
            => source.ApplyProcessor(new GaussianBlurProcessor(sigma), rectangle);
    }
}