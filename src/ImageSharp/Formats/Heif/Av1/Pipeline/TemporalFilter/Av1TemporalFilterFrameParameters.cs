// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

/// <summary>
/// Holds the group-of-pictures and rate-control state that the temporal filter reads for one filtered frame.
/// </summary>
/// <remarks>
/// libaom filters key frames and alternate references in av1_tf_info_filtering(), while the encoder is positioned
/// at the first frame of the group. Several decisions therefore read the frame being filtered (its gf_frame_index)
/// and others read the frame the encoder is positioned at (cpi->gf_frame_index); the properties say which.
/// </remarks>
internal readonly struct Av1TemporalFilterFrameParameters
{
    /// <summary>
    /// Gets the update type of the filtered frame, gf_group->update_type[gf_frame_index]: key frame, alternate
    /// reference or intermediate alternate reference.
    /// </summary>
    public Av1FrameUpdateType UpdateType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the filtered frame is a key frame, gf_group->frame_type[gf_frame_index].
    /// </summary>
    public bool IsKeyFrame { get; init; }

    /// <summary>
    /// Gets a value indicating whether the filtered frame is a forward key frame. Reference:
    /// av1_gop_check_forward_keyframe().
    /// </summary>
    public bool IsForwardKeyFrame { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame the encoder is positioned at is a key frame,
    /// gf_group->frame_type[cpi->gf_frame_index]. With key frame filtering mode one, a key frame limits the strength
    /// of blocks with a large mean difference, also while the group's alternate reference is filtered.
    /// </summary>
    public bool CurrentFrameIsKeyFrame { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame the encoder is positioned at has the key frame update type,
    /// gf_group->update_type[cpi->gf_frame_index] == KF_UPDATE, which disables the start-SAD row subsampling.
    /// </summary>
    public bool CurrentFrameIsKeyFrameUpdate { get; init; }

    /// <summary>
    /// Gets the number of frames since the last key frame, rc->frames_since_key.
    /// </summary>
    public int FramesSinceKey { get; init; }

    /// <summary>
    /// Gets the number of frames to the next key frame, rc->frames_to_key.
    /// </summary>
    public int FramesToKey { get; init; }

    /// <summary>
    /// Gets the quantizer index that sets the filter strength: rc_cfg.cq_level in constant quality mode, otherwise
    /// p_rc->avg_frame_qindex of the frame type. Reference: get_q().
    /// </summary>
    public int FilterQIndex { get; init; }

    /// <summary>
    /// Gets a value indicating whether eighth-sample motion is allowed, cm->features.allow_high_precision_mv as the
    /// encoder holds it when the filter runs.
    /// </summary>
    public bool AllowHighPrecisionMotion { get; init; }

    /// <summary>
    /// Gets a value indicating whether motion is restricted to whole samples,
    /// cm->features.cur_frame_force_integer_mv.
    /// </summary>
    public bool ForceIntegerMotion { get; init; }

    /// <summary>
    /// Gets the encoder frame border, oxcf->border_in_pixels: the superblock size plus 32 without resizing. The
    /// look-ahead frames must hold at least this many edge-extended samples on every side.
    /// </summary>
    public int BorderInPixels { get; init; }
}
