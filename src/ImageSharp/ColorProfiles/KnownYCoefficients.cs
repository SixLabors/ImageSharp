// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Provides standard Y (luma) coefficient sets for weighted RGB conversions.
/// </summary>
public static class KnownYCoefficients
{
    /// <summary>
    /// ITU-R BT.601 (SD video standard).
    /// </summary>
    public static readonly Vector3 BT601 = new(0.299F, 0.587F, 0.114F);

    /// <summary>
    /// ITU-R BT.709 (HD video, sRGB standard).
    /// </summary>
    public static readonly Vector3 BT709 = new(0.2126F, 0.7152F, 0.0722F);

    /// <summary>
    /// ITU-R BT.2020 (UHD/4K video standard).
    /// </summary>
    public static readonly Vector3 BT2020 = new(0.2627F, 0.6780F, 0.0593F);
}
