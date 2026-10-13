// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <summary>
/// Provides the one-dimensional alpha masks used by AV1 overlapping motion compensation.
/// </summary>
internal static class Av1ObmcMask
{
    /// <summary>
    /// Gets the mask whose length matches one overlapping prediction axis.
    /// </summary>
    /// <param name="length">The power-of-two overlap length.</param>
    /// <returns>The alpha values applied to the regular block predictor.</returns>
    public static ReadOnlySpan<byte> Get(int length) => length switch
    {
        1 => [64],
        2 => [45, 64],
        4 => [39, 50, 59, 64],
        8 => [36, 42, 48, 53, 57, 61, 64, 64],
        16 => [34, 37, 40, 43, 46, 49, 52, 54, 56, 58, 60, 61, 64, 64, 64, 64],
        32 =>
        [
            33, 35, 36, 38, 40, 41, 43, 44,
            45, 47, 48, 50, 51, 52, 53, 55,
            56, 57, 58, 59, 60, 60, 61, 62,
            64, 64, 64, 64, 64, 64, 64, 64
        ],
        64 =>
        [
            33, 34, 35, 35, 36, 37, 38, 39,
            40, 40, 41, 42, 43, 44, 44, 44,
            45, 46, 47, 47, 48, 49, 50, 51,
            51, 51, 52, 52, 53, 54, 55, 56,
            56, 56, 57, 57, 58, 58, 59, 60,
            60, 60, 60, 60, 61, 62, 62, 62,
            62, 62, 63, 63, 63, 63, 64, 64,
            64, 64, 64, 64, 64, 64, 64, 64
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(length))
    };
}
