// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing.Processors.Overlays;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the application of a radial glow to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class GlowExtensions
    {
        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Glow<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => Glow(source, GraphicsOptions.Default);

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the glow.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Glow<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color)
            where TPixel : struct, IPixel<TPixel>
        {
            return Glow(source, GraphicsOptions.Default, color);
        }

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The the radius.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Glow<TPixel>(this IImageProcessingContext<TPixel> source, float radius)
            where TPixel : struct, IPixel<TPixel>
            => Glow(source, GraphicsOptions.Default, radius);

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Glow<TPixel>(this IImageProcessingContext<TPixel> source, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.Glow(GraphicsOptions.Default, rectangle);

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
        public static IImageProcessingContext<TPixel> Glow<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, float radius, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.Glow(GraphicsOptions.Default, color, ValueSize.Absolute(radius), rectangle);

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting things like blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Glow<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
            => source.Glow(options, NamedColors<TPixel>.Black, ValueSize.PercentageOfWidth(0.5f));

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting things like blending.</param>
        /// <param name="color">The color to set as the glow.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Glow<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, TPixel color)
            where TPixel : struct, IPixel<TPixel>
            => source.Glow(options, color, ValueSize.PercentageOfWidth(0.5f));

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting things like blending.</param>
        /// <param name="radius">The the radius.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Glow<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, float radius)
            where TPixel : struct, IPixel<TPixel>
            => source.Glow(options, NamedColors<TPixel>.Black, ValueSize.Absolute(radius));

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting things like blending.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Glow<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.Glow(options, NamedColors<TPixel>.Black, ValueSize.PercentageOfWidth(0.5f), rectangle);

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting things like blending.</param>
        /// <param name="color">The color to set as the glow.</param>
        /// <param name="radius">The the radius.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Glow<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, TPixel color, float radius, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.Glow(options, color, ValueSize.Absolute(radius), rectangle);

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting things like blending.</param>
        /// <param name="color">The color to set as the glow.</param>
        /// <param name="radius">The the radius.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        private static IImageProcessingContext<TPixel> Glow<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, TPixel color, ValueSize radius, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new GlowProcessor<TPixel>(color, radius, options), rectangle);

        /// <summary>
        /// Applies a radial glow effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting things like blending.</param>
        /// <param name="color">The color to set as the glow.</param>
        /// <param name="radius">The the radius.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        private static IImageProcessingContext<TPixel> Glow<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, TPixel color, ValueSize radius)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new GlowProcessor<TPixel>(color, radius, options));
    }
}