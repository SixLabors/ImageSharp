// <copyright file="Contrast.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Alters the contrast component of the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new contrast of the image. Must be between -100 and 100.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Contrast<TColor, TPacked>(this Image<TColor, TPacked> source, int amount)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return Contrast(source, amount, source.Bounds);
        }

        /// <summary>
        /// Alters the contrast component of the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new contrast of the image. Must be between -100 and 100.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Contrast<TColor, TPacked>(this Image<TColor, TPacked> source, int amount, Rectangle rectangle)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return source.Process(rectangle, new ContrastProcessor<TColor, TPacked>(amount));
        }
    }
}
