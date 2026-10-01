// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The one-pass constant-bitrate rate control of real-time usage. libavif codes a real-time sequence at libaom's
/// default rate (256 kbps, 1000 ms buffer, 600 ms starting and optimal level, 50% under- and overshoot) and lets the
/// quantizer move four steps either side of the requested one. Reference: RATE_CONTROL and PRIMARY_RATE_CONTROL,
/// with the AOM_USAGE_REALTIME defaults of the encoder configuration.
/// </summary>
internal sealed class Av1RateControl
{
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
    /// The key frame boost of real-time coding. Reference: DEFAULT_KF_BOOST_RT.
    /// </summary>
    private const int DefaultKeyFrameBoost = 2300;

    /// <summary>
    /// Reference: kf_low_rtc.
    /// </summary>
    private const int KeyFrameLowBoost = 400;

    /// <summary>
    /// Reference: kf_high_rtc.
    /// </summary>
    private const int KeyFrameHighBoost = 5000;

    /// <summary>
    /// The target rate in bits per second. Reference: the rc_target_bitrate default of AOM_USAGE_REALTIME.
    /// </summary>
    private const long TargetBandwidth = 256 * 1000;

    /// <summary>
    /// Reference: the rc_undershoot_pct default of AOM_USAGE_REALTIME.
    /// </summary>
    private const int UnderShootPercentage = 50;

    /// <summary>
    /// Reference: the rc_overshoot_pct default of AOM_USAGE_REALTIME.
    /// </summary>
    private const int OverShootPercentage = 50;

    private readonly Av1BitDepth bitDepth;
    private readonly int width;
    private readonly int height;
    private readonly int macroblockCount;
    private readonly int bestQuality;
    private readonly int worstQuality;
    private readonly bool accurateBitEstimate;
    private readonly double framerate;
    private readonly long startingBufferLevel;
    private readonly long optimalBufferLevel;
    private readonly long maximumBufferSize;
    private readonly int keyFrameMaximumDistance;
    private readonly Av1CyclicRefresh? cyclicRefresh;
    private int averageFrameBandwidth;
    private int maximumFrameBandwidth;
    private int previousAverageFrameBandwidth;
    private long bufferLevel;
    private long bitsOffTarget;
    private int averageKeyFrameQIndex;
    private int averageInterFrameQIndex;
    private int lastBoostedQIndex;
    private double keyFrameCorrectionFactor;
    private double interFrameCorrectionFactor;
    private int firstFrameQIndex;
    private int secondFrameQIndex;
    private int firstFrameRateSign;
    private int secondFrameRateSign;
    private int framesSinceKey;
    private int framesToKey;
    private int keyFrameBoost;
    private bool thisKeyFrameForced;
    private int thisFrameTarget;
    private int bitEstimateRatio;
    private ulong reconstructionError;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1RateControl"/> class. Reference: av1_rc_init(),
    /// av1_primary_rc_init(), set_rc_buffer_sizes() and av1_rc_update_framerate().
    /// </summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="speed">The cpu-used tier.</param>
    /// <param name="bestAllowedQIndex">The lowest quantizer index the frames may use. Reference: best_allowed_q.</param>
    /// <param name="worstAllowedQIndex">The highest quantizer index the frames may use. Reference: worst_allowed_q.</param>
    /// <param name="keyFrameMaximumDistance">The largest number of frames between key frames. Reference: kf_max_dist.</param>
    /// <param name="usesAdaptiveQuantization">Whether the sequence uses any adaptive quantization mode.</param>
    /// <param name="cyclicRefresh">The cyclic refresh of the sequence, or <see langword="null"/> without it.</param>
    public Av1RateControl(
        int width,
        int height,
        Av1BitDepth bitDepth,
        HeifEncodingSpeed speed,
        int bestAllowedQIndex,
        int worstAllowedQIndex,
        int keyFrameMaximumDistance,
        bool usesAdaptiveQuantization,
        Av1CyclicRefresh? cyclicRefresh)
    {
        this.cyclicRefresh = cyclicRefresh;
        this.keyFrameMaximumDistance = keyFrameMaximumDistance;
        this.width = width;
        this.height = height;
        this.bitDepth = bitDepth;
        this.macroblockCount = GetMacroblockCount(width, height);
        this.bestQuality = bestAllowedQIndex;
        this.worstQuality = worstAllowedQIndex;

        // Speed 7 estimates inter frame bits from the error against the last reconstruction, for 8-bit frames from
        // 360 to 720 lines without adaptive quantization. Lossless coding turns it off. Reference: the speed 7
        // accurate_bit_estimate setting of the 360p-or-larger branch of set_rt_speed_feature_framesize_dependent(),
        // with is_lossless_requested() in set_rt_speed_features().
        int shortSide = Math.Min(width, height);
        this.accurateBitEstimate = speed == HeifEncodingSpeed.Level7 &&
            shortSide >= 360 &&
            shortSide <= 720 &&
            bitDepth == Av1BitDepth.EightBit &&
            worstAllowedQIndex != 0 &&
            !usesAdaptiveQuantization;

        // libavif gives every frame time stamp 0 and duration 1 in libaom's default 1/30 time base, so
        // adjust_frame_rate() sees one constant duration of 333333 ten-megahertz ticks. Reference: the
        // aom_codec_encode() call of aomCodecEncodeImage(), with timebase_units_to_ticks().
        this.framerate = 10000000.0 / 333333;

        this.startingBufferLevel = 600 * TargetBandwidth / 1000;
        this.optimalBufferLevel = 600 * TargetBandwidth / 1000;
        this.maximumBufferSize = 1000 * TargetBandwidth / 1000;
        this.bufferLevel = this.startingBufferLevel;
        this.bitsOffTarget = this.startingBufferLevel;
        this.averageKeyFrameQIndex = worstAllowedQIndex;
        this.averageInterFrameQIndex = worstAllowedQIndex;
        this.keyFrameCorrectionFactor = 1.0;
        this.interFrameCorrectionFactor = 0.7;
        this.framesSinceKey = 8;
        this.reconstructionError = ulong.MaxValue;
        this.UpdateFramerate();
    }

    /// <summary>
    /// Gets the running average quantizer index of inter frames. Reference: avg_frame_qindex[INTER_FRAME].
    /// </summary>
    public int AverageInterFrameQIndex => this.averageInterFrameQIndex;

    /// <summary>
    /// Gets the coded sample bit depth.
    /// </summary>
    public Av1BitDepth BitDepth => this.bitDepth;

    /// <summary>
    /// Gets the lowest allowed quantizer index. Reference: rc->best_quality.
    /// </summary>
    public int BestQuality => this.bestQuality;

    /// <summary>
    /// Gets a value indicating whether every allowed quantizer is lossless. Reference: is_lossless_requested().
    /// </summary>
    public bool IsLosslessRequested => this.bestQuality == 0 && this.worstQuality == 0;

    /// <summary>
    /// Gets the bits per frame at the target rate. Reference: rc->avg_frame_bandwidth.
    /// </summary>
    public int AverageFrameBandwidth => this.averageFrameBandwidth;

    /// <summary>
    /// Gets the target rate of the current frame per 64x64 area. Reference: rc->sb64_target_rate.
    /// </summary>
    public int SuperblockTargetRate => GetSuperblockTargetRate(this.thisFrameTarget, this.width, this.height);

