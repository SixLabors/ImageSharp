// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the recreation of an old Polaroid camera effect on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class PolaroidExtensions
    {
        /// <summary>
        /// Alters the colors of the image recreating an old Polaroid camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Polaroid(this IImageProcessingContext source)
            => source.ApplyProcessor(new PolaroidProcessor(source.GetGraphicsOptions()));

        /// <summary>
        /// Alters the colors of the image recreating an old Polaroid camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Polaroid(this IImageProcessingContext source, Rectangle rectangle)
            => source.ApplyProcessor(new PolaroidProcessor(source.GetGraphicsOptions()), rectangle);
    }
}
