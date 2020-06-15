// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Implements an algorithm to alter the pixels of an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public interface IImageProcessor<TPixel> : IDisposable
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Executes the process against the specified <see cref="Image{TPixel}"/>.
        /// </summary>
        void Execute();
    }
}
