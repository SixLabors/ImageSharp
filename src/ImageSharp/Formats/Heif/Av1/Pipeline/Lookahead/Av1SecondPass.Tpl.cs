// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <content>
/// Exposes the rate-control state that the temporal dependency model reads when it measures a golden group.
/// </content>
internal sealed partial class Av1SecondPass
{
    /// <summary>
    /// Gets the number of frames shown since the last key frame. Reference: current_frame.frame_number.
    /// </summary>
    public int FrameNumber => this.frameNumber;

    /// <summary>
    /// Gets the effective look-ahead depth, which raises a request of 32 to 38 frames to 39. Reference:
    /// gf_cfg->lag_in_frames after set_encoder_config().
    /// </summary>
    public int LagInFrames => this.lagInFrames;

    /// <summary>
    /// Gets the golden interval of the current group. Reference: p_rc->baseline_gf_interval.
    /// </summary>
    public int BaselineGoldenInterval => this.baselineGoldenInterval;

    /// <summary>
    /// Gets the lowest allowed quantizer index. Reference: rc->best_quality.
    /// </summary>
    public int BestQuality => this.bestQuality;

    /// <summary>
    /// Gets the highest allowed quantizer index. Reference: rc->worst_quality.
    /// </summary>
    public int WorstQuality => this.worstQuality;

    /// <summary>
    /// Attaches the temporal dependency test of long golden intervals, once the lookahead it reads exists.
    /// </summary>
    /// <param name="evaluator">The test.</param>
    public void AttachGopLengthEvaluator(IGopLengthEvaluator evaluator) => this.gopLengthEvaluator = evaluator;

    /// <summary>
    /// Records the screen content type that the quantizer choices read, which an intra frame decides before its
    /// filtering and its temporal dependency model. Reference: cpi->is_screen_content_type, as
    /// av1_set_screen_content_options() sets it in av1_encode_strategy().
    /// </summary>
    /// <param name="screenContent">Whether the frames are classified as screen content.</param>
    public void SetScreenContentType(bool screenContent) => this.screenContentType = screenContent;

    /// <summary>
    /// Estimates the quantizer of every frame of the group from the current frame on, which the model codes each
    /// frame at. The group length test reads the screen content type of the last coded frame, the coding run that
    /// of the current frame. Reference: av1_tpl_preload_rc_estimate().
    /// </summary>
    public void PreloadTplQuantizers()
    {
        for (int index = this.groupFrameIndex; index < this.group.Size; ++index)
        {
            this.group.QValues[index] = this.PickQIndex(index, this.screenContentType);
        }
    }
}
