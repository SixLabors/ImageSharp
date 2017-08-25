// <copyright file="Glow.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using ImageSharp.PixelFormats;

    using Processing.Processors;
    using SixLabors.Primitives;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Glow<TPixel>(this Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
        {
            return Glow(source, GraphicsOptions.Default);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the glow.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Glow<TPixel>(this Image<TPixel> source, TPixel color)
            where TPixel : struct, IPixel<TPixel>
        {
            return Glow(source, color, GraphicsOptions.Default);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The the radius.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Glow<TPixel>(this Image<TPixel> source, float radius)
            where TPixel : struct, IPixel<TPixel>
        {
            return Glow(source, radius, GraphicsOptions.Default);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Glow<TPixel>(this Image<TPixel> source, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            return Glow(source, rectangle, GraphicsOptions.Default);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the glow.</param>
        /// <param name="radius">The the radius.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Glow<TPixel>(this Image<TPixel> source, TPixel color, float radius, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            return Glow(source, color, radius, rectangle, GraphicsOptions.Default);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting things like blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Glow<TPixel>(this Image<TPixel> source, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return Glow(source, NamedColors<TPixel>.Black, source.Bounds.Width * .5F, source.Bounds, options);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the glow.</param>
        /// <param name="options">The options effecting things like blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Glow<TPixel>(this Image<TPixel> source, TPixel color, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return Glow(source, color, source.Bounds.Width * .5F, source.Bounds, options);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The the radius.</param>
        /// <param name="options">The options effecting things like blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Glow<TPixel>(this Image<TPixel> source, float radius, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return Glow(source, NamedColors<TPixel>.Black, radius, source.Bounds, options);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="options">The options effecting things like blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Glow<TPixel>(this Image<TPixel> source, Rectangle rectangle, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return Glow(source, NamedColors<TPixel>.Black, 0, rectangle, options);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the glow.</param>
        /// <param name="radius">The the radius.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="options">The options effecting things like blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Glow<TPixel>(this Image<TPixel> source, TPixel color, float radius, Rectangle rectangle, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            var processor = new GlowProcessor<TPixel>(color, options) { Radius = radius, };
            source.ApplyProcessor(processor, rectangle);
            return source;
        }
    }
}
