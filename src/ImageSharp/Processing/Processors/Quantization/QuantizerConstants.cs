// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Contains color quantization specific constants.
/// </summary>
public static class QuantizerConstants
{
    /// <summary>
    /// The minimum number of colors to use when quantizing an image.
    /// </summary>
    public const int MinColors = 1;

    /// <summary>
    /// The maximum number of colors to use when quantizing an image.
    /// </summary>
    public const int MaxColors = 256;

    /// <summary>
    /// The minimum dithering scale used to adjust the amount of dither.
    /// </summary>
    public const float MinDitherScale = 0;

    /// <summary>
    /// The maximum dithering scale used to adjust the amount of dither.
    /// </summary>
    public const float MaxDitherScale = 1F;

    /// <summary>
    /// The default threshold at which to consider a pixel transparent.
    /// </summary>
    public const float DefaultTransparencyThreshold = 64 / 255F;

    /// <summary>
    /// The minimum threshold at which to consider a pixel transparent.
    /// </summary>
    public const float MinTransparencyThreshold = 0F;

    /// <summary>
    /// The maximum threshold at which to consider a pixel transparent.
    /// </summary>
    public const float MaxTransparencyThreshold = 1F;

    /// <summary>
    /// Gets the default dithering algorithm to use.
    /// </summary>
    public static IDither DefaultDither { get; } = KnownDitherings.FloydSteinberg;
}
