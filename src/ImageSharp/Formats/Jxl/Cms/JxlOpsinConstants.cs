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

    /// <summary>Gets the frozen forward absorbance matrix in row-major order.</summary>
    public static ReadOnlySpan<float> OpsinAbsorbanceMatrix =>
    [
        0.30F, 1F - 0.078F - 0.30F, 0.078F,
        0.23F, 1F - 0.078F - 0.23F, 0.078F,
        0.24342268924547819F, 0.20476744424496821F, 1F - 0.24342268924547819F - 0.20476744424496821F
    ];

    /// <summary>Gets the output scales for X, Y and B minus Y in the native scaled-XYB representation.</summary>
    public static ReadOnlySpan<float> ScaledXybScale => [22.995788804F, 1.183000077F, 1.502141333F];

    /// <summary>Gets the offsets added to X, Y and B minus Y before scaled-XYB multiplication.</summary>
    public static ReadOnlySpan<float> ScaledXybOffset => [0.015386134F, 0F, 0.27770459F];

    /// <summary>
    /// Gets the frozen default inverse opsin matrix in row-major order, as defined by the JPEG XL bitstream.
    /// </summary>
    public static ReadOnlySpan<float> DefaultInverseOpsinAbsorbanceMatrix =>
    [
        11.031566901960783f, -9.866943921568629f, -0.16462299647058826f,
        -3.254147380392157f, 4.418770392156863f, -0.16462299647058826f,
        -3.6588512862745097f, 2.7129230470588235f, 1.9459282392156863f
    ];
}
