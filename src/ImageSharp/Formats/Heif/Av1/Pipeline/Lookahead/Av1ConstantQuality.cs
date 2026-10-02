// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// The quantizer arithmetic of constant-quality rate control with look-ahead statistics: the conversions between
/// quantizer indices and real quantizers, the active quality curves that map a boost to a quantizer floor, and the
/// temporal dependency adjustments of the boost and the quantizer.
/// </summary>
internal static class Av1ConstantQuality
{
    /// <summary>
    /// The largest quantizer index. Reference: MAXQ.
    /// </summary>
    public const int MaximumQIndex = 255;

    /// <summary>
    /// The upper bound of the tempered boost projection. Reference: MAX_GFUBOOST_FACTOR.
    /// </summary>
    public const double MaximumBoostFactor = 10.0;

    /// <summary>
    /// The upper bound of the factor that weighs the prior boost against the temporal dependency boost.
    /// Reference: MAX_BOOST_COMBINE_FACTOR.
    /// </summary>
    public const double MaximumBoostCombineFactor = 12.0;

    /// <summary>
    /// The key frame boost at and below which the high motion floor applies. Reference: kf_low.
    /// </summary>
    private const int KeyFrameLowBoost = 553;

    /// <summary>
    /// The key frame boost at and above which the low motion floor applies. Reference: kf_high.
    /// </summary>
    private const int KeyFrameHighBoost = 8000;

    /// <summary>
    /// Gets the linear coefficients of the good-quality floor curves: the low and the high motion key frame curves,
    /// the low and the high motion golden and alternate reference curves, and the inter curve, for frames below 608
    /// lines followed by frames of 608 lines or more. Reference: x1[0] of init_minq_luts().
    /// </summary>
    private static ReadOnlySpan<double> CurveCoefficients =>
    [
        0.1771, 0.379, 0.3279, 0.6634, 1.385,
        0.1917, 0.3760, 0.34570, 0.6916, 1.14820
    ];

    /// <summary>
    /// Gets the golden boost average below which the first pair of golden boost bounds applies, per resolution
    /// class. Reference: gfboost_thresh.
    /// </summary>
    private static ReadOnlySpan<int> GoldenBoostThresholds => [4000, 4000, 3000];

    /// <summary>
    /// Converts a quantizer index to the real quantizer on the 8-bit scale.
    /// Reference: av1_convert_qindex_to_q().
    /// </summary>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The real quantizer.</returns>
    public static double ConvertQIndexToQ(int qIndex, Av1BitDepth bitDepth)
    {
        int acQuantizer = Av1InverseTransformMath.GetAcQuantization(qIndex, 0, bitDepth);
        return bitDepth switch
        {
            Av1BitDepth.EightBit => acQuantizer / 4.0,
            Av1BitDepth.TenBit => acQuantizer / 16.0,
            _ => acQuantizer / 64.0
        };
    }

