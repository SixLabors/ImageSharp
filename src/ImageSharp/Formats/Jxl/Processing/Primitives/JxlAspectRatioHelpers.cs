// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// Aspect ratio utilities
/// </summary>
internal static class JxlAspectRatioHelpers
{
    /// <summary>
    /// Well-known aspect ratios.
    /// </summary>
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

    /// <summary>
    /// Returns a built-in aspect ratio.
    /// </summary>
    /// <param name="ratio">The index of the aspect ratio.</param>
    /// <returns>A well-known aspect ratio.</returns>
    public static SignedRational FixedAspectRatios(int ratio) => Ratios[ratio - 1];

    /// <summary>
    /// Finds the first wire aspect-ratio code that reproduces the width after truncation.
    /// </summary>
    /// <param name="x">The image width in pixels.</param>
    /// <param name="y">The image height in pixels.</param>
    /// <returns>A code from one through seven, or zero when the width must be stored explicitly.</returns>
    public static int FindAspectRatio(int x, int y)
    {
        for (int i = 0; i < 7; i++)
        {
            if (x == MultiplyTruncate(Ratios[i], y))
            {
                return i + 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// Multiplies a positive dimension by an aspect ratio and truncates to whole pixels.
    /// </summary>
    /// <param name="rational">The positive aspect ratio.</param>
    /// <param name="multiplicand">The image height in pixels.</param>
    /// <returns>The derived width before validation against the caller's dimension limit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long MultiplyTruncate(SignedRational rational, int multiplicand)
    {
        // Widen before multiplying. Valid 30-bit dimensions can overflow Int32 even when the final quotient fits.
        return ((long)multiplicand * rational.Numerator) / rational.Denominator;
    }
}
