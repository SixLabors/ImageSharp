// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds Gaussian sharpening extensions to the <see cref="Image"/> type.
    /// </summary>
    public static class GaussianSharpenExtensions
    {
        /// <summary>
        /// Applies a Gaussian sharpening filter to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext GaussianSharpen(this IImageProcessingContext source) =>
            source.ApplyProcessor(new GaussianSharpenProcessor());

        /// <summary>
        /// Applies a Gaussian sharpening filter to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext GaussianSharpen(this IImageProcessingContext source, float sigma) =>
            source.ApplyProcessor(new GaussianSharpenProcessor(sigma));

        /// <summary>
        /// Applies a Gaussian sharpening filter to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext GaussianSharpen(
            this IImageProcessingContext source,
            float sigma,
            Rectangle rectangle) =>
            source.ApplyProcessor(new GaussianSharpenProcessor(sigma), rectangle);
    }
}