// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Enumerate the possible sources of the white point used in chromatic adaptation.
/// </summary>
public enum ChromaticAdaptionWhitePointSource
{
    /// <summary>
    /// The white point of the source color space.
    /// </summary>
    WhitePoint,

    /// <summary>
    /// The white point of the source working space.
    /// </summary>
    RgbWorkingSpace
}
