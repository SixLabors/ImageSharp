// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
namespace SixLabors.ImageSharp.Formats.Qoi;

/// <summary>
/// Enum for the different QOI color spaces.
/// </summary>
public enum QoiColorSpace
{
    /// <summary>
    /// sRGB color space with linear alpha value
    /// </summary>
    SrgbWithLinearAlpha,

    /// <summary>
    /// All the values in the color space are linear
    /// </summary>
    AllChannelsLinear
}