    /// <summary>
    /// Gets a value indicating whether the key frame interval places a key frame on the next frame. Automatic key
    /// frames are on, because the smallest key frame distance (0) differs from the largest. Reference: the auto_key
    /// test of set_key_frame().
    /// </summary>
    public bool IsKeyFrameDue => this.framesToKey == 0;

    /// <summary>
    /// Gets the frames left before the next key frame. Reference: rc->frames_to_key.
    /// </summary>
    public int FramesToKey => this.framesToKey;

    /// <summary>
    /// Sets the frame rate dependent limits. Reference: av1_rc_update_framerate(), with the 2000 vbrmax_section of
    /// AOM_USAGE_REALTIME. Only the variable-bitrate clamp reads min_frame_bandwidth.
    /// </summary>
    private void UpdateFramerate()
    {
        this.averageFrameBandwidth = (int)Math.Round(TargetBandwidth / this.framerate, MidpointRounding.AwayFromZero);
        long maximumSectionBits = Math.Min((long)this.averageFrameBandwidth * 2000 / 100, int.MaxValue);
        this.maximumFrameBandwidth = Math.Max(Math.Max(this.macroblockCount * MaximumMacroblockRate, MaximumRate1080P), (int)maximumSectionBits);
    }

    /// <summary>
    /// Sets the frame type state and the bit target of a frame. Reference: the frame type and target size parts of
    /// av1_get_one_pass_rt_params(), with av1_rc_set_frame_target().
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="frameNumber">The number of frames coded before this one. Reference: current_frame.frame_number.</param>
    public void BeginFrame(bool keyFrame, uint frameNumber)
    {
        if (keyFrame)
        {
            this.thisKeyFrameForced = frameNumber != 0 && this.framesToKey == 0;
            this.framesToKey = this.keyFrameMaximumDistance;
            this.keyFrameBoost = DefaultKeyFrameBoost;
        }

        this.thisFrameTarget = keyFrame ? this.GetIntraFrameTarget(frameNumber) : this.GetInterFrameTarget();
    }

    /// <summary>
    /// Returns the bit target of a key frame. Reference: av1_calc_iframe_target_size_one_pass_cbr() and
    /// clamp_iframe_target_size(), with a zero max_intra_bitrate_pct.
    /// </summary>
    /// <param name="frameNumber">The number of frames coded before this one.</param>
    /// <returns>The target in bits.</returns>
    private int GetIntraFrameTarget(uint frameNumber)
    {
        long target;
        if (frameNumber == 0)
        {
            target = this.startingBufferLevel / 2 > int.MaxValue ? int.MaxValue : this.startingBufferLevel / 2;
        }
        else
        {
            int boost = Math.Max(32, (int)Math.Round((2 * this.framerate) - 16, MidpointRounding.AwayFromZero));
            if (this.framesSinceKey < this.framerate / 2)
            {
                boost = (int)(boost * this.framesSinceKey / (this.framerate / 2));
            }

            target = ((long)(16 + boost) * this.averageFrameBandwidth) >> 4;
        }

        return (int)Math.Min(target, this.maximumFrameBandwidth);
    }

    /// <summary>
    /// Returns the bit target of an inter frame, moved toward the optimal buffer level. Reference:
    /// av1_calc_pframe_target_size_one_pass_cbr(), with a zero gf_cbr_boost_pct and max_inter_bitrate_pct.
    /// </summary>
    /// <returns>The target in bits.</returns>
    private int GetInterFrameTarget()
    {
        long difference = this.optimalBufferLevel - this.bufferLevel;
        long onePercentBits = 1 + (this.optimalBufferLevel / 100);
        int minimumFrameTarget = Math.Max(this.averageFrameBandwidth >> 4, FrameOverheadBits);
        long target = this.averageFrameBandwidth;
        if (difference > 0)
        {
            int percentLow = (int)Math.Min(difference / onePercentBits, UnderShootPercentage);
            target -= target * percentLow / 200;
        }
        else if (difference < 0)
        {
            int percentHigh = (int)Math.Min(-difference / onePercentBits, OverShootPercentage);
            target += target * percentHigh / 200;
        }

        return Math.Max(minimumFrameTarget, (int)Math.Min(target, int.MaxValue));
    }

    /// <summary>
    /// Picks the quantizer index of a frame from the buffer state and the bits it expects each quantizer to cost.
    /// Reference: the constant-bitrate branch of av1_rc_pick_q_and_bounds(), with rc_pick_q_and_bounds_no_stats_cbr().
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <typeparam name="TMotion">The error operations.</typeparam>
    /// <typeparam name="TBlock">The block averaging operations.</typeparam>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="frameNumber">The number of frames coded before this one.</param>
    /// <param name="screenContent">Whether the frame is screen content. Reference: is_screen_content_type.</param>
    /// <param name="sourceSad">The scene statistics of this frame.</param>
    /// <param name="source">The bordered source luma plane.</param>
    /// <param name="lastReconstruction">The bordered luma plane of the LAST reference, for an inter frame.</param>
    /// <returns>The quantizer index.</returns>
    public int PickQuantizer<TSample, TMotion, TBlock>(
        bool keyFrame,
        uint frameNumber,
        bool screenContent,
        in SourceSadStatistics sourceSad,
        Av1PlaneRegion<TSample> source,
        Av1PlaneRegion<TSample> lastReconstruction)
        where TSample : unmanaged
        where TMotion : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
        where TBlock : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
    {
        this.reconstructionError = ulong.MaxValue;
        if (this.accurateBitEstimate && !keyFrame)
        {
            this.MeasureReconstructionError<TSample, TMotion, TBlock>(source, lastReconstruction);
        }

        int activeWorstQuality = this.GetActiveWorstQuality(keyFrame, frameNumber);
        int activeBestQuality = this.GetActiveBestQuality(keyFrame, frameNumber, activeWorstQuality);
        activeBestQuality = Av1Math.Clamp(activeBestQuality, this.bestQuality, this.worstQuality);
        activeWorstQuality = Av1Math.Clamp(activeWorstQuality, activeBestQuality, this.worstQuality);
        int topIndex = activeWorstQuality;
        int bottomIndex = activeBestQuality;

        // Limit the range of a later key frame that the interval did not force.
        if (keyFrame && !this.thisKeyFrameForced && frameNumber != 0)
        {
            topIndex = activeWorstQuality + this.GetQDeltaByRate(keyFrame, screenContent, activeWorstQuality, 2.0);
            topIndex = Math.Max(topIndex, bottomIndex);
        }

        int q = this.RegulateQuantizer(keyFrame, screenContent, in sourceSad, activeBestQuality, activeWorstQuality);
        if (q > topIndex)
        {
            // Targeting the largest allowed frame keeps the chosen quantizer.
            if (this.thisFrameTarget >= this.maximumFrameBandwidth)
            {
                topIndex = q;
            }
            else
            {
                q = topIndex;
            }
        }

        return q;
    }

