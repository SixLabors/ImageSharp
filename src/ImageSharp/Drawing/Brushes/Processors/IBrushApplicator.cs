// <copyright file="IBrushApplicator.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// primitive that converts a point in to a color for discovering the fill color based on an implementation
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <seealso cref="System.IDisposable" />
    public interface IBrushApplicator<TColor> : IDisposable // disposable will be required if/when there is an ImageBrush
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Gets the color for a single pixel.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>The color</returns>
        TColor GetColor(Vector2 point);
    }
}
