// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Dithering.ErrorDiffusion;
using SixLabors.ImageSharp.Processing.Quantization.FrameQuantizers;

namespace SixLabors.ImageSharp.Processing.Quantization
{
    /// <summary>
    /// Provides methods for allowing quantization of images pixels with configurable dithering.
    /// </summary>
    public interface IQuantizer
    {
        /// <summary>
        /// Gets a value indicating whether to apply dithering to the output image.
        /// </summary>
        bool Dither { get; }

        /// <summary>
        /// Gets the dithering algorithm to apply to the output image.
        /// </summary>
        IErrorDiffuser DitherType { get; }

        /// <summary>
        /// Creates the generic frame quantizer
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>The <see cref="IFrameQuantizer{TPixel}"/></returns>
        IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>()
            where TPixel : struct, IPixel<TPixel>;
    }
}