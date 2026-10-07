// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <content>
/// The rate control of a look-ahead sequence that codes against a bit budget: the bits of each key frame group and
/// golden frame group, the target of each frame, the quantizer the rate model picks for it, and the update after it
/// is coded. libavif keeps libaom's default rate of 256 kbps with the good-quality buffer of 6000 ms and 25% under-
/// and overshoot. Reference: the one-pass look-ahead (LAP) bit allocation of find_next_key_frame() and
/// define_gf_group(), the targets of av1_set_frame_size(), and the statistics branch of rc_pick_q_and_bounds().
/// </content>
internal sealed partial class Av1SecondPass
{
    /// <summary>
    /// The target rate in bits per second. Reference: the rc_target_bitrate default.
    /// </summary>
    private const long TargetBandwidth = 256 * 1000;

    /// <summary>
    /// The largest frame in percent of the average frame. Reference: the rc_2pass_vbr_maxsection_pct default.
    /// </summary>
    private const int VariableBitrateMaximumSection = 2000;

    /// <summary>
    /// The undershoot tolerance in percent. Reference: the rc_undershoot_pct default.
    /// </summary>
    private const int UnderShootPercentage = 25;

    /// <summary>
    /// The overshoot tolerance in percent. Reference: the rc_overshoot_pct default.
    /// </summary>
    private const int OverShootPercentage = 25;

    /// <summary>
    /// The buffer level before the first frame, in milliseconds. Reference: the rc_buf_initial_sz default.
    /// </summary>
    private const int StartingBufferMilliseconds = 4000;

    /// <summary>
    /// The largest buffer level, in milliseconds. Reference: the rc_buf_sz default.
    /// </summary>
    private const int MaximumBufferMilliseconds = 6000;

    /// <summary>
    /// The bits-per-macroblock precision. Reference: BPER_MB_NORMBITS.
    /// </summary>
    private const int BitsPerMacroblockShift = 9;

    /// <summary>
    /// The least bits a frame is given. Reference: FRAME_OVERHEAD_BITS.
    /// </summary>
    private const int FrameOverheadBits = 200;

    /// <summary>
    /// Reference: MIN_BPB_FACTOR.
    /// </summary>
    private const double MinimumBitsPerBlockFactor = 0.005;

    /// <summary>
    /// Reference: MAX_BPB_FACTOR.
    /// </summary>
    private const double MaximumBitsPerBlockFactor = 50;

    /// <summary>
    /// Reference: MAX_MB_RATE.
    /// </summary>
    private const int MaximumMacroblockRate = 250;

    /// <summary>
    /// Reference: MAXRATE_1080P.
    /// </summary>
    private const int MaximumRate1080P = 2025000;

    /// <summary>
    /// The seconds of frames a look-ahead key frame group allocates its key frame bits over at most. Reference:
    /// MAX_KF_BITS_INTERVAL_SINGLE_PASS.
    /// </summary>
    private const int MaximumKeyFrameBitsIntervalSeconds = 5;

    /// <summary>
    /// The largest share, in percent, of a frame target that the variable-bitrate correction moves. Reference:
    /// VBR_PCT_ADJUSTMENT_LIMIT.
    /// </summary>
    private const int VariableBitrateAdjustmentLimit = 50;

    /// <summary>
    /// The largest quantizer floor extension of the variable-bitrate mode. Reference: MINQ_ADJ_LIMIT.
    /// </summary>
    private const int MinimumQAdjustmentLimit = 48;

    /// <summary>
    /// The largest quantizer floor extension of the constrained-quality mode. Reference: MINQ_ADJ_LIMIT_CQ.
    /// </summary>
    private const int MinimumQAdjustmentLimitConstrained = 20;

    /// <summary>
    /// The undershoot below which an ordinary frame feeds its unused bits back quickly. Reference:
    /// HIGH_UNDERSHOOT_RATIO.
    /// </summary>
    private const int HighUndershootRatio = 2;

    /// <summary>
    /// The error scale of the worst quantizer estimate. Reference: ERR_DIVISOR.
    /// </summary>
    private const double ErrorDivisor = 96.0;

    /// <summary>
    /// The zero motion percentage at and above which a key frame group is static. Reference:
    /// STATIC_KF_GROUP_THRESH.
    /// </summary>
    private const int StaticKeyFrameGroupPercent = 99;

    /// <summary>
    /// The zero motion percentage of the last key frame group at and above which a forced key frame keeps its
    /// quantizer range. Reference: STATIC_MOTION_THRESH.
    /// </summary>
    private const int StaticMotionThreshold = 95;

    /// <summary>
    /// The share of the quality level the spending must reach before the constrained-quality mode keeps its level.
    /// Reference: cq_adjust_threshold of get_active_cq_level().
    /// </summary>
    private const double ConstantQualityAdjustThreshold = 0.1;

    /// <summary>
    /// The rate correction factor level of ordinary inter frames. Reference: INTER_NORMAL.
    /// </summary>
    private const int InterNormalLevel = 0;

    /// <summary>
    /// The rate correction factor level of internal alternate references. Reference: GF_ARF_LOW.
    /// </summary>
    private const int GoldenLowLevel = 1;

    /// <summary>
    /// The rate correction factor level of golden frames and alternate references. Reference: GF_ARF_STD.
    /// </summary>
    private const int GoldenStandardLevel = 2;

    /// <summary>
    /// The rate correction factor level of key frames. Reference: KF_STD.
    /// </summary>
    private const int KeyFrameLevel = 3;

    /// <summary>
    /// The rate control mode. Reference: rc_cfg.mode.
    /// </summary>
    private readonly Av1RateControlMode mode;

    /// <summary>
    /// The frame rate. Reference: cpi->framerate.
    /// </summary>
    private readonly double framerate;

    /// <summary>
    /// The bits per frame at the target rate. Reference: rc->avg_frame_bandwidth.
    /// </summary>
    private readonly int averageFrameBandwidth;

    /// <summary>
    /// The largest bit target of a frame. Reference: rc->max_frame_bandwidth.
    /// </summary>
    private readonly int maximumFrameBandwidth;

    /// <summary>
    /// The largest buffer level, in bits. Reference: p_rc->maximum_buffer_size.
    /// </summary>
    private readonly long maximumBufferSize;

    /// <summary>
    /// The rate correction factors, indexed by level. Reference: p_rc->rate_correction_factors.
    /// </summary>
    private readonly double[] rateCorrectionFactors = [0.7, 0.7, 0.7, 1.0];

    /// <summary>
    /// The quantizer index of the last frame of each pyramid level and the levels above it. Reference:
    /// p_rc->active_best_quality.
    /// </summary>
    private readonly int[] activeBestQualities = new int[Av1GopStructure.MaximumArfLayers + 1];

    /// <summary>
    /// The sum of the statistics of every analysed frame. Reference: stats_buf_ctx->total_stats.
    /// </summary>
    private Av1FirstPassStatistics totalStatistics;

    /// <summary>
    /// The bits left for the frames of the key frame group. Reference: twopass->kf_group_bits.
    /// </summary>
    private long keyFrameGroupBits;

