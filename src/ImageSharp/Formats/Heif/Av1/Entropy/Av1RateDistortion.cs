// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Combines fixed-point AV1 rate and distortion values for encoder decisions.
/// </summary>
internal static class Av1RateDistortion
{
    /// <summary>
    /// Gets the key-frame rate multiplier for an AV1 quantizer and sample precision.
    /// </summary>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The rate multiplier.</returns>
    public static int GetKeyFrameRateMultiplier(int qIndex, Av1BitDepth bitDepth)
    {
        int quantizer = Av1QuantizationLookup.GetDcQuant(qIndex, 0, bitDepth);

        // Key frames use a quantizer-dependent weight over the squared DC step. High-bit-depth
        // distortion is normalized back to the eight-bit domain, so its rate multiplier follows it.
        long multiplier = (long)((quantizer * (long)quantizer) * (3.3 + (0.0015 * quantizer)));
        int shift = (bitDepth.GetBitCount() - 8) * 2;
        if (shift > 0)
        {
            multiplier = (multiplier + (1L << (shift - 1))) >> shift;
        }

        return (int)Math.Max(multiplier, 1);
    }

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
        return roundedRate + (distortion << 7);
    }

    /// <summary>
    /// Gets the variance-domain cost of a full-pixel motion candidate.
    /// </summary>
    /// <param name="rateMultiplier">The rate weight selected by the encoder quality model.</param>
    /// <param name="motionVectorRate">The motion-vector syntax rate in 1/512-bit units.</param>
    /// <param name="variance">The normalized sample variance.</param>
    /// <returns>The variance plus the motion-vector error cost.</returns>
    public static int GetMotionSearchCost(int rateMultiplier, int motionVectorRate, int variance)
    {
        const int RateMultiplierShift = 6;
        const int MotionErrorShift = 14;
        int errorPerBit = Math.Max(rateMultiplier >> RateMultiplierShift, 1);

        // Motion search compares pixel variance directly, so the syntax term is reduced to the same
        // error domain instead of using the final mode-decision distortion scale.
        long weightedRate = (long)motionVectorRate * errorPerBit;
        int motionError = (int)((weightedRate + (1 << (MotionErrorShift - 1))) >> MotionErrorShift);
        return variance + motionError;
    }
}
