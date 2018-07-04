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
    public static class VignetteExtensions
    {
        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => Vignette(source, GraphicsOptions.Default);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the vignette.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color)
            where TPixel : struct, IPixel<TPixel>
            => Vignette(source, GraphicsOptions.Default, color);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radiusX">The the x-radius.</param>
        /// <param name="radiusY">The the y-radius.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, float radiusX, float radiusY)
            where TPixel : struct, IPixel<TPixel>
            => Vignette(source, GraphicsOptions.Default, radiusX, radiusY);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => Vignette(source, GraphicsOptions.Default, rectangle);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the vignette.</param>
        /// <param name="radiusX">The the x-radius.</param>
        /// <param name="radiusY">The the y-radius.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, float radiusX, float radiusY, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.Vignette(GraphicsOptions.Default, color, radiusX, radiusY, rectangle);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
            => source.VignetteInternal(options, NamedColors<TPixel>.Black, ValueSize.PercentageOfWidth(.5f), ValueSize.PercentageOfHeight(.5f));

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <param name="color">The color to set as the vignette.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, TPixel color)
            where TPixel : struct, IPixel<TPixel>
            => source.VignetteInternal(options, color, ValueSize.PercentageOfWidth(.5f), ValueSize.PercentageOfHeight(.5f));

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <param name="radiusX">The the x-radius.</param>
        /// <param name="radiusY">The the y-radius.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, float radiusX, float radiusY)
            where TPixel : struct, IPixel<TPixel>
            => source.VignetteInternal(options, NamedColors<TPixel>.Black, radiusX, radiusY);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.VignetteInternal(options, NamedColors<TPixel>.Black, ValueSize.PercentageOfWidth(.5f), ValueSize.PercentageOfHeight(.5f), rectangle);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <param name="color">The color to set as the vignette.</param>
        /// <param name="radiusX">The the x-radius.</param>
        /// <param name="radiusY">The the y-radius.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, TPixel color, float radiusX, float radiusY, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.VignetteInternal(options, color, radiusX, radiusY, rectangle);

        private static IImageProcessingContext<TPixel> VignetteInternal<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, TPixel color, ValueSize radiusX, ValueSize radiusY, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new VignetteProcessor<TPixel>(color, radiusX, radiusY, options), rectangle);

        private static IImageProcessingContext<TPixel> VignetteInternal<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, TPixel color, ValueSize radiusX, ValueSize radiusY)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new VignetteProcessor<TPixel>(color, radiusX, radiusY, options));
    }
}