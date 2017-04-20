// <copyright file="IImageBase{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

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

        /// <summary>
        /// Locks the image providing access to the pixels.
        /// <remarks>
        /// It is imperative that the accessor is correctly disposed off after use.
        /// </remarks>
        /// </summary>
        /// <returns>The <see cref="PixelAccessor{TPixel}"/></returns>
        PixelAccessor<TPixel> Lock();
    }
}