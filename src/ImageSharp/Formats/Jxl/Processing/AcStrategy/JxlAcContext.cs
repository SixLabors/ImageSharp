// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;

/// <summary>
/// AC context
/// </summary>
internal static class JxlAcContext
{
    public const int DctOrderContextStart = 0;
    public const int NonZeroBuckets = 37;
    public const int ZeroDensityContextCount = 458;
    public const int ZeroDensityContextLimit = 474;

    public static ReadOnlySpan<int> CoefficientFrequencyContext =>
    [
        0xBAD, 0,  1,  2,  3,  4,  5,  6,  7,  8,  9,  10, 11, 12, 13, 14,
        15,    15, 16, 16, 17, 17, 18, 18, 19, 19, 20, 20, 21, 21, 22, 22,
        23,    23, 23, 23, 24, 24, 24, 24, 25, 25, 25, 25, 26, 26, 26, 26,
        27,    27, 27, 27, 28, 28, 28, 28, 29, 29, 29, 29, 30, 30, 30, 30,
    ];

    public static ReadOnlySpan<int> CoefficientNumNonzeroContext =>
    [
        0xBAD, 0,   31,  62,  62,  93,  93,  93,  93,  123, 123, 123, 123,
        152,   152, 152, 152, 152, 152, 152, 152, 180, 180, 180, 180, 180,
        180,   180, 180, 180, 180, 180, 180, 206, 206, 206, 206, 206, 206,
        206,   206, 206, 206, 206, 206, 206, 206, 206, 206, 206, 206, 206,
        206,   206, 206, 206, 206, 206, 206, 206, 206, 206, 206, 206,
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ZeroDensityContext(int nonZeroesLeft, int k, int coveredBlocks, int log2CoveredBlocks, int prev)
    {
        DebugGuard.IsTrue((1 << log2CoveredBlocks) == coveredBlocks, "log2CoveredBlocks must be equal to Log2(coveredBlocks)");

        nonZeroesLeft = (nonZeroesLeft + coveredBlocks - 1) >> log2CoveredBlocks;
        k >>= log2CoveredBlocks;

        if (k is < 0 or >= 64)
        {
            throw new InvalidOperationException("k must be within range of 0..63 inclusive");
        }

        if (nonZeroesLeft is <= 0 or >= 64)
        {
            throw new InvalidOperationException("nonZeroesLeft must be within range of 1..63 inclusive");
        }

        return ((CoefficientNumNonzeroContext[nonZeroesLeft] + CoefficientFrequencyContext[k]) * 2) + prev;
    }
}
