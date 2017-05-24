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
        /// Gets the pixels as an array of the given packed pixel format.
        /// Important. Due to the nature in the way this is constructed do not rely on the length
        /// of the array for calculations. Use Width * Height.
        /// </summary>
        TPixel[] Pixels { get; }
    }
}