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

    /// <summary>
    /// Gets the output scales for X, Y and B minus Y in the native scaled-XYB representation.
    /// </summary>
    public static ReadOnlySpan<float> ScaledXybScale => [22.995788804F, 1.183000077F, 1.502141333F];

    /// <summary>
    /// Gets the offsets added to X, Y and B minus Y before scaled-XYB multiplication.
    /// </summary>
    public static ReadOnlySpan<float> ScaledXybOffset => [0.015386134F, 0F, 0.27770459F];
}
