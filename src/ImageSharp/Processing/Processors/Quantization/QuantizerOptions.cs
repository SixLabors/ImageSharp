// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Defines options for quantization.
/// </summary>
public class QuantizerOptions : IDeepCloneable<QuantizerOptions>
{
#pragma warning disable IDE0032 // Use auto property
    private float ditherScale = QuantizerConstants.MaxDitherScale;
    private int maxColors = QuantizerConstants.MaxColors;
    private float threshold = QuantizerConstants.DefaultTransparencyThreshold;
#pragma warning restore IDE0032 // Use auto property

    /// <summary>
    /// Initializes a new instance of the <see cref="QuantizerOptions"/> class.
    /// </summary>
    public QuantizerOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QuantizerOptions"/> class.
    /// </summary>
    /// <param name="options">The options to clone.</param>
    private QuantizerOptions(QuantizerOptions options)
    {
        this.Dither = options.Dither;
        this.DitherScale = options.DitherScale;
        this.MaxColors = options.MaxColors;
        this.TransparencyThreshold = options.TransparencyThreshold;
        this.ColorMatchingMode = options.ColorMatchingMode;
        this.TransparentColorMode = options.TransparentColorMode;
    }

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
    /// Gets or sets the color matching mode used for matching pixel values to palette colors.
    /// Defaults to <see cref="ColorMatchingMode.Coarse"/>.
    /// </summary>
    public ColorMatchingMode ColorMatchingMode { get; set; } = ColorMatchingMode.Coarse;

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
    /// Gets or sets the transparent color mode used for handling transparent colors
    /// when not using thresholding.
    /// Defaults to <see cref="TransparentColorMode.Preserve"/>.
    /// </summary>
    public TransparentColorMode TransparentColorMode { get; set; } = TransparentColorMode.Preserve;

    /// <inheritdoc/>
    public QuantizerOptions DeepClone() => new(this);
}
