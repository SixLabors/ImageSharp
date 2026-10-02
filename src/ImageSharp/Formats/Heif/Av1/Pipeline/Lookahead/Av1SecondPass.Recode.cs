// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <content>
/// The recode loop of a look-ahead sequence that codes against a bit budget: after a frame is coded and packed once
/// to measure its size, a frame outside the tolerance around its target is coded again at a quantizer that the rate
/// model, or a binary search once both an over- and an undershoot were seen, picks between the bounds of the frame.
/// Reference: encode_with_recode_loop() with recode_loop_update_q() and recode_loop_test().
/// </content>
internal sealed partial class Av1SecondPass
{
    /// <summary>
    /// Whether every frame may be coded again, not only key frames, golden frames and alternate references.
    /// Reference: hl_sf.recode_loop == ALLOW_RECODE, which speeds 0 and 1 keep.
    /// </summary>
    private readonly bool recodesEveryFrame;

    /// <summary>
    /// The tolerance around the frame target, in percent, inside which a frame is not coded again. Reference:
    /// hl_sf.recode_tolerance.
    /// </summary>
    private readonly int recodeTolerance;

    /// <summary>
    /// The lowest quantizer index the rate control allowed the current frame. Reference: the bottom_index of
    /// av1_rc_pick_q_and_bounds().
    /// </summary>
    private int pickedBottomIndex;

    /// <summary>
    /// The highest quantizer index the rate control allowed the current frame. Reference: the top_index of
    /// av1_rc_pick_q_and_bounds().
    /// </summary>
    private int pickedTopIndex;

    /// <summary>
    /// The lowest quantizer index of the frame's bounds. Reference: bottom_index of encode_with_recode_loop().
    /// </summary>
    private int recodeBottomIndex;

    /// <summary>
    /// The highest quantizer index of the frame's bounds. Reference: top_index of encode_with_recode_loop().
    /// </summary>
    private int recodeTopIndex;

    /// <summary>
    /// The lowest quantizer index the search may still try. Reference: q_low.
    /// </summary>
    private int recodeLowIndex;

    /// <summary>
    /// The highest quantizer index the search may still try. Reference: q_high.
    /// </summary>
    private int recodeHighIndex;

    /// <summary>
    /// The number of times the frame was coded again. Reference: loop_count.
    /// </summary>
    private int recodeCount;

    /// <summary>
    /// Whether a coding of the frame came out below the tolerance. Reference: undershoot_seen.
    /// </summary>
    private bool undershootSeen;

    /// <summary>
    /// Whether a coding of the frame came out above the tolerance. Reference: overshoot_seen.
    /// </summary>
    private bool overshootSeen;

    /// <summary>
    /// The luma squared error of the frame before a forced key frame, which the forced key frame matches its error
    /// to. Reference: cpi->ambient_err.
    /// </summary>
    private long ambientError;

    /// <summary>
    /// Gets a value indicating whether each coding of a frame is packed to measure its size and may be coded again.
    /// Reference: the do_dummy_pack test of encode_with_recode_loop(), with a recode loop of ALLOW_RECODE_KFARFGF or
    /// more outside AOM_Q.
    /// </summary>
    public bool UsesRecodeLoop => this.UsesBitBudget;

    /// <summary>
    /// Gets a value indicating whether the current frame is the last before a forced key frame, whose reconstruction
    /// error the key frame aims for. Reference: the next_key_frame_forced and frames_to_key == 1 test of
    /// encode_with_recode_loop_and_filter().
    /// </summary>
    public bool RecordsAmbientError => this.nextKeyFrameForced && this.framesToKey == 1;

    /// <summary>
    /// Gets a value indicating whether the current frame is a forced key frame, which a recode matches to the error of
    /// the frame before it. Reference: the KEY_FRAME and this_key_frame_forced test of recode_loop_update_q().
    /// </summary>
    public bool MatchesAmbientError => this.current.IsKeyFrame && this.thisKeyFrameForced;

    /// <summary>
    /// Sets the luma squared error of the reconstruction of the frame before a forced key frame. Reference:
    /// cpi->ambient_err.
    /// </summary>
    /// <param name="squaredError">The luma squared error of the unfiltered reconstruction.</param>
    public void SetAmbientError(long squaredError) => this.ambientError = squaredError;

