// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Provides methods to allow the execution of the quantization process on an image frame.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
public interface IQuantizer<TPixel> : IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public Configuration Configuration { get; }

    /// <summary>
    /// Gets the quantizer options defining quantization rules.
    /// </summary>
    public QuantizerOptions Options { get; }

    /// <summary>
    /// Gets the quantized color palette.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The palette has not been built via <see cref="AddPaletteColors(in Buffer2DRegion{TPixel})"/>.
    /// </exception>
    public ReadOnlyMemory<TPixel> Palette { get; }

    /// <summary>
    /// Adds colors to the quantized palette from the given pixel source.
    /// </summary>
    /// <param name="pixelRegion">The <see cref="Buffer2DRegion{T}"/> of source pixels to register.</param>
    public void AddPaletteColors(in Buffer2DRegion<TPixel> pixelRegion);

    /// <summary>
    /// Quantizes an image frame and return the resulting output pixels.
    /// </summary>
    /// <param name="source">The source image frame to quantize.</param>
    /// <param name="bounds">The bounds within the frame to quantize.</param>
    /// <returns>
    /// A <see cref="IndexedImageFrame{TPixel}"/> representing a quantized version of the source frame pixels.
    /// </returns>
    /// <remarks>
    /// Only executes the second (quantization) step. The palette has to be built by calling <see cref="AddPaletteColors(in Buffer2DRegion{TPixel})"/>.
    /// To run both steps, use <see cref="QuantizerUtilities.BuildPaletteAndQuantizeFrame{TPixel}(IQuantizer{TPixel}, ImageFrame{TPixel}, Rectangle)"/>.
    /// </remarks>
    public IndexedImageFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> source, Rectangle bounds);

    /// <summary>
    /// Returns the index and color from the quantized palette corresponding to the given color.
    /// </summary>
    /// <param name="color">The color to match.</param>
    /// <param name="match">The matched color.</param>
    /// <returns>The <see cref="byte"/> index.</returns>
    public byte GetQuantizedColor(TPixel color, out TPixel match);

    // TODO: Enable bulk operations.
    // void GetQuantizedColors(ReadOnlySpan<TPixel> colors, ReadOnlySpan<TPixel> palette, Span<byte> indices, Span<TPixel> matches);
}
