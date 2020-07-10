// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Defines the contract for types that apply dithering to images.
    /// </summary>
    public interface IDither
    {
        /// <summary>
        /// Transforms the quantized image frame applying a dither matrix.
        /// This method should be treated as destructive, altering the input pixels.
        /// </summary>
        /// <typeparam name="TFrameQuantizer">The type of frame quantizer.</typeparam>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="quantizer">The frame quantizer.</param>
        /// <param name="source">The source image.</param>
        /// <param name="destination">The destination quantized frame.</param>
        /// <param name="bounds">The region of interest bounds.</param>
        void ApplyQuantizationDither<TFrameQuantizer, TPixel>(
            ref TFrameQuantizer quantizer,
            ImageFrame<TPixel> source,
            IndexedImageFrame<TPixel> destination,
            Rectangle bounds)
            where TFrameQuantizer : struct, IQuantizer<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <summary>
        /// Transforms the image frame applying a dither matrix.
        /// This method should be treated as destructive, altering the input pixels.
        /// </summary>
        /// <typeparam name="TPaletteDitherImageProcessor">The type of palette dithering processor.</typeparam>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="processor">The palette dithering processor.</param>
        /// <param name="source">The source image.</param>
        /// <param name="bounds">The region of interest bounds.</param>
        void ApplyPaletteDither<TPaletteDitherImageProcessor, TPixel>(
            in TPaletteDitherImageProcessor processor,
            ImageFrame<TPixel> source,
            Rectangle bounds)
            where TPaletteDitherImageProcessor : struct, IPaletteDitherImageProcessor<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>;
    }
}
