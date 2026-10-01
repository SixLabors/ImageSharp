// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Places each block of 16x16 or larger in one of five segments by its coded rate against the frame's target rate and
/// by its source variance, and gives each segment a quantizer that scales the expected rate by a fixed ratio. Segment
/// 3 keeps the frame quantizer. Reference: av1_setup_in_frame_q_adj() and av1_caq_select_segment().
/// </summary>
internal static class Av1ComplexityAdaptiveQuantization
{
    /// <summary>
    /// The number of segments the mode uses. Reference: AQ_C_SEGMENTS.
    /// </summary>
    private const int SegmentCount = 5;

    /// <summary>
    /// The segment that keeps the frame quantizer, and the segment of every block before the search places it.
    /// Reference: DEFAULT_AQ2_SEG.
    /// </summary>
    private const byte DefaultSegment = 3;

    /// <summary>
    /// The lowest superblock target rate that enables the segments. Below it the segment syntax usually costs more
    /// than it saves. Reference: is_sb_aq_enabled().
    /// </summary>
    private const int MinimumSuperblockTargetRate = 256;

    /// <summary>
    /// The log variance threshold of a sequence without first-pass statistics. Reference: DEFAULT_LV_THRESH.
    /// </summary>
    private const double DefaultLowVarianceThreshold = 10.0;

    /// <summary>
    /// Gets the rate ratio of each segment, five per strength. Reference: aq_c_q_adj_factor.
    /// </summary>
    private static ReadOnlySpan<double> QuantizerAdjustmentFactors =>
    [
        1.75, 1.25, 1.05, 1.00, 0.90,
        2.00, 1.50, 1.15, 1.00, 0.85,
        2.50, 1.75, 1.25, 1.00, 0.80
    ];

    /// <summary>
    /// Gets the upper bound of each segment's projected rate as a fraction of the target rate, five per strength.
    /// Reference: aq_c_transitions.
    /// </summary>
    private static ReadOnlySpan<double> Transitions =>
    [
        0.15, 0.30, 0.55, 2.00, 100.0,
        0.20, 0.40, 0.65, 2.00, 100.0,
        0.25, 0.50, 0.75, 2.00, 100.0
    ];

    /// <summary>
    /// Gets the upper bound of each segment's log variance as an offset from the low variance threshold, five per
    /// strength. Reference: aq_c_var_thresholds.
    /// </summary>
    private static ReadOnlySpan<double> VarianceThresholds =>
    [
        -4.0, -3.0, -2.0, 100.00, 100.0,
        -3.5, -2.5, -1.5, 100.00, 100.0,
        -3.0, -2.0, -1.0, 100.00, 100.0
    ];

    /// <summary>
    /// Returns whether a frame is coded with enough target rate per superblock for its segments to pay off.
    /// Reference: is_sb_aq_enabled().
    /// </summary>
    /// <param name="superblockTargetRate">The frame's target rate per 64x64 area. Reference: rc->sb64_target_rate.</param>
    /// <returns>Whether the segments are enabled.</returns>
    public static bool IsSuperblockEnabled(int superblockTargetRate) => superblockTargetRate >= MinimumSuperblockTargetRate;

