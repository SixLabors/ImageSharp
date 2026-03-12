// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Constants use for Cie conversion calculations
/// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_Lab.html"/>
/// </summary>
internal static class CieConstants
{
    /// <summary>
    /// 216F / 24389F
    /// </summary>
    public const float Epsilon = 216f / 24389f;

    /// <summary>
    /// 24389F / 27F
    /// </summary>
    public const float Kappa = 24389f / 27f;
}
