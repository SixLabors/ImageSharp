// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Defines options for quantization.
/// </summary>
public class QuantizerOptions
{
    private float ditherScale = QuantizerConstants.MaxDitherScale;
    private int maxColors = QuantizerConstants.MaxColors;
    private float threshold = QuantizerConstants.DefaultTransparencyThreshold;

    /// <summary>
    /// Gets or sets the  algorithm to apply to the output image.
    /// Defaults to <see cref="QuantizerConstants.DefaultDither"/>; set to <see langword="null"/> for no dithering.
    /// </summary>
    public IDither? Dither { get; set; } = QuantizerConstants.DefaultDither;

    /// <summary>
    /// Gets or sets the dithering scale used to adjust the amount of dither. Range 0..1.
    /// Defaults to <see cref="QuantizerConstants.MaxDitherScale"/>.
    /// </summary>
    public float DitherScale
    {
        get => this.ditherScale;
        set => this.ditherScale = Numerics.Clamp(value, QuantizerConstants.MinDitherScale, QuantizerConstants.MaxDitherScale);
    }

    /// <summary>
    /// Gets or sets the maximum number of colors to hold in the color palette. Range 0..256.
    /// Defaults to <see cref="QuantizerConstants.MaxColors"/>.
    /// </summary>
    public int MaxColors
    {
        get => this.maxColors;
        set => this.maxColors = Numerics.Clamp(value, QuantizerConstants.MinColors, QuantizerConstants.MaxColors);
    }

    /// <summary>
    /// Gets or sets the threshold at which to consider a pixel transparent. Range 0..1.
    /// Defaults to <see cref="QuantizerConstants.DefaultTransparencyThreshold"/>.
    /// </summary>
    public float TransparencyThreshold
    {
        get => this.threshold;
        set => this.threshold = Numerics.Clamp(value, QuantizerConstants.MinTransparencyThreshold, QuantizerConstants.MaxTransparencyThreshold);
    }

    /// <summary>
    /// Gets or sets the color used for replacing colors with an alpha component below the threshold.
    /// Defaults to <see cref="Color.Transparent"/>.
    /// </summary>
    public Color ThresholdReplacementColor { get; set; } = Color.Transparent;

    /// <summary>
    /// Gets or sets the color matching mode used for matching pixel values to palette colors.
    /// </summary>
    public ColorMatchingMode ColorMatchingMode { get; set; } = ColorMatchingMode.Hybrid;
}
