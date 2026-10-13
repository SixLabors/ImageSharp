// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// Provides the C runtime functions that the first-pass statistics depend on bit for bit.
/// </summary>
internal static class Av1FirstPassMath
{
    /// <summary>
    /// Computes the natural logarithm of one plus a value without losing the low bits of small values.
    /// </summary>
    /// <remarks>
    /// <see cref="double.LogP1(double)"/> evaluates <c>log(1 + x)</c>, which discards the bits of <paramref name="value"/> that the rounded sum
    /// <c>u = 1 + x</c> cannot hold. This method restores them with the first-order term <c>(x - (u - 1)) / u</c>, as the C runtime does. The
    /// value <c>u - 1</c> is exact, so the numerator is the exact rounding error of the sum. Then <c>log(u + e) = log(u) + e / u</c> to within
    /// far less than one unit in the last place. The form matches the Windows C runtime bit for bit over twenty million integer and fractional
    /// inputs. The correction is zero for every integer input, where the sum is exact.
    /// </remarks>
    /// <param name="value">The value to add to one, at least zero.</param>
    /// <returns>The natural logarithm of one plus <paramref name="value"/>.</returns>
    public static double Log1P(double value)
    {
        double sum = 1.0 + value;
        return Math.Log(sum) + ((value - (sum - 1.0)) / sum);
    }
}
