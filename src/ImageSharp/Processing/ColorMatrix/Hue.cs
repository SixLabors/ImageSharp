// <copyright file="Hue.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using ImageSharp.PixelFormats;

    using ImageSharp.Processing;
    using Processing.Processors;
    using SixLabors.Primitives;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Alters the hue component of the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="degrees">The angle in degrees to adjust the image.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Hue<TPixel>(this Image<TPixel> source, float degrees)
            where TPixel : struct, IPixel<TPixel>
        {
            return Hue(source, degrees, source.Bounds);
        }

        /// <summary>
        /// Alters the hue component of the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="degrees">The angle in degrees to adjust the image.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Hue<TPixel>(this Image<TPixel> source, float degrees, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            source.ApplyProcessor(new HueProcessor<TPixel>(degrees), rectangle);
            return source;
        }
    }
}
