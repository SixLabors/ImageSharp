// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Implements an algorithm to alter the pixels of an image via palette dithering.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public interface IPaletteDitherImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the configuration instance to use when performing operations.
        /// </summary>
        Configuration Configuration { get; }

        /// <summary>
        /// Gets the dithering palette.
        /// </summary>
        ReadOnlyMemory<TPixel> Palette { get; }

        /// <summary>
        /// Gets the dithering scale used to adjust the amount of dither. Range 0..1.
        /// </summary>
        float DitherScale { get; }

        /// <summary>
        /// Returns the color from the dithering palette corresponding to the given color.
        /// </summary>
        /// <param name="color">The color to match.</param>
        /// <returns>The <typeparamref name="TPixel"/> match.</returns>
        TPixel GetPaletteColor(TPixel color);
    }
}
