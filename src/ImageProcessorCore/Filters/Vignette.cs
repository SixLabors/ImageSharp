// <copyright file="Vignette.cs" company="James Jackson-South">
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
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Vignette<TColor, TPacked>(this Image<TColor, TPacked> source)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return Vignette(source, default(TColor), source.Bounds.Width * .5F, source.Bounds.Height * .5F, source.Bounds);
        }

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the vignette.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Vignette<TColor, TPacked>(this Image<TColor, TPacked> source, TColor color)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return Vignette(source, color, source.Bounds.Width * .5F, source.Bounds.Height * .5F, source.Bounds);
        }

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radiusX">The the x-radius.</param>
        /// <param name="radiusY">The the y-radius.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Vignette<TColor, TPacked>(this Image<TColor, TPacked> source, float radiusX, float radiusY)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return Vignette(source, default(TColor), radiusX, radiusY, source.Bounds);
        }

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Vignette<TColor, TPacked>(this Image<TColor, TPacked> source, Rectangle rectangle)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return Vignette(source, default(TColor), 0, 0, rectangle);
        }

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the vignette.</param>
        /// <param name="radiusX">The the x-radius.</param>
        /// <param name="radiusY">The the y-radius.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Vignette<TColor, TPacked>(this Image<TColor, TPacked> source, TColor color, float radiusX, float radiusY, Rectangle rectangle)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            VignetteProcessor<TColor, TPacked> processor = new VignetteProcessor<TColor, TPacked> { RadiusX = radiusX, RadiusY = radiusY };

            if (!color.Equals(default(TColor)))
            {
                processor.VignetteColor = color;
            }

            return source.Process(rectangle, processor);
        }
    }
}