// <copyright file="ImageProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processors
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Allows the application of processors to images.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public abstract class ImageProcessor<TColor> : IImageProcessor
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <inheritdoc/>
        public virtual ParallelOptions ParallelOptions { get; set; }

        /// <inheritdoc/>
        public virtual bool Compand { get; set; } = false;
    }
}