// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
        {
            return Vignette(source, GraphicsOptions.Default);
        }

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the vignette.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color)
            where TPixel : struct, IPixel<TPixel>
        {
            return Vignette(source, color, GraphicsOptions.Default);
        }

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
        {
            return Vignette(source, radiusX, radiusY, GraphicsOptions.Default);
        }

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
        {
            return Vignette(source, rectangle, GraphicsOptions.Default);
        }

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
         => source.Vignette(color, radiusX, radiusY, rectangle, GraphicsOptions.Default);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
         => source.VignetteInternal(NamedColors<TPixel>.Black, ValueSize.PercentageOfWidth(.5f), ValueSize.PercentageOfHeight(.5f), options);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the vignette.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
         => source.VignetteInternal(color, ValueSize.PercentageOfWidth(.5f), ValueSize.PercentageOfHeight(.5f), options);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radiusX">The the x-radius.</param>
        /// <param name="radiusY">The the y-radius.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, float radiusX, float radiusY, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
         => source.VignetteInternal(NamedColors<TPixel>.Black, radiusX, radiusY, options);

        /// <summary>
        /// Applies a radial vignette effect to an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, Rectangle rectangle, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
         => source.VignetteInternal(NamedColors<TPixel>.Black, ValueSize.PercentageOfWidth(.5f), ValueSize.PercentageOfHeight(.5f), rectangle, options);

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
        /// <param name="options">The options effecting pixel blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Vignette<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, float radiusX, float radiusY, Rectangle rectangle, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
         => source.VignetteInternal(color, radiusX, radiusY, rectangle, options);

        private static IImageProcessingContext<TPixel> VignetteInternal<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, ValueSize radiusX, ValueSize radiusY, Rectangle rectangle, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new VignetteProcessor<TPixel>(color, radiusX, radiusY, options), rectangle);

        private static IImageProcessingContext<TPixel> VignetteInternal<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, ValueSize radiusX, ValueSize radiusY, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new VignetteProcessor<TPixel>(color, radiusX, radiusY, options));
    }
}