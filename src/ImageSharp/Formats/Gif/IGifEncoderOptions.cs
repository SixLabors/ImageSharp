// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// The configuration options used for encoding gifs.
    /// </summary>
    internal interface IGifEncoderOptions
    {
        /// <summary>
        /// Gets the quantizer used to generate the color palette.
        /// </summary>
        IQuantizer Quantizer { get; }

        /// <summary>
        /// Gets the color table mode: Global or local.
        /// </summary>
        GifColorTableMode? ColorTableMode { get; }

        /// <summary>
        /// Gets the <see cref="IPixelSamplingStrategy"/> used for quantization when building a global color table.
        /// </summary>
        IPixelSamplingStrategy GlobalPixelSamplingStrategy { get; }
    }
}
