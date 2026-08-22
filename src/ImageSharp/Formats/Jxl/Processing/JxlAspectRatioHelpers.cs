// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal static class JxlAspectRatioHelpers
{
    private static readonly SignedRational[] Ratios =
    [
        new(1, 1),
        new(12, 10),
        new(4, 3),
        new(3, 2),
        new(16, 9),
        new(5, 4),
        new(2, 1)
    ];

    public static SignedRational FixedAspectRatios(int ratio) => Ratios[ratio - 1];

    public static int FindAspectRatio(int x, int y)
    {
        for (int i = 0; i < 7; i++)
        {
            if (x == MultiplyTruncate(Ratios[i], y))
            {
                return i;
            }
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MultiplyTruncate(SignedRational rational, int multiplicand) => (multiplicand * rational.Numerator) / rational.Denominator;
}
