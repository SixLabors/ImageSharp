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

    /// <summary>
    /// Gets the sum-of-absolute-differences rate scale for a frame quantizer.
    /// </summary>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The multiplier that converts motion-vector rate into the absolute-difference domain.</returns>
    public static int GetMotionSearchSadPerBit(int qIndex, Av1BitDepth bitDepth)
    {
        int quantizerDivisor = 1 << (bitDepth.GetBitCount() - 6);
        double quantizer = Av1QuantizationLookup.GetAcQuant(qIndex, 0, bitDepth) / (double)quantizerDivisor;
        return (int)((0.0418 * quantizer) + 2.4107);
    }

    /// <summary>
    /// Gets the sum-of-absolute-differences cost of a full-pixel motion candidate.
    /// </summary>
    /// <param name="sadPerBit">The quantizer-derived motion-rate scale.</param>
    /// <param name="motionVectorRate">The motion-vector syntax rate in 1/512-bit units.</param>
    /// <param name="sumOfAbsoluteDifferences">The unnormalized sample-domain absolute difference.</param>
    /// <returns>The absolute difference plus the motion-vector search cost.</returns>
    public static int GetMotionSearchSadCost(
        int sadPerBit,
        int motionVectorRate,
        int sumOfAbsoluteDifferences)
    {
        const int MotionRateShift = 9;

        // Full-pixel traversal uses absolute differences, so its quantizer-derived rate scale is deliberately
        // distinct from the variance-domain error-per-bit scale used to compare the resulting search paths.
        long weightedRate = (long)motionVectorRate * sadPerBit;
        int motionError = (int)((weightedRate + (1 << (MotionRateShift - 1))) >> MotionRateShift);
        return sumOfAbsoluteDifferences + motionError;
    }
}