    /// <summary>
    /// The error left for the golden groups of the key frame group, one per frame. Reference:
    /// twopass->kf_group_error_left.
    /// </summary>
    private double keyFrameGroupErrorLeft;

    /// <summary>
    /// The bits of the current golden group. Reference: p_rc->gf_group_bits.
    /// </summary>
    private long goldenGroupBits;

    /// <summary>
    /// The bit allocation of the current frame. Reference: rc->base_frame_target.
    /// </summary>
    private int baseFrameTarget;

    /// <summary>
    /// The bit target of the current frame after the variable-bitrate correction. Reference: rc->this_frame_target.
    /// </summary>
    private int thisFrameTarget;

    /// <summary>
    /// The bits the coded frames used beyond their allocations, negative when they overshot. Reference:
    /// p_rc->vbr_bits_off_target.
    /// </summary>
    private long variableBitrateBitsOffTarget;

    /// <summary>
    /// The bits large undershoots left for quick reuse. Reference: p_rc->vbr_bits_off_target_fast.
    /// </summary>
    private long variableBitrateBitsOffTargetFast;

    /// <summary>
    /// The extra bits the current frame took from the quick reuse. Reference: rc->frame_level_fast_extra_bits.
    /// </summary>
    private int frameFastExtraBits;

    /// <summary>
    /// Whether the current frame took extra bits from the quick reuse. Reference:
    /// cpi->do_update_vbr_bits_off_target_fast.
    /// </summary>
    private bool updatesFastBitsOffTarget;

    /// <summary>
    /// The bits off target in percent of the bits spent, from -100 to 100. Reference: p_rc->rate_error_estimate.
    /// </summary>
    private int rateErrorEstimate;

    /// <summary>
    /// The correction of the expected bits per macroblock of the worst quantizer estimate. Reference:
    /// twopass->bpm_factor.
    /// </summary>
    private double bitsPerMacroblockFactor = 1.0;

    /// <summary>
    /// The allocated bits of the current alternate reference group. Reference:
    /// twopass->rolling_arf_group_target_bits.
    /// </summary>
    private long rollingGroupTargetBits = 1;

    /// <summary>
    /// The coded bits of the current alternate reference group. Reference: twopass->rolling_arf_group_actual_bits.
    /// </summary>
    private long rollingGroupActualBits = 1;

    /// <summary>
    /// The decrease of the quantizer floor from repeated undershoots. Reference: twopass->extend_minq.
    /// </summary>
    private int extendMinimumQ;

    /// <summary>
    /// The increase of the quantizer ceiling from repeated overshoots. Reference: twopass->extend_maxq.
    /// </summary>
    private int extendMaximumQ;

    /// <summary>
    /// The running target of inter frames. Reference: p_rc->rolling_target_bits.
    /// </summary>
    private int rollingTargetBits;

    /// <summary>
    /// The running size of inter frames. Reference: p_rc->rolling_actual_bits.
    /// </summary>
    private int rollingActualBits;

    /// <summary>
    /// The bits every coded frame used. Reference: p_rc->total_actual_bits.
    /// </summary>
    private long totalActualBits;

    /// <summary>
    /// The average frame bandwidth summed over the shown frames. Reference: p_rc->total_target_bits.
    /// </summary>
    private long totalTargetBits;

    /// <summary>
    /// The bits saved against the target, capped at the buffer size. Reference: p_rc->bits_off_target.
    /// </summary>
    private long bitsOffTarget;

    /// <summary>
    /// The zero motion percentage of the current key frame group. Reference: twopass->kf_zeromotion_pct.
    /// </summary>
    private int keyFrameZeroMotionPercent = 100;

    /// <summary>
    /// The zero motion percentage of the key frame group of the last inter frame. Reference:
    /// twopass->last_kfgroup_zeromotion_pct.
    /// </summary>
    private int lastKeyFrameGroupZeroMotionPercent = 100;

    /// <summary>
    /// Whether the refresh flags of the last coded frame refresh GOLDEN or ALTREF, which the quantizer estimates
    /// read until the next frame is coded. Reference: cpi->refresh_frame.golden_frame || alt_ref_frame.
    /// </summary>
    private bool lastRefreshesBoostedReference;

    /// <summary>
    /// Whether the current frame codes the source of an alternate reference, an overlay. Reference:
    /// rc->is_src_frame_alt_ref.
    /// </summary>
    private bool sourceIsAlternate;

    /// <summary>
    /// Gets the bit target of the current frame, zero in constant-quality coding. Reference: rc->this_frame_target.
    /// </summary>
    public int FrameTarget => this.thisFrameTarget;

    /// <summary>
    /// Gets a value indicating whether the frames code against a bit budget. Reference: rc_cfg.mode != AOM_Q.
    /// </summary>
    private bool UsesBitBudget => this.mode != Av1RateControlMode.Quality;

    /// <summary>
    /// Gets a value indicating whether a high bit depth sequence uses sharpness 3.
    /// Then the fast return of undershoot bits stops while the sequence uses too many bits.
    /// Reference: the bit_depth &gt; 8 &amp;&amp; sharpness == 3 tests of vbr_rate_correction() and av1_twopass_postencode_update().
    /// </summary>
    private bool UsesHighBitDepthSharpness => this.bitDepth.GetBitCount() > 8 && this.sharpness == 3;

    /// <summary>
    /// Returns the quantizer index whose quantizer sets the strength of the temporal filter: the quality level, or
    /// under a bit budget the running average quantizer of the frame type of the frame being coded.
    /// Reference: get_q() of the temporal filter.
    /// </summary>
    /// <param name="groupIndex">The index in the group of the frame being coded. Reference: gf_frame_index.</param>
    /// <returns>The quantizer index.</returns>
    public int GetTemporalFilterQIndex(int groupIndex)
    {
        if (!this.UsesBitBudget)
        {
            return this.cqLevel;
        }

        return this.group.KeyFrames[groupIndex] ? this.averageKeyFrameQIndex : this.averageInterFrameQIndex;
    }

    /// <summary>
    /// Returns the expected bits of a frame at a quantizer, the bits per macroblock at the current correction factor
    /// times the macroblock count. Reference: av1_estimate_bits_at_q() without the accurate estimate.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="correctionFactor">The rate correction factor.</param>
    /// <returns>The expected bits.</returns>
    private int EstimateBitsAtQ(bool keyFrame, int qIndex, double correctionFactor)
    {
        int bitsPerMacroblock = this.GetBitsPerMacroblock(keyFrame, qIndex, correctionFactor);
        return Math.Max(FrameOverheadBits, (int)((ulong)bitsPerMacroblock * (ulong)this.macroblockCount) >> BitsPerMacroblockShift);
    }

    /// <summary>
    /// Returns the expected bits per macroblock at a quantizer. Reference: av1_rc_bits_per_mb() with
    /// get_bpmb_enumerator(), outside the constant-bitrate accurate estimate.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="correctionFactor">The rate correction factor.</param>
    /// <returns>The bits per macroblock.</returns>
    private int GetBitsPerMacroblock(bool keyFrame, int qIndex, double correctionFactor)
    {
        double q = Av1ConstantQuality.ConvertQIndexToQ(qIndex, this.bitDepth);
        int enumerator = this.screenContentType ? keyFrame ? 1000000 : 750000 : keyFrame ? 2000000 : 1500000;
        return (int)(enumerator * correctionFactor / q);
    }

