// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Provides standard YCbCr matrices for RGB to YCbCr conversion.
/// </summary>
public static class KnownYCbCrMatrices
{
#pragma warning disable SA1137 // Elements should have the same indentation
#pragma warning disable SA1117 // Parameters should be on same line or separate lines
    /// <summary>
    /// ITU-R BT.601 (SD video standard).
    /// </summary>
    public static readonly YCbCrTransform BT601 = new(
        new(
            0.299000F, 0.587000F, 0.114000F, 0F,
           -0.168736F, -0.331264F, 0.500000F, 0F,
            0.500000F, -0.418688F, -0.081312F, 0F,
            0F, 0F, 0F, 1F),
        new(
            1.000000F, 0.000000F, 1.402000F, 0F,
            1.000000F, -0.344136F, -0.714136F, 0F,
            1.000000F, 1.772000F, 0.000000F, 0F,
            0F, 0F, 0F, 1F),
        new(0F, 0.5F, 0.5F));

    /// <summary>
    /// ITU-R BT.709 (HD video, sRGB standard).
    /// </summary>
    public static readonly YCbCrTransform BT709 = new(
        new(
            0.212600F, 0.715200F, 0.072200F, 0F,
           -0.114572F, -0.385428F, 0.500000F, 0F,
            0.500000F, -0.454153F, -0.045847F, 0F,
            0F, 0F, 0F, 1F),
        new(
            1.000000F, 0.000000F, 1.574800F, 0F,
            1.000000F, -0.187324F, -0.468124F, 0F,
            1.000000F, 1.855600F, 0.000000F, 0F,
            0F, 0F, 0F, 1F),
        new(0F, 0.5F, 0.5F));

    /// <summary>
    /// ITU-R BT.2020 (UHD/4K video standard).
    /// </summary>
    public static readonly YCbCrTransform BT2020 = new(
        new(
            0.262700F, 0.678000F, 0.059300F, 0F,
           -0.139630F, -0.360370F, 0.500000F, 0F,
            0.500000F, -0.459786F, -0.040214F, 0F,
            0F, 0F, 0F, 1F),
        new(
            1.000000F, 0.000000F, 1.474600F, 0F,
            1.000000F, -0.164553F, -0.571353F, 0F,
            1.000000F, 1.881400F, 0.000000F, 0F,
            0F, 0F, 0F, 1F),
        new(0F, 0.5F, 0.5F));
}
