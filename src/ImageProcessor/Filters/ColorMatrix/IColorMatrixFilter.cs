// <copyright file="IColorMatrixFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System.Numerics;

    /// <summary>
    /// Encapsulates properties and methods for creating processors that utilize a matrix to
    /// alter the image pixels.
    /// </summary>
    public interface IColorMatrixFilter : IImageProcessor
    {
        /// <summary>
        /// Gets the <see cref="Matrix4x4"/> used to alter the image.
        /// </summary>
        Matrix4x4 Matrix { get; }

        /// <summary>
        /// Gets a value indicating whether to compand the value on processing.
        /// </summary>
        bool Compand { get; }
    }
}
