// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Enumerates the various color blending modes.
/// </summary>
public enum PixelColorBlendingMode
{
    /// <summary>
    /// Default blending mode, also known as "Normal" or "Alpha Blending"
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Blends the 2 values by multiplication.
    /// </summary>
    Multiply,

    /// <summary>
    /// Blends the 2 values by addition.
    /// </summary>
    Add,

    /// <summary>
    /// Blends the 2 values by subtraction.
    /// </summary>
    Subtract,

    /// <summary>
    /// Multiplies the complements of the backdrop and source values, then complements the result.
    /// </summary>
    Screen,

    /// <summary>
    /// Selects the minimum of the backdrop and source values.
    /// </summary>
    Darken,

    /// <summary>
    /// Selects the max of the backdrop and source values.
    /// </summary>
    Lighten,

    /// <summary>
    /// Multiplies or screens the values, depending on the backdrop vector values.
    /// </summary>
    Overlay,

    /// <summary>
    /// Multiplies or screens the colors, depending on the source value.
    /// </summary>
    HardLight,

    /// <summary>
    /// Brightens the backdrop color to reflect the source color.
    /// </summary>
    ColorDodge,

    /// <summary>
    /// Darkens the backdrop color to reflect the source color.
    /// </summary>
    ColorBurn,

    /// <summary>
    /// Darkens or lightens the colors, depending on the source color.
    /// </summary>
    SoftLight,

    /// <summary>
    /// Subtracts the darker color from the lighter color.
    /// </summary>
    Difference,

    /// <summary>
    /// Produces an effect similar to Difference with lower contrast.
    /// </summary>
    Exclusion,

    /// <summary>
    /// Uses the hue of the source color with the saturation and luminosity of the backdrop color.
    /// </summary>
    Hue,

    /// <summary>
    /// Uses the saturation of the source color with the hue and luminosity of the backdrop color.
    /// </summary>
    Saturation,

    /// <summary>
    /// Uses the hue and saturation of the source color with the luminosity of the backdrop color.
    /// </summary>
    Color,

    /// <summary>
    /// Uses the luminosity of the source color with the hue and saturation of the backdrop color.
    /// </summary>
    Luminosity,
}
