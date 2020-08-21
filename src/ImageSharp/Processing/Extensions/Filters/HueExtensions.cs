// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the alteration of the hue component of an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class HueExtensions
    {
        /// <summary>
        /// Alters the hue component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="degrees">The rotation angle in degrees to adjust the hue.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Hue(this IImageProcessingContext source, float degrees)
            => source.ApplyProcessor(new HueProcessor(degrees));

        /// <summary>
        /// Alters the hue component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="degrees">The rotation angle in degrees to adjust the hue.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Hue(this IImageProcessingContext source, float degrees, Rectangle rectangle)
            => source.ApplyProcessor(new HueProcessor(degrees), rectangle);
    }
}