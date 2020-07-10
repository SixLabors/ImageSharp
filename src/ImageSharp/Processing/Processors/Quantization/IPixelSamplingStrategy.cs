// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Provides an abstraction to enumerate pixel regions within a multi-framed <see cref="Image{TPixel}"/>.
    /// </summary>
    public interface IPixelSamplingStrategy
    {
        /// <summary>
        /// Enumerates pixel regions within the image as <see cref="Buffer2DRegion{T}"/>.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <returns>An enumeration of pixel regions.</returns>
        IEnumerable<Buffer2DRegion<TPixel>> EnumeratePixelRegions<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>;
    }
}