    /// <summary>
    /// Updates the quantizer of a frame after a coding of it was packed, and returns whether the frame must be coded
    /// again at the new quantizer. An overlay within the largest frame size keeps its coding. A forced key frame
    /// moves its quantizer by the ratio of its error to that of the frame before it. Any other frame outside the
    /// tolerance around its target moves its rate correction and picks the quantizer the rate model expects to
    /// meet the target, or the middle of the remaining range once it has both over- and undershot.
    /// Reference: recode_loop_update_q() with av1_rc_compute_frame_size_bounds(), recode_loop_test(),
    /// get_regulated_q_overshoot() and get_regulated_q_undershoot().
    /// </summary>
    /// <param name="projectedFrameSize">The packed size of the frame in bits. Reference: rc->projected_frame_size.</param>
    /// <param name="keyFrameError">
    /// The luma squared error of the unfiltered reconstruction of a forced key frame, else ignored.
    /// </param>
    /// <param name="qIndex">The quantizer index of the coding; receives the quantizer index of the next coding.</param>
    /// <returns>Whether the frame must be coded again.</returns>
    public bool UpdateRecodeQuantizer(int projectedFrameSize, long keyFrameError, ref int qIndex)
    {
        bool keyFrame = this.current.IsKeyFrame;
        bool recode = this.UpdateRecodeQuantizer(projectedFrameSize, keyFrameError, keyFrame, ref qIndex);
        if (recode)
        {
            ++this.recodeCount;
        }

        return recode;
    }

    /// <summary>
    /// Starts the recode search of the current frame from the bounds its quantizer pick allowed. Reference: the q_low
    /// and q_high setup of encode_with_recode_loop().
    /// </summary>
    private void BeginRecode()
    {
        this.recodeBottomIndex = this.pickedBottomIndex;
        this.recodeTopIndex = this.pickedTopIndex;
        this.recodeLowIndex = this.pickedBottomIndex;
        this.recodeHighIndex = this.pickedTopIndex;
        this.recodeCount = 0;
        this.undershootSeen = false;
        this.overshootSeen = false;
    }

