// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Allows the mapping of input colors to colors within a given palette.
    /// TODO: Expose this somehow.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal interface IPixelMap<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the color palette containing colors to match.
        /// </summary>
        ReadOnlyMemory<TPixel> Palette { get; }

        /// <summary>
        /// Returns the closest color in the palette and the index of that pixel.
        /// </summary>
        /// <param name="color">The color to match.</param>
        /// <param name="match">The matched color.</param>
        /// <returns>The <see cref="int"/> index.</returns>
        int GetClosestColor(TPixel color, out TPixel match);
    }
}