    /// <summary>
    /// Sets up the rate control state. Reference: av1_primary_rc_init(), av1_rc_init(), set_rc_buffer_sizes(),
    /// av1_rc_update_framerate() and av1_init_single_pass_lap().
    /// </summary>
    private void InitializeRateControl()
    {
        this.bitsOffTarget = StartingBufferMilliseconds * TargetBandwidth / 1000;

        // The first frames of the group targets start at the bits of one frame at the configured frame rate.
        // Reference: the rolling_target_bits of av1_primary_rc_init(), with the init_framerate of the 1/30 time base.
        this.rollingTargetBits = Math.Max(1, (int)(TargetBandwidth / 30.0));
        this.rollingActualBits = this.rollingTargetBits;
    }

    /// <summary>
    /// Returns the largest bit target of one ordinary frame. Reference: frame_max_bits().
    /// </summary>
    /// <returns>The largest target.</returns>
    private int GetFrameMaximumBits()
    {
        long maximumBits = (long)this.averageFrameBandwidth * VariableBitrateMaximumSection / 100;
        return (int)Math.Clamp(maximumBits, 0, this.maximumFrameBandwidth);
    }

    /// <summary>
    /// Returns the bits of a group that its boosted frame takes: the boost weighs against one hundred per other frame.
    /// Reference: calculate_boost_bits().
    /// </summary>
    /// <param name="frameCount">The number of other frames of the group.</param>
    /// <param name="boost">The boost.</param>
    /// <param name="totalGroupBits">The bits of the group.</param>
    /// <returns>The bits of the boosted frame.</returns>
    private static int CalculateBoostBits(int frameCount, int boost, long totalGroupBits)
    {
        if (boost == 0 || totalGroupBits <= 0)
        {
            return 0;
        }

        if (frameCount <= 0)
        {
            return (int)Math.Min(totalGroupBits, int.MaxValue);
        }

        int allocationChunks = (frameCount * 100) + boost;

        // A large boost scales both terms down to prevent an overflow.
        if (boost > 1023)
        {
            int divisor = boost >> 10;
            boost /= divisor;
            allocationChunks /= divisor;
        }

        return Math.Max((int)((long)boost * totalGroupBits / allocationChunks), 0);
    }

    /// <summary>
    /// Allocates the bits of a new key frame group of a look-ahead sequence and the key frame's own bits: the group
    /// gets one average frame per frame, capped at the largest frame per frame, and the key frame takes its boost's
    /// share of the bits of at most five seconds of frames. Constant-quality coding gives the group no bits.
    /// Reference: the bit allocation of find_next_key_frame() with get_kf_group_bits() in the one-pass look-ahead
    /// stage, without corpus complexity or level targets.
    /// </summary>
    private void AllocateKeyFrameGroupBits()
    {
        this.keyFrameGroupBits = 0;
        if (this.UsesBitBudget)
        {
            int maximumBits = this.GetFrameMaximumBits();
            long maximumGroupBits = (long)maximumBits * this.framesToKey;
            this.keyFrameGroupBits = Math.Min((long)this.framesToKey * this.averageFrameBandwidth, maximumGroupBits);
        }

        // A long key frame interval allocates the key frame bits over at most five seconds of frames.
        int framesToKeyClipped = (int)(MaximumKeyFrameBitsIntervalSeconds * this.framerate);
        long keyFrameGroupBitsClipped = long.MaxValue;
        if (this.framesToKey > framesToKeyClipped)
        {
            keyFrameGroupBitsClipped = (long)((double)this.keyFrameGroupBits * framesToKeyClipped / this.framesToKey);
        }

        int keyFrameBits = CalculateBoostBits(
            Math.Min(this.framesToKey, framesToKeyClipped) - 1,
            this.keyFrameBoost,
            Math.Min(this.keyFrameGroupBits, keyFrameGroupBitsClipped));

        this.keyFrameGroupBits -= keyFrameBits;
        this.group.BitAllocations[0] = keyFrameBits;

        // The look-ahead cannot know the error of the group, so every frame after the key frame counts one.
        this.keyFrameGroupErrorLeft = this.framesToKey - 1;
    }

    /// <summary>
    /// Allocates the bits of a golden frame group and of each of its frames: the group takes its share of the key
    /// frame group's bits by its frame count, the alternate reference or golden frame its boost's share of them, and
    /// the internal alternate references their layer's fraction of that. A final definition also estimates the
    /// highest quantizer of the group and restarts the group's running bit counts. Reference: the bit allocation of
    /// set_gop_bits_boost() with calculate_total_gf_group_bits(), get_twopass_worst_quality() under
    /// GROUP_ADAPTIVE_MAXQ, av1_gop_bit_allocation() and allocate_gf_group_bits().
    /// </summary>
    /// <param name="finalPass">Whether this is the final definition of the group, not a trial.</param>
    /// <param name="useAltRef">Whether the group uses an alternate reference.</param>
    /// <param name="rawError">The coded error summed over the frames of the group. Reference: gf_group_raw_error.</param>
    /// <param name="skipPercent">The intra skip share summed over the frames of the group. Reference: gf_group_skip_pct.</param>
    /// <param name="inactiveZoneRows">
    /// The inactive rows summed over the frames of the group. Reference: gf_group_inactive_zone_rows.
    /// </param>
    private void AllocateGoldenGroupBits(bool finalPass, bool useAltRef, double rawError, double skipPercent, double inactiveZoneRows)
    {
        // The look-ahead cannot know the error of the group, so every frame counts one.
        double groupError = this.baselineGoldenInterval;
        int maximumBits = this.GetFrameMaximumBits();
        long totalGroupBits = this.keyFrameGroupBits > 0 && this.keyFrameGroupErrorLeft > 0
            ? (long)(this.keyFrameGroupBits * (groupError / this.keyFrameGroupErrorLeft))
            : 0;

        totalGroupBits = totalGroupBits < 0 ? 0 : Math.Min(totalGroupBits, this.keyFrameGroupBits);
        totalGroupBits = Math.Min(totalGroupBits, (long)maximumBits * this.baselineGoldenInterval);
        this.goldenGroupBits = totalGroupBits;

        // An estimate of the highest quantizer the group needs, which corrects an expected overshoot more than an
        // expected undershoot.
        if (this.UsesBitBudget && this.baselineGoldenInterval > 1 && finalPass)
        {
            int groupBitsPerFrame = (int)(this.goldenGroupBits / this.baselineGoldenInterval);
            double averageError = rawError / this.baselineGoldenInterval;
            double averageSkip = skipPercent / this.baselineGoldenInterval;
            double averageInactiveZone = inactiveZoneRows * 2 / (this.baselineGoldenInterval * (double)this.macroblockRows);
            int q = this.GetTwopassWorstQuality(averageError, averageSkip + averageInactiveZone, groupBitsPerFrame);
            this.activeWorstQuality = Math.Max(q, this.activeWorstQuality >> 1);
        }

        if (finalPass)
        {
            this.keyFrameGroupErrorLeft -= groupError;
        }

        // av1_gop_bit_allocation(): the boosted frame of the group, then every frame of it.
        bool keyFrameGroup = this.framesSinceKey == 0;
        int boostedBits = CalculateBoostBits(this.baselineGoldenInterval - (keyFrameGroup ? 1 : 0), this.goldenBoost, this.goldenGroupBits);
        this.AllocateGroupFrameBits(boostedBits, keyFrameGroup, useAltRef);

        if (finalPass)
        {
            this.rollingGroupTargetBits = 1;
            this.rollingGroupActualBits = 1;
        }
    }

