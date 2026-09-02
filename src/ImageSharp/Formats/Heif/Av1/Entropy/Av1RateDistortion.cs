// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Combines fixed-point AV1 rate and distortion values for encoder decisions.
/// </summary>
internal static class Av1RateDistortion
{
    /// <summary>
    /// Gets a rate-distortion cost using the encoder probability-cost precision.
    /// </summary>
    /// <param name="rateMultiplier">The rate weight selected by the encoder quality model.</param>
    /// <param name="rate">The syntax rate in 1/512-bit units.</param>
    /// <param name="distortion">The sample-domain distortion.</param>
    /// <returns>The rounded weighted rate plus distortion.</returns>
    public static long GetCost(int rateMultiplier, int rate, long distortion)
    {
        long weightedRate = (long)rate * rateMultiplier;
        long roundedRate = (weightedRate + (1 << (Av1ProbabilityCost.CostShift - 1))) >> Av1ProbabilityCost.CostShift;
        return roundedRate + distortion;
    }
}
