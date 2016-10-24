// <copyright file="Skew.cs" company="James Jackson-South">
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
        /// Skews an image by the given angles in degrees, expanding the image to fit the skewed result.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to skew.</param>
        /// <param name="degreesX">The angle in degrees to perform the rotation along the x-axis.</param>
        /// <param name="degreesY">The angle in degrees to perform the rotation along the y-axis.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor, TPacked> Skew<TColor, TPacked>(this Image<TColor, TPacked> source, float degreesX, float degreesY)
            where TColor : struct, IPackedVector<TPacked>
            where TPacked : struct
        {
            return Skew(source, degreesX, degreesY, true);
        }

        /// <summary>
        /// Skews an image by the given angles in degrees.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to skew.</param>
        /// <param name="degreesX">The angle in degrees to perform the rotation along the x-axis.</param>
        /// <param name="degreesY">The angle in degrees to perform the rotation along the y-axis.</param>
        /// <param name="expand">Whether to expand the image to fit the skewed result.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor, TPacked> Skew<TColor, TPacked>(this Image<TColor, TPacked> source, float degreesX, float degreesY, bool expand)
            where TColor : struct, IPackedVector<TPacked>
            where TPacked : struct
        {
            SkewProcessor<TColor, TPacked> processor = new SkewProcessor<TColor, TPacked> { AngleX = degreesX, AngleY = degreesY, Expand = expand };
            return source.Process(source.Width, source.Height, source.Bounds, source.Bounds, processor);
        }
    }
}
