// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// The frame-level decisions of one coded frame of a look-ahead sequence: its role in the golden frame group, the
/// look-ahead frame it codes, whether it is shown, and the display order it carries. Reference: the fields of
/// EncodeFrameParams and GF_GROUP that av1_encode_strategy() hands to av1_encode().
/// </summary>
internal readonly struct Av1SecondPassFrame
{
    /// <summary>
    /// Gets the frame's index in its golden frame group. Reference: gf_frame_index.
    /// </summary>
    public int GroupIndex { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame is the first of a new golden frame group, so that the temporal
    /// filter and the temporal dependency model run for the group before it is coded.
    /// </summary>
    public bool StartsGroup { get; init; }

    /// <summary>
    /// Gets the update role of the frame. Reference: update_type.
    /// </summary>
    public Av1FrameUpdateType UpdateType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame is a key frame. Reference: frame_type.
    /// </summary>
    public bool IsKeyFrame { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame refreshes every reference slot. Reference: refbuf_state.
    /// </summary>
    public bool ResetsReferences { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame is displayed. Reference: show_frame.
    /// </summary>
    public bool ShowFrame { get; init; }

    /// <summary>
    /// Gets a value indicating whether a hidden frame may be shown later. The temporal filter clears it for an
    /// alternate reference whose overlay will be coded. Reference: showable_frame of choose_frame_source().
    /// </summary>
    public bool ShowableFrame { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame repeats the reference slot that holds its display position.
    /// Reference: show_existing_frame.
    /// </summary>
    public bool ShowExistingFrame { get; init; }

    /// <summary>
    /// Gets the offset of the frame's source from the first frame of the look-ahead that is not yet shown.
    /// Reference: arf_src_offset.
    /// </summary>
    public int SourceOffset { get; init; }

    /// <summary>
    /// Gets the display position of the frame since the last key frame, which is also the order hint before the
    /// modulo of the order hint bits. Reference: display_order_hint.
    /// </summary>
    public int DisplayOrder { get; init; }

    /// <summary>
    /// Gets the number of frames shown since the last key frame when the frame begins, before a key frame that resets
    /// the references restarts the count. The temporal dependency model runs at that point. Reference:
    /// current_frame.frame_number in av1_tpl_setup_stats().
    /// </summary>
    public int FrameNumber { get; init; }

    /// <summary>
    /// Gets the pyramid layer of the frame. Reference: layer_depth.
    /// </summary>
    public int LayerDepth { get; init; }

    /// <summary>
    /// Gets the deepest pyramid layer of the group. Reference: max_layer_depth.
    /// </summary>
    public int MaxLayerDepth { get; init; }

    /// <summary>
    /// Gets the pyramid level that ranks the frame for reference mapping. Reference: pyramid_level from
    /// get_true_pyr_level().
    /// </summary>
    public int PyramidLevel { get; init; }

    /// <summary>
    /// Gets the boost of the frame. Reference: arf_boost.
    /// </summary>
    public int ArfBoost { get; init; }

    /// <summary>
    /// Gets a value indicating whether the group length test already ran the temporal dependency model on this
    /// group with its final length, so that its statistics are reused. Reference: skip_tpl_setup_stats.
    /// </summary>
    public bool ReusesTplStatistics { get; init; }

    /// <summary>
    /// Gets a value indicating whether the first pass classed the source as animation or graphics.
    /// Reference: fr_content_type equal to FC_GRAPHICS_ANIMATION.
    /// </summary>
    public bool IsGraphicsAnimation { get; init; }

    /// <summary>
    /// Gets the log of the source's first-pass intra error. Reference: mb_av_energy.
    /// </summary>
    public double MacroblockAverageEnergy { get; init; }

    /// <summary>
    /// Gets the log of the source's first-pass wavelet energy, or 0 when the look-ahead has no valid energy.
    /// Reference: frame_avg_haar_energy.
    /// </summary>
    public double FrameAverageHaarEnergy { get; init; }
}
