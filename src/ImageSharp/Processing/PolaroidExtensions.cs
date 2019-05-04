// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the recreation of an old Polaroid camera effect to the <see cref="Image"/> type.
    /// </summary>
    public static class PolaroidExtensions
    {
        /// <summary>
        /// Alters the colors of the image recreating an old Polaroid camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Polaroid(this IImageProcessingContext source)
            => source.ApplyProcessor(new PolaroidProcessor());

        /// <summary>
        /// Alters the colors of the image recreating an old Polaroid camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Polaroid(this IImageProcessingContext source, Rectangle rectangle)
            => source.ApplyProcessor(new PolaroidProcessor(), rectangle);
    }
}
