// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Converts AV1 symbol probabilities into fixed-point encoder rate costs.
/// </summary>
internal static class Av1ProbabilityCost
{
    /// <summary>
    /// The number of fractional bits in an encoder rate cost.
    /// </summary>
    public const int CostShift = 9;

    /// <summary>
    /// Gets the probability costs for normalized eight-bit probabilities from 128 through 255.
    /// </summary>
    private static ReadOnlySpan<ushort> ProbabilityCosts =>
    [
        512, 506, 501, 495, 489, 484, 478, 473, 467, 462, 456, 451, 446, 441, 435,
        430, 425, 420, 415, 410, 405, 400, 395, 390, 385, 380, 375, 371, 366, 361,
        356, 352, 347, 343, 338, 333, 329, 324, 320, 316, 311, 307, 302, 298, 294,
        289, 285, 281, 277, 273, 268, 264, 260, 256, 252, 248, 244, 240, 236, 232,
        228, 224, 220, 216, 212, 209, 205, 201, 197, 194, 190, 186, 182, 179, 175,
        171, 168, 164, 161, 157, 153, 150, 146, 143, 139, 136, 132, 129, 125, 122,
        119, 115, 112, 109, 105, 102, 99, 95, 92, 89, 86, 82, 79, 76, 73, 70,
        66, 63, 60, 57, 54, 51, 48, 45, 42, 38, 35, 32, 29, 26, 23, 20, 18, 15,
        12, 9, 6, 3
    ];

    /// <summary>
    /// Gets the fixed-point cost of writing the requested number of equiprobable bits.
    /// </summary>
    /// <param name="bitCount">The number of bits.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public static int GetLiteralCost(int bitCount) => bitCount << CostShift;

    /// <summary>
    /// Gets the fixed-point cost of coding one symbol from an inverse cumulative distribution.
    /// </summary>
    /// <param name="distribution">The distribution used by the entropy writer.</param>
    /// <param name="symbol">The zero-based symbol.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public static int GetSymbolCost(Av1Distribution distribution, int symbol)
    {
        int inverseLower = symbol == 0 ? Av1Distribution.ProbabilityTop : (int)distribution[symbol - 1];
        int inverseUpper = (int)distribution[symbol];
        return GetSymbolCost(inverseLower - inverseUpper);
    }

    /// <summary>
    /// Gets the fixed-point cost of an entropy-coded symbol with a Q15 probability.
    /// </summary>
    /// <param name="probability">The Q15 probability numerator.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public static int GetSymbolCost(int probability)
    {
        // The range coder reserves a minimum interval even when CDF adaptation collapses a symbol's mass.
        // RD costs use that same floor; the raw probability conversion below retains its separate numerical domain.
        return GetProbabilityCost(Math.Max(probability, Av1Distribution.ProbabilityMinimum));
    }

    /// <summary>
    /// Gets the fixed-point cost of a Q15 probability.
    /// </summary>
    /// <param name="probability">The Q15 probability numerator.</param>
    /// <returns>The rate cost in 1/512-bit units.</returns>
    public static int GetProbabilityCost(int probability)
    {
        probability = Math.Clamp(probability, 1, Av1Distribution.ProbabilityTop - 1);
        int shift = 14 - BitOperations.Log2((uint)probability);

        // Normalization puts every probability in the upper half of an eight-bit range. The lookup therefore
        // covers one binary order of magnitude, while the shift contributes the exact number of whole bits.
        int normalizedProbability = (((probability << shift) * 256) + (Av1Distribution.ProbabilityTop >> 1))
            / Av1Distribution.ProbabilityTop;
        normalizedProbability = Math.Min(normalizedProbability, 255);

        return ProbabilityCosts[normalizedProbability - 128] + (shift << CostShift);
    }
}
