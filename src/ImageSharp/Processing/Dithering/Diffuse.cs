// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Dithering;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
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
        {
            source.ApplyProcessor(new ErrorDiffusionPaletteProcessor<TPixel>(diffuser, threshold));
            return source;
        }

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
        {
            source.ApplyProcessor(new ErrorDiffusionPaletteProcessor<TPixel>(diffuser, threshold), rectangle);
            return source;
        }

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
        {
            source.ApplyProcessor(new ErrorDiffusionPaletteProcessor<TPixel>(diffuser, threshold, palette));
            return source;
        }

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
        {
            source.ApplyProcessor(new ErrorDiffusionPaletteProcessor<TPixel>(diffuser, threshold, palette), rectangle);
            return source;
        }
    }
}
