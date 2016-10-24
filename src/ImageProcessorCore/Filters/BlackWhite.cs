// <copyright file="BlackWhite.cs" company="James Jackson-South">
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
        /// Applies black and white toning to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> BlackWhite<TColor, TPacked>(this Image<TColor, TPacked> source)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return BlackWhite(source, source.Bounds);
        }

        /// <summary>
        /// Applies black and white toning to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> BlackWhite<TColor, TPacked>(this Image<TColor, TPacked> source, Rectangle rectangle)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return source.Process(rectangle, new BlackWhiteProcessor<TColor, TPacked>());
        }
    }
}