    /// <summary>
    /// Divides the bits of a golden frame group between its frames. Each alternate reference layer takes its fraction
    /// of the boosted bits, which the frames of the layer share on top of the base bits of every frame; overlays take
    /// none, and a key frame keeps the bits its group allocated. Reference: allocate_gf_group_bits().
    /// </summary>
    /// <param name="boostedBits">The extra bits of the alternate reference layers.</param>
    /// <param name="keyFrame">Whether a key frame starts the group.</param>
    /// <param name="useAltRef">Whether the group uses an alternate reference.</param>
    private void AllocateGroupFrameBits(int boostedBits, bool keyFrame, bool useAltRef)
    {
        ReadOnlySpan<double> layerFraction = [1.0, 0.70, 0.55, 0.60, 0.60, 1.0, 1.0];
        long totalGroupBits = this.goldenGroupBits;
        int frameIndex = keyFrame ? 1 : 0;
        if (useAltRef)
        {
            totalGroupBits -= boostedBits;
        }

        int frameCount = Math.Max(1, this.baselineGoldenInterval - (this.framesSinceKey == 0 ? 1 : 0));
        int baseFrameBits = (int)(totalGroupBits / frameCount);

        // The number of frames of each layer, for a group of any length.
        int maximumLayer = this.group.MaxLayerDepth - 1;
        Span<int> layerFrames = stackalloc int[Av1GopStructure.MaximumArfLayers + 1];
        layerFrames.Clear();
        for (int index = frameIndex; index < this.group.Size; ++index)
        {
            if (this.group.UpdateTypes[index] is Av1FrameUpdateType.Alternate or Av1FrameUpdateType.IntermediateAlternate)
            {
                layerFrames[this.group.LayerDepths[index]]++;
            }
        }

        // Each layer takes its fraction of what the layers above it left; the deepest layer takes the rest.
        Span<int> layerExtraBits = stackalloc int[Av1GopStructure.MaximumArfLayers + 1];
        layerExtraBits.Clear();
        int arfBits = boostedBits;
        for (int layer = 1; layer <= maximumLayer; ++layer)
        {
            double fraction = layer == maximumLayer ? 1.0 : layerFraction[layer];
            layerExtraBits[layer] = (int)(arfBits * fraction / Math.Max(1, layerFrames[layer]));
            arfBits -= (int)(arfBits * fraction);
        }

        for (int index = frameIndex; index < this.group.Size; ++index)
        {
            switch (this.group.UpdateTypes[index])
            {
                case Av1FrameUpdateType.Alternate:
                case Av1FrameUpdateType.IntermediateAlternate:
                    int extraBits = layerExtraBits[this.group.LayerDepths[index]];
                    this.group.BitAllocations[index] = baseFrameBits > int.MaxValue - extraBits ? int.MaxValue : baseFrameBits + extraBits;
                    break;
                case Av1FrameUpdateType.Overlay:
                case Av1FrameUpdateType.IntermediateOverlay:
                    this.group.BitAllocations[index] = 0;
                    break;
                default:
                    this.group.BitAllocations[index] = baseFrameBits;
                    break;
            }
        }

        // The frame after the group, the overlay that starts the next group, takes no bits of this group.
        if (this.group.Size < Av1GopStructure.MaximumLength)
        {
            this.group.BitAllocations[this.group.Size] = 0;
        }
    }

