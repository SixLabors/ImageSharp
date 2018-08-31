// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Dithering
{
    /// <summary>
    /// Adds diffusion extensions to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class DiffuseExtensions
    {
        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using error diffusion.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Diffuse<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => Diffuse(source, KnownDiffusers.FloydSteinberg, .5F);

        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using error diffusion.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Diffuse<TPixel>(this IImageProcessingContext<TPixel> source, float threshold)
            where TPixel : struct, IPixel<TPixel>
            => Diffuse(source, KnownDiffusers.FloydSteinberg, threshold);

        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using error diffusion.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Diffuse<TPixel>(this IImageProcessingContext<TPixel> source, IErrorDiffuser diffuser, float threshold)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new ErrorDiffusionPaletteProcessor<TPixel>(diffuser, threshold));

        /// <summary>
        /// Dithers the image reducing it to a web-safe palette using error diffusion.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Diffuse<TPixel>(this IImageProcessingContext<TPixel> source, IErrorDiffuser diffuser, float threshold, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new ErrorDiffusionPaletteProcessor<TPixel>(diffuser, threshold), rectangle);

        /// <summary>
        /// Dithers the image reducing it to the given palette using error diffusion.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="palette">The palette to select substitute colors from.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Diffuse<TPixel>(this IImageProcessingContext<TPixel> source, IErrorDiffuser diffuser, float threshold, TPixel[] palette)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new ErrorDiffusionPaletteProcessor<TPixel>(diffuser, threshold, palette));

        /// <summary>
        /// Dithers the image reducing it to the given palette using error diffusion.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="palette">The palette to select substitute colors from.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Diffuse<TPixel>(this IImageProcessingContext<TPixel> source, IErrorDiffuser diffuser, float threshold, TPixel[] palette, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new ErrorDiffusionPaletteProcessor<TPixel>(diffuser, threshold, palette), rectangle);
    }
}