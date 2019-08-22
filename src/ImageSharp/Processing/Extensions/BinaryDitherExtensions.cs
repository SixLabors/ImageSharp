// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions to apply binary dithering on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class BinaryDitherExtensions
    {
        /// <summary>
        /// Dithers the image reducing it to two colors using ordered dithering.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext
            BinaryDither(this IImageProcessingContext source, IOrderedDither dither) =>
            source.ApplyProcessor(new BinaryOrderedDitherProcessor(dither));

        /// <summary>
        /// Dithers the image reducing it to two colors using ordered dithering.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryDither(
            this IImageProcessingContext source,
            IOrderedDither dither,
            Color upperColor,
            Color lowerColor) =>
            source.ApplyProcessor(new BinaryOrderedDitherProcessor(dither, upperColor, lowerColor));

        /// <summary>
        /// Dithers the image reducing it to two colors using ordered dithering.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryDither(
            this IImageProcessingContext source,
            IOrderedDither dither,
            Rectangle rectangle) =>
            source.ApplyProcessor(new BinaryOrderedDitherProcessor(dither), rectangle);

        /// <summary>
        /// Dithers the image reducing it to two colors using ordered dithering.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryDither(
            this IImageProcessingContext source,
            IOrderedDither dither,
            Color upperColor,
            Color lowerColor,
            Rectangle rectangle) =>
            source.ApplyProcessor(new BinaryOrderedDitherProcessor(dither, upperColor, lowerColor), rectangle);
    }
}