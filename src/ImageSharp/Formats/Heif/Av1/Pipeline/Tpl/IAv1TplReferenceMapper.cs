// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// Supplies the reference mapping and refresh decisions of the golden group structure. The model uses them to simulate
/// the reference slots over the frames of a group. The owner of the reference structure supplies the implementation.
/// Both calls must behave as they do for the real coding of the frame, including the refresh skip lists of the group.
/// </summary>
internal interface IAv1TplReferenceMapper
{
    /// <summary>
    /// Maps the seven named references to slots, as the single-threaded encoder maps them.
    /// </summary>
    /// <param name="pairDisplayOrders">The display order of the frame in each slot, or -1.</param>
    /// <param name="pairPyramidLevels">The pyramid level of the frame in each slot, or -1.</param>
    /// <param name="displayOrder">The display order of the frame.</param>
    /// <param name="groupIndex">The group index of the frame.</param>
    /// <param name="remappedSlots">Receives the slot of each named reference, LAST first.</param>
    void GetReferenceFrames(
        ReadOnlySpan<int> pairDisplayOrders,
        ReadOnlySpan<int> pairPyramidLevels,
        int displayOrder,
        int groupIndex,
        Span<int> remappedSlots);

    /// <summary>
    /// Chooses the slots that the frame refreshes.
    /// </summary>
    /// <param name="groupIndex">The group index of the frame.</param>
    /// <param name="updateType">The update type of the frame.</param>
    /// <param name="showExistingFrame">Whether the frame shows an existing frame.</param>
    /// <param name="displayOrder">The display order of the frame.</param>
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
