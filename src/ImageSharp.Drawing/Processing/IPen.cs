// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Interface representing a Pen
    /// </summary>
    /// <typeparam name="TPixel">The type of the color.</typeparam>
    public interface IPen<TPixel> : IPen
            where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the stroke fill.
        /// </summary>
        IBrush<TPixel> StrokeFill { get; }
    }

    /// <summary>
    /// Interface representing the pattern and size of the stroke to apply with a Pen.
    /// </summary>
    public interface IPen
    {
        /// <summary>
        /// Gets the width to apply to the stroke
        /// </summary>
        float StrokeWidth { get; }

        /// <summary>
        /// Gets the stoke pattern.
        /// </summary>
        ReadOnlySpan<float> StrokePattern { get; }
    }
}
