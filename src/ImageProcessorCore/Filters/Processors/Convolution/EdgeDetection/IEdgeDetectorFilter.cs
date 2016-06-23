// <copyright file="IEdgeDetectorFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// Provides properties and methods allowing the detection of edges within an image.
    /// </summary>
    public interface IEdgeDetectorFilter : IImageProcessor
    {
        /// <summary>
        /// Gets or sets a value indicating whether to convert the
        /// image to greyscale before performing edge detection.
        /// </summary>
        bool Greyscale { get; set; }
    }
}
