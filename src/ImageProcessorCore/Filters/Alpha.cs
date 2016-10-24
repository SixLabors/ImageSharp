// <copyright file="Alpha.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Alters the alpha component of the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="percent">The new opacity of the image. Must be between 0 and 100.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Alpha<TColor, TPacked>(this Image<TColor, TPacked> source, int percent)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return Alpha(source, percent, source.Bounds);
        }

        /// <summary>
        /// Alters the alpha component of the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="percent">The new opacity of the image. Must be between 0 and 100.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image<TColor, TPacked> Alpha<TColor, TPacked>(this Image<TColor, TPacked> source, int percent, Rectangle rectangle)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return source.Process(rectangle, new AlphaProcessor<TColor, TPacked>(percent));
        }
    }
}
