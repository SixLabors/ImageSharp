// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Dithering;

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
            BinaryDither(this IImageProcessingContext source, IDither dither) =>
            BinaryDither(source, dither, Color.White, Color.Black);

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
            IDither dither,
            Color upperColor,
            Color lowerColor) =>
            source.ApplyProcessor(new PaletteDitherProcessor(dither, new[] { upperColor, lowerColor }));

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
            IDither dither,
            Rectangle rectangle) =>
            BinaryDither(source, dither, Color.White, Color.Black, rectangle);

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
            IDither dither,
            Color upperColor,
            Color lowerColor,
            Rectangle rectangle) =>
            source.ApplyProcessor(new PaletteDitherProcessor(dither, new[] { upperColor, lowerColor }), rectangle);
    }
}