    /// <summary>
    /// Sets the segmentation of a frame that refreshes its segments. Every block starts in the default segment. When
    /// the target rate is too low the frame codes without segments. The frame size of a sequence never changes, so the
    /// resolution change reset is left out. Reference: av1_setup_in_frame_q_adj().
    /// </summary>
    /// <param name="segmentation">The segmentation state of the frame.</param>
    /// <param name="encoderSegmentMap">The segment map the encoder keeps across frames. Reference: cpi->enc_seg.map.</param>
    /// <param name="keyFrame">Whether the frame is a key frame. Reference: current_frame.frame_type.</param>
    /// <param name="screenContent">Whether the frame is screen content. Reference: is_screen_content_type.</param>
    /// <param name="baseQIndex">The frame quantizer index.</param>
    /// <param name="superblockTargetRate">The frame's target rate per 64x64 area. Reference: rc->sb64_target_rate.</param>
    /// <param name="bitDepth">The coded bit depth.</param>
    /// <param name="bestQIndex">The lowest allowed quantizer index. Reference: rc->best_quality.</param>
    /// <param name="worstQIndex">The highest allowed quantizer index. Reference: rc->worst_quality.</param>
    public static void SetupRefreshFrame(
        ObuSegmentationParameters segmentation,
        Span<byte> encoderSegmentMap,
        bool keyFrame,
        bool screenContent,
        int baseQIndex,
        int superblockTargetRate,
        Av1BitDepth bitDepth,
        int bestQIndex,
        int worstQIndex)
    {
        int strength = GetStrength(baseQIndex, bitDepth);
        encoderSegmentMap.Fill(DefaultSegment);
        segmentation.ClearFeatures();
        if (!IsSuperblockEnabled(superblockTargetRate))
        {
            // Reference: av1_disable_segmentation().
            segmentation.Enabled = false;
            segmentation.SegmentationUpdateMap = 0;
            segmentation.SegmentationUpdateData = 0;
            segmentation.SegmentationTemporalUpdate = 0;
            return;
        }

        // Reference: av1_enable_segmentation().
        segmentation.Enabled = true;
        segmentation.SegmentationUpdateMap = 1;
        segmentation.SegmentationUpdateData = 1;
        segmentation.SegmentationTemporalUpdate = 0;
        segmentation.SetFeatureEnabled(DefaultSegment, (int)ObuSegmentationLevelFeature.AlternativeQuantizer, false);
        for (int segment = 0; segment < SegmentCount; segment++)
        {
            if (segment == DefaultSegment)
            {
                continue;
            }

            int qIndexDelta = Av1RateControl.GetQDeltaByRate(
                keyFrame,
                screenContent,
                baseQIndex,
                QuantizerAdjustmentFactors[(strength * SegmentCount) + segment],
                bitDepth,
                bestQIndex,
                worstQIndex);

            // A lossy frame never gives a segment quantizer zero, which would make the segment lossless.
            if (baseQIndex != 0 && baseQIndex + qIndexDelta == 0)
            {
                qIndexDelta = -baseQIndex + 1;
            }

            if (baseQIndex + qIndexDelta > 0)
            {
                segmentation.SetFeatureEnabled(segment, (int)ObuSegmentationLevelFeature.AlternativeQuantizer, true);
                segmentation.SetFeatureData(segment, (int)ObuSegmentationLevelFeature.AlternativeQuantizer, qIndexDelta);
            }
        }
    }

    /// <summary>
    /// Selects the segment of a searched block: the first segment whose rate and variance bounds the block falls
    /// under, else the last. A higher segment holds a costlier, more complex block. Reference: av1_caq_select_segment().
    /// </summary>
    /// <param name="projectedRate">The block's searched rate.</param>
    /// <param name="superblockTargetRate">The frame's target rate per 64x64 area. Reference: rc->sb64_target_rate.</param>
    /// <param name="visibleModeInfoCount">The number of the block's 4x4 units inside the frame.</param>
    /// <param name="superblockModeInfoSize">The superblock size in 4x4 units. Reference: mib_size.</param>
    /// <param name="logVariance">The block's mean log source variance. Reference: av1_log_block_var().</param>
    /// <param name="baseQIndex">The frame quantizer index.</param>
    /// <param name="bitDepth">The coded bit depth.</param>
    /// <returns>The block's segment.</returns>
    public static byte SelectSegment(
        int projectedRate,
        int superblockTargetRate,
        int visibleModeInfoCount,
        int superblockModeInfoSize,
        double logVariance,
        int baseQIndex,
        Av1BitDepth bitDepth)
    {
        // The target covers the block's share of a superblock in rate cost units. Reference: AV1_PROB_COST_SHIFT.
        long targetRate = (((long)superblockTargetRate * visibleModeInfoCount) << 9) /
            (superblockModeInfoSize * superblockModeInfoSize);

        int strength = GetStrength(baseQIndex, bitDepth);

        // A sequence without first-pass statistics of a separate pass uses the fixed threshold. Reference: the
        // is_stat_consumption_stage_twopass() choice of low_var_thresh.
        const double lowVarianceThreshold = DefaultLowVarianceThreshold;
        for (int segment = 0; segment < SegmentCount; segment++)
        {
            int index = (strength * SegmentCount) + segment;
            if (projectedRate < targetRate * Transitions[index] &&
                logVariance < lowVarianceThreshold + VarianceThresholds[index])
            {
                return (byte)segment;
            }
        }

        return SegmentCount - 1;
    }

    /// <summary>
    /// Gets the strength of the segment adjustments from the frame quantizer: 0 at fine quantizers, up to 2 at coarse
    /// ones. Reference: get_aq_c_strength().
    /// </summary>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="bitDepth">The coded bit depth.</param>
    /// <returns>The strength, from 0 to 2.</returns>
    private static int GetStrength(int qIndex, Av1BitDepth bitDepth)
    {
        int baseQuantizer = Av1QuantizationLookup.GetAcQuant(qIndex, 0, bitDepth) / 4;
        return (baseQuantizer > 10 ? 1 : 0) + (baseQuantizer > 25 ? 1 : 0);
    }
}
