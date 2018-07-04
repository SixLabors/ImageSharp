// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Provides properties and methods allowing the detection of edges within an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public interface IEdgeDetectorProcessor<TPixel> : IImageProcessor<TPixel>, IEdgeDetectorProcessor
        where TPixel : struct, IPixel<TPixel>
    {
    }

    /// <summary>
    /// Provides properties and methods allowing the detection of edges within an image.
    /// </summary>
    public interface IEdgeDetectorProcessor
    {
        /// <summary>
        /// Gets a value indicating whether to convert the image to grayscale before performing edge detection.
        /// </summary>
        bool Grayscale { get; }
    }
}