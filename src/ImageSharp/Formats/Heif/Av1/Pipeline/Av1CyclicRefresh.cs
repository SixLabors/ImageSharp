// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Codes a rotating part of each real-time inter frame at a lower quantizer, so that static areas get sharper over a
/// few frames without a costly key frame. Segment 1 holds the refreshed superblocks, and segment 2 a stronger boost
/// for large static blocks. Reference: CYCLIC_REFRESH.
/// </summary>
internal sealed class Av1CyclicRefresh
{
    /// <summary>
    /// The segment of blocks that are not refreshed. Reference: CR_SEGMENT_ID_BASE.
    /// </summary>
    public const int BaseSegment = 0;

    /// <summary>
    /// The segment of refreshed blocks. Reference: CR_SEGMENT_ID_BOOST1.
    /// </summary>
    public const int FirstBoostSegment = 1;

    /// <summary>
    /// The segment of refreshed large static blocks. Reference: CR_SEGMENT_ID_BOOST2.
    /// </summary>
    public const int SecondBoostSegment = 2;

    /// <summary>
    /// The largest rate ratio of a segment quantizer change. Reference: CR_MAX_RATE_TARGET_RATIO.
    /// </summary>
    private const double MaximumRateTargetRatio = 4.0;

    /// <summary>
    /// The refresh state of each 4x4 unit: 1 for a unit that is no refresh candidate, 0 for a candidate, and a
    /// negative count of frames to wait after a refresh. Reference: cr->map.
    /// </summary>
    private readonly sbyte[] map;

    /// <summary>
    /// The frame width in 4x4 units. Reference: mi_params.mi_cols.
    /// </summary>
    private readonly int modeInfoColumns;

    /// <summary>
    /// The frame height in 4x4 units. Reference: mi_params.mi_rows.
    /// </summary>
    private readonly int modeInfoRows;

    /// <summary>
    /// The quantizer change of each segment in the current frame: none for the base segment, then the two boosted
    /// segments. Reference: cr->qindex_delta.
    /// </summary>
    private InlineArray3<int> qIndexDelta;

    /// <summary>
    /// The change to the refresh percentage that overshoots and undershoots move, from -5 to 5. Reference:
    /// cr->percent_refresh_adjustment.
    /// </summary>
    private int percentRefreshAdjustment = 5;

    /// <summary>
    /// The change to the rate ratio of the first boosted segment that overshoots and undershoots move, from 0 to 0.25.
    /// Reference: cr->rate_ratio_qdelta_adjustment.
    /// </summary>
    private double rateRatioQDeltaAdjustment = 0.25;

    /// <summary>
    /// The largest quantizer decrease of a boosted segment, as a percentage of the frame quantizer. Reference:
    /// cr->max_qdelta_perc.
    /// </summary>
    private int maximumQDeltaPercent;

    /// <summary>
    /// The superblock where the next frame's refresh starts. Reference: cr->sb_index.
    /// </summary>
    private int superblockIndex;

    /// <summary>
    /// The superblock where the current frame's refresh started. Reference: cr->last_sb_index.
    /// </summary>
    private int lastSuperblockIndex;

    /// <summary>
    /// The frames a refreshed block waits before it may be refreshed again, kept negative in the map. Reference:
    /// cr->time_for_refresh.
    /// </summary>
    private int timeForRefresh;

    /// <summary>
    /// The 4x4 units the current frame marked for refresh. Reference: cr->target_num_seg_blocks.
    /// </summary>
    private int targetSegmentBlockCount;

    /// <summary>
    /// The rate under which a large static block takes the second boosted segment. Reference: cr->thresh_rate_sb.
    /// </summary>
    private long rateThreshold;

    /// <summary>
    /// The distortion above which a single-reference block that moves far or is intra is not refreshed. Reference:
    /// cr->thresh_dist_sb.
    /// </summary>
    private long distortionThreshold;

    /// <summary>
    /// The motion vector component, in eighth samples, above which a block counts as moving far. Reference:
    /// cr->motion_thresh.
    /// </summary>
    private int motionThreshold;

