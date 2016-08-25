// <copyright file="IEdgeDetectorFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// Provides properties and methods allowing the detection of edges within an image.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public interface IEdgeDetectorFilter<TColor, TPacked> : IImageProcessor<TColor, TPacked>, IEdgeDetectorFilter
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
    }

    /// <summary>
    /// Provides properties and methods allowing the detection of edges within an image.
    /// </summary>
    public interface IEdgeDetectorFilter
    {
        /// <summary>
        /// Gets or sets a value indicating whether to convert the
        /// image to Grayscale before performing edge detection.
        /// </summary>
        bool Grayscale { get; set; }
    }
}