    /// <summary>
    /// Updates the quantizer of a frame after a coding of it was packed. Reference: recode_loop_update_q() without
    /// a minimum compression ratio or level targets.
    /// </summary>
    /// <param name="projectedFrameSize">The packed size of the frame in bits.</param>
    /// <param name="keyFrameError">The luma squared error of the unfiltered reconstruction of a forced key frame.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="qIndex">The quantizer index of the coding; receives the quantizer index of the next coding.</param>
    /// <returns>Whether the frame must be coded again.</returns>
    private bool UpdateRecodeQuantizer(int projectedFrameSize, long keyFrameError, bool keyFrame, ref int qIndex)
    {
        // An overlay within the largest frame size keeps its coding.
        if (this.sourceIsAlternate && projectedFrameSize < this.maximumFrameBandwidth)
        {
            return false;
        }

        int lastQIndex = qIndex;
        this.GetFrameSizeBounds(out int underShootLimit, out int overShootLimit);
        if (overShootLimit == 0)
        {
            overShootLimit = 1;
        }

        if (keyFrame && this.thisKeyFrameForced && projectedFrameSize < this.maximumFrameBandwidth)
        {
            // The forced key frame aims for the error of the frame before it, within half of it, to avoid popping.
            long highErrorTarget = this.ambientError;
            long lowErrorTarget = this.ambientError >> 1;
            long keyError = Math.Max(keyFrameError, 1);
            if ((keyError > highErrorTarget && projectedFrameSize <= overShootLimit) ||
                (keyError > lowErrorTarget && projectedFrameSize <= underShootLimit))
            {
                // The key frame is not good enough, or can afford to be better: lower the ceiling and the quantizer.
                this.recodeHighIndex = Math.Max(qIndex - 1, this.recodeLowIndex);
                qIndex = (int)(qIndex * highErrorTarget / keyError);
                qIndex = Math.Min(qIndex, (this.recodeHighIndex + this.recodeLowIndex) >> 1);
            }
            else if (keyError < lowErrorTarget && projectedFrameSize >= underShootLimit)
            {
                // The key frame is much better than the frame before it: raise the floor and the quantizer.
                this.recodeLowIndex = Math.Min(qIndex + 1, this.recodeHighIndex);
                qIndex = (int)(qIndex * lowErrorTarget / keyError);
                qIndex = Math.Min(qIndex, (this.recodeHighIndex + this.recodeLowIndex + 1) >> 1);
            }

            qIndex = Math.Clamp(qIndex, this.recodeLowIndex, this.recodeHighIndex);
            return qIndex != lastQIndex;
        }

        int maximumQIndex = Math.Max(this.recodeHighIndex, this.recodeTopIndex);
        if (!this.IsRecodeNeeded(projectedFrameSize, overShootLimit, underShootLimit, qIndex, maximumQIndex, this.recodeBottomIndex))
        {
            return false;
        }

        bool laterCoding = this.recodeCount > 2 || (this.recodeCount == 2 && !keyFrame);
        if (projectedFrameSize > this.thisFrameTarget)
        {
            // A frame at the ceiling that exceeds the largest frame size raises the ceiling by the overshoot.
            if (qIndex == this.recodeHighIndex && projectedFrameSize >= this.maximumFrameBandwidth)
            {
                double highQ = Av1ConstantQuality.ConvertQIndexToQ(this.recodeHighIndex, this.bitDepth);
                double newHighQ = highQ * ((double)projectedFrameSize / this.maximumFrameBandwidth);
                this.recodeHighIndex = Av1ConstantQuality.FindQIndex(newHighQ, this.bitDepth, this.bestQuality, this.worstQuality);
            }

            // The quantizer rises to at least the next index.
            this.recodeLowIndex = Math.Min(qIndex + 1, this.recodeHighIndex);
            if (this.undershootSeen || laterCoding)
            {
                this.UpdateRateCorrectionFactor(projectedFrameSize, qIndex, keyFrame);
                qIndex = (this.recodeHighIndex + this.recodeLowIndex + 1) / 2;
            }
            else if (this.recodeCount == 2 && keyFrame)
            {
                // Halfway between the middle of the range and the rate model, for a smooth move to the search.
                int middle = (this.recodeHighIndex + this.recodeLowIndex + 1) / 2;
                int regulated = this.GetRegulatedOvershootQIndex(projectedFrameSize, qIndex, keyFrame);
                qIndex = (middle + regulated + 1) / 2;
            }
            else
            {
                qIndex = this.GetRegulatedOvershootQIndex(projectedFrameSize, qIndex, keyFrame);
            }

            this.overshootSeen = true;
        }
        else
        {
            // The quantizer falls to at most the previous index.
            this.recodeHighIndex = Math.Max(qIndex - 1, this.recodeLowIndex);
            if (this.overshootSeen || laterCoding)
            {
                this.UpdateRateCorrectionFactor(projectedFrameSize, qIndex, keyFrame);
                qIndex = (this.recodeHighIndex + this.recodeLowIndex) / 2;
            }
            else if (this.recodeCount == 2 && keyFrame)
            {
                int middle = (this.recodeHighIndex + this.recodeLowIndex) / 2;
                int regulated = this.GetRegulatedUndershootQIndex(projectedFrameSize, qIndex, keyFrame);
                qIndex = (middle + regulated) / 2;

                // A constrained-quality frame far below its target may go below the floor of its quality level.
                if (this.mode == Av1RateControlMode.ConstrainedQuality && regulated < this.recodeLowIndex)
                {
                    this.recodeLowIndex = qIndex;
                }
            }
            else
            {
                qIndex = this.GetRegulatedUndershootQIndex(projectedFrameSize, qIndex, keyFrame);
                if (this.mode == Av1RateControlMode.ConstrainedQuality && qIndex < this.recodeLowIndex)
                {
                    this.recodeLowIndex = qIndex;
                }
            }

            this.undershootSeen = true;
        }

        qIndex = Math.Clamp(qIndex, this.recodeLowIndex, this.recodeHighIndex);
        return qIndex != lastQIndex;
    }

    /// <summary>
    /// Returns whether a coding of a frame is far enough from its target to code it again: a frame at or above the
    /// largest size, any frame when every frame may be recoded, or else a key frame, golden frame or alternate
    /// reference, whose size lies outside the limits while the quantizer can still move, or a constrained-quality
    /// frame above its quality level below seven eighths of its target. Reference: recode_loop_test().
    /// </summary>
    /// <param name="projectedFrameSize">The packed size of the frame in bits.</param>
    /// <param name="highLimit">The overshoot limit in bits.</param>
    /// <param name="lowLimit">The undershoot limit in bits.</param>
    /// <param name="qIndex">The quantizer index of the coding.</param>
    /// <param name="maximumQIndex">The highest quantizer index the search allows.</param>
    /// <param name="minimumQIndex">The lowest quantizer index the search allows.</param>
    /// <returns>Whether to code the frame again.</returns>
    private bool IsRecodeNeeded(int projectedFrameSize, int highLimit, int lowLimit, int qIndex, int maximumQIndex, int minimumQIndex)
    {
        Av1FrameUpdateType updateType = this.group.UpdateTypes[this.groupFrameIndex];
        bool boosted = updateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;
        if (projectedFrameSize < this.maximumFrameBandwidth && !this.recodesEveryFrame && !boosted)
        {
            return false;
        }

        if ((projectedFrameSize > highLimit && qIndex < maximumQIndex) || (projectedFrameSize < lowLimit && qIndex > minimumQIndex))
        {
            return true;
        }

        // The constrained-quality mode recodes an undershoot above its quality level.
        return this.mode == Av1RateControlMode.ConstrainedQuality &&
            qIndex > this.cqLevel &&
            projectedFrameSize < ((long)this.thisFrameTarget * 7) >> 3;
    }

