// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds binary diffusion extensions to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class BinaryDiffuseExtensions
    {
        /// <summary>
        /// Dithers the image reducing it to two colors using error diffusion.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BinaryDiffuse<TPixel>(this IImageProcessingContext<TPixel> source, IErrorDiffuser diffuser, float threshold)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BinaryErrorDiffusionProcessor<TPixel>(diffuser, threshold));

        /// <summary>
        /// Dithers the image reducing it to two colors using error diffusion.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BinaryDiffuse<TPixel>(this IImageProcessingContext<TPixel> source, IErrorDiffuser diffuser, float threshold, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BinaryErrorDiffusionProcessor<TPixel>(diffuser, threshold), rectangle);

        /// <summary>
        /// Dithers the image reducing it to two colors using error diffusion.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BinaryDiffuse<TPixel>(this IImageProcessingContext<TPixel> source, IErrorDiffuser diffuser, float threshold, TPixel upperColor, TPixel lowerColor)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BinaryErrorDiffusionProcessor<TPixel>(diffuser, threshold, upperColor, lowerColor));

        /// <summary>
        /// Dithers the image reducing it to two colors using error diffusion.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BinaryDiffuse<TPixel>(this IImageProcessingContext<TPixel> source, IErrorDiffuser diffuser, float threshold, TPixel upperColor, TPixel lowerColor, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BinaryErrorDiffusionProcessor<TPixel>(diffuser, threshold, upperColor, lowerColor), rectangle);
    }
}