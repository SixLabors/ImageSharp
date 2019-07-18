// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Dithering
{
    /// <summary>
    /// Defines extension methods to apply diffusion to an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class DiffuseExtensions
    {
        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using error diffusion.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Diffuse(this IImageProcessingContext source) =>
            Diffuse(source, KnownDiffusers.FloydSteinberg, .5F);

        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using error diffusion.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Diffuse(this IImageProcessingContext source, float threshold) =>
            Diffuse(source, KnownDiffusers.FloydSteinberg, threshold);

        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using error diffusion.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Diffuse(
            this IImageProcessingContext source,
            IErrorDiffuser diffuser,
            float threshold) =>
            source.ApplyProcessor(new ErrorDiffusionPaletteProcessor(diffuser, threshold));

        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using error diffusion.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Diffuse(
            this IImageProcessingContext source,
            IErrorDiffuser diffuser,
            float threshold,
            Rectangle rectangle) =>
            source.ApplyProcessor(new ErrorDiffusionPaletteProcessor(diffuser, threshold), rectangle);

        /// <summary>
        /// Dithers the image reducing it to the given palette using error diffusion.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="palette">The palette to select substitute colors from.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Diffuse(
            this IImageProcessingContext source,
            IErrorDiffuser diffuser,
            float threshold,
            ReadOnlyMemory<Color> palette) =>
            source.ApplyProcessor(new ErrorDiffusionPaletteProcessor(diffuser, threshold, palette));

        /// <summary>
        /// Dithers the image reducing it to the given palette using error diffusion.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="palette">The palette to select substitute colors from.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Diffuse(
            this IImageProcessingContext source,
            IErrorDiffuser diffuser,
            float threshold,
            ReadOnlyMemory<Color> palette,
            Rectangle rectangle) =>
            source.ApplyProcessor(new ErrorDiffusionPaletteProcessor(diffuser, threshold, palette), rectangle);
    }
}