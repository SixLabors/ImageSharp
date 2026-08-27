// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Provides the normative Q7 interpolation coefficients used by AV1 inter prediction.
/// </content>
internal static partial class Av1InterPredictor
{
    /// <summary>
    /// The number of stored coefficient positions in every decoder interpolation kernel.
    /// </summary>
    private const int FilterCoefficientCount = 8;

    /// <summary>
    /// Gets the regular eight-tap kernels for the sixteen subpixel phases.
    /// </summary>
    private static ReadOnlySpan<short> RegularEightTap =>
    [
        0, 0, 0, 128, 0, 0, 0, 0,
        0, 2, -6, 126, 8, -2, 0, 0,
        0, 2, -10, 122, 18, -4, 0, 0,
        0, 2, -12, 116, 28, -8, 2, 0,
        0, 2, -14, 110, 38, -10, 2, 0,
        0, 2, -14, 102, 48, -12, 2, 0,
        0, 2, -16, 94, 58, -12, 2, 0,
        0, 2, -14, 84, 66, -12, 2, 0,
        0, 2, -14, 76, 76, -14, 2, 0,
        0, 2, -12, 66, 84, -14, 2, 0,
        0, 2, -12, 58, 94, -16, 2, 0,
        0, 2, -12, 48, 102, -14, 2, 0,
        0, 2, -10, 38, 110, -14, 2, 0,
        0, 2, -8, 28, 116, -12, 2, 0,
        0, 0, -4, 18, 122, -10, 2, 0,
        0, 0, -2, 8, 126, -6, 2, 0,
    ];

    /// <summary>
    /// Gets the smooth eight-tap kernels for the sixteen subpixel phases.
    /// </summary>
    private static ReadOnlySpan<short> SmoothEightTap =>
    [
        0, 0, 0, 128, 0, 0, 0, 0,
        0, 2, 28, 62, 34, 2, 0, 0,
        0, 0, 26, 62, 36, 4, 0, 0,
        0, 0, 22, 62, 40, 4, 0, 0,
        0, 0, 20, 60, 42, 6, 0, 0,
        0, 0, 18, 58, 44, 8, 0, 0,
        0, 0, 16, 56, 46, 10, 0, 0,
        0, -2, 16, 54, 48, 12, 0, 0,
        0, -2, 14, 52, 52, 14, -2, 0,
        0, 0, 12, 48, 54, 16, -2, 0,
        0, 0, 10, 46, 56, 16, 0, 0,
        0, 0, 8, 44, 58, 18, 0, 0,
        0, 0, 6, 42, 60, 20, 0, 0,
        0, 0, 4, 40, 62, 22, 0, 0,
        0, 0, 4, 36, 62, 26, 0, 0,
        0, 0, 2, 34, 62, 28, 2, 0,
    ];

    /// <summary>
    /// Gets the sharp eight-tap kernels for the sixteen subpixel phases.
    /// </summary>
    private static ReadOnlySpan<short> SharpEightTap =>
    [
        0, 0, 0, 128, 0, 0, 0, 0,
        -2, 2, -6, 126, 8, -2, 2, 0,
        -2, 6, -12, 124, 16, -6, 4, -2,
        -2, 8, -18, 120, 26, -10, 6, -2,
        -4, 10, -22, 116, 38, -14, 6, -2,
        -4, 10, -22, 108, 48, -18, 8, -2,
        -4, 10, -24, 100, 60, -20, 8, -2,
        -4, 10, -24, 90, 70, -22, 10, -2,
        -4, 12, -24, 80, 80, -24, 12, -4,
        -2, 10, -22, 70, 90, -24, 10, -4,
        -2, 8, -20, 60, 100, -24, 10, -4,
        -2, 8, -18, 48, 108, -22, 10, -4,
        -2, 6, -14, 38, 116, -22, 10, -4,
        -2, 6, -10, 26, 120, -18, 8, -2,
        -2, 4, -6, 16, 124, -12, 6, -2,
        0, 2, -2, 8, 126, -6, 2, -2,
    ];

    /// <summary>
    /// Gets the regular reduced kernels selected when a block dimension is at most four samples.
    /// </summary>
    private static ReadOnlySpan<short> RegularFourTap =>
    [
        0, 0, 0, 128, 0, 0, 0, 0,
        0, 0, -4, 126, 8, -2, 0, 0,
        0, 0, -8, 122, 18, -4, 0, 0,
        0, 0, -10, 116, 28, -6, 0, 0,
        0, 0, -12, 110, 38, -8, 0, 0,
        0, 0, -12, 102, 48, -10, 0, 0,
        0, 0, -14, 94, 58, -10, 0, 0,
        0, 0, -12, 84, 66, -10, 0, 0,
        0, 0, -12, 76, 76, -12, 0, 0,
        0, 0, -10, 66, 84, -12, 0, 0,
        0, 0, -10, 58, 94, -14, 0, 0,
        0, 0, -10, 48, 102, -12, 0, 0,
        0, 0, -8, 38, 110, -12, 0, 0,
        0, 0, -6, 28, 116, -10, 0, 0,
        0, 0, -4, 18, 122, -8, 0, 0,
        0, 0, -2, 8, 126, -4, 0, 0,
    ];

    /// <summary>
    /// Gets the smooth reduced kernels selected when a block dimension is at most four samples.
    /// </summary>
    private static ReadOnlySpan<short> SmoothFourTap =>
    [
        0, 0, 0, 128, 0, 0, 0, 0,
        0, 0, 30, 62, 34, 2, 0, 0,
        0, 0, 26, 62, 36, 4, 0, 0,
        0, 0, 22, 62, 40, 4, 0, 0,
        0, 0, 20, 60, 42, 6, 0, 0,
        0, 0, 18, 58, 44, 8, 0, 0,
        0, 0, 16, 56, 46, 10, 0, 0,
        0, 0, 14, 54, 48, 12, 0, 0,
        0, 0, 12, 52, 52, 12, 0, 0,
        0, 0, 12, 48, 54, 14, 0, 0,
        0, 0, 10, 46, 56, 16, 0, 0,
        0, 0, 8, 44, 58, 18, 0, 0,
        0, 0, 6, 42, 60, 20, 0, 0,
        0, 0, 4, 40, 62, 22, 0, 0,
        0, 0, 4, 36, 62, 26, 0, 0,
        0, 0, 2, 34, 62, 30, 0, 0,
    ];

    /// <summary>
    /// Gets the bilinear kernels for the sixteen subpixel phases.
    /// </summary>
    private static ReadOnlySpan<short> Bilinear =>
    [
        0, 0, 0, 128, 0, 0, 0, 0,
        0, 0, 0, 120, 8, 0, 0, 0,
        0, 0, 0, 112, 16, 0, 0, 0,
        0, 0, 0, 104, 24, 0, 0, 0,
        0, 0, 0, 96, 32, 0, 0, 0,
        0, 0, 0, 88, 40, 0, 0, 0,
        0, 0, 0, 80, 48, 0, 0, 0,
        0, 0, 0, 72, 56, 0, 0, 0,
        0, 0, 0, 64, 64, 0, 0, 0,
        0, 0, 0, 56, 72, 0, 0, 0,
        0, 0, 0, 48, 80, 0, 0, 0,
        0, 0, 0, 40, 88, 0, 0, 0,
        0, 0, 0, 32, 96, 0, 0, 0,
        0, 0, 0, 24, 104, 0, 0, 0,
        0, 0, 0, 16, 112, 0, 0, 0,
        0, 0, 0, 8, 120, 0, 0, 0,
    ];
}
