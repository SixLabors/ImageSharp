// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Builds AV1 inter-intra prediction masks.
/// </content>
internal static partial class Av1InterIntraMaskBuilder
{
    /// <summary>
    /// Gets the reference decoder's one-dimensional inter-intra alpha curve.
    /// </summary>
    private static ReadOnlySpan<byte> InterIntraWeights =>
    [
        60, 58, 56, 54, 52, 50, 48, 47, 45, 44, 42, 41, 39, 38, 37, 35,
        34, 33, 32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 22, 21, 20,
        19, 19, 18, 18, 17, 16, 16, 15, 15, 14, 14, 13, 13, 12, 12, 12,
        11, 11, 10, 10, 10, 9, 9, 9, 8, 8, 8, 8, 7, 7, 7, 7,
        6, 6, 6, 6, 6, 5, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4,
        4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
    ];

    /// <summary>
    /// Fills a smooth inter-intra mask for one plane.
    /// </summary>
    public static void FillInterIntraMask(
        Span<byte> mask,
        int maskStride,
        int width,
        int height,
        Av1InterIntraMode mode,
        bool invert)
    {
        int sizeScale = 128 / Math.Max(width, height);
        for (int row = 0; row < height; row++)
        {
            Span<byte> maskRow = mask.Slice(row * maskStride, width);
            for (int column = 0; column < width; column++)
            {
                int alpha = mode switch
                {
                    Av1InterIntraMode.Vertical => InterIntraWeights[row * sizeScale],
                    Av1InterIntraMode.Horizontal => InterIntraWeights[column * sizeScale],
                    Av1InterIntraMode.Smooth => InterIntraWeights[Math.Min(row, column) * sizeScale],
                    _ => 32,
                };

                maskRow[column] = (byte)(invert ? MaximumMaskAlpha - alpha : alpha);
            }
        }
    }
}
