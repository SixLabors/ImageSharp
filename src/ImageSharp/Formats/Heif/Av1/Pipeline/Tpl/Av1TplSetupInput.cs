// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The encoder state that one run of the model reads: the golden group, the look-ahead sources, the reference slots, the
/// rate control values and the speed features in force. The caller fills it before each call of
/// <see cref="Av1TplModel{TSample, TSearchOperator, TSampleOperator}.SetupStatistics"/>. Reference: the AV1_COMP, AV1_PRIMARY
/// and EncodeFrameParams fields that av1_tpl_setup_stats() reads.
/// </summary>
/// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
internal sealed class Av1TplSetupInput<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TplSetupInput{TSample}"/> class.
    /// </summary>
    public Av1TplSetupInput()
    {
        int slots = Av1TplModelConstants.ReferenceFrameSlotCount;
        this.Group = new Av1TplGroup();
        this.Lookahead = new Av1EncoderFrame<TSample>[Av1TplModelConstants.MaximumLagBuffers + 1];
        this.FilteredFrames = new Av1EncoderFrame<TSample>[Av1TplModelConstants.MaximumFrameIndex];
        this.HasFilteredFrame = new bool[Av1TplModelConstants.MaximumFrameIndex];
        this.SlotFrames = new Av1EncoderFrame<TSample>[slots];
        this.SlotBufferIds = new int[slots];
        this.SlotDisplayOrderHints = new int[slots];
        this.PairDisplayOrders = new int[slots];
        this.PairPyramidLevels = new int[slots];
        this.MotionVectorContext = new Av1MotionVectorContext();
    }

    /// <summary>
    /// Gets or sets a value indicating whether the frame that starts the group is a key frame. Reference:
    /// frame_params->frame_type == KEY_FRAME.
    /// </summary>
    public bool IsKeyFrame { get; set; }

    /// <summary>
    /// Gets or sets the display number of the frame that starts the group. Reference: cm->current_frame.frame_number.
    /// </summary>
    public int FrameNumber { get; set; }

    /// <summary>
    /// Gets the golden group. The model writes the update type and quantizer of look-ahead extension entries.
    /// Reference: cpi->ppi->gf_group.
    /// </summary>
    public Av1TplGroup Group { get; }

    /// <summary>
    /// Gets or sets the reference mapping of the group structure.
    /// </summary>
    public IAv1TplReferenceMapper? ReferenceMapper { get; set; }

    /// <summary>
    /// Gets the look-ahead source frames by offset from the frame that starts the group, with borders extended from the
    /// visible size. Reference: the img of av1_lookahead_peek().
    /// </summary>
    public Av1EncoderFrame<TSample>[] Lookahead { get; }

    /// <summary>
    /// Gets or sets the number of frames available in <see cref="Lookahead"/>. Reference: the read_ctx sz of the lookahead.
    /// </summary>
    public int LookaheadCount { get; set; }

    /// <summary>
    /// Gets the temporally filtered source of each group entry that has one, with borders extended.
    /// Reference: av1_tf_info_get_filtered_buf().
    /// </summary>
    public Av1EncoderFrame<TSample>[] FilteredFrames { get; }

    /// <summary>
    /// Gets a value for each group entry indicating whether <see cref="FilteredFrames"/> holds its filtered source.
    /// </summary>
    public bool[] HasFilteredFrame { get; }

    /// <summary>
    /// Gets the reconstruction held by each reference slot. Ignored for a key frame. Reference: cm->ref_frame_map[i]->buf.
    /// </summary>
    public Av1EncoderFrame<TSample>[] SlotFrames { get; }

    /// <summary>
    /// Gets the identity of the buffer in each slot; slots that hold the same buffer share it. Reference: the
    /// RefCntBuffer pointers of cm->ref_frame_map.
    /// </summary>
    public int[] SlotBufferIds { get; }

    /// <summary>
    /// Gets the display order of the frame in each slot. Reference: cm->ref_frame_map[i]->display_order_hint.
    /// </summary>
    public int[] SlotDisplayOrderHints { get; }

    /// <summary>
    /// Gets the display order of the frame in each slot, or -1 for an empty or repeated slot. Reference:
    /// init_ref_map_pair() disp_order.
    /// </summary>
    public int[] PairDisplayOrders { get; }

    /// <summary>
    /// Gets the pyramid level of the frame in each slot, or -1. Reference: init_ref_map_pair() pyr_level.
    /// </summary>
    public int[] PairPyramidLevels { get; }

    /// <summary>
    /// Gets or sets the configured look-ahead depth. Reference: oxcf->gf_cfg.lag_in_frames.
    /// </summary>
    public int LagInFrames { get; set; }

    /// <summary>
    /// Gets or sets the number of frames to the next key frame. Reference: rc->frames_to_key.
    /// </summary>
    public int FramesToKey { get; set; }

    /// <summary>
    /// Gets or sets the golden interval of the group. Reference: p_rc->baseline_gf_interval.
    /// </summary>
    public int BaselineGoldenInterval { get; set; }

    /// <summary>
    /// Gets or sets the golden boost in force when the model runs. Reference: p_rc->gfu_boost.
    /// </summary>
    public int GoldenBoost { get; set; }

    /// <summary>
    /// Gets or sets the lowest quantizer index of the rate control. Reference: rc->best_quality.
    /// </summary>
    public int BestQuality { get; set; }

    /// <summary>
    /// Gets or sets the highest quantizer index of the rate control. Reference: rc->worst_quality.
    /// </summary>
    public int WorstQuality { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rate control is in constant quality mode, where the leaf quantizer
    /// is lowered for the model. Reference: rc_cfg.mode == AOM_Q or AOM_VBR.
    /// </summary>
    public bool AdjustLeafQuantizer { get; set; } = true;

    /// <summary>
    /// Gets or sets the model speed features.
    /// </summary>
    public Av1TplSpeedFeatures SpeedFeatures { get; set; }

    /// <summary>
    /// Gets or sets the frame motion search features the model reuses: mesh patterns and thresholds, downsampled
    /// absolute differences, the fractional search method and its iterations and taps. The model runs before the
    /// frame sets its own speed features, so these are the features of the frame coded before it, except for the
    /// first key frame, whose features are set first. The exhaustive search threshold drops to its screen-content
    /// value when that earlier frame was classified as graphics or animation from its first-pass statistics, which
    /// changes the vectors of the eight-point search at speeds 0 and 1. Reference: cpi->sf.mv_sf with
    /// exhaustive_searches_thresh from cpi->twopass_frame.fr_content_type.
    /// </summary>
    public Av1MotionSearchSettings MotionSettings { get; set; }

    /// <summary>
    /// Gets the entropy context whose motion vector distributions price the model search. For a key frame the model
    /// resets it to the defaults. Reference: cm->fc->nmvc.
    /// </summary>
    public Av1MotionVectorContext MotionVectorContext { get; }

    /// <summary>
    /// Gets or sets a value indicating whether eighth-sample vectors are allowed, as the most recently coded inter frame
    /// left the flag. Reference: cm->features.allow_high_precision_mv.
    /// </summary>
    public bool AllowHighPrecisionMotionVector { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether integer vectors are forced, as the previous frame left the flag.
    /// Reference: cm->features.cur_frame_force_integer_mv.
    /// </summary>
    public bool ForceIntegerMotionVector { get; set; }

    /// <summary>
    /// Gets or sets the superblock quantizer delta that the model's quantizers add to its quantizer: the delta of the
    /// last superblock when the previous coded frame codes delta quantizers, and zero otherwise. Reference:
    /// x->delta_qindex with cm->delta_q_info.delta_q_present_flag in the av1_init_plane_quantizers() call of
    /// av1_frame_init_quantizer().
    /// </summary>
    public int QuantizerDeltaQIndex { get; set; }

    /// <summary>
    /// Gets or sets the mode-information row that ends the first tile of the frame. Reference: xd->tile.mi_row_end
    /// after av1_tile_init(&amp;xd->tile, cm, 0, 0).
    /// </summary>
    public int TileModeInfoRowEnd { get; set; }

    /// <summary>
    /// Gets or sets the mode-information column that ends the first tile of the frame. Reference:
    /// xd->tile.mi_col_end after av1_tile_init(&amp;xd->tile, cm, 0, 0).
    /// </summary>
    public int TileModeInfoColumnEnd { get; set; }

    /// <summary>
    /// Gets or sets the tune metric. Reference: oxcf->tune_cfg.tuning.
    /// </summary>
    public Av1Tuning Tuning { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the encoder consumes first-pass statistics, which applies the layer and
    /// boost adjustments of the rate multiplier. One-pass good quality with look-ahead does. Reference:
    /// is_stat_consumption_stage().
    /// </summary>
    public bool IsStatConsumptionStage { get; set; } = true;

    /// <summary>
    /// Gets or sets the fixed quantizer offset mode. Reference: q_cfg.use_fixed_qp_offsets.
    /// </summary>
    public int UseFixedQpOffsets { get; set; }

    /// <summary>
    /// Gets or sets the superblock size of the sequence. Reference: seq_params->sb_size.
    /// </summary>
    public Av1BlockSize SuperblockSize { get; set; } = Av1BlockSize.Block128x128;

    /// <summary>
    /// Gets or sets a value indicating whether the sequence enables the intra edge filter. Reference:
    /// seq_params->enable_intra_edge_filter.
    /// </summary>
    public bool EnableIntraEdgeFilter { get; set; } = true;
}