    /// <summary>
    /// Estimates the highest quantizer at which a group of frames codes within a bit rate, from the average coded
    /// error per macroblock and a correction from the rate errors so far. Reference: get_twopass_worst_quality() with
    /// twopass_update_bpm_factor(), calc_correction_factor() and find_qindex_by_rate_with_correction().
    /// </summary>
    /// <param name="averageFrameError">The average coded error per frame.</param>
    /// <param name="inactiveZone">The share of the frame to ignore, such as letterbox bands.</param>
    /// <param name="averageTargetBandwidth">The target bits per frame.</param>
    /// <returns>The highest quantizer index.</returns>
    private int GetTwopassWorstQuality(double averageFrameError, double inactiveZone, int averageTargetBandwidth)
    {
        inactiveZone = Math.Clamp(inactiveZone, 0.0, 0.9999);
        if (averageTargetBandwidth <= 0)
        {
            return this.worstQuality;
        }

        // The first group uses a smaller rate constant.
        bool smallerEnumerator = this.totalActualBits == 0;

        int activeMacroblocks = Math.Max(1, this.macroblockCount - (int)(this.macroblockCount * inactiveZone));
        double errorPerMacroblock = averageFrameError / (1.0 - inactiveZone);
        ulong targetBitsPerMacroblock = ((ulong)averageTargetBandwidth << BitsPerMacroblockShift) / (ulong)activeMacroblocks;
        int rateErrorTolerance = Math.Min(UnderShootPercentage, OverShootPercentage);

        this.UpdateBitsPerMacroblockFactor(rateErrorTolerance);

        // A group whose alternate reference codes well below the group's error lowers its quantizer on static
        // frames.
        bool lowerOnStaticFrame = false;
        int arfOffset = this.baselineGoldenInterval - 1;
        if (this.statisticsInfo.Contains(arfOffset))
        {
            lowerOnStaticFrame = this.statisticsInfo.Peek(arfOffset).CodedError < 2 * errorPerMacroblock;
        }

        int low = this.bestQuality;
        int high = this.worstQuality;
        int enumerator = (smallerEnumerator ? 1050000 : 1125750) + (300000 * Math.Min(75, Math.Max(rateErrorTolerance - 25, 0)) / 75);
        while (low < high)
        {
            int middle = (low + high) >> 1;
            double factor = GetWorstQualityCorrectionFactor(errorPerMacroblock, middle, inactiveZone, lowerOnStaticFrame);
            double q = Av1ConstantQuality.ConvertQIndexToQ(middle, this.bitDepth);
            ulong bitsPerMacroblock = (ulong)(enumerator * factor * this.bitsPerMacroblockFactor / q);
            if (bitsPerMacroblock > targetBitsPerMacroblock)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        // The constrained-quality mode keeps the ceiling at or above its quality level.
        return this.mode == Av1RateControlMode.ConstrainedQuality ? Math.Max(low, this.cqLevel) : low;
    }

    /// <summary>
    /// Returns the correction of the bits per macroblock of the worst quantizer estimate at a quantizer index: the
    /// error per macroblock to a power that grows with the quantizer. Reference: calc_correction_factor() with
    /// q_pow_term.
    /// </summary>
    /// <param name="errorPerMacroblock">The coded error per macroblock.</param>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="inactiveZone">The share of the frame to ignore.</param>
    /// <param name="lowerOnStaticFrame">Whether the inactive share reduces the error.</param>
    /// <returns>The correction factor.</returns>
    private static double GetWorstQualityCorrectionFactor(double errorPerMacroblock, int qIndex, double inactiveZone, bool lowerOnStaticFrame)
    {
        ReadOnlySpan<double> powerTerms = [0.65, 0.70, 0.75, 0.80, 0.85, 0.90, 0.95, 0.95, 0.95];
        double errorTerm = errorPerMacroblock / ErrorDivisor;
        if (lowerOnStaticFrame)
        {
            errorTerm *= 1 - Math.Min(0.6, inactiveZone);
        }

        int index = qIndex >> 5;
        double powerTerm = powerTerms[index] + ((powerTerms[index + 1] - powerTerms[index]) * (qIndex % 32) / 32.0);
        return Math.Clamp(Math.Pow(errorTerm, powerTerm), 0.05, 5.0);
    }

    /// <summary>
    /// Moves the bits per macroblock correction of the worst quantizer estimate toward the rate error of the last
    /// alternate reference group, damped and only in the direction that the error is getting worse. Reference:
    /// twopass_update_bpm_factor() in the one-pass look-ahead stage.
    /// </summary>
    /// <param name="rateErrorTolerance">The smaller of the under- and overshoot tolerances, in percent.</param>
    private void UpdateBitsPerMacroblockFactor(int rateErrorTolerance)
    {
        double dampingFactor = Math.Max(5.0, rateErrorTolerance / 10.0);
        double rateErrorFactor = 1.0;
        double adjustmentLimitCap;
        if (rateErrorTolerance != 0)
        {
            const double initialAdjustmentLimit = 0.0626;
            double absoluteErrorTolerance = 0.6 * rateErrorTolerance;
            double stepSize = (0.2 - initialAdjustmentLimit) / (rateErrorTolerance - absoluteErrorTolerance);

            // The look-ahead leaves no bits, so the error measures against the bits spent. Before the first frame
            // nothing is off target, and the error is zero.
            double absoluteErrorSoFar = 100 * Math.Abs((double)this.variableBitrateBitsOffTarget / Math.Max(this.totalActualBits, 1));
            adjustmentLimitCap = initialAdjustmentLimit;
            if (absoluteErrorSoFar >= absoluteErrorTolerance)
            {
                adjustmentLimitCap = Math.Min(0.2, initialAdjustmentLimit + ((absoluteErrorSoFar - absoluteErrorTolerance) * stepSize));
            }
        }
        else
        {
            adjustmentLimitCap = 0.2;
        }

        double adjustmentLimit = Math.Max(adjustmentLimitCap, (100 - rateErrorTolerance) / 200.0);
        double minimumFactor = 1.0 - adjustmentLimit;
        double maximumFactor = 1.0 + adjustmentLimit;
        if (this.bitsOffTarget != 0 && this.totalActualBits > 0)
        {
            rateErrorFactor = this.rollingGroupActualBits / DoubleDivideCheck(this.rollingGroupTargetBits);

            // The look-ahead holds only a few frames of statistics, so the adjustment is damped.
            rateErrorFactor = 1.0 + ((rateErrorFactor - 1.0) / dampingFactor);
            rateErrorFactor = Math.Max(minimumFactor, Math.Min(maximumFactor, rateErrorFactor));
        }

        if ((rateErrorFactor < 1.0 && this.rateErrorEstimate >= 0) || (rateErrorFactor > 1.0 && this.rateErrorEstimate <= 0))
        {
            this.bitsPerMacroblockFactor *= rateErrorFactor;
            this.bitsPerMacroblockFactor = rateErrorTolerance >= 100
                ? Math.Max(minimumFactor, Math.Min(maximumFactor, this.bitsPerMacroblockFactor))
                : Math.Max(0.1, Math.Min(10.0, this.bitsPerMacroblockFactor));
        }
    }

    /// <summary>
    /// Sets the highest quantizer of a look-ahead sequence before its first frame from the statistics the look-ahead
    /// holds, and starts the quantizer history from it. Reference: the first frame branch of
    /// process_first_pass_stats() with get_section_target_bandwidth() in the one-pass look-ahead stage.
    /// </summary>
    private void InitializeFirstFrameQuality()
    {
        double sectionLength = this.totalStatistics.Count;
        double sectionError = this.totalStatistics.CodedError / sectionLength;
        double sectionIntraSkip = this.totalStatistics.IntraSkipPercent / sectionLength;
        double sectionInactiveZone = this.totalStatistics.InactiveZoneRows * 2 / (this.macroblockRows * sectionLength);
        int q = this.GetTwopassWorstQuality(sectionError, sectionIntraSkip + sectionInactiveZone, this.averageFrameBandwidth);

        this.activeWorstQuality = q;
        this.averageInterFrameQIndex = q;
        this.lastKeyFrameQIndex = (q + this.bestQuality) / 2;
        this.averageKeyFrameQIndex = this.lastKeyFrameQIndex;
    }

    /// <summary>
    /// Sets the bit target of the current frame from its allocation, moved by a limited share of the bits the earlier
    /// frames left or overspent, plus the bits a large undershoot left for an ordinary frame. Reference:
    /// av1_setup_target_rate(), then av1_set_target_rate() with vbr_rate_correction() in av1_set_frame_size().
    /// </summary>
    private void SetFrameTarget()
    {
        this.baseFrameTarget = this.group.BitAllocations[this.groupFrameIndex];
        long frameTarget = this.baseFrameTarget;
        this.updatesFastBitsOffTarget = false;
        if (this.mode is Av1RateControlMode.VariableBitRate or Av1RateControlMode.ConstrainedQuality)
        {
            // The correction spreads the bits off target over the next sixteen frames at most.
            int frameWindow = (int)Math.Min(16, this.totalStatistics.Count - this.frameNumber);
            if (frameWindow > 0)
            {
                long maximumDelta = Math.Min(Math.Abs(this.variableBitrateBitsOffTarget / frameWindow), frameTarget * VariableBitrateAdjustmentLimit / 100);
                frameTarget += this.variableBitrateBitsOffTarget >= 0 ? maximumDelta : -maximumDelta;
            }

            // An ordinary frame quickly takes back the bits of a large local undershoot.
            // At high bit depth sharpness 3, this does not occur while the sequence uses too many bits.
            // Reference: the fast redistribution test of vbr_rate_correction().
            Av1FrameUpdateType updateType = this.group.UpdateTypes[this.groupFrameIndex];
            bool boosted = updateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;
            bool sourceIsAlternate = updateType is Av1FrameUpdateType.Overlay or Av1FrameUpdateType.IntermediateOverlay;
            bool overspent = this.UsesHighBitDepthSharpness && this.variableBitrateBitsOffTarget < 0;
            if (!boosted && this.variableBitrateBitsOffTargetFast != 0 && !overspent && !sourceIsAlternate)
            {
                long oneFrameBits = Math.Max(this.averageFrameBandwidth, frameTarget);
                long fastExtraBits = Math.Min(this.variableBitrateBitsOffTargetFast, oneFrameBits);
                fastExtraBits = Math.Min(fastExtraBits, Math.Max(oneFrameBits / 8, this.variableBitrateBitsOffTargetFast / 8));
                fastExtraBits = Math.Min(fastExtraBits, int.MaxValue);
                if (fastExtraBits > 0)
                {
                    frameTarget += fastExtraBits;
                }

                this.frameFastExtraBits = (int)fastExtraBits;
                this.updatesFastBitsOffTarget = true;
            }
        }

        this.thisFrameTarget = (int)Math.Min(frameTarget, int.MaxValue);
    }

    /// <summary>
    /// Returns the rate correction factor level of a frame of the group by its update type. Reference:
    /// get_rate_factor_level() with rate_factor_levels.
    /// </summary>
    /// <param name="groupIndex">The frame's index in the group.</param>
    /// <returns>The level.</returns>
    private int GetRateFactorLevel(int groupIndex) => this.group.UpdateTypes[groupIndex] switch
    {
        Av1FrameUpdateType.Key => KeyFrameLevel,
        Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate => GoldenStandardLevel,
        Av1FrameUpdateType.IntermediateAlternate => GoldenLowLevel,
        _ => InterNormalLevel
    };

    /// <summary>
    /// Returns the rate correction factor of a frame: the key frame factor for a key frame, else the factor of the
    /// level of the frame being coded, which the estimates of the other frames of the group read too. Reference:
    /// get_rate_correction_factor() in a statistics-consuming stage, which reads the level at gf_frame_index.
    /// </summary>
    /// <param name="groupIndex">The index in the group of the frame the factor is for.</param>
    /// <returns>The factor.</returns>
    private double GetRateCorrectionFactor(int groupIndex)
    {
        double factor = this.group.KeyFrames[groupIndex]
            ? this.rateCorrectionFactors[KeyFrameLevel]
            : this.rateCorrectionFactors[this.GetRateFactorLevel(this.groupFrameIndex)];

        return Math.Clamp(factor, MinimumBitsPerBlockFactor, MaximumBitsPerBlockFactor);
    }

    /// <summary>
    /// Picks the quantizer index of a frame of the group that codes against a bit budget. The floor comes from the
    /// key frame or golden tables, the floor of the layer above for a deeper frame, or the inter table for a leaf;
    /// the ceiling from the group's estimate, moved toward the floor for a boosted frame, by the layer's rate step and
    /// by the corrections of the earlier rate errors. The rate model then picks the quantizer that meets the frame
    /// target between them. Reference: rc_pick_q_and_bounds() outside AOM_Q, with get_intra_q_and_bounds(),
    /// get_active_best_quality(), adjust_active_best_and_worst_quality() and get_q().
    /// </summary>
    /// <param name="groupIndex">The frame's index in the group.</param>
    /// <param name="screenContent">Whether the frame is screen content. Reference: is_screen_content_type.</param>
    /// <param name="refreshesBoostedReference">
    /// Whether the frame being set up refreshes GOLDEN or ALTREF, which the estimates of the other frames of the group
    /// also read. Reference: refresh_frame->golden_frame || refresh_frame->alt_ref_frame.
    /// </param>
    /// <returns>The quantizer index.</returns>
    private int PickBitBudgetQIndex(int groupIndex, bool screenContent, bool refreshesBoostedReference)
    {
        bool keyFrame = this.group.KeyFrames[groupIndex];
        Av1FrameUpdateType updateType = this.group.UpdateTypes[groupIndex];
        bool intermediateArf = updateType == Av1FrameUpdateType.IntermediateAlternate;
        int activeBestQuality = 0;
        int activeWorst = this.activeWorstQuality;
        if (keyFrame)
        {
            this.GetIntraQAndBounds(ref activeBestQuality, ref activeWorst, screenContent);
        }
        else
        {
            // A frame below the first pyramid level takes the floor of the level above, halfway to the ceiling.
            int pyramidLevel = this.group.LayerDepths[groupIndex];
            if (pyramidLevel <= 1 || pyramidLevel > Av1GopStructure.MaximumArfLayers)
            {
                activeBestQuality = this.GetActiveBestQuality(activeWorst, groupIndex, this.GetActiveConstantQualityLevel());
            }
            else
            {
                activeBestQuality = this.activeBestQualities[pyramidLevel - 1] + 1;
                activeBestQuality = Math.Min(activeBestQuality, activeWorst);
                activeBestQuality += (activeWorst - activeBestQuality) / 2;
            }

            // A boosted frame moves its ceiling a quarter of the way to its floor, so that its quantizer stays below
            // that of the leaves on hard sections.
            if (!this.sourceIsAlternate && (refreshesBoostedReference || intermediateArf))
            {
                activeWorst = (activeBestQuality + (3 * activeWorst) + 2) / 4;
            }
        }

        // adjust_active_best_and_worst_quality(): the corrections of the earlier rate errors, then the rate step of
        // the layer of the frame being coded, unless the frame is a static forced key frame.
        activeBestQuality -= this.extendMinimumQ / 4;
        activeWorst += this.extendMaximumQ;
        if (!keyFrame || !this.thisKeyFrameForced || this.lastKeyFrameGroupZeroMotionPercent < StaticMotionThreshold)
        {
            int qDelta = this.GetFrameTypeQDelta(this.groupFrameIndex, activeWorst);
            activeWorst = Math.Max(activeWorst + qDelta, activeBestQuality);
        }

        activeBestQuality = Math.Clamp(activeBestQuality, this.bestQuality, this.worstQuality);
        activeWorst = Math.Clamp(activeWorst, activeBestQuality, this.worstQuality);

        // get_q().
        int q;
        if (keyFrame && !this.thisKeyFrameForced && this.keyFrameZeroMotionPercent >= StaticKeyFrameGroupPercent && this.framesToKey > 1)
        {
            q = activeBestQuality;
        }
        else if (keyFrame && this.thisKeyFrameForced)
        {
            // A forced key frame stays near the last boosted quantizer to limit popping, or after a static group
            // at the better of that and the last key frame quantizer.
            q = this.lastKeyFrameGroupZeroMotionPercent >= StaticMotionThreshold
                ? Math.Min(this.lastKeyFrameQIndex, this.lastBoostedQIndex)
                : Math.Min(this.lastBoostedQIndex, (activeBestQuality + activeWorst) / 2);

            q = Math.Clamp(q, activeBestQuality, activeWorst);
        }
        else
        {
            q = this.RegulateQuantizer(keyFrame, groupIndex, activeBestQuality, activeWorst);

            // Targeting the largest allowed frame keeps the chosen quantizer.
            if (q > activeWorst && this.thisFrameTarget < this.maximumFrameBandwidth)
            {
                q = activeWorst;
            }

            q = Math.Max(q, activeBestQuality);
        }

        // The bounds a recode searches between; a frame that targets the largest allowed size may exceed the ceiling.
        this.pickedBottomIndex = activeBestQuality;
        this.pickedTopIndex = this.thisFrameTarget >= this.maximumFrameBandwidth && q > activeWorst ? q : activeWorst;
        return q;
    }

    /// <summary>
    /// Returns the quality level of the constrained-quality mode, lowered in proportion while the frames so far spent
    /// less than a tenth of their target, else the configured level. Reference: get_active_cq_level() without
    /// superres.
    /// </summary>
    /// <returns>The active level.</returns>
    private int GetActiveConstantQualityLevel()
    {
        int level = this.cqLevel;
        if (this.mode == Av1RateControlMode.ConstrainedQuality && this.totalTargetBits > 0)
        {
            double spentShare = (double)this.totalActualBits / this.totalTargetBits;
            if (spentShare < ConstantQualityAdjustThreshold)
            {
                level = (int)(level * spentShare / ConstantQualityAdjustThreshold);
            }
        }

        return level;
    }

    /// <summary>
    /// Returns the quantizer index change of a frame's layer: the change that scales the expected rate by the rate
    /// step of the layer, one for ordinary frames. Reference: frame_type_qdelta() with arf_layer_deltas.
    /// </summary>
    /// <param name="groupIndex">The index in the group of the frame being coded. Reference: gf_frame_index.</param>
    /// <param name="q">The quantizer index the step applies to.</param>
    /// <returns>The quantizer index change.</returns>
    private int GetFrameTypeQDelta(int groupIndex, int q)
    {
        ReadOnlySpan<double> layerDeltas = [2.50, 2.00, 1.75, 1.50, 1.25, 1.15, 1.0];
        int level = this.GetRateFactorLevel(groupIndex);
        int layer = Math.Min(this.group.LayerDepths[groupIndex], Av1GopStructure.MaximumArfLayers);
        double rateFactor = level == InterNormalLevel ? 1.0 : layerDeltas[layer];
        bool keyFrame = this.group.KeyFrames[groupIndex];
        return this.GetQDeltaByRate(keyFrame, q, rateFactor);
    }

    /// <summary>
    /// Returns the quantizer index change that scales the expected rate by a ratio, at the base rate correction.
    /// Reference: av1_compute_qdelta_by_rate() and find_qindex_by_rate().
    /// </summary>
    /// <param name="keyFrame">Whether the rate is of a key frame.</param>
    /// <param name="qIndex">The base quantizer index.</param>
    /// <param name="rateTargetRatio">The rate ratio.</param>
    /// <returns>The quantizer index change.</returns>
    private int GetQDeltaByRate(bool keyFrame, int qIndex, double rateTargetRatio)
    {
        int baseBitsPerMacroblock = this.GetBitsPerMacroblock(keyFrame, qIndex, 1.0);
        int targetBitsPerMacroblock = (int)(rateTargetRatio * baseBitsPerMacroblock);
        int low = this.bestQuality;
        int high = this.worstQuality;
        while (low < high)
        {
            int middle = (low + high) >> 1;
            if (this.GetBitsPerMacroblock(keyFrame, middle, 1.0) > targetBitsPerMacroblock)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low - qIndex;
    }

    /// <summary>
    /// Updates the rate control after a frame is coded: the rate correction factor of the frame's level moves toward
    /// the ratio of the coded to the expected size unless the frame codes an overlay, the buffer gains a frame's
    /// bits for a shown frame and loses the coded size, the running inter frame target and size follow the frame,
    /// and the totals add up. Reference: av1_rc_postencode_update() with av1_rc_update_rate_correction_factors()
    /// and update_buffer_level(), for frames of the original size.
    /// </summary>
    /// <param name="projectedFrameSize">The coded size in bits. Reference: rc->projected_frame_size.</param>
    /// <param name="qIndex">The frame's base quantizer index.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="showFrame">Whether the frame is shown.</param>
    private void UpdateRateAfterFrame(int projectedFrameSize, int qIndex, bool keyFrame, bool showFrame)
    {
        this.UpdateRateCorrectionFactor(projectedFrameSize, qIndex, keyFrame);

        // update_buffer_level(): a hidden frame is pure overhead.
        this.bitsOffTarget += showFrame ? this.averageFrameBandwidth - projectedFrameSize : -projectedFrameSize;
        this.bitsOffTarget = Math.Min(this.bitsOffTarget, this.maximumBufferSize);

        if (!keyFrame)
        {
            this.rollingTargetBits = (int)(((3L * this.rollingTargetBits) + this.thisFrameTarget + 2) >> 2);
            this.rollingActualBits = (int)(((3L * this.rollingActualBits) + projectedFrameSize + 2) >> 2);
        }

        this.totalActualBits += projectedFrameSize;
        this.totalTargetBits += showFrame ? this.averageFrameBandwidth : 0;
    }

    /// <summary>
    /// Moves the rate correction factor of the current frame's level toward the ratio of the coded size to the size
    /// the factor expected, damped more for small errors and for screen content. An overlay keeps the factors.
    /// Reference: av1_rc_update_rate_correction_factors() and set_rate_correction_factor() in a
    /// statistics-consuming stage, without cyclic refresh.
    /// </summary>
    /// <param name="projectedFrameSize">The coded size in bits.</param>
    /// <param name="qIndex">The frame's base quantizer index.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    private void UpdateRateCorrectionFactor(int projectedFrameSize, int qIndex, bool keyFrame)
    {
        if (this.sourceIsAlternate)
        {
            return;
        }

        double rateCorrectionFactor = this.GetRateCorrectionFactor(this.groupFrameIndex);
        int projectedSizeAtQ = this.EstimateBitsAtQ(keyFrame, qIndex, rateCorrectionFactor);

        // The ratio of the coded size to the expected size, at least a quarter.
        double correctionFactor = 1.0;
        if (projectedSizeAtQ > FrameOverheadBits)
        {
            correctionFactor = (double)projectedFrameSize / projectedSizeAtQ;
        }

        correctionFactor = Math.Max(correctionFactor, 0.25);

        // A small error moves the factor a quarter of the way, a large one up to five eighths.
        double adjustmentLimit = 0.25 + ((this.screenContentType ? 0.5 : 0.75) * Math.Min(0.5, Math.Abs(Math.Log10(correctionFactor))));
        if (correctionFactor > 1.01)
        {
            correctionFactor = 1.0 + ((correctionFactor - 1.0) * adjustmentLimit);
            rateCorrectionFactor = Math.Min(rateCorrectionFactor * correctionFactor, MaximumBitsPerBlockFactor);
        }
        else if (correctionFactor < 0.99)
        {
            correctionFactor = 1.0 / correctionFactor;
            correctionFactor = 1.0 + ((correctionFactor - 1.0) * adjustmentLimit);
            correctionFactor = 1.0 / correctionFactor;
            rateCorrectionFactor = Math.Max(rateCorrectionFactor * correctionFactor, MinimumBitsPerBlockFactor);
        }

        int level = keyFrame ? KeyFrameLevel : this.GetRateFactorLevel(this.groupFrameIndex);
        this.rateCorrectionFactors[level] = Math.Clamp(rateCorrectionFactor, MinimumBitsPerBlockFactor, MaximumBitsPerBlockFactor);
    }

    /// <summary>
    /// Updates the bit budget after a frame is coded: the bits off target gain the frame's allocation less its size,
    /// the quick reuse loses what the frame took, the alternate reference group's totals and the rate error follow,
    /// the pyramid floors of the frame's layer and below take its quantizer unless it codes an overlay, and the key
    /// frame group loses the allocation of an inter frame. A bit budget then extends the quantizer floor after
    /// repeated undershoot and the ceiling after repeated overshoot, and feeds the bits of a large undershoot of an
    /// ordinary frame back quickly. Reference: av1_twopass_postencode_update() after the statistics are consumed.
    /// </summary>
    /// <param name="projectedFrameSize">The coded size in bits.</param>
    /// <param name="qIndex">The frame's base quantizer index.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    private void UpdateBitsAfterFrame(int projectedFrameSize, int qIndex, bool keyFrame)
    {
        this.variableBitrateBitsOffTarget += this.baseFrameTarget - projectedFrameSize;
        if (this.updatesFastBitsOffTarget)
        {
            this.variableBitrateBitsOffTargetFast -= this.frameFastExtraBits;
            this.frameFastExtraBits = 0;
        }

        this.rollingGroupTargetBits = this.rollingGroupTargetBits > int.MaxValue - this.baseFrameTarget
            ? int.MaxValue
            : this.rollingGroupTargetBits + this.baseFrameTarget;

        this.rollingGroupActualBits += projectedFrameSize;
        this.rateErrorEstimate = this.totalActualBits != 0
            ? (int)Math.Clamp(this.variableBitrateBitsOffTarget * 100 / this.totalActualBits, -100, 100)
            : 0;

        // The floor of the frame's layer and of every deeper layer.
        if (!this.sourceIsAlternate)
        {
            int pyramidLevel = this.group.LayerDepths[this.groupFrameIndex];
            for (int layer = pyramidLevel; layer <= Av1GopStructure.MaximumArfLayers; ++layer)
            {
                this.activeBestQualities[layer] = qIndex;
            }
        }

        if (!keyFrame)
        {
            this.keyFrameGroupBits -= this.baseFrameTarget;
            this.lastKeyFrameGroupZeroMotionPercent = this.keyFrameZeroMotionPercent;
        }

        this.keyFrameGroupBits = Math.Max(this.keyFrameGroupBits, 0);
        if (!this.UsesBitBudget || this.sourceIsAlternate || this.rollingTargetBits <= 0)
        {
            return;
        }

        int minimumQAdjustmentLimit = this.mode == Av1RateControlMode.ConstrainedQuality
            ? MinimumQAdjustmentLimitConstrained
            : MinimumQAdjustmentLimit;

        int maximumQAdjustmentLimit = this.worstQuality - this.activeWorstQuality;
        if (this.rollingActualBits < this.rollingTargetBits)
        {
            // An undershoot beyond the tolerance lowers the floor while the rate error says bits are left.
            int percentError = (int)((this.rollingTargetBits - (long)this.rollingActualBits) * 100 / this.rollingTargetBits);
            if (percentError >= UnderShootPercentage && this.rateErrorEstimate > 0)
            {
                this.extendMinimumQ += 1;
            }

            this.extendMaximumQ -= 1;
        }
        else if (this.rollingActualBits > this.rollingTargetBits)
        {
            // An overshoot beyond the tolerance raises the ceiling while the rate error says bits are overspent.
            int percentError = (int)((this.rollingActualBits - (long)this.rollingTargetBits) * 100 / this.rollingTargetBits);
            if (percentError >= OverShootPercentage && this.rateErrorEstimate < 0)
            {
                this.extendMaximumQ += 1;
            }

            this.extendMinimumQ -= 1;
        }
        else if (projectedFrameSize > 2 * this.baseFrameTarget && projectedFrameSize > 2 * this.averageFrameBandwidth)
        {
            // An extreme local overshoot raises the ceiling.
            ++this.extendMaximumQ;
        }
        else if (this.rollingTargetBits > this.rollingActualBits)
        {
            // Unwinds the extreme overshoot raise. With tolerances below 100% this branch sees equal running bits
            // and never runs.
            --this.extendMaximumQ;
        }

        this.extendMinimumQ = Math.Clamp(this.extendMinimumQ, -minimumQAdjustmentLimit, minimumQAdjustmentLimit);
        this.extendMaximumQ = Math.Clamp(this.extendMaximumQ, 0, maximumQAdjustmentLimit);

        // An ordinary frame with an unexpectedly good prediction quickly returns the bits that it did not use.
        // At high bit depth sharpness 3, a sequence that uses too many bits clears the quick bits instead.
        // Reference: the vbr_bits_off_target_fast update of av1_twopass_postencode_update().
        Av1FrameUpdateType updateType = this.group.UpdateTypes[this.groupFrameIndex];
        if (updateType is not (Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate))
        {
            if (this.UsesHighBitDepthSharpness && this.variableBitrateBitsOffTarget < 0)
            {
                this.variableBitrateBitsOffTargetFast = 0;
            }
            else
            {
                int fastExtraThreshold = this.baseFrameTarget / HighUndershootRatio;
                if (projectedFrameSize < fastExtraThreshold)
                {
                    this.variableBitrateBitsOffTargetFast += fastExtraThreshold - projectedFrameSize;
                    this.variableBitrateBitsOffTargetFast = Math.Min(this.variableBitrateBitsOffTargetFast, 4L * this.averageFrameBandwidth);
                }
            }
        }
    }

    /// <summary>
    /// Returns the quantizer index whose expected bits per macroblock lie closest to the frame target, choosing between
    /// the first index at or below the target and the one before it. Reference: av1_rc_regulate_q() with
    /// find_closest_qindex_by_rate(), outside constant-bitrate coding without statistics.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="groupIndex">The frame's index in the group, which selects the correction factor.</param>
    /// <param name="bestQIndex">The lowest quantizer index.</param>
    /// <param name="worstQIndex">The highest quantizer index.</param>
    /// <returns>The quantizer index.</returns>
    private int RegulateQuantizer(bool keyFrame, int groupIndex, int bestQIndex, int worstQIndex)
    {
        double correctionFactor = this.GetRateCorrectionFactor(groupIndex);
        int desired = (int)(((ulong)this.thisFrameTarget << BitsPerMacroblockShift) / (ulong)this.macroblockCount);
        int low = bestQIndex;
        int high = worstQIndex;
        while (low < high)
        {
            int middle = (low + high) >> 1;
            if (this.GetBitsPerMacroblock(keyFrame, middle, correctionFactor) > desired)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        int currentQ = low;
        int currentBits = this.GetBitsPerMacroblock(keyFrame, currentQ, correctionFactor);
        int currentDifference = currentBits <= desired ? desired - currentBits : int.MaxValue;
        int previousDifference = currentDifference == int.MaxValue || currentQ == bestQIndex
            ? int.MaxValue
            : this.GetBitsPerMacroblock(keyFrame, currentQ - 1, correctionFactor) - desired;

        return currentDifference <= previousDifference ? currentQ : currentQ - 1;
    }
}