    /// <summary>
    /// Measures the squared error between the 4x4-averaged source and the LAST reconstruction over 64x64 blocks,
    /// including the replicated border that edge blocks reach. Reference: rc_compute_variance_onepass_rt().
    /// </summary>
    /// <typeparam name="TSample">The sample storage type.</typeparam>
    /// <typeparam name="TMotion">The error operations.</typeparam>
    /// <typeparam name="TBlock">The block averaging operations.</typeparam>
    /// <param name="source">The bordered source luma plane.</param>
    /// <param name="lastReconstruction">The bordered LAST reconstruction luma plane.</param>
    private void MeasureReconstructionError<TSample, TMotion, TBlock>(Av1PlaneRegion<TSample> source, Av1PlaneRegion<TSample> lastReconstruction)
        where TSample : unmanaged
        where TMotion : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
        where TBlock : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
    {
        int modeInfoColumns = Av1Math.AlignPowerOf2(this.width, 3) >> Av1Constants.ModeInfoSizeLog2;
        int modeInfoRows = Av1Math.AlignPowerOf2(this.height, 3) >> Av1Constants.ModeInfoSizeLog2;
        int columns = (modeInfoColumns + 15) / 16;
        int rows = (modeInfoRows + 15) / 16;
        Span<TSample> averaged = stackalloc TSample[64 * 64];

        // Edge blocks reach into the replicated border, so the averages read the complete bordered buffer.
        Av1PlaneRegion<TSample> bordered = source.GetFullPlane();
        Point sourceOffset = source.Bounds.Location;
        ulong total = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Point origin = new(column << 6, row << 6);
                for (int y = 0; y < 64; y += 4)
                {
                    for (int x = 0; x < 64; x += 4)
                    {
                        TSample average = TBlock.CreateSample(TBlock.GetAverage4x4(bordered, new Point(sourceOffset.X + origin.X + x, sourceOffset.Y + origin.Y + y)));
                        for (int m = 0; m < 4; m++)
                        {
                            averaged.Slice(((y + m) * 64) + x, 4).Fill(average);
                        }
                    }
                }

                TMotion.GetMoments(
                    averaged,
                    64,
                    Av1TransformBlockEncoder.GetPlaneSpan(lastReconstruction, origin),
                    lastReconstruction.Stride,
                    64,
                    64,
                    out _,
                    out long squares);

                total += (ulong)squares;
            }
        }

        this.reconstructionError = total > 0 ? total : 1;
    }

    /// <summary>
    /// Returns the highest quantizer the frame may use from the buffer fullness. Reference:
    /// calc_active_worst_quality_no_stats_cbr(), without spatial variance, cyclic refresh or layers.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="frameNumber">The number of frames coded before this one.</param>
    /// <returns>The active worst quality.</returns>
    private int GetActiveWorstQuality(bool keyFrame, uint frameNumber)
    {
        if (keyFrame)
        {
            return this.worstQuality;
        }

        // The first frames after a key frame weigh the key frame quantizer into the ambient quantizer.
        const uint keyWeightFrames = 5;
        long criticalLevel = this.optimalBufferLevel >> 3;
        int ambientQuantizer = frameNumber < keyWeightFrames
            ? Math.Min(this.averageInterFrameQIndex, this.averageKeyFrameQIndex)
            : this.averageInterFrameQIndex;

        ambientQuantizer = Math.Min(this.worstQuality, ambientQuantizer);

        int activeWorstQuality;
        if (this.bufferLevel > this.optimalBufferLevel)
        {
            // A buffer above the optimal level lowers the quantizer with its fullness.
            activeWorstQuality = Math.Min(this.worstQuality, ambientQuantizer * 5 / 4);
            int maximumAdjustmentDown = activeWorstQuality / 3;
            if (maximumAdjustmentDown != 0)
            {
                long bufferStep = (this.maximumBufferSize - this.optimalBufferLevel) / maximumAdjustmentDown;
                if (bufferStep != 0)
                {
                    activeWorstQuality -= (int)((this.bufferLevel - this.optimalBufferLevel) / bufferStep);
                }
            }
        }
        else if (this.bufferLevel > criticalLevel)
        {
            // A buffer between the critical and the optimal level raises the quantizer from the ambient one.
            activeWorstQuality = Math.Min(this.worstQuality, ambientQuantizer);
            if (criticalLevel != 0)
            {
                long bufferStep = this.optimalBufferLevel - criticalLevel;
                if (bufferStep != 0)
                {
                    activeWorstQuality += (int)((this.worstQuality - ambientQuantizer) * (this.optimalBufferLevel - this.bufferLevel) / bufferStep);
                }
            }
        }
        else
        {
            activeWorstQuality = this.worstQuality;
        }

        return activeWorstQuality;
    }

    /// <summary>
    /// Returns the lowest quantizer the frame may use. Reference: calc_active_best_quality_no_stats_cbr() in
    /// real-time mode, with a zero gf_cbr_boost_pct.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="frameNumber">The number of frames coded before this one.</param>
    /// <param name="activeWorstQuality">The active worst quality.</param>
    /// <returns>The active best quality.</returns>
    private int GetActiveBestQuality(bool keyFrame, uint frameNumber, int activeWorstQuality)
    {
        int activeBestQuality = this.bestQuality;
        if (keyFrame)
        {
            if (this.thisKeyFrameForced)
            {
                // A forced key frame keeps the quantizer near the last boosted one to limit popping.
                double lastBoostedQ = ConvertQIndexToQ(this.lastBoostedQIndex, this.bitDepth);
                int deltaQIndex = this.ComputeQDelta(lastBoostedQ, lastBoostedQ * 0.75);
                activeBestQuality = Math.Max(this.lastBoostedQIndex + deltaQIndex, this.bestQuality);
            }
            else if (frameNumber > 0)
            {
                double adjustmentFactor = 1.0;
                activeBestQuality = this.GetKeyFrameActiveQuality(this.averageKeyFrameQIndex);

                // Small formats allow a somewhat lower key frame quantizer.
                if (this.width * this.height <= 352 * 288)
                {
                    adjustmentFactor -= 0.25;
                }

                double q = ConvertQIndexToQ(activeBestQuality, this.bitDepth);
                activeBestQuality += this.ComputeQDelta(q, q * adjustmentFactor);
            }
        }
        else
        {
            // The lower of the active worst quality and the recent average sets the lowest quantizer.
            int average = frameNumber > 1 ? this.averageInterFrameQIndex : this.averageKeyFrameQIndex;
            activeBestQuality = average < activeWorstQuality
                ? GetRealtimeMinimumQuality(average, this.bitDepth)
                : GetRealtimeMinimumQuality(activeWorstQuality, this.bitDepth);
        }

        return activeBestQuality;
    }

    /// <summary>
    /// Returns the quantizer index of a key frame in one-pass constant-quality coding without lookahead: the key
    /// frame floor of the good-quality tables at the default boost, lowered for small formats. Reference:
    /// get_intra_q_and_bounds() for a key frame the interval did not force, with rc_pick_q_and_bounds_q_mode(),
    /// DEFAULT_KF_BOOST and the cq_level active_worst_quality of av1_get_second_pass_params().
    /// </summary>
    /// <param name="cqLevel">The constant-quality index. Reference: cq_level.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="screenContent">Whether the frame is screen content. Reference: is_screen_content_type.</param>
    /// <param name="bestAllowedQIndex">The lowest allowed quantizer index. Reference: best_allowed_q.</param>
    /// <param name="worstAllowedQIndex">The highest allowed quantizer index. Reference: worst_allowed_q.</param>
    /// <returns>The key frame quantizer index.</returns>
    public static int GetConstantQualityKeyFrameQIndex(
        int cqLevel,
        int width,
        int height,
        Av1BitDepth bitDepth,
        bool screenContent,
        int bestAllowedQIndex,
        int worstAllowedQIndex)
    {
        const int defaultKeyFrameBoost = 2300;
        const int keyFrameLow = 553;
        const int keyFrameHigh = 8000;

        // Good-quality tables of the 608-line and larger class, else of the smaller class. Reference: x1[0] with
        // the res_idx > 1 selection of ASSIGN_MINQ_TABLE_2().
        bool large = Math.Min(width, height) >= 608;
        double maximumQ = ConvertQIndexToQ(cqLevel, bitDepth);
        int lowMotion = GetMinimumQIndex(maximumQ, 0.000001, -0.0004, large ? 0.1917 : 0.1771, bitDepth);
        int highMotion = GetMinimumQIndex(maximumQ, 0.0000021, -0.00125, large ? 0.3760 : 0.379, bitDepth);
        int activeBestQuality = GetActiveQuality(defaultKeyFrameBoost, keyFrameLow, keyFrameHigh, lowMotion, highMotion);
        if (screenContent)
        {
            activeBestQuality /= 2;
        }

        // Small formats allow a somewhat lower key frame quantizer.
        double adjustmentFactor = width * height <= 352 * 288 ? 0.75 : 1.0;
        double q = ConvertQIndexToQ(activeBestQuality, bitDepth);
        activeBestQuality += FindQIndex(q * adjustmentFactor, bitDepth, bestAllowedQIndex, worstAllowedQIndex) -
            FindQIndex(q, bitDepth, bestAllowedQIndex, worstAllowedQIndex);

        if (cqLevel > 0)
        {
            activeBestQuality = Math.Max(1, activeBestQuality);
        }

        return Av1Math.Clamp(activeBestQuality, bestAllowedQIndex, worstAllowedQIndex);
    }

    /// <summary>
    /// Returns the quantizer index of a key frame that the key frame interval placed, in one-pass constant-quality
    /// coding without lookahead. It stays near the last boosted quantizer to limit a quality jump: half its real
    /// quantizer, and no lower than the best allowed. Reference: the this_key_frame_forced branch of
    /// get_intra_q_and_bounds() without first-pass statistics, with rc_pick_q_and_bounds_q_mode().
    /// </summary>
    /// <param name="cqLevel">The constant-quality index. Reference: cq_level.</param>
    /// <param name="lastBoostedQIndex">The last boosted quantizer index. Reference: last_boosted_qindex.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="bestAllowedQIndex">The lowest allowed quantizer index. Reference: best_allowed_q.</param>
    /// <param name="worstAllowedQIndex">The highest allowed quantizer index. Reference: worst_allowed_q.</param>
    /// <returns>The key frame quantizer index.</returns>
    public static int GetConstantQualityForcedKeyFrameQIndex(
        int cqLevel,
        int lastBoostedQIndex,
        Av1BitDepth bitDepth,
        int bestAllowedQIndex,
        int worstAllowedQIndex)
    {
        double lastBoostedQ = ConvertQIndexToQ(lastBoostedQIndex, bitDepth);
        int deltaQIndex = FindQIndex(lastBoostedQ * 0.5, bitDepth, bestAllowedQIndex, worstAllowedQIndex) -
            FindQIndex(lastBoostedQ, bitDepth, bestAllowedQIndex, worstAllowedQIndex);

        int activeBestQuality = Math.Max(lastBoostedQIndex + deltaQIndex, bestAllowedQIndex);
        if (cqLevel > 0)
        {
            activeBestQuality = Math.Max(1, activeBestQuality);
        }

        return Av1Math.Clamp(activeBestQuality, bestAllowedQIndex, worstAllowedQIndex);
    }

    /// <summary>
    /// Returns the quantizer index of a golden or alternate-reference update in one-pass constant-quality coding
    /// without lookahead. The base is the constant-quality index, or the running average of the ordinary inter
    /// frames when that is lower after the first inter frame; the result is the high motion floor of the
    /// good-quality golden-frame table, because the one-pass group definition leaves arf_boost_factor at zero.
    /// Reference: get_active_best_quality() for a frame that is not a leaf, with rc_pick_q_and_bounds_q_mode() and
    /// the cq_level active_worst_quality of av1_get_second_pass_params().
    /// </summary>
    /// <param name="cqLevel">The constant-quality index. Reference: cq_level.</param>
    /// <param name="averageInterQIndex">
    /// The running average quantizer of the ordinary inter frames. Reference: avg_frame_qindex[INTER_FRAME].
    /// </param>
    /// <param name="framesSinceKey">The number of frames since the last key frame. Reference: frames_since_key.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="bestAllowedQIndex">The lowest allowed quantizer index. Reference: best_allowed_q.</param>
    /// <param name="worstAllowedQIndex">The highest allowed quantizer index. Reference: worst_allowed_q.</param>
    /// <returns>The golden frame quantizer index.</returns>
    public static int GetConstantQualityGoldenFrameQIndex(
        int cqLevel,
        int averageInterQIndex,
        int framesSinceKey,
        int width,
        int height,
        Av1BitDepth bitDepth,
        int bestAllowedQIndex,
        int worstAllowedQIndex)
    {
        // The active worst quality of constant-quality coding is the constant-quality index. The lower recent
        // average applies once the frame follows more than one frame after the key frame.
        int q = framesSinceKey > 1 && averageInterQIndex < cqLevel ? averageInterQIndex : cqLevel;

        // min_boost - (int)(boost * arf_boost_factor) keeps the high motion floor of the good-quality tables of
        // the 608-line and larger class, else of the smaller class. Reference: get_gf_high_motion_quality() with
        // x1[0] and the res_idx > 1 selection of ASSIGN_MINQ_TABLE_2().
        bool large = Math.Min(width, height) >= 608;
        int activeBestQuality = GetMinimumQIndex(
            ConvertQIndexToQ(q, bitDepth), 0.0000021, -0.00125, large ? 0.6916 : 0.6634, bitDepth);

        if (cqLevel > 0)
        {
            activeBestQuality = Math.Max(1, activeBestQuality);
        }

        return Av1Math.Clamp(activeBestQuality, bestAllowedQIndex, worstAllowedQIndex);
    }

    /// <summary>
    /// Returns the key frame quantizer floor between the low and high motion curves by the key frame boost.
    /// Reference: get_kf_active_quality() in real-time mode.
    /// </summary>
    /// <param name="q">The quantizer index the floor applies to.</param>
    /// <returns>The key frame active quality.</returns>
    private int GetKeyFrameActiveQuality(int q)
    {
        // Real-time mode uses the same curve coefficients for every resolution. Reference: x1[1] with
        // init_minq_luts().
        double maximumQ = ConvertQIndexToQ(q, this.bitDepth);
        int lowMotion = GetMinimumQIndex(maximumQ, 0.000001, -0.0004, 0.15, this.bitDepth);
        int highMotion = GetMinimumQIndex(maximumQ, 0.0000021, -0.00125, 0.45, this.bitDepth);
        return GetActiveQuality(this.keyFrameBoost, KeyFrameLowBoost, KeyFrameHighBoost, lowMotion, highMotion);
    }

    /// <summary>
    /// Interpolates between a low and a high motion quantizer floor by a boost. Reference: get_active_quality().
    /// </summary>
    /// <param name="boost">The boost.</param>
    /// <param name="low">The boost at and below which the high motion floor applies.</param>
    /// <param name="high">The boost at and above which the low motion floor applies.</param>
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
        return lowMotion + (((offset * difference) + (gap >> 1)) / gap);
    }

    /// <summary>
    /// Returns the real-time quantizer floor of a quantizer index. Reference: rtc_minq from init_minq_luts().
    /// </summary>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The floor.</returns>
    private static int GetRealtimeMinimumQuality(int qIndex, Av1BitDepth bitDepth)
        => GetMinimumQIndex(ConvertQIndexToQ(qIndex, bitDepth), 0.00000271, -0.00113, 0.70, bitDepth);

    /// <summary>
    /// Returns the quantizer index of a third-order polynomial of the real quantizer. Reference: get_minq_index().
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

        return FindQIndex(target, bitDepth, 0, Av1Constants.MaxQ);
    }

    /// <summary>
    /// Returns the quantizer index whose expected rate is closest to the frame target, then limits its change from
    /// the preceding frames. Reference: av1_rc_regulate_q().
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="sourceSad">The scene statistics of this frame.</param>
    /// <param name="activeBestQuality">The lowest quantizer.</param>
    /// <param name="activeWorstQuality">The highest quantizer.</param>
    /// <returns>The quantizer index.</returns>
    private int RegulateQuantizer(bool keyFrame, bool screenContent, in SourceSadStatistics sourceSad, int activeBestQuality, int activeWorstQuality)
    {
        double correctionFactor = this.GetRateCorrectionFactor(keyFrame);
        int targetBitsPerMacroblock = (int)(((ulong)this.thisFrameTarget << BitsPerMacroblockShift) / (ulong)this.macroblockCount);
        int q = this.FindClosestQIndexByRate(keyFrame, screenContent, targetBitsPerMacroblock, correctionFactor, activeBestQuality, activeWorstQuality);
        return this.AdjustConstantBitrateQuantizer(keyFrame, screenContent, in sourceSad, q);
    }

    /// <summary>
    /// Returns the quantizer index whose expected bits per macroblock lie closest to the desired rate, choosing
    /// between the first index at or below the rate and the one before it. Reference: find_closest_qindex_by_rate().
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="desiredBitsPerMacroblock">The desired bits per macroblock.</param>
    /// <param name="correctionFactor">The rate correction factor.</param>
    /// <param name="bestQIndex">The lowest quantizer index.</param>
    /// <param name="worstQIndex">The highest quantizer index.</param>
    /// <returns>The quantizer index.</returns>
    private int FindClosestQIndexByRate(
        bool keyFrame,
        bool screenContent,
        int desiredBitsPerMacroblock,
        double correctionFactor,
        int bestQIndex,
        int worstQIndex)
    {
        int low = bestQIndex;
        int high = worstQIndex;
        while (low < high)
        {
            int middle = (low + high) >> 1;
            if (this.GetSearchBitsPerMacroblock(keyFrame, screenContent, middle, correctionFactor) > desiredBitsPerMacroblock)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        int currentQ = low;
        int currentBits = this.GetSearchBitsPerMacroblock(keyFrame, screenContent, currentQ, correctionFactor);
        int currentDifference = currentBits <= desiredBitsPerMacroblock ? desiredBitsPerMacroblock - currentBits : int.MaxValue;
        int previousDifference;
        if (currentDifference == int.MaxValue || currentQ == bestQIndex)
        {
            previousDifference = int.MaxValue;
        }
        else
        {
            previousDifference = this.GetSearchBitsPerMacroblock(keyFrame, screenContent, currentQ - 1, correctionFactor) -
                desiredBitsPerMacroblock;
        }

        return currentDifference <= previousDifference ? currentQ : currentQ - 1;
    }

    /// <summary>
    /// Returns the expected bits per macroblock that the quantizer search compares. When cyclic refresh refreshes
    /// the frame, its expected boosted share codes at the lower quantizer of the first boosted segment. Reference:
    /// get_bits_per_mb() with av1_cyclic_refresh_rc_bits_per_mb().
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="correctionFactor">The rate correction factor.</param>
    /// <returns>The bits per macroblock.</returns>
    private int GetSearchBitsPerMacroblock(bool keyFrame, bool screenContent, int qIndex, double correctionFactor)
    {
        if (this.cyclicRefresh is not { Apply: true } cyclicRefresh)
        {
            return this.GetBitsPerMacroblock(keyFrame, screenContent, qIndex, correctionFactor, this.accurateBitEstimate);
        }

        double weight = cyclicRefresh.GetExpectedSegmentWeight(this.macroblockCount);
        int qIndexDelta = cyclicRefresh.GetExpectedQDelta(this, keyFrame, screenContent, qIndex);
        return (int)Math.Round(
            ((1.0 - weight) * this.GetBitsPerMacroblock(keyFrame, screenContent, qIndex, correctionFactor, this.accurateBitEstimate)) +
            (weight * this.GetBitsPerMacroblock(keyFrame, screenContent, qIndex + qIndexDelta, correctionFactor, this.accurateBitEstimate)),
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Returns the expected bits per macroblock at a quantizer. An inter frame with the accurate estimate derives
    /// the rate constant from the reconstruction error. Reference: av1_rc_bits_per_mb() and
    /// get_bpmb_enumerator(), with a zero max_intra_bitrate_pct.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="correctionFactor">The rate correction factor.</param>
    /// <param name="accurateEstimate">Whether the reconstruction error may set the rate constant.</param>
    /// <returns>The bits per macroblock.</returns>
    private int GetBitsPerMacroblock(bool keyFrame, bool screenContent, int qIndex, double correctionFactor, bool accurateEstimate)
    {
        double q = ConvertQIndexToQ(qIndex, this.bitDepth);
        int enumerator = GetBitsPerMacroblockEnumerator(keyFrame, screenContent);
        if (!keyFrame && accurateEstimate && this.reconstructionError != ulong.MaxValue)
        {
            double errorRoot = (double)((int)Math.Sqrt(this.reconstructionError) << BitsPerMacroblockShift) / this.macroblockCount;
            int ratio = this.bitEstimateRatio == 0 ? (int)(300000 / errorRoot) : this.bitEstimateRatio;

            // A clamped rate constant limits quantizer fluctuations.
            enumerator = Av1Math.Clamp((int)(ratio * errorRoot), 20000, 170000);
        }

        return (int)(enumerator * correctionFactor / q);
    }

    /// <summary>
    /// Returns the rate constant of a frame type. Reference: get_bpmb_enumerator().
    /// </summary>
    /// <param name="keyFrame">Whether the rate is of a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <returns>The rate constant.</returns>
    private static int GetBitsPerMacroblockEnumerator(bool keyFrame, bool screenContent)
        => screenContent ? keyFrame ? 1000000 : 750000 : keyFrame ? 2000000 : 1500000;

    /// <summary>
    /// Limits the quantizer change against the two preceding frames and the scene statistics. Reference:
    /// adjust_q_cbr(), without layers, resizing or reference biasing.
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="sourceSad">The scene statistics of this frame.</param>
    /// <param name="q">The quantizer index the rate model chose.</param>
    /// <returns>The quantizer index.</returns>
    private int AdjustConstantBitrateQuantizer(bool keyFrame, bool screenContent, in SourceSadStatistics sourceSad, int q)
    {
        // A preceding overshoot with a low buffer relaxes the limits on the next increase.
        bool overshootBufferLow = this.firstFrameRateSign == -1 &&
            sourceSad.FrameSad > 1000 &&
            this.bufferLevel < (this.optimalBufferLevel >> 1) &&
            this.framesSinceKey > 4;

        int maximumDeltaUp = overshootBufferLow ? 120 : 20;
        bool bandwidthChanged = Math.Abs(this.averageFrameBandwidth - this.previousAverageFrameBandwidth) > 0.1 * this.averageFrameBandwidth;
        int maximumDeltaDown;
        if (this.cyclicRefresh is { Apply: true } cyclicRefresh)
        {
            // Static screen content limits the decrease until the next refresh cycle starts, and links the
            // increase to the decrease and the buffer.
            maximumDeltaDown = screenContent && cyclicRefresh.CycleAdvanced
                ? Av1Math.Clamp(this.firstFrameQIndex / 32, 1, 8)
                : Av1Math.Clamp(this.firstFrameQIndex / 8, 1, 16);

            if (screenContent)
            {
                if (this.bufferLevel > this.optimalBufferLevel)
                {
                    maximumDeltaUp = Math.Max(4, maximumDeltaDown);
                }
                else if (!overshootBufferLow)
                {
                    maximumDeltaUp = Math.Max(8, maximumDeltaDown);
                }
            }
        }
        else
        {
            maximumDeltaDown = screenContent
                ? Av1Math.Clamp(this.firstFrameQIndex / 16, 1, 8)
                : Av1Math.Clamp(this.firstFrameQIndex / 8, 1, 16);
        }

        if (!keyFrame && this.framesSinceKey > 1 && this.firstFrameQIndex > 0 && this.secondFrameQIndex > 0 && !bandwidthChanged)
        {
            // An overshoot and an undershoot in the two preceding frames clamp the quantizer between theirs.
            if (this.firstFrameRateSign * this.secondFrameRateSign == -1 &&
                this.firstFrameQIndex != this.secondFrameQIndex &&
                !overshootBufferLow)
            {
                int clamped = Av1Math.Clamp(
                    q,
                    Math.Min(this.firstFrameQIndex, this.secondFrameQIndex),
                    Math.Max(this.firstFrameQIndex, this.secondFrameQIndex));

                // After an overshoot, a larger increase is reduced less for a faster reaction.
                q = this.firstFrameRateSign == -1 && q > clamped && this.framesSinceKey > 10
                    ? (q + clamped) >> 1
                    : clamped;
            }

            // Falling content change pushes a high quantizer down while the buffer is stable, and rising change
            // slows a decrease while the buffer is below its maximum.
            if (sourceSad.PreviousAverageSad > 0 && this.framesSinceKey > 10 && sourceSad.FrameSad > 0)
            {
                double delta = ((double)sourceSad.AverageSad / sourceSad.PreviousAverageSad) - 1.0;
                if (delta < 0.0 && this.bufferLevel > (this.optimalBufferLevel >> 2) && q > (this.worstQuality >> 1))
                {
                    double adjustmentFactor = 1.0 + (0.5 * Math.Tanh(4.0 * delta));
                    double qValue = ConvertQIndexToQ(q, this.bitDepth);
                    q += this.ComputeQDelta(qValue, qValue * adjustmentFactor);
                }
                else if (this.firstFrameQIndex - q > 0 &&
                    delta > 0.1 &&
                    this.bufferLevel < Math.Min(this.maximumBufferSize, this.optimalBufferLevel << 1))
                {
                    q = ((3 * q) + this.firstFrameQIndex) >> 2;
                }
            }

            // Limit the decrease and the increase from the preceding frame.
            if (this.firstFrameQIndex - q > maximumDeltaDown)
            {
                q = this.firstFrameQIndex - maximumDeltaDown;
            }
            else if (q - this.firstFrameQIndex > maximumDeltaUp)
            {
                q = this.firstFrameQIndex + maximumDeltaUp;
            }
        }

        return Av1Math.Clamp(q, this.bestQuality, this.worstQuality);
    }

    /// <summary>
    /// Raises the quantizer of a scene change toward the worst quality and resets the state that later frames
    /// read, so that they do not settle at a low quantizer and overshoot again. Reference:
    /// av1_encodedframe_overshoot_cbr(), without spatial variance, layers or screen content tuning.
    /// </summary>
    /// <param name="q">The quantizer index of the frame.</param>
    /// <param name="averageSourceSad">The running average source SAD. Reference: avg_source_sad.</param>
    /// <returns>The raised quantizer index.</returns>
    public int ApplyOvershootQuantizer(int q, ulong averageSourceSad)
    {
        // A scene change restarts the cyclic refresh count. Reference: the counter_encode_maxq_scene_change reset.
        if (this.cyclicRefresh is not null)
        {
            this.cyclicRefresh.SceneChangeFrameCount = 0;
        }

        // An easy scene change in a large frame with a stable buffer uses a lower quantizer.
        const ulong sadThreshold = 64 * 64 * 32;
        if (this.width * this.height >= 1280 * 720 &&
            this.bufferLevel > (this.optimalBufferLevel >> 1) &&
            averageSourceSad < sadThreshold)
        {
            q = (q + this.worstQuality) >> 1;
        }
        else
        {
            q = ((3 * this.worstQuality) + q) >> 2;
        }

        this.averageInterFrameQIndex = q;
        this.bufferLevel = this.optimalBufferLevel;
        this.bitsOffTarget = this.optimalBufferLevel;
        this.firstFrameRateSign = 0;
        this.secondFrameRateSign = 0;

        // Base the correction factor on the target rate at this quantizer, the inverse of the bits estimate. The
        // rate factor level INTER_NORMAL is passed where a frame type is read, which selects the key frame rate
        // constant.
        int targetBitsPerMacroblock = (int)(((ulong)this.averageFrameBandwidth << BitsPerMacroblockShift) / (ulong)this.macroblockCount);
        double qValue = ConvertQIndexToQ(q, this.bitDepth);
        int enumerator = GetBitsPerMacroblockEnumerator(keyFrame: true, screenContent: false);
        double newFactor = targetBitsPerMacroblock * qValue / enumerator;
        if (newFactor > this.interFrameCorrectionFactor)
        {
            this.interFrameCorrectionFactor = Math.Min((newFactor + this.interFrameCorrectionFactor) / 2.0, MaximumBitsPerBlockFactor);
        }

        return q;
    }

    /// <summary>
    /// Updates the rate model, the quantizer averages and the buffer after a frame is coded. Reference:
    /// av1_rc_postencode_update() and update_buffer_level().
    /// </summary>
    /// <param name="frameBytes">The coded size of the frame, without the temporal delimiter.</param>
    /// <param name="qIndex">The quantizer index of the frame.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="refreshesGolden">Whether the frame refreshes the GOLDEN reference.</param>
    /// <param name="constrainedGoldenGroup">
    /// Whether the golden group ends at the next key frame. Reference: p_rc->constrained_gf_group.
    /// </param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="segmentationEnabled">Whether the frame codes segments. Reference: cm->seg.enabled.</param>
    /// <param name="sceneChange">Whether the frame is a scene change. Reference: rc->high_source_sad.</param>
    public void UpdateAfterFrame(
        int frameBytes,
        int qIndex,
        bool keyFrame,
        bool refreshesGolden,
        bool constrainedGoldenGroup,
        bool screenContent,
        bool segmentationEnabled,
        bool sceneChange)
    {
        int projectedFrameSize = frameBytes << 3;
        this.UpdateRateCorrectionFactors(projectedFrameSize, qIndex, keyFrame, screenContent, segmentationEnabled, sceneChange);

        if (!keyFrame && this.accurateBitEstimate)
        {
            double q = ConvertQIndexToQ(qIndex, this.bitDepth);
            int ratio = (int)(projectedFrameSize * q / Math.Sqrt(this.reconstructionError));
            this.bitEstimateRatio = this.bitEstimateRatio == 0 ? ratio : ((7 * this.bitEstimateRatio) + ratio) / 8;
        }

        if (keyFrame)
        {
            this.averageKeyFrameQIndex = ((3 * this.averageKeyFrameQIndex) + qIndex + 2) >> 2;
        }
        else if (!refreshesGolden)
        {
            this.averageInterFrameQIndex = ((3 * this.averageInterFrameQIndex) + qIndex + 2) >> 2;
        }

        // Keep the last boosted quantizer, which a forced key frame reads. A golden refresh in a group that ends at
        // the next key frame does not count. Reference: the last_boosted_qindex update of av1_rc_postencode_update().
        if (qIndex < this.lastBoostedQIndex || keyFrame || (refreshesGolden && !constrainedGoldenGroup))
        {
            this.lastBoostedQIndex = qIndex;
        }

        // A leaky bucket: each shown frame adds the average frame bandwidth and removes its own size.
        this.bitsOffTarget = Math.Min(this.bitsOffTarget + this.averageFrameBandwidth - projectedFrameSize, this.maximumBufferSize);
        this.bufferLevel = this.bitsOffTarget;
        this.previousAverageFrameBandwidth = this.averageFrameBandwidth;
        if (keyFrame)
        {
            this.framesSinceKey = 0;
        }
    }

    /// <summary>
    /// Advances the key frame counters after a shown frame. Reference: update_keyframe_counters().
    /// </summary>
    public void EndFrame()
    {
        if (this.framesToKey != 0)
        {
            this.framesSinceKey++;
            this.framesToKey--;
        }
    }

    /// <summary>
    /// Moves the rate correction factor of the frame type toward the ratio of the coded and the expected size.
    /// Reference: av1_rc_update_rate_correction_factors() in the post-encode stage.
    /// </summary>
    /// <param name="projectedFrameSize">The coded size in bits.</param>
    /// <param name="qIndex">The quantizer index of the frame.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="segmentationEnabled">Whether the frame codes segments. Reference: cm->seg.enabled.</param>
    /// <param name="sceneChange">Whether the frame is a scene change. Reference: rc->high_source_sad.</param>
    private void UpdateRateCorrectionFactors(
        int projectedFrameSize,
        int qIndex,
        bool keyFrame,
        bool screenContent,
        bool segmentationEnabled,
        bool sceneChange)
    {
        Av1CyclicRefresh? cyclicRefresh = segmentationEnabled ? this.cyclicRefresh : null;

        // The overshoot of a scene change already reset the factors, so only the quantizer history restarts. The
        // count is zero exactly on that frame: ApplyOvershootQuantizer cleared it, and a scene change refreshes
        // nothing, so the cyclic refresh setup did not count the frame. Reference: the FAST_DETECTION_MAXQ return of
        // av1_rc_update_rate_correction_factors().
        if (this.cyclicRefresh is { SceneChangeFrameCount: 0 } && sceneChange && !keyFrame)
        {
            this.secondFrameQIndex = qIndex;
            this.firstFrameQIndex = qIndex;
            this.secondFrameRateSign = 0;
            this.firstFrameRateSign = 0;
            return;
        }

        double rateCorrectionFactor = this.GetRateCorrectionFactor(keyFrame);
        double correctionFactor = 1.0;
        int projectedSizeBasedOnQ = cyclicRefresh is null
            ? this.EstimateBitsAtQ(keyFrame, screenContent, qIndex, rateCorrectionFactor)
            : this.EstimateCyclicRefreshBitsAtQ(cyclicRefresh, keyFrame, screenContent, qIndex, rateCorrectionFactor);

        if (projectedSizeBasedOnQ > FrameOverheadBits)
        {
            correctionFactor = (double)projectedFrameSize / projectedSizeBasedOnQ;
        }

        correctionFactor = Math.Max(correctionFactor, 0.25);
        this.secondFrameQIndex = this.firstFrameQIndex;
        this.firstFrameQIndex = qIndex;
        this.secondFrameRateSign = this.firstFrameRateSign;
        this.firstFrameRateSign = correctionFactor > 1.1 ? -1 : correctionFactor < 0.9 ? 1 : 0;

        // Dampen the adjustment.
        double adjustmentLimit = screenContent
            ? 0.25 + (0.5 * Math.Min(0.5, Math.Abs(Math.Log10(correctionFactor))))
            : 0.25 + (0.75 * Math.Min(0.5, Math.Abs(Math.Log10(correctionFactor))));

        // An overshoot or undershoot moves the refresh amount and its quantizer change.
        if (cyclicRefresh is not null && this.thisFrameTarget > 0)
        {
            cyclicRefresh.AdjustForRate(correctionFactor);
        }

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

        rateCorrectionFactor = Math.Clamp(rateCorrectionFactor, MinimumBitsPerBlockFactor, MaximumBitsPerBlockFactor);
        if (keyFrame)
        {
            this.keyFrameCorrectionFactor = rateCorrectionFactor;
        }
        else
        {
            this.interFrameCorrectionFactor = rateCorrectionFactor;
        }
    }

    /// <summary>
    /// Returns the expected frame size at a quantizer. Reference: av1_estimate_bits_at_q().
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="correctionFactor">The rate correction factor.</param>
    /// <returns>The expected size in bits.</returns>
    private int EstimateBitsAtQ(bool keyFrame, bool screenContent, int qIndex, double correctionFactor)
    {
        int bitsPerMacroblock = this.GetBitsPerMacroblock(keyFrame, screenContent, qIndex, correctionFactor, this.accurateBitEstimate);
        return Math.Max(FrameOverheadBits, (int)((ulong)bitsPerMacroblock * (ulong)this.macroblockCount) >> BitsPerMacroblockShift);
    }

    /// <summary>
    /// Returns the expected frame size at a quantizer with the quantizer changes of the boosted segments, weighted by
    /// the units the frame coded in them. Reference: av1_cyclic_refresh_estimate_bits_at_q().
    /// </summary>
    /// <param name="cyclicRefresh">The cyclic refresh of the sequence.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="correctionFactor">The rate correction factor.</param>
    /// <returns>The expected size in bits.</returns>
    private int EstimateCyclicRefreshBitsAtQ(
        Av1CyclicRefresh cyclicRefresh,
        bool keyFrame,
        bool screenContent,
        int qIndex,
        double correctionFactor)
    {
        int unitCount = this.macroblockCount << 4;
        double firstWeight = (double)cyclicRefresh.FirstSegmentBlockCount / unitCount;
        double secondWeight = (double)cyclicRefresh.SecondSegmentBlockCount / unitCount;
        int firstQIndex = qIndex + cyclicRefresh.GetSegmentQDelta(Av1CyclicRefresh.FirstBoostSegment);
        int secondQIndex = qIndex + cyclicRefresh.GetSegmentQDelta(Av1CyclicRefresh.SecondBoostSegment);
        return (int)Math.Round(
            ((1.0 - firstWeight - secondWeight) * this.EstimateBitsAtQ(keyFrame, screenContent, qIndex, correctionFactor)) +
            (firstWeight * this.EstimateBitsAtQ(keyFrame, screenContent, firstQIndex, correctionFactor)) +
            (secondWeight * this.EstimateBitsAtQ(keyFrame, screenContent, secondQIndex, correctionFactor)),
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Returns the rate correction factor of the frame type. A GOLDEN refresh uses the inter factor because the
    /// constant-bitrate golden boost is zero. Reference: get_rate_correction_factor().
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <returns>The factor.</returns>
    private double GetRateCorrectionFactor(bool keyFrame)
        => Math.Clamp(keyFrame ? this.keyFrameCorrectionFactor : this.interFrameCorrectionFactor, MinimumBitsPerBlockFactor, MaximumBitsPerBlockFactor);

    /// <summary>
    /// Returns the quantizer index change that scales the expected rate by a ratio. Reference:
    /// av1_compute_qdelta_by_rate() and find_qindex_by_rate().
    /// </summary>
    /// <param name="keyFrame">Whether the rate is of a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The base quantizer index.</param>
    /// <param name="rateTargetRatio">The rate ratio.</param>
    /// <returns>The quantizer index change.</returns>
    internal int GetQDeltaByRate(bool keyFrame, bool screenContent, int qIndex, double rateTargetRatio)
        => GetQDeltaByRate(keyFrame, screenContent, qIndex, rateTargetRatio, this.bitDepth, this.bestQuality, this.worstQuality);

    /// <summary>
    /// Returns the quantizer index change that scales the expected rate by a ratio, at the base rate correction and
    /// without the accurate estimate. Reference: av1_compute_qdelta_by_rate() and find_qindex_by_rate().
    /// </summary>
    /// <param name="keyFrame">Whether the rate is of a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The base quantizer index.</param>
    /// <param name="rateTargetRatio">The rate ratio.</param>
    /// <param name="bitDepth">The coded bit depth.</param>
    /// <param name="bestQIndex">The lowest allowed quantizer index. Reference: rc->best_quality.</param>
    /// <param name="worstQIndex">The highest allowed quantizer index. Reference: rc->worst_quality.</param>
    /// <returns>The quantizer index change.</returns>
    public static int GetQDeltaByRate(
        bool keyFrame,
        bool screenContent,
        int qIndex,
        double rateTargetRatio,
        Av1BitDepth bitDepth,
        int bestQIndex,
        int worstQIndex)
    {
        int enumerator = GetBitsPerMacroblockEnumerator(keyFrame, screenContent);
        int baseBitsPerMacroblock = (int)(enumerator * 1.0 / ConvertQIndexToQ(qIndex, bitDepth));
        int targetBitsPerMacroblock = (int)(rateTargetRatio * baseBitsPerMacroblock);
        int low = bestQIndex;
        int high = worstQIndex;
        while (low < high)
        {
            int middle = (low + high) >> 1;
            if ((int)(enumerator * 1.0 / ConvertQIndexToQ(middle, bitDepth)) > targetBitsPerMacroblock)
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
    /// Returns the target rate of a frame per 64x64 area, partial areas included. Reference: the sb64_target_rate of
    /// av1_rc_set_frame_target().
    /// </summary>
    /// <param name="frameTarget">The frame's target size in bits. Reference: rc->this_frame_target.</param>
    /// <param name="width">The frame width in samples.</param>
    /// <param name="height">The frame height in samples.</param>
    /// <returns>The target rate per 64x64 area.</returns>
    public static int GetSuperblockTargetRate(int frameTarget, int width, int height)
        => (int)Math.Min(((long)frameTarget << 12) / (width * height), int.MaxValue);

    /// <summary>
    /// Returns the quantizer index change between two real quantizers inside the allowed range. Reference:
    /// av1_compute_qdelta().
    /// </summary>
    /// <param name="qStart">The starting real quantizer.</param>
    /// <param name="qTarget">The target real quantizer.</param>
    /// <returns>The quantizer index change.</returns>
    private int ComputeQDelta(double qStart, double qTarget)
        => FindQIndex(qTarget, this.bitDepth, this.bestQuality, this.worstQuality) -
            FindQIndex(qStart, this.bitDepth, this.bestQuality, this.worstQuality);

    /// <summary>
    /// Returns the first quantizer index whose real quantizer reaches a value. Reference: av1_find_qindex().
    /// </summary>
    /// <param name="desiredQ">The real quantizer.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="bestQIndex">The lowest quantizer index.</param>
    /// <param name="worstQIndex">The highest quantizer index.</param>
    /// <returns>The quantizer index.</returns>
    private static int FindQIndex(double desiredQ, Av1BitDepth bitDepth, int bestQIndex, int worstQIndex)
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
    /// Converts a quantizer index to the real quantizer on the 8-bit scale. Reference: av1_convert_qindex_to_q().
    /// </summary>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The real quantizer.</returns>
    internal static double ConvertQIndexToQ(int qIndex, Av1BitDepth bitDepth)
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
    /// Returns the number of 16x16 macroblocks of a frame. Reference: av1_get_MBs().
    /// </summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <returns>The macroblock count.</returns>
    private static int GetMacroblockCount(int width, int height)
    {
        int modeInfoColumns = Av1Math.AlignPowerOf2(width, 3) >> Av1Constants.ModeInfoSizeLog2;
        int modeInfoRows = Av1Math.AlignPowerOf2(height, 3) >> Av1Constants.ModeInfoSizeLog2;
        return ((modeInfoRows + 2) >> 2) * ((modeInfoColumns + 2) >> 2);
    }

    /// <summary>
    /// The source change statistics of a frame that the quantizer limits read.
    /// </summary>
    internal readonly struct SourceSadStatistics
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SourceSadStatistics"/> struct.
        /// </summary>
        /// <param name="frameSad">The average 64x64 SAD of the frame. Reference: frame_source_sad.</param>
        /// <param name="averageSad">The running average after this frame. Reference: avg_source_sad.</param>
        /// <param name="previousAverageSad">The running average before this frame. Reference: prev_avg_source_sad.</param>
        public SourceSadStatistics(ulong frameSad, ulong averageSad, ulong previousAverageSad)
        {
            this.FrameSad = frameSad;
            this.AverageSad = averageSad;
            this.PreviousAverageSad = previousAverageSad;
        }

        /// <summary>
        /// Gets the average 64x64 SAD of the frame.
        /// </summary>
        public ulong FrameSad { get; }

        /// <summary>
        /// Gets the running average after this frame.
        /// </summary>
        public ulong AverageSad { get; }

        /// <summary>
        /// Gets the running average before this frame.
        /// </summary>
        public ulong PreviousAverageSad { get; }
    }
}
