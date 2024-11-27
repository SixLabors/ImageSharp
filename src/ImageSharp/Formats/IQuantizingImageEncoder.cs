// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Defines the contract for all image encoders that allow color palette generation via quantization.
/// </summary>
public interface IQuantizingImageEncoder
{
    /// <summary>
    /// Gets the quantizer used to generate the color palette.
    /// </summary>
    IQuantizer? Quantizer { get; }

    /// <summary>
    /// Gets the <see cref="IPixelSamplingStrategy"/> used for quantization when building color palettes.
    /// </summary>
    IPixelSamplingStrategy PixelSamplingStrategy { get; }
}

/// <summary>
/// Acts as a base class for all image encoders that allow color palette generation via quantization.
/// </summary>
public abstract class QuantizingImageEncoder : AlphaAwareImageEncoder, IQuantizingImageEncoder
{
    /// <inheritdoc/>
    public IQuantizer? Quantizer { get; init; }

    /// <inheritdoc/>
    public IPixelSamplingStrategy PixelSamplingStrategy { get; init; } = new DefaultPixelSamplingStrategy();
}

/// <summary>
/// Acts as a base class for all image encoders that allow color palette generation via quantization when
/// encoding animation sequences.
/// </summary>
public abstract class QuantizingAnimatedImageEncoder : QuantizingImageEncoder, IAnimatedImageEncoder
{
    /// <inheritdoc/>
    public Color? BackgroundColor { get; }

    /// <inheritdoc/>
    public ushort? RepeatCount { get; }

    /// <inheritdoc/>
    public bool? AnimateRootFrame { get; }
}
