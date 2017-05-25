// <copyright file="IImageBase{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Encapsulates the basic properties and methods required to manipulate images in varying formats.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public interface IImageBase<TPixel> : IImageBase, IDisposable
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the representation of the pixels as an area of contiguous memory in the given pixel format.
        /// </summary>
        Span<TPixel> Pixels { get; }
    }
}