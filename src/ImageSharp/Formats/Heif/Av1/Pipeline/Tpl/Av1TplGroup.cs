// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The golden group entries that the temporal dependency model reads and writes. The arrays keep entries past
/// <see cref="Size"/>: the model reads the layer depth of those look-ahead extension frames as the group arrays hold it
/// (stale values of earlier groups, or zero), and writes their update type and quantizer. The owner of the group
/// structure must mirror those entries in both directions. Reference: GF_GROUP.
/// </summary>
internal sealed class Av1TplGroup
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TplGroup"/> class.
    /// </summary>
    public Av1TplGroup()
    {
        int capacity = Av1TplModelConstants.MaximumFrameIndex;
        this.UpdateType = new Av1FrameUpdateType[capacity];
        this.IsKeyFrame = new bool[capacity];
        this.LayerDepth = new int[capacity];
        this.ArfSourceOffset = new int[capacity];
        this.CurrentFrameIndex = new int[capacity];
        this.DisplayIndex = new int[capacity];
        this.QIndex = new int[capacity];
        this.IsNonReference = new bool[capacity];
    }

    /// <summary>
    /// Gets or sets the number of frames in the group. Reference: size.
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// Gets or sets the largest layer depth of the group. Reference: max_layer_depth.
    /// </summary>
    public int MaximumLayerDepth { get; set; }

    /// <summary>
    /// Gets or sets the largest layer depth the group may use. Reference: max_layer_depth_allowed.
    /// </summary>
    public int MaximumLayerDepthAllowed { get; set; }

    /// <summary>
    /// Gets or sets the group index of the base-layer alternate reference, or -1. Reference: arf_index.
    /// </summary>
    public int ArfIndex { get; set; }

    /// <summary>
    /// Gets the update type of each entry. Reference: update_type.
    /// </summary>
    public Av1FrameUpdateType[] UpdateType { get; }

    /// <summary>
    /// Gets a value for each entry indicating whether it is a key frame. Reference: frame_type.
    /// </summary>
    public bool[] IsKeyFrame { get; }

    /// <summary>
    /// Gets the layer depth of each entry. Reference: layer_depth.
    /// </summary>
    public int[] LayerDepth { get; }

    /// <summary>
    /// Gets the distance of each entry's source from its coding position. Reference: arf_src_offset.
    /// </summary>
    public int[] ArfSourceOffset { get; }

    /// <summary>
    /// Gets the look-ahead index of each entry's coding position. Reference: cur_frame_idx.
    /// </summary>
    public int[] CurrentFrameIndex { get; }

    /// <summary>
    /// Gets the display index of each entry within the key frame interval. Reference: display_idx.
    /// </summary>
    public int[] DisplayIndex { get; }

    /// <summary>
    /// Gets the quantizer index of each entry, as av1_tpl_preload_rc_estimate() chose it. Reference: q_val.
    /// </summary>
    public int[] QIndex { get; }

    /// <summary>
    /// Gets a value for each entry indicating whether no later frame references it. Reference: is_frame_non_ref.
    /// </summary>
    public bool[] IsNonReference { get; }

    /// <summary>
    /// Returns whether the model supplies statistics for an entry: alternate references, golden frames and key frames.
    /// Reference: is_frame_tpl_eligible().
    /// </summary>
    /// <param name="index">The group index.</param>
    /// <returns><see langword="true"/> when the entry is eligible.</returns>
    public bool IsTplEligible(int index)
        => this.UpdateType[index] is Av1FrameUpdateType.Alternate or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Key;
}
