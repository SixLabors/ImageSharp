// <copyright file="BackgroundColor.cs" company="James Jackson-South">
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
        /// Replaces the background color of image with the given one.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the background.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> BackgroundColor<TColor, TPacked>(this Image<TColor, TPacked> source, TColor color)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return source.Process(source.Bounds, new BackgroundColorProcessor<TColor, TPacked>(color));
        }
    }
}
