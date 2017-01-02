// <copyright file="Glow.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using Processing.Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Glow<TColor>(this Image<TColor> source)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return Glow(source, default(TColor), source.Bounds.Width * .5F, source.Bounds);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the glow.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Glow<TColor>(this Image<TColor> source, TColor color)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return Glow(source, color, source.Bounds.Width * .5F, source.Bounds);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The the radius.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Glow<TColor>(this Image<TColor> source, float radius)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return Glow(source, default(TColor), radius, source.Bounds);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Glow<TColor>(this Image<TColor> source, Rectangle rectangle)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return Glow(source, default(TColor), 0, rectangle);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the glow.</param>
        /// <param name="radius">The the radius.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Glow<TColor>(this Image<TColor> source, TColor color, float radius, Rectangle rectangle)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            GlowProcessor<TColor> processor = new GlowProcessor<TColor> { Radius = radius, };

            if (!color.Equals(default(TColor)))
            {
                processor.GlowColor = color;
            }

            return source.Apply(rectangle, processor);
        }
    }
}
