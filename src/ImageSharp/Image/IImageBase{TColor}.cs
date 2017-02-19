// <copyright file="IImageBase{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// Encapsulates the basic properties and methods required to manipulate images in varying formats.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public interface IImageBase<TColor> : IImageBase, IDisposable
        where TColor : struct, IPixel<TColor>
    {
        /// <summary>
        /// Gets the pixels as an array of the given packed pixel format.
        /// Important. Due to the nature in the way this is constructed do not rely on the length
        /// of the array for calculations. Use Width * Height.
        /// </summary>
        TColor[] Pixels { get; }

        /// <summary>
        /// Sets the size of the pixel array of the image to the given width and height.
        /// </summary>
        /// <param name="width">The new width of the image. Must be greater than zero.</param>
        /// <param name="height">The new height of the image. Must be greater than zero.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown if either <paramref name="width"/> or <paramref name="height"/> are less than or equal to 0.
        /// </exception>
        void InitPixels(int width, int height);

        /// <summary>
        /// Locks the image providing access to the pixels.
        /// <remarks>
        /// It is imperative that the accessor is correctly disposed off after use.
        /// </remarks>
        /// </summary>
        /// <returns>The <see cref="PixelAccessor{TColor}"/></returns>
        PixelAccessor<TColor> Lock();
    }
}