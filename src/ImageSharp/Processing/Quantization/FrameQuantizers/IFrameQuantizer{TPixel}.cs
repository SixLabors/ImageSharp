// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Dithering.ErrorDiffusion;

namespace SixLabors.ImageSharp.Processing.Quantization.FrameQuantizers
{
    /// <summary>
    /// Provides methods to allow the execution of the quantization process on an image frame.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public interface IFrameQuantizer<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets a value indicating whether to apply dithering to the output image.
        /// </summary>
        bool Dither { get; }

        /// <summary>
        /// Gets the error diffusion algorithm to apply to the output image.
        /// </summary>
        IErrorDiffuser Diffuser { get; }

        /// <summary>
        /// Quantize an image frame and return the resulting output pixels.
        /// </summary>
        /// <param name="image">The image to quantize.</param>
        /// <param name="quantizedPixels">A buffer to write the quantized image pixels.</param>
        /// <param name="quantizedPalette">The quantized image palette.</param>
        void QuantizeFrame(ImageFrame<TPixel> image, Span<byte> quantizedPixels, out TPixel[] quantizedPalette);
    }
}