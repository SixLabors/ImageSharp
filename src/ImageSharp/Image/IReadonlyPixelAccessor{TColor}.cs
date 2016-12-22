// <copyright file="IReadonlyPixelAccessor{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Provides per-pixel readonly access to generic <see cref="Image{TColor}"/> pixels.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public interface IReadonlyPixelAccessor<TColor>
    {
        /// <summary>
        /// Gets the size of a single pixel in the number of bytes.
        /// </summary>
        int PixelSize { get; }

        /// <summary>
        /// Gets the width of one row in the number of bytes.
        /// </summary>
        int RowStride { get; }

        /// <summary>
        /// Gets the width of the image.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the height of the image.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets or sets the pixel at the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than zero and smaller than the width of the pixel.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than zero and smaller than the width of the pixel.</param>
        /// <returns>The <see typeparam="TColor"/> at the specified position.</returns>
        TColor this[int x, int y]
        {
            get;
        }
    }
}