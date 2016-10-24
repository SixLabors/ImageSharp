// <copyright file="GuassianSharpen.cs" company="James Jackson-South">
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
        /// Applies a Guassian sharpening filter to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> GuassianSharpen<TColor, TPacked>(this Image<TColor, TPacked> source, float sigma = 3f)
            where TColor : struct, IPackedVector<TPacked>
            where TPacked : struct
        {
            return GuassianSharpen(source, sigma, source.Bounds);
        }

        /// <summary>
        /// Applies a Guassian sharpening filter to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> GuassianSharpen<TColor, TPacked>(this Image<TColor, TPacked> source, float sigma, Rectangle rectangle)
            where TColor : struct, IPackedVector<TPacked>
            where TPacked : struct
        {
            return source.Process(rectangle, new GuassianSharpenProcessor<TColor, TPacked>(sigma));
        }
    }
}