    /// <summary>
    /// Returns the frame size limits inside which a coding of the current frame is kept: the target with the recode
    /// tolerance, at least 100 bits, on either side, below the largest frame size. Reference:
    /// av1_rc_compute_frame_size_bounds() outside AOM_Q.
    /// </summary>
    /// <param name="underShootLimit">Receives the undershoot limit in bits.</param>
    /// <param name="overShootLimit">Receives the overshoot limit in bits.</param>
    private void GetFrameSizeBounds(out int underShootLimit, out int overShootLimit)
    {
        int tolerance = (int)Math.Max(100, (long)this.recodeTolerance * this.thisFrameTarget / 100);
        underShootLimit = Math.Max(this.thisFrameTarget - tolerance, 0);
        overShootLimit = (int)Math.Min((long)this.thisFrameTarget + tolerance, this.maximumFrameBandwidth);
    }

    /// <summary>
    /// Moves the rate correction toward an overshooting coding and returns the quantizer index the rate model picks
    /// for the target, moving the correction again up to ten times while the pick stays below the search floor.
    /// Reference: get_regulated_q_overshoot().
    /// </summary>
    /// <param name="projectedFrameSize">The packed size of the frame in bits.</param>
    /// <param name="qIndex">The quantizer index of the coding.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <returns>The quantizer index.</returns>
    private int GetRegulatedOvershootQIndex(int projectedFrameSize, int qIndex, bool keyFrame)
    {
        int maximumQIndex = Math.Max(this.recodeHighIndex, this.recodeTopIndex);
        this.UpdateRateCorrectionFactor(projectedFrameSize, qIndex, keyFrame);
        int regulated = this.RegulateQuantizer(keyFrame, this.groupFrameIndex, this.recodeBottomIndex, maximumQIndex);
        for (int retries = 0; regulated < this.recodeLowIndex && retries < 10; retries++)
        {
            this.UpdateRateCorrectionFactor(projectedFrameSize, qIndex, keyFrame);
            regulated = this.RegulateQuantizer(keyFrame, this.groupFrameIndex, this.recodeBottomIndex, maximumQIndex);
        }

        return regulated;
    }

    /// <summary>
    /// Moves the rate correction toward an undershooting coding and returns the quantizer index the rate model picks
    /// for the target, moving the correction again up to ten times while the pick stays above the search ceiling.
    /// Reference: get_regulated_q_undershoot().
    /// </summary>
    /// <param name="projectedFrameSize">The packed size of the frame in bits.</param>
    /// <param name="qIndex">The quantizer index of the coding.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <returns>The quantizer index.</returns>
    private int GetRegulatedUndershootQIndex(int projectedFrameSize, int qIndex, bool keyFrame)
    {
        this.UpdateRateCorrectionFactor(projectedFrameSize, qIndex, keyFrame);
        int regulated = this.RegulateQuantizer(keyFrame, this.groupFrameIndex, this.recodeBottomIndex, this.recodeTopIndex);
        for (int retries = 0; regulated > this.recodeHighIndex && retries < 10; retries++)
        {
            this.UpdateRateCorrectionFactor(projectedFrameSize, qIndex, keyFrame);
            regulated = this.RegulateQuantizer(keyFrame, this.groupFrameIndex, this.recodeBottomIndex, this.recodeTopIndex);
        }

        return regulated;
    }

    /// <summary>
    /// Returns the recode tolerance of a good-quality frame size and speed. Reference: the recode_tolerance of
    /// init_hl_sf() and set_good_speed_feature_framesize_dependent() with libavif's 25% under- and overshoot.
    /// </summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="speed">The cpu-used tier.</param>
    /// <returns>The tolerance in percent.</returns>
    private static int GetRecodeTolerance(int width, int height, int speed)
    {
        bool large = Math.Min(width, height) >= 720;
        int tolerance = large ? 25 : 25 + (Math.Min(UnderShootPercentage, OverShootPercentage) >> 2);
        if (speed >= 2 && !large)
        {
            tolerance = 50;
        }

        if (speed >= 4)
        {
            tolerance = large ? 32 : 55;
        }

        if (speed >= 5 && large)
        {
            tolerance = 40;
        }

        if (speed >= 6)
        {
            tolerance = 55;
        }

        return tolerance;
    }
}
