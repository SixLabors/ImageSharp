// <copyright file="IBrushApplicator.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// primitive that converts a point in to a color for discoving the fill color based on an implmentation
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    /// <typeparam name="TPacked">The type of the packed.</typeparam>
    /// <seealso cref="System.IDisposable" />
    public interface IBrushApplicator<TColor, TPacked> : IDisposable // disposable will be required if/when there is an ImageBrush
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Gets the color for a single pixel.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>The color</returns>
        TColor GetColor(Vector2 point);
    }
}
