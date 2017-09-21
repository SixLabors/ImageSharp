// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
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
        /// Alters the colors of the image recreating an old Lomograph camera effect.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Lomograph<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
        {
            return Lomograph(source, GraphicsOptions.Default);
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Lomograph camera effect.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Lomograph<TPixel>(this IImageProcessingContext<TPixel> source, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            return Lomograph(source, rectangle, GraphicsOptions.Default);
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Lomograph camera effect.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Lomograph<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            source.ApplyProcessor(new LomographProcessor<TPixel>(options));
            return source;
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Lomograph camera effect.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Lomograph<TPixel>(this IImageProcessingContext<TPixel> source, Rectangle rectangle, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            source.ApplyProcessor(new LomographProcessor<TPixel>(options), rectangle);
            return source;
        }
    }
}