    /// <summary>
    /// The rate ratio of the first boosted segment to the frame, which sets its quantizer decrease. Reference:
    /// cr->rate_ratio_qdelta.
    /// </summary>
    private double rateRatioQDelta;

    /// <summary>
    /// Ten times the factor by which the second boosted segment raises the rate ratio. Reference: cr->rate_boost_fac.
    /// </summary>
    private int rateBoostFactor;

    /// <summary>
    /// Whether the source change of each 64x64 block can force or prevent its refresh. Reference:
    /// cr->use_block_sad_scene_det.
    /// </summary>
    private bool useBlockSadSceneDetection;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1CyclicRefresh"/> class. Reference: av1_cyclic_refresh_alloc().
    /// </summary>
    /// <param name="modeInfoColumns">The frame width in 4x4 units.</param>
    /// <param name="modeInfoRows">The frame height in 4x4 units.</param>
    public Av1CyclicRefresh(int modeInfoColumns, int modeInfoRows)
    {
        this.modeInfoColumns = modeInfoColumns;
        this.modeInfoRows = modeInfoRows;
        this.map = new sbyte[modeInfoColumns * modeInfoRows];
    }

    /// <summary>
    /// Gets the percentage of the frame each frame refreshes. Reference: cr->percent_refresh.
    /// </summary>
    public int PercentRefresh { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the current frame refreshes any block. Reference: cr->apply_cyclic_refresh.
    /// </summary>
    public bool Apply { get; private set; }

    /// <summary>
    /// Gets the rate multiplier of the first boosted segment. Reference: cr->rdmult.
    /// </summary>
    public int RateMultiplier { get; private set; }

    /// <summary>
    /// Gets or sets the frames coded since the last scene change at the worst quantizer. Reference:
    /// cr->counter_encode_maxq_scene_change.
    /// </summary>
    public int SceneChangeFrameCount { get; set; }

    /// <summary>
    /// Gets a value indicating whether the last frame moved the refresh position forward without wrapping to the
    /// start of the frame, so the next refresh cycle has not begun. Reference: the sb_index and last_sb_index test of
    /// adjust_q_cbr().
    /// </summary>
    public bool CycleAdvanced => this.superblockIndex > this.lastSuperblockIndex;

    /// <summary>
    /// Gets or sets the 4x4 units the current frame coded in the first boosted segment. Reference:
    /// cr->actual_num_seg1_blocks.
    /// </summary>
    public int FirstSegmentBlockCount { get; set; }

    /// <summary>
    /// Gets or sets the 4x4 units the current frame coded in the second boosted segment. Reference:
    /// cr->actual_num_seg2_blocks.
    /// </summary>
    public int SecondSegmentBlockCount { get; set; }

    /// <summary>
    /// Returns whether a segment is one of the boosted segments. Reference: cyclic_refresh_segment_id_boosted().
    /// </summary>
    /// <param name="segmentId">The segment.</param>
    /// <returns>Whether the segment is boosted.</returns>
    public static bool IsBoosted(int segmentId) => segmentId is FirstBoostSegment or SecondBoostSegment;

    /// <summary>
    /// Sets the refresh parameters of a frame before its quantizer is chosen, and decides whether the frame refreshes
    /// any block. Reference: av1_cyclic_refresh_update_parameters() for a single layer without screen content tuning,
    /// region of interest, active map or external rate control.
    /// </summary>
    /// <param name="intraFrame">Whether the frame is intra only.</param>
    /// <param name="sceneChange">Whether the frame is a scene change. Reference: rc->high_source_sad.</param>
    /// <param name="lossless">Whether lossless coding is requested.</param>
    /// <param name="framesSinceKey">The frames since the last key frame. Reference: rc->frames_since_key.</param>
    /// <param name="averageInterQIndex">The running inter quantizer. Reference: avg_frame_qindex[INTER_FRAME].</param>
    /// <param name="bestQuality">The lowest allowed quantizer index. Reference: rc->best_quality.</param>
    /// <param name="averageFrameLowMotion">The running zero motion percentage. Reference: rc->avg_frame_low_motion.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="averageFrameBandwidth">The bits per frame. Reference: rc->avg_frame_bandwidth.</param>
    /// <param name="superblockSize">The superblock size.</param>
    public void UpdateParameters(
        bool intraFrame,
        bool sceneChange,
        bool lossless,
        int framesSinceKey,
        int averageInterQIndex,
        int bestQuality,
        int averageFrameLowMotion,
        int width,
        int height,
        int averageFrameBandwidth,
        Av1BlockSize superblockSize)
    {
        // Below this quantizer the frame is already sharp enough that a refresh only costs bits. Reference: qp_thresh.
        int qpThreshold = Math.Max(16, bestQuality + 4);

        // Above this quantizer, held long after a scene change, the bits are better spent on the whole frame.
        // Reference: qp_max_thresh.
        const int qpMaximumThreshold = (118 * Av1Constants.MaxQ) >> 7;

        // A scene change or key frame starts a refresh cycle.
        int framesSinceSceneChange = framesSinceKey;
        if (intraFrame || sceneChange)
        {
            this.percentRefreshAdjustment = 5;
            this.rateRatioQDeltaAdjustment = 0.25;
        }

        // Intra frames, lossless coding and scene changes refresh nothing. Neither does a frame at a low running
        // quantizer, a frame long after a scene change at a very high quantizer, or a frame with little zero motion
        // long after a scene change.
        this.Apply = true;
        if (intraFrame ||
            lossless ||
            sceneChange ||
            averageInterQIndex < qpThreshold ||
            (framesSinceSceneChange > 20 && averageInterQIndex > qpMaximumThreshold) ||
            (averageFrameLowMotion != 0 && averageFrameLowMotion < 30 && framesSinceSceneChange > 40))
        {
            this.Apply = false;
            return;
        }

        this.PercentRefresh = 10 + this.percentRefreshAdjustment;
        this.maximumQDeltaPercent = 60;
        this.timeForRefresh = 0;
        this.useBlockSadSceneDetection = superblockSize == Av1BlockSize.Block64x64;
        this.motionThreshold = 32;
        this.rateBoostFactor = 15;

        // The first refresh cycles after a scene change take a larger quantizer change.
        if (this.PercentRefresh > 0)
        {
            this.rateRatioQDelta = framesSinceSceneChange < 4 * (100 / this.PercentRefresh)
                ? 3.0 + this.rateRatioQDeltaAdjustment
                : 2.25 + this.rateRatioQDeltaAdjustment;
        }
        else
        {
            this.rateRatioQDelta = 2.25 + this.rateRatioQDeltaAdjustment;
        }

        // Frames up to 352x288 refresh fewer moving blocks and boost less at a low bit rate, and limit the quantizer
        // decrease at a higher bit rate.
        if (width * height <= 352 * 288)
        {
            if (averageFrameBandwidth < 3000)
            {
                this.motionThreshold = 16;
                this.rateBoostFactor = 13;
            }
            else
            {
                this.maximumQDeltaPercent = 50;
                this.rateRatioQDelta = Math.Max(this.rateRatioQDelta, 2.0);
            }
        }
    }

    /// <summary>
    /// Sets the segment quantizers and the segment map of a frame after its quantizer is chosen. A frame that
    /// refreshes nothing codes without segments. Reference: av1_cyclic_refresh_setup() for a single layer without
    /// region of interest or active map.
    /// </summary>
    /// <param name="segmentation">The segmentation state of the frame.</param>
    /// <param name="encoderSegmentMap">The segment map the encoder keeps across frames. Reference: cpi->enc_seg.map.</param>
    /// <param name="rateControl">The rate control of the sequence.</param>
    /// <param name="parent">The frame state.</param>
    /// <param name="intraFrame">Whether the frame is intra only.</param>
    /// <param name="sceneChange">Whether the frame is a scene change. Reference: rc->high_source_sad.</param>
    /// <param name="speed">The encoding speed.</param>
    /// <param name="framesSinceKey">The frames since the last key frame. Reference: rc->frames_since_key.</param>
    /// <param name="superblockModeInfoSize">The superblock size in 4x4 units. Reference: mib_size.</param>
    /// <param name="superblockSads">The source SAD of each 64x64 block against the previous source, or empty.</param>
    public void Setup(
        ObuSegmentationParameters segmentation,
        Span<byte> encoderSegmentMap,
        Av1RateControl rateControl,
        Av1PictureParentControlSet parent,
        bool intraFrame,
        bool sceneChange,
        HeifEncodingSpeed speed,
        int framesSinceKey,
        int superblockModeInfoSize,
        ReadOnlySpan<ulong> superblockSads)
    {
        if (!this.Apply)
        {
            encoderSegmentMap.Clear();
            DisableSegmentation(segmentation);
            if (intraFrame || sceneChange)
            {
                this.superblockIndex = 0;
                this.lastSuperblockIndex = 0;
                this.SceneChangeFrameCount = 0;
                this.FirstSegmentBlockCount = 0;
                this.SecondSegmentBlockCount = 0;
            }

            return;
        }

        this.SceneChangeFrameCount++;
        ObuQuantizationParameters quantization = parent.FrameHeader.QuantizationParameters;
        int baseQIndex = quantization.BaseQIndex;
        Av1BitDepth bitDepth = rateControl.BitDepth;
        double q = Av1RateControl.ConvertQIndexToQ(baseQIndex, bitDepth);
        int width = parent.FrameHeader.FrameSize.FrameWidth;
        int height = parent.FrameHeader.FrameSize.FrameHeight;

        // The rate threshold is twice the superblock target in 1/256 units, and the distortion threshold is
        // quadratic in the quantizer. Lower speeds and small frames do not use them.
        this.rateThreshold = ((long)rateControl.SuperblockTargetRate << 8) << 2;
        this.distortionThreshold = (long)(q * q) << 2;
        if (speed <= HeifEncodingSpeed.Level7 || width * height < 640 * 360)
        {
            this.distortionThreshold = 0;
            this.rateThreshold = long.MaxValue;
        }

        // Reference: av1_enable_segmentation() and av1_clearall_segfeatures().
        segmentation.Enabled = true;
        segmentation.SegmentationUpdateMap = 1;
        segmentation.SegmentationUpdateData = 1;
        segmentation.SegmentationTemporalUpdate = 0;
        segmentation.ClearFeatures();
        const int alternativeQuantizer = (int)ObuSegmentationLevelFeature.AlternativeQuantizer;
        segmentation.SetFeatureEnabled(BaseSegment, alternativeQuantizer, false);
        segmentation.SetFeatureEnabled(FirstBoostSegment, alternativeQuantizer, true);
        segmentation.SetFeatureEnabled(SecondBoostSegment, alternativeQuantizer, true);

        bool keyFrame = parent.FrameHeader.FrameType == ObuFrameType.KeyFrame;
        int qIndexDelta = this.ComputeQDelta(rateControl, keyFrame, parent.IsScreenContent, baseQIndex, this.rateRatioQDelta);
        this.qIndexDelta[1] = qIndexDelta;

        // The first boosted segment searches at the multiplier of its own quantizer.
        int boostedQIndex = Av1Math.Clamp(baseQIndex + quantization.DeltaQDc[0] + qIndexDelta, 0, Av1Constants.MaxQ);
        this.RateMultiplier = parent.GetRateMultiplier(boostedQIndex, bitDepth);
        segmentation.SetFeatureData(FirstBoostSegment, alternativeQuantizer, qIndexDelta);

        // The second boosted segment takes a larger quantizer change.
        qIndexDelta = this.ComputeQDelta(
            rateControl,
            keyFrame,
            parent.IsScreenContent,
            baseQIndex,
            Math.Min(MaximumRateTargetRatio, 0.1 * this.rateBoostFactor * this.rateRatioQDelta));

        this.qIndexDelta[2] = qIndexDelta;
        segmentation.SetFeatureData(SecondBoostSegment, alternativeQuantizer, qIndexDelta);
        this.UpdateMap(segmentation, encoderSegmentMap, superblockModeInfoSize, framesSinceKey, superblockSads, width, height);
    }

    /// <summary>
    /// Returns the quantizer change of a boosted segment in the current frame. Reference: cr->qindex_delta.
    /// </summary>
    /// <param name="segmentId">The boosted segment.</param>
    /// <returns>The quantizer index change.</returns>
    public int GetSegmentQDelta(int segmentId) => this.qIndexDelta[segmentId];

    /// <summary>
    /// Returns the share of the frame the next frame expects in the first boosted segment: the average of the units
    /// it targets and the units the previous frame coded. Reference: the weight_segment of
    /// av1_cyclic_refresh_rc_bits_per_mb().
    /// </summary>
    /// <param name="macroblockCount">The 16x16 units of the frame. Reference: mi_params.MBs.</param>
    /// <returns>The share of the frame.</returns>
    public double GetExpectedSegmentWeight(int macroblockCount)
        => (double)((this.targetSegmentBlockCount + this.FirstSegmentBlockCount + this.SecondSegmentBlockCount) >> 1) /
            (macroblockCount << 4);

    /// <summary>
    /// Returns the quantizer change of the first boosted segment at a quantizer index. Reference: the
    /// compute_deltaq() call of av1_cyclic_refresh_rc_bits_per_mb().
    /// </summary>
    /// <param name="rateControl">The rate control of the sequence.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The quantizer index.</param>
    /// <returns>The quantizer index change.</returns>
    public int GetExpectedQDelta(Av1RateControl rateControl, bool keyFrame, bool screenContent, int qIndex)
        => this.ComputeQDelta(rateControl, keyFrame, screenContent, qIndex, this.rateRatioQDelta);

    /// <summary>
    /// Moves the refresh amount and the quantizer change after an overshoot or undershoot of the frame target.
    /// Reference: the cyclic refresh adjustment of av1_rc_update_rate_correction_factors().
    /// </summary>
    /// <param name="correctionFactor">The ratio of the coded size to the expected size.</param>
    public void AdjustForRate(double correctionFactor)
    {
        if (correctionFactor > 1.25)
        {
            this.percentRefreshAdjustment = Math.Max(this.percentRefreshAdjustment - 1, -5);
            this.rateRatioQDeltaAdjustment = Math.Max(this.rateRatioQDeltaAdjustment - 0.05, 0.0);
        }
        else if (correctionFactor < 0.5)
        {
            this.percentRefreshAdjustment = Math.Min(this.percentRefreshAdjustment + 1, 5);
            this.rateRatioQDeltaAdjustment = Math.Min(this.rateRatioQDeltaAdjustment + 0.05, 0.25);
        }
    }

    /// <summary>
    /// Updates the segment of a coded block and the refresh map. A boosted block stays boosted only when it is a
    /// refresh candidate that codes coefficients. A refreshed block waits before it may be refreshed again.
    /// Reference: av1_cyclic_refresh_update_segment() and candidate_refresh_aq().
    /// </summary>
    /// <param name="encoderSegmentMap">The segment map the encoder keeps across frames. Reference: cpi->enc_seg.map.</param>
    /// <param name="frameSegmentMap">The segment map of the frame buffer. Reference: cm->cur_frame->seg_map.</param>
    /// <param name="modeInfoPosition">The block position in 4x4 units.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="segmentId">The block's segment, updated in place.</param>
    /// <param name="interBlock">Whether the block is inter predicted.</param>
    /// <param name="compound">Whether the block predicts from two references.</param>
    /// <param name="vector">The block's first motion vector.</param>
    /// <param name="rate">The block's searched rate.</param>
    /// <param name="distortion">The block's searched distortion.</param>
    /// <param name="skip">Whether the block codes no coefficients.</param>
    /// <param name="noiseLevel">The estimated noise level, the lowest level when noise estimation is off.</param>
    /// <param name="countBlocks">Whether the frame counts the segment units, which only the output encode does.</param>
    public void UpdateSegment(
        Span<byte> encoderSegmentMap,
        Span<byte> frameSegmentMap,
        Point modeInfoPosition,
        Av1BlockSize blockSize,
        ref int segmentId,
        bool interBlock,
        bool compound,
        Av1MotionVector vector,
        long rate,
        long distortion,
        bool skip,
        int noiseLevel,
        bool countBlocks)
    {
        int visibleColumns = Math.Min(this.modeInfoColumns - modeInfoPosition.X, blockSize.Get4x4WideCount());
        int visibleRows = Math.Min(this.modeInfoRows - modeInfoPosition.Y, blockSize.Get4x4HighCount());
        int blockIndex = (modeInfoPosition.Y * this.modeInfoColumns) + modeInfoPosition.X;
        int refreshThisBlock = this.GetRefreshCandidate(interBlock, compound, vector, rate, distortion, blockSize, noiseLevel);

        // Reference: the skip_over4x4 row step, which only speeds above 9 set.
        const int rowStep = 1;
        int newMapValue = this.map[blockIndex];

        // A boosted block takes the candidate segment, and the base segment when it codes nothing.
        if (IsBoosted(segmentId))
        {
            segmentId = skip ? BaseSegment : refreshThisBlock;
        }

        if (IsBoosted(segmentId))
        {
            newMapValue = -this.timeForRefresh;
        }
        else if (refreshThisBlock != BaseSegment)
        {
            // A candidate that was refreshed before becomes a candidate for a later cleanup.
            if (this.map[blockIndex] == 1)
            {
                newMapValue = 0;
            }
        }
        else
        {
            newMapValue = 1;
        }

        for (int row = 0; row < visibleRows; row += rowStep)
        {
            int offset = blockIndex + (row * this.modeInfoColumns);
            this.map.AsSpan(offset, visibleColumns).Fill((sbyte)newMapValue);
            encoderSegmentMap.Slice(offset, visibleColumns).Fill((byte)segmentId);
            frameSegmentMap.Slice(offset, visibleColumns).Fill((byte)segmentId);
        }

        if (countBlocks)
        {
            this.CountBlocks(segmentId, visibleColumns * visibleRows);
        }
    }

    /// <summary>
    /// Gives a skipped block of a boosted superblock the predicted segment, so the map codes cheaply, and removes it
    /// from the segment counts. Reference: av1_cyclic_reset_segment_skip() without skip_over4x4.
    /// </summary>
    /// <param name="encoderSegmentMap">The segment map the encoder keeps across frames. Reference: cpi->enc_seg.map.</param>
    /// <param name="frameSegmentMap">The segment map of the frame buffer. Reference: cm->cur_frame->seg_map.</param>
    /// <param name="modeInfoPosition">The block position in 4x4 units.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="segmentId">The block's segment, updated in place.</param>
    /// <param name="predictedSegmentId">The spatially predicted segment. Reference: av1_get_spatial_seg_pred().</param>
    /// <param name="countBlocks">Whether the frame counts the segment units, which only the output encode does.</param>
    public void ResetSegmentSkip(
        Span<byte> encoderSegmentMap,
        Span<byte> frameSegmentMap,
        Point modeInfoPosition,
        Av1BlockSize blockSize,
        ref int segmentId,
        int predictedSegmentId,
        bool countBlocks)
    {
        int previousSegmentId = segmentId;
        int visibleColumns = Math.Min(this.modeInfoColumns - modeInfoPosition.X, blockSize.Get4x4WideCount());
        int visibleRows = Math.Min(this.modeInfoRows - modeInfoPosition.Y, blockSize.Get4x4HighCount());
        segmentId = predictedSegmentId;
        if (previousSegmentId != segmentId)
        {
            int blockIndex = (modeInfoPosition.Y * this.modeInfoColumns) + modeInfoPosition.X;
            for (int row = 0; row < visibleRows; row++)
            {
                int offset = blockIndex + (row * this.modeInfoColumns);
                this.map.AsSpan(offset, visibleColumns).Clear();
                encoderSegmentMap.Slice(offset, visibleColumns).Fill((byte)segmentId);
                frameSegmentMap.Slice(offset, visibleColumns).Fill((byte)segmentId);
            }
        }

        if (countBlocks)
        {
            this.CountBlocks(previousSegmentId, -(visibleColumns * visibleRows));
        }
    }

    /// <summary>
    /// Disables segmentation. Reference: av1_disable_segmentation().
    /// </summary>
    /// <param name="segmentation">The segmentation state of the frame.</param>
    private static void DisableSegmentation(ObuSegmentationParameters segmentation)
    {
        segmentation.Enabled = false;
        segmentation.SegmentationUpdateMap = 0;
        segmentation.SegmentationUpdateData = 0;
        segmentation.SegmentationTemporalUpdate = 0;
    }

    /// <summary>
    /// Returns the segment a coded block may take: the base segment for a single-reference block with high
    /// distortion that is intra or moves far, the second boosted segment for a low-noise compound block or a large
    /// static inter block under the rate threshold, and the first boosted segment otherwise. Reference:
    /// candidate_refresh_aq().
    /// </summary>
    /// <param name="interBlock">Whether the block is inter predicted.</param>
    /// <param name="compound">Whether the block predicts from two references.</param>
    /// <param name="vector">The block's first motion vector.</param>
    /// <param name="rate">The block's searched rate.</param>
    /// <param name="distortion">The block's searched distortion.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="noiseLevel">The estimated noise level, the lowest level when noise estimation is off.</param>
    /// <returns>The candidate segment.</returns>
    private int GetRefreshCandidate(
        bool interBlock,
        bool compound,
        Av1MotionVector vector,
        long rate,
        long distortion,
        Av1BlockSize blockSize,
        int noiseLevel)
    {
        // Reference: kMedium of NOISE_LEVEL.
        const int mediumNoise = 2;
        if (!compound && distortion > this.distortionThreshold &&
            (vector.Row > this.motionThreshold || vector.Row < -this.motionThreshold ||
             vector.Column > this.motionThreshold || vector.Column < -this.motionThreshold ||
             !interBlock))
        {
            return BaseSegment;
        }

        if ((compound && noiseLevel < mediumNoise) ||
            (blockSize >= Av1BlockSize.Block16x16 && rate < this.rateThreshold && interBlock && vector.IsZero &&
             this.rateBoostFactor > 10))
        {
            return SecondBoostSegment;
        }

        return FirstBoostSegment;
    }

    /// <summary>
    /// Returns the quantizer change of a segment that scales the expected rate by a ratio, at most the largest
    /// allowed share of the quantizer. Reference: compute_deltaq().
    /// </summary>
    /// <param name="rateControl">The rate control of the sequence, which models the rate of each quantizer.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="screenContent">Whether the frame is screen content.</param>
    /// <param name="qIndex">The quantizer index the change starts from.</param>
    /// <param name="rateFactor">The ratio of the segment's expected rate to the frame's.</param>
    /// <returns>The quantizer index change, never lower than the negative largest share.</returns>
    private int ComputeQDelta(Av1RateControl rateControl, bool keyFrame, bool screenContent, int qIndex, double rateFactor)
    {
        int qIndexDelta = rateControl.GetQDeltaByRate(keyFrame, screenContent, qIndex, rateFactor);
        if (-qIndexDelta > this.maximumQDeltaPercent * qIndex / 100)
        {
            qIndexDelta = -this.maximumQDeltaPercent * qIndex / 100;
        }

        return qIndexDelta;
    }

    /// <summary>
    /// Adds units to the count of a boosted segment. Reference: the actual_num_seg1_blocks and
    /// actual_num_seg2_blocks updates.
    /// </summary>
    /// <param name="segmentId">The segment of the units; the base segment is not counted.</param>
    /// <param name="units">The number of 4x4 units to add, negative to remove them.</param>
    private void CountBlocks(int segmentId, int units)
    {
        if (segmentId == FirstBoostSegment)
        {
            this.FirstSegmentBlockCount += units;
        }
        else if (segmentId == SecondBoostSegment)
        {
            this.SecondSegmentBlockCount += units;
        }
    }

    /// <summary>
    /// Marks the superblocks the frame refreshes in the first boosted segment, going round the frame from where the
    /// previous frame stopped until the refresh percentage is reached. A superblock is refreshed when at least half
    /// of it is a candidate and its source changed little. Reference: cyclic_refresh_update_map() without active map.
    /// </summary>
    /// <param name="segmentation">The segmentation state, disabled when no superblock is refreshed.</param>
    /// <param name="encoderSegmentMap">The segment map the encoder keeps across frames. Reference: cpi->enc_seg.map.</param>
    /// <param name="superblockModeInfoSize">The superblock size in 4x4 units. Reference: mib_size.</param>
    /// <param name="framesSinceKey">The frames since the last key frame. Reference: rc->frames_since_key.</param>
    /// <param name="superblockSads">The source SAD of each 64x64 block against the previous source, or empty.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    private void UpdateMap(
        ObuSegmentationParameters segmentation,
        Span<byte> encoderSegmentMap,
        int superblockModeInfoSize,
        int framesSinceKey,
        ReadOnlySpan<ulong> superblockSads,
        int width,
        int height)
    {
        encoderSegmentMap.Clear();
        int superblockColumns = (this.modeInfoColumns + superblockModeInfoSize - 1) / superblockModeInfoSize;
        int superblockRows = (this.modeInfoRows + superblockModeInfoSize - 1) / superblockModeInfoSize;
        int superblockCount = superblockColumns * superblockRows;
        int blockCount = this.PercentRefresh * this.modeInfoRows * this.modeInfoColumns / 100;
        if (this.superblockIndex >= superblockCount)
        {
            this.superblockIndex = 0;
        }

        int index = this.superblockIndex;
        this.lastSuperblockIndex = this.superblockIndex;
        this.targetSegmentBlockCount = 0;
        ulong superblockSad = 0;
        ulong lowSadThreshold = 0;

        // Without the scene detection no superblock is too changed to refresh. Reference: the INT64_MAX thresh_sad.
        ulong sadThreshold = long.MaxValue;
        do
        {
            int sumMap = 0;
            int superblockRow = index / superblockColumns;
            int superblockColumn = index - (superblockRow * superblockColumns);
            int modeInfoRow = superblockRow * superblockModeInfoSize;
            int modeInfoColumn = superblockColumn * superblockModeInfoSize;
            int blockIndex = (modeInfoRow * this.modeInfoColumns) + modeInfoColumn;
            int visibleColumns = Math.Min(this.modeInfoColumns - modeInfoColumn, superblockModeInfoSize);
            int visibleRows = Math.Min(this.modeInfoRows - modeInfoRow, superblockModeInfoSize);

            // Long after a key frame and a scene change, a superblock whose source barely changed is refreshed anyway,
            // and one whose source changed a lot is not. The thresholds are a few units of change per sample over a
            // 64x64 block.
            if (this.useBlockSadSceneDetection && framesSinceKey > 30 && this.SceneChangeFrameCount > 30 && !superblockSads.IsEmpty)
            {
                superblockSad = superblockSads[superblockColumn + (superblockColumns * superblockRow)];
                ulong scale = width * height < 640 * 360 ? 6UL : 8UL;
                sadThreshold = scale * 64 * 64;
                lowSadThreshold = 2 * 64 * 64;
            }

            // The refresh map is only kept at 8x8.
            for (int y = 0; y < visibleRows; y += 2)
            {
                for (int x = 0; x < visibleColumns; x += 2)
                {
                    int unitIndex = blockIndex + (y * this.modeInfoColumns) + x;
                    if (this.map[unitIndex] == 0 || superblockSad < lowSadThreshold)
                    {
                        sumMap += 4;
                    }
                    else if (this.map[unitIndex] < 0)
                    {
                        this.map[unitIndex]++;
                    }
                }
            }

            // The whole superblock takes one segment.
            if (sumMap >= (visibleColumns * visibleRows) >> 1 && superblockSad < sadThreshold)
            {
                for (int row = 0; row < visibleRows; row++)
                {
                    encoderSegmentMap.Slice(blockIndex + (row * this.modeInfoColumns), visibleColumns).Fill(FirstBoostSegment);
                }

                this.targetSegmentBlockCount += visibleColumns * visibleRows;
            }

            index++;
            if (index == superblockCount)
            {
                index = 0;
            }
        }
        while (this.targetSegmentBlockCount < blockCount && index != this.superblockIndex);

        this.superblockIndex = index;
        if (this.targetSegmentBlockCount == 0)
        {
            DisableSegmentation(segmentation);
        }
    }
}
