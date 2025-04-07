// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering;

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
    public Configuration Configuration { get; }

    /// <summary>
    /// Gets the dithering palette.
    /// </summary>
    public ReadOnlyMemory<TPixel> Palette { get; }

    /// <summary>
    /// Gets the dithering scale used to adjust the amount of dither. Range 0..1.
    /// </summary>
    public float DitherScale { get; }

    /// <summary>
    /// Returns the color from the dithering palette corresponding to the given color.
    /// </summary>
    /// <param name="color">The color to match.</param>
    /// <returns>The <typeparamref name="TPixel"/> match.</returns>
    public TPixel GetPaletteColor(TPixel color);
}
