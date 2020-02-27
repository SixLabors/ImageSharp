// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
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
        /// <param name="palette">The quantized palette.</param>
        /// <param name="source">The source image.</param>
        /// <param name="output">The output target</param>
        /// <param name="bounds">The region of interest bounds.</param>
        void ApplyQuantizationDither<TFrameQuantizer, TPixel>(
            ref TFrameQuantizer quantizer,
            ReadOnlyMemory<TPixel> palette,
            ImageFrame<TPixel> source,
            Memory<byte> output,
            Rectangle bounds)
            where TFrameQuantizer : struct, IFrameQuantizer<TPixel>
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <summary>
        /// Transforms the image frame applying a dither matrix.
        /// This method should be treated as destructive, altering the input pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="palette">The quantized palette.</param>
        /// <param name="source">The source image.</param>
        /// <param name="bounds">The region of interest bounds.</param>
        /// <param name="scale">The dithering scale used to adjust the amount of dither. Range 0..1.</param>
        void ApplyPaletteDither<TPixel>(
            Configuration configuration,
            ReadOnlyMemory<TPixel> palette,
            ImageFrame<TPixel> source,
            Rectangle bounds,
            float scale)
            where TPixel : unmanaged, IPixel<TPixel>;
    }
}
