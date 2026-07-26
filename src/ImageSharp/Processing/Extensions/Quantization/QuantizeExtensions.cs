// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Processing;

/// <summary>
/// Defines extensions that allow the application of quantizing algorithms on an <see cref="Image"/>
/// using Mutate/Clone.
/// </summary>
public static class QuantizeExtensions
{
    /// <summary>
    /// Applies quantization to the image using the <see cref="HexadecatreeQuantizer"/>.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext Quantize(this IImageProcessingContext source) =>
        Quantize(source, KnownQuantizers.Hexadecatree);

    /// <summary>
    /// Applies quantization to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="quantizer">The quantizer to apply to perform the operation.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext Quantize(this IImageProcessingContext source, IQuantizer quantizer) =>
        source.ApplyProcessor(new QuantizeProcessor(quantizer));

    /// <summary>
    /// Applies quantization to the image using the <see cref="HexadecatreeQuantizer"/>.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext Quantize(this IImageProcessingContext source, Rectangle rectangle) =>
        Quantize(source, KnownQuantizers.Hexadecatree, rectangle);

    /// <summary>
    /// Applies quantization to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="quantizer">The quantizer to apply to perform the operation.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext Quantize(this IImageProcessingContext source, IQuantizer quantizer, Rectangle rectangle) =>
        source.ApplyProcessor(new QuantizeProcessor(quantizer), rectangle);
}
