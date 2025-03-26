// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Defines a delegate for processing a row of pixels in an image for quantization.
/// </summary>
/// <typeparam name="TPixel">Represents a pixel type that can be processed in a quantizing operation.</typeparam>
internal interface IQuantizingPixelRowDelegate<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the transparent color mode to use when adding colors to the palette.
    /// </summary>
    public TransparentColorMode TransparentColorMode { get; }

    /// <summary>
    /// Processes a row of pixels for quantization.
    /// </summary>
    /// <param name="row">The row of pixels to process.</param>
    /// <param name="rowIndex">The index of the row being processed.</param>
    public void Invoke(ReadOnlySpan<TPixel> row, int rowIndex);
}
