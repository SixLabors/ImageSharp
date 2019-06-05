// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extension methods to apply binary diffusion on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class BinaryDiffuseExtensions
    {
        /// <summary>
        /// Dithers the image reducing it to two colors using error diffusion.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryDiffuse(
            this IImageProcessingContext source,
            IErrorDiffuser diffuser,
            float threshold) =>
            source.ApplyProcessor(new BinaryErrorDiffusionProcessor(diffuser, threshold));

        /// <summary>
        /// Dithers the image reducing it to two colors using error diffusion.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryDiffuse(
            this IImageProcessingContext source,
            IErrorDiffuser diffuser,
            float threshold,
            Rectangle rectangle) =>
            source.ApplyProcessor(new BinaryErrorDiffusionProcessor(diffuser, threshold), rectangle);

        /// <summary>
        /// Dithers the image reducing it to two colors using error diffusion.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryDiffuse(
            this IImageProcessingContext source,
            IErrorDiffuser diffuser,
            float threshold,
            Color upperColor,
            Color lowerColor) =>
            source.ApplyProcessor(new BinaryErrorDiffusionProcessor(diffuser, threshold, upperColor, lowerColor));

        /// <summary>
        /// Dithers the image reducing it to two colors using error diffusion.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryDiffuse(
            this IImageProcessingContext source,
            IErrorDiffuser diffuser,
            float threshold,
            Color upperColor,
            Color lowerColor,
            Rectangle rectangle) =>
            source.ApplyProcessor(
                new BinaryErrorDiffusionProcessor(diffuser, threshold, upperColor, lowerColor),
                rectangle);
    }
}