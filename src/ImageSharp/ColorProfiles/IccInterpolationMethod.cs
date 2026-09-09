// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Defines the interpolation method for ICC color lookup tables.
/// </summary>
public enum IccInterpolationMethod
{
    /// <summary>
    /// Selects trilinear interpolation for Lab output and Lab device-link or abstract profiles, and tetrahedral interpolation otherwise.
    /// </summary>
    Auto,

    /// <summary>
    /// Uses trilinear interpolation for three input channels and multilinear interpolation for four input channels.
    /// </summary>
    Trilinear,

    /// <summary>
    /// Uses tetrahedral interpolation for three input channels and linearly blends tetrahedral results for four input channels.
    /// </summary>
    Tetrahedral
}
