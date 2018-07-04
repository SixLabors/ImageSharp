// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
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
        /// <returns>
        /// A <see cref="QuantizedFrame{TPixel}"/> representing a quantized version of the image pixels.
        /// </returns>
        QuantizedFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> image);
    }
}