// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// Supplies the reference mapping and refresh decisions of the golden group structure, so the model can simulate the
/// reference slots over the frames of a group. The implementation belongs to the owner of the reference structure;
/// both calls must behave as they do for the real coding of the frame, including the refresh skip lists of the group.
/// </summary>
internal interface IAv1TplReferenceMapper
{
    /// <summary>
    /// Maps the seven named references to slots. Reference: av1_get_ref_frames() without parallel encoding.
    /// </summary>
    /// <param name="pairDisplayOrders">The display order of the frame in each slot, or -1. Reference: disp_order.</param>
    /// <param name="pairPyramidLevels">The pyramid level of the frame in each slot, or -1. Reference: pyr_level.</param>
    /// <param name="displayOrder">The display order of the frame. Reference: cur_frame_disp.</param>
    /// <param name="groupIndex">The group index of the frame. Reference: gf_index.</param>
    /// <param name="remappedSlots">Receives the slot of each named reference, LAST first. Reference: remapped_ref_idx.</param>
    void GetReferenceFrames(
        ReadOnlySpan<int> pairDisplayOrders,
        ReadOnlySpan<int> pairPyramidLevels,
        int displayOrder,
        int groupIndex,
        Span<int> remappedSlots);

    /// <summary>
    /// Chooses the slots the frame refreshes. Reference: av1_get_refresh_frame_flags().
    /// </summary>
    /// <param name="groupIndex">The group index of the frame. Reference: gf_index.</param>
    /// <param name="updateType">The update type of the frame. Reference: frame_update_type.</param>
    /// <param name="showExistingFrame">Whether the frame shows an existing frame. Reference: show_existing_frame.</param>
    /// <param name="displayOrder">The display order of the frame. Reference: cur_disp_order.</param>
    /// <param name="pairDisplayOrders">The display order of the frame in each slot, or -1.</param>
    /// <param name="pairPyramidLevels">The pyramid level of the frame in each slot, or -1.</param>
    /// <returns>The refresh mask, one bit per slot.</returns>
    int GetRefreshFrameFlags(
        int groupIndex,
        Av1FrameUpdateType updateType,
        bool showExistingFrame,
        int displayOrder,
        ReadOnlySpan<int> pairDisplayOrders,
        ReadOnlySpan<int> pairPyramidLevels);
}
