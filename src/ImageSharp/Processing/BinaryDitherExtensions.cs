// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds binary dithering extensions to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class BinaryDitherExtensions
    {
        /// <summary>
        /// Dithers the image reducing it to two colors using ordered dithering.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BinaryDither<TPixel>(this IImageProcessingContext<TPixel> source, IOrderedDither dither)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BinaryOrderedDitherProcessor<TPixel>(dither));

        /// <summary>
        /// Dithers the image reducing it to two colors using ordered dithering.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BinaryDither<TPixel>(this IImageProcessingContext<TPixel> source, IOrderedDither dither, TPixel upperColor, TPixel lowerColor)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BinaryOrderedDitherProcessor<TPixel>(dither, upperColor, lowerColor));

        /// <summary>
        /// Dithers the image reducing it to two colors using ordered dithering.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BinaryDither<TPixel>(this IImageProcessingContext<TPixel> source, IOrderedDither dither, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BinaryOrderedDitherProcessor<TPixel>(dither), rectangle);

        /// <summary>
        /// Dithers the image reducing it to two colors using ordered dithering.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BinaryDither<TPixel>(this IImageProcessingContext<TPixel> source, IOrderedDither dither, TPixel upperColor, TPixel lowerColor, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BinaryOrderedDitherProcessor<TPixel>(dither, upperColor, lowerColor), rectangle);
    }
}