// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow to change image lightness.
    /// </summary>
    public static class LightnessExtension
    {
        /// <summary>
        /// Alters the lightness parameter of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="lightness">Lightness parameter of image.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Lightness(this IImageProcessingContext source, float lightness)
            => source.ApplyProcessor(new LightnessProcessor(lightness));

        /// <summary>
        /// Alters the lightness parameter of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="lightness">Lightness parameter of image.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Lightness(this IImageProcessingContext source, float lightness, Rectangle rectangle)
            => source.ApplyProcessor(new LightnessProcessor(lightness), rectangle);
    }
}
