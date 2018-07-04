// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds dithering extensions to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class DitherExtensions
    {
        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using Bayer4x4 ordered dithering.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Dither<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => Dither(source, KnownDitherers.BayerDither4x4);

        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using ordered dithering.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Dither<TPixel>(this IImageProcessingContext<TPixel> source, IOrderedDither dither)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new OrderedDitherPaletteProcessor<TPixel>(dither));

        /// <summary>
        /// Dithers the image reducing it to the given palette using ordered dithering.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="palette">The palette to select substitute colors from.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Dither<TPixel>(this IImageProcessingContext<TPixel> source, IOrderedDither dither, TPixel[] palette)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new OrderedDitherPaletteProcessor<TPixel>(dither, palette));

        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using ordered dithering.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Dither<TPixel>(this IImageProcessingContext<TPixel> source, IOrderedDither dither, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new OrderedDitherPaletteProcessor<TPixel>(dither), rectangle);

        /// <summary>
        /// Dithers the image reducing it to the given palette using ordered dithering.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="palette">The palette to select substitute colors from.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Dither<TPixel>(this IImageProcessingContext<TPixel> source, IOrderedDither dither, TPixel[] palette, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new OrderedDitherPaletteProcessor<TPixel>(dither, palette), rectangle);
    }
}