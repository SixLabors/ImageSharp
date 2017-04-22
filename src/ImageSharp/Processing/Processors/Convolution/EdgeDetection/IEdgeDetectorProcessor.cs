// <copyright file="IEdgeDetectorProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;

    using ImageSharp.PixelFormats;

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
        /// Gets or sets a value indicating whether to convert the image to grayscale before performing edge detection.
        /// </summary>
        bool Grayscale { get; set; }
    }
}