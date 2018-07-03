// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds binary thresholding extensions to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class BinaryThresholdExtensions
    {
        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BinaryThreshold<TPixel>(this IImageProcessingContext<TPixel> source, float threshold)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BinaryThresholdProcessor<TPixel>(threshold));

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BinaryThreshold<TPixel>(this IImageProcessingContext<TPixel> source, float threshold, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BinaryThresholdProcessor<TPixel>(threshold), rectangle);

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BinaryThreshold<TPixel>(this IImageProcessingContext<TPixel> source, float threshold, TPixel upperColor, TPixel lowerColor)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BinaryThresholdProcessor<TPixel>(threshold, upperColor, lowerColor));

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BinaryThreshold<TPixel>(this IImageProcessingContext<TPixel> source, float threshold, TPixel upperColor, TPixel lowerColor, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BinaryThresholdProcessor<TPixel>(threshold, upperColor, lowerColor), rectangle);
    }
}
