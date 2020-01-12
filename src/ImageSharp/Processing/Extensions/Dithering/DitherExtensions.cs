// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines dithering extensions to apply on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class DitherExtensions
    {
        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using Bayer4x4 ordered dithering.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Dither(this IImageProcessingContext source) =>
            Dither(source, KnownDitherers.BayerDither4x4);

        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using ordered dithering.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Dither(this IImageProcessingContext source, IOrderedDither dither) =>
            source.ApplyProcessor(new OrderedDitherPaletteProcessor(dither));

        /// <summary>
        /// Dithers the image reducing it to the given palette using ordered dithering.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="palette">The palette to select substitute colors from.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Dither(
            this IImageProcessingContext source,
            IOrderedDither dither,
            ReadOnlyMemory<Color> palette) =>
            source.ApplyProcessor(new OrderedDitherPaletteProcessor(dither, palette));

        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using ordered dithering.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Dither(
            this IImageProcessingContext source,
            IOrderedDither dither,
            Rectangle rectangle) =>
            source.ApplyProcessor(new OrderedDitherPaletteProcessor(dither), rectangle);

        /// <summary>
        /// Dithers the image reducing it to the given palette using ordered dithering.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="palette">The palette to select substitute colors from.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Dither(
            this IImageProcessingContext source,
            IOrderedDither dither,
            ReadOnlyMemory<Color> palette,
            Rectangle rectangle) =>
            source.ApplyProcessor(new OrderedDitherPaletteProcessor(dither, palette), rectangle);
    }
}