    /// <summary>
    /// Returns the first quantizer index in a range whose real quantizer reaches a value, or the top of the range.
    /// Reference: av1_find_qindex().
    /// </summary>
    /// <param name="desiredQ">The real quantizer.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="bestQIndex">The lowest quantizer index of the range.</param>
    /// <param name="worstQIndex">The highest quantizer index of the range.</param>
    /// <returns>The quantizer index.</returns>
    public static int FindQIndex(double desiredQ, Av1BitDepth bitDepth, int bestQIndex, int worstQIndex)
    {
        int low = bestQIndex;
        int high = worstQIndex;
        while (low < high)
        {
            int middle = (low + high) >> 1;
            if (ConvertQIndexToQ(middle, bitDepth) < desiredQ)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    /// <summary>
    /// Returns the quantizer index change between two real quantizers inside the allowed range.
    /// Reference: av1_compute_qdelta().
    /// </summary>
    /// <param name="qStart">The starting real quantizer.</param>
    /// <param name="qTarget">The target real quantizer.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="bestQIndex">The lowest allowed quantizer index. Reference: best_quality.</param>
    /// <param name="worstQIndex">The highest allowed quantizer index. Reference: worst_quality.</param>
    /// <returns>The quantizer index change.</returns>
    public static int ComputeQDelta(double qStart, double qTarget, Av1BitDepth bitDepth, int bestQIndex, int worstQIndex)
    {
        int startIndex = FindQIndex(qStart, bitDepth, bestQIndex, worstQIndex);
        int targetIndex = FindQIndex(qTarget, bitDepth, bestQIndex, worstQIndex);
        return targetIndex - startIndex;
    }

    /// <summary>
    /// Returns the key frame quantizer floor between the low and the high motion curves by the key frame boost.
    /// Reference: get_kf_active_quality() in good-quality mode.
    /// </summary>
    /// <param name="q">The quantizer index the floor applies to.</param>
    /// <param name="keyFrameBoost">The key frame boost. Reference: kf_boost.</param>
    /// <param name="largeResolution">Whether the shorter frame side has 608 lines or more.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The key frame floor.</returns>
    public static int GetKeyFrameActiveQuality(int q, int keyFrameBoost, bool largeResolution, Av1BitDepth bitDepth)
    {
        int lowMotion = GetFloor(q, 0.000001, -0.0004, 0, largeResolution, bitDepth);
        int highMotion = GetFloor(q, 0.0000021, -0.00125, 1, largeResolution, bitDepth);
        return GetActiveQuality(keyFrameBoost, KeyFrameLowBoost, KeyFrameHighBoost, lowMotion, highMotion);
    }

    /// <summary>
    /// Returns the golden and alternate reference quantizer floor between the low and the high motion curves by
    /// the golden boost. The average boost of the key frame group selects the pair of boost bounds.
    /// Reference: get_gf_active_quality() and get_gf_active_quality_no_rc() in good-quality mode.
    /// </summary>
    /// <param name="q">The quantizer index the floor applies to.</param>
    /// <param name="goldenBoost">The golden boost. Reference: gfu_boost.</param>
    /// <param name="averageGoldenBoost">The average golden boost. Reference: gfu_boost_average.</param>
    /// <param name="resolutionIndex">0 below 480 lines, 1 below 608 lines, else 2.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The golden floor.</returns>
    public static int GetGoldenActiveQuality(int q, int goldenBoost, int averageGoldenBoost, int resolutionIndex, Av1BitDepth bitDepth)
    {
        bool largeResolution = resolutionIndex > 1;
        int lowMotion = GetFloor(q, 0.0000015, -0.0009, 2, largeResolution, bitDepth);
        int highMotion = GetGoldenHighMotionQuality(q, largeResolution, bitDepth);
        bool firstBounds = averageGoldenBoost < GoldenBoostThresholds[resolutionIndex];
        int low = firstBounds ? 562 : 100;
        int high = firstBounds ? 2875 : 4994;
        return GetActiveQuality(goldenBoost, low, high, lowMotion, highMotion);
    }

    /// <summary>
    /// Returns the high motion golden and alternate reference floor. Reference: get_gf_high_motion_quality().
    /// </summary>
    /// <param name="q">The quantizer index the floor applies to.</param>
    /// <param name="largeResolution">Whether the shorter frame side has 608 lines or more.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The floor.</returns>
    public static int GetGoldenHighMotionQuality(int q, bool largeResolution, Av1BitDepth bitDepth)
        => GetFloor(q, 0.0000021, -0.00125, 3, largeResolution, bitDepth);

    /// <summary>
    /// Returns the good-quality floor of an ordinary inter frame. Reference: inter_minq.
    /// </summary>
    /// <param name="q">The quantizer index the floor applies to.</param>
    /// <param name="largeResolution">Whether the shorter frame side has 608 lines or more.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The floor.</returns>
    public static int GetInterActiveQuality(int q, bool largeResolution, Av1BitDepth bitDepth)
        => GetFloor(q, 0.00000271, -0.00113, 4, largeResolution, bitDepth);

    /// <summary>
    /// Returns the quantizer index whose DC quantizer step is the leaf step scaled by a ratio, searching down from
    /// the leaf index for a ratio below one and up for a ratio of one or more.
    /// Reference: av1_get_q_index_from_qstep_ratio().
    /// </summary>
    /// <param name="leafQIndex">The quantizer index of leaf frames.</param>
    /// <param name="qStepRatio">The quantizer step ratio.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The quantizer index.</returns>
    public static int GetQIndexFromQStepRatio(int leafQIndex, double qStepRatio, Av1BitDepth bitDepth)
    {
        double leafQStep = Av1InverseTransformMath.GetDcQuantization(leafQIndex, 0, bitDepth);
        double targetQStep = leafQStep * qStepRatio;
        int qIndex;
        if (qStepRatio < 1.0)
        {
            for (qIndex = leafQIndex; qIndex > 0; --qIndex)
            {
                double qStep = Av1InverseTransformMath.GetDcQuantization(qIndex, 0, bitDepth);
                if (qStep <= targetQStep)
                {
                    break;
                }
            }
        }
        else
        {
            for (qIndex = leafQIndex; qIndex < MaximumQIndex; ++qIndex)
            {
                double qStep = Av1InverseTransformMath.GetDcQuantization(qIndex, 0, bitDepth);
                if (qStep >= targetQStep)
                {
                    break;
                }
            }
        }

        return qIndex;
    }

    /// <summary>
    /// Returns the factor that scales a golden boost with the number of frames it covers: 200 plus ten times the
    /// square root of the count, with the root clamped to a range.
    /// Reference: av1_get_gfu_boost_projection_factor().
    /// </summary>
    /// <param name="minimumFactor">The lower bound of the root.</param>
    /// <param name="maximumFactor">The upper bound of the root.</param>
    /// <param name="frameCount">The number of frames.</param>
    /// <returns>The projection factor.</returns>
    public static double GetGoldenBoostProjectionFactor(double minimumFactor, double maximumFactor, int frameCount)
    {
        double factor = Math.Sqrt(frameCount);
        factor = Math.Min(factor, maximumFactor);
        factor = Math.Max(factor, minimumFactor);
        factor = 200.0 + (10.0 * factor);
        return factor;
    }

    /// <summary>
    /// Returns the golden boost that a temporal dependency ratio implies for a number of frames.
    /// Reference: get_gfu_boost_from_r0_lap().
    /// </summary>
    /// <param name="minimumFactor">The lower bound of the projection root.</param>
    /// <param name="maximumFactor">The upper bound of the projection root.</param>
    /// <param name="r0">The ratio of the propagated cost to the intra cost. Reference: r0.</param>
    /// <param name="frameCount">The number of frames the boost covers.</param>
    /// <returns>The boost.</returns>
    public static int GetGoldenBoostFromR0(double minimumFactor, double maximumFactor, double r0, int frameCount)
    {
        double factor = GetGoldenBoostProjectionFactor(minimumFactor, maximumFactor, frameCount);
        return (int)Math.Round(factor / r0, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Returns the factor that scales a key frame boost with the number of frames it covers: 75 plus 14 times the
    /// square root of the count, with the root clamped between 4 and 10.
    /// Reference: av1_get_kf_boost_projection_factor().
    /// </summary>
    /// <param name="frameCount">The number of frames.</param>
    /// <returns>The projection factor.</returns>
    public static double GetKeyFrameBoostProjectionFactor(int frameCount)
    {
        double factor = Math.Sqrt(frameCount);
        factor = Math.Min(factor, 10.0);
        factor = Math.Max(factor, 4.0);
        factor = 75.0 + (14.0 * factor);
        return factor;
    }

    /// <summary>
    /// Blends a boost from the first-pass statistics with a boost from the temporal dependency model. More frames
    /// give the statistics more weight. Reference: combine_prior_with_tpl_boost().
    /// </summary>
    /// <param name="minimumFactor">The lower bound of the weight.</param>
    /// <param name="maximumFactor">The upper bound of the weight.</param>
    /// <param name="priorBoost">The boost from the first-pass statistics.</param>
    /// <param name="tplBoost">The boost from the temporal dependency model.</param>
    /// <param name="frameCount">The number of frames whose root weighs the statistics.</param>
    /// <returns>The blended boost.</returns>
    public static int CombinePriorWithTplBoost(double minimumFactor, double maximumFactor, int priorBoost, int tplBoost, int frameCount)
    {
        double factor = Math.Sqrt(frameCount);
        double range = maximumFactor - minimumFactor;
        factor = Math.Min(factor, maximumFactor);
        factor = Math.Max(factor, minimumFactor);
        factor -= minimumFactor;
        return (int)(((factor * priorBoost) + ((range - factor) * tplBoost)) / range);
    }

    /// <summary>
    /// Returns the value of one floor curve at a quantizer index. The curve is a third-order polynomial of the real
    /// quantizer whose linear coefficient depends on the curve and the resolution class. Reference: the lookup of
    /// kf_low_motion_minq, kf_high_motion_minq, arfgf_low_motion_minq and arfgf_high_motion_minq, which
    /// init_minq_luts() fills with get_minq_index().
    /// </summary>
    /// <param name="q">The quantizer index.</param>
    /// <param name="x3">The cubic coefficient.</param>
    /// <param name="x2">The quadratic coefficient.</param>
    /// <param name="curve">The curve index into the linear coefficients.</param>
    /// <param name="largeResolution">Whether the shorter frame side has 608 lines or more.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The floor quantizer index.</returns>
    private static int GetFloor(int q, double x3, double x2, int curve, bool largeResolution, Av1BitDepth bitDepth)
        => GetMinimumQIndex(ConvertQIndexToQ(q, bitDepth), x3, x2, CurveCoefficients[((largeResolution ? 1 : 0) * 5) + curve], bitDepth);

    /// <summary>
    /// Returns the quantizer index of a third-order polynomial of the real quantizer, capped at that quantizer.
    /// Reference: get_minq_index().
    /// </summary>
    /// <param name="maximumQ">The real quantizer.</param>
    /// <param name="x3">The cubic coefficient.</param>
    /// <param name="x2">The quadratic coefficient.</param>
    /// <param name="x1">The linear coefficient.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The quantizer index.</returns>
    private static int GetMinimumQIndex(double maximumQ, double x3, double x2, double x1, Av1BitDepth bitDepth)
    {
        double target = Math.Min(((((x3 * maximumQ) + x2) * maximumQ) + x1) * maximumQ, maximumQ);

        // The step from q 2.0 down to lossless has its own case.
        if (target <= 2.0)
        {
            return 0;
        }

        return FindQIndex(target, bitDepth, 0, MaximumQIndex);
    }

    /// <summary>
    /// Interpolates between a low and a high motion floor by a boost, rounding to nearest.
    /// Reference: get_active_quality().
    /// </summary>
    /// <param name="boost">The boost.</param>
    /// <param name="low">The boost below which the high motion floor applies.</param>
    /// <param name="high">The boost above which the low motion floor applies.</param>
    /// <param name="lowMotion">The low motion floor.</param>
    /// <param name="highMotion">The high motion floor.</param>
    /// <returns>The active quality.</returns>
    private static int GetActiveQuality(int boost, int low, int high, int lowMotion, int highMotion)
    {
        if (boost > high)
        {
            return lowMotion;
        }

        if (boost < low)
        {
            return highMotion;
        }

        int gap = high - low;
        int offset = high - boost;
        int difference = highMotion - lowMotion;
        int adjustment = ((offset * difference) + (gap >> 1)) / gap;
        return lowMotion + adjustment;
    }
}
