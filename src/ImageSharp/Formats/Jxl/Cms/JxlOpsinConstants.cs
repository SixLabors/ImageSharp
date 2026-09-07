// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

/// <summary>
/// Opsin constants used by the color management system
/// </summary>
internal static class JxlOpsinConstants
{
    public const float BScale = 1f;

    // The following constants are used for XYB.
    // They can be adjusted to change how Y<->B ratio
    // works. For example, YToBRatio works better
    // with 0.50017729543783418.
    public const float YToBRatio = 1f;
    public const float BToYRatio = 1f / YToBRatio;

    // Adjusting these constants influences the opsin absorbance.
    public const float OpsinAbsorbanceBias0 = 0.0037930732552754493f;
    public const float OpsinAbsorbanceBias1 = OpsinAbsorbanceBias0;
    public const float OpsinAbsorbanceBias2 = OpsinAbsorbanceBias0;

    public static ReadOnlySpan<float> OpsinAbsorbanceBias => [OpsinAbsorbanceBias0, OpsinAbsorbanceBias1, OpsinAbsorbanceBias2];
}
