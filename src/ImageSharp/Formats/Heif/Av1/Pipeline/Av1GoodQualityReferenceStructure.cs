// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Chooses the reference slots, the refreshed slots, and the primary reference of good-quality frames coded without
/// lookahead. A golden group holds up to 32 frames without an alternate reference, and the frames of a group form
/// a low-delay pyramid of layer depths that ranks the frames for reference mapping. Reference: the no-stats path of
/// av1_get_second_pass_params(), with define_gf_group_pass0(), set_ld_layer_depth(), av1_get_ref_frames(),
/// av1_get_refresh_frame_flags(), choose_primary_ref_frame(), and update_fb_of_context_type().
/// </summary>
internal sealed class Av1GoodQualityReferenceStructure
{
    /// <summary>
    /// The number of reference buffer slots. Reference: REF_FRAMES.
    /// </summary>
    private const int SlotCount = Av1Constants.ReferenceFrameCount;

    /// <summary>
    /// The number of named references. Reference: INTER_REFS_PER_FRAME.
    /// </summary>
    private const int ReferenceCount = Av1Constants.ReferencesPerFrame;

    /// <summary>
    /// The largest layer depth of a pyramid. Reference: MAX_ARF_LAYERS.
    /// </summary>
    private const int MaximumLayers = 6;

    /// <summary>
    /// The lowest pyramid level that reference mapping uses. Reference: MIN_PYR_LEVEL.
    /// </summary>
    private const int MinimumPyramidLevel = 1;

    /// <summary>
    /// The golden interval of lag-0 coding. get_default_max_gf_interval() never returns less than
    /// MAX_GF_INTERVAL, and av1_get_second_pass_params() uses it as the group length without lookahead.
    /// </summary>
    private const int MaximumGoldenInterval = 32;

    /// <summary>
    /// The unassigned slot marker. Reference: INVALID_IDX.
    /// </summary>
    private const int InvalidIndex = -1;

    /// <summary>
    /// The display order of the frame in each slot, or -1 for an empty slot. Reference: the display_order_hint
    /// of cm->ref_frame_map.
    /// </summary>
    private readonly int[] slotDisplayOrder = [-1, -1, -1, -1, -1, -1, -1, -1];

    /// <summary>
    /// The pyramid level of the frame in each slot. Reference: the pyramid_level of cm->ref_frame_map.
    /// </summary>
    private readonly int[] slotPyramidLevel = new int[SlotCount];

    /// <summary>
    /// The identity of the buffer in each slot. A key frame stores one buffer in every slot. Reference: the
    /// RefCntBuffer pointers of cm->ref_frame_map.
    /// </summary>
    private readonly int[] slotBuffer = [-1, -1, -1, -1, -1, -1, -1, -1];

    /// <summary>
    /// The slot that holds the most recent frame of each reference type. Reference: ppi->fb_of_context_type.
    /// </summary>
    private readonly int[] contextTypeSlots = [-1, -1, -1, -1, -1, -1, -1, -1];

    /// <summary>
    /// The slot of each named reference of the last frame that mapped its references, LAST first. A frame whose
    /// external flags request a refresh keeps this map. Reference: cm->remapped_ref_idx.
    /// </summary>
    private readonly int[] remappedSlots = new int[ReferenceCount];

    /// <summary>
    /// The frame's index in its golden group. Reference: cpi->gf_frame_index.
    /// </summary>
    private int groupIndex;

    /// <summary>
    /// The number of frames in the current golden group. Reference: gf_group->size.
    /// </summary>
    private int groupLength;

    /// <summary>
    /// The layer depth of the current frame. Reference: gf_group->layer_depth[cpi->gf_frame_index].
    /// </summary>
    private int layerDepth;

    /// <summary>
    /// The pyramid level of the current frame. Reference: cm->current_frame.pyramid_level.
    /// </summary>
    private int pyramidLevel;

    /// <summary>
    /// The display order of the current frame. Reference: cm->current_frame.display_order_hint.
    /// </summary>
    private int displayOrder;

    /// <summary>
    /// The identity of the next coded buffer.
    /// </summary>
    private int nextBuffer;

    /// <summary>
    /// Gets the update type of the current frame. Reference: gf_group->update_type[cpi->gf_frame_index].
    /// </summary>
    public Av1FrameUpdateType UpdateType { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the current golden group ends at the next key frame. Reference:
    /// p_rc->constrained_gf_group.
    /// </summary>
    public bool IsConstrainedGroup { get; private set; }

    /// <summary>
    /// Gets the pyramid level of the frame being coded. Reference: cm->cur_frame->pyramid_level.
    /// </summary>
    public int PyramidLevel => this.pyramidLevel;

    /// <summary>
    /// Gets the pyramid level of the frame in each reference slot. Reference: the pyramid_level of each RefCntBuffer.
    /// </summary>
    public ReadOnlySpan<int> SlotPyramidLevels => this.slotPyramidLevel;

    /// <summary>
    /// Chooses the golden-group position, the reference slots, the refreshed slots, and the primary reference of a
    /// frame.
    /// </summary>
    /// <param name="frameHeader">The frame header that receives the slots and the primary reference.</param>
    /// <param name="framesSinceKey">The number of frames since the last key frame. Reference: frame_number.</param>
    /// <param name="framesToKey">The number of frames left before the next key frame. Reference: rc->frames_to_key.</param>
    /// <param name="usesLayerFlags">
    /// Whether the frame is a layer after the first. Its flags refresh only LAST, so the frame keeps the reference map
    /// of the frame before it and refreshes the slot of LAST. Reference: the update_pending tests of
    /// av1_encode_strategy() and av1_get_refresh_frame_flags(), with the ext_refresh_frame_flags that
    /// av1_apply_encoding_flags() sets for AOM_EFLAG_NO_UPD_GF and AOM_EFLAG_NO_UPD_ARF.
    /// </param>
    public void Configure(ObuFrameHeader frameHeader, int framesSinceKey, int framesToKey, bool usesLayerFlags = false)
    {
        bool keyFrame = frameHeader.FrameType == ObuFrameType.KeyFrame;
        this.displayOrder = framesSinceKey;

        // define_gf_group_pass0(): a new group starts at a key frame and after the last frame of a group. Its
        // length is the lag-0 golden interval, bounded by the frames left before the next key frame. A group that
        // reaches the next key frame is constrained.
        if (keyFrame || this.groupIndex == this.groupLength)
        {
            this.groupIndex = 0;
            this.groupLength = Math.Min(MaximumGoldenInterval, framesToKey);
            this.IsConstrainedGroup = this.groupLength >= framesToKey;
        }

        this.UpdateType = keyFrame
            ? Av1FrameUpdateType.Key
            : this.groupIndex == 0 ? Av1FrameUpdateType.Golden : Av1FrameUpdateType.Last;

        this.layerDepth = GetLayerDepth(this.groupLength, this.groupIndex, out int maximumLayerDepth);
        this.pyramidLevel = GetTruePyramidLevel(this.layerDepth, this.displayOrder, maximumLayerDepth);

        Span<int> pairOrder = stackalloc int[SlotCount];
        Span<int> pairLevel = stackalloc int[SlotCount];
        this.InitializeReferenceMapPairs(keyFrame, pairOrder, pairLevel);

        Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        if (usesLayerFlags && !keyFrame)
        {
            // A pending external refresh maps no references, so the frame keeps the map of the frame before it, and
            // only the LAST refresh of the external flags is set.
            for (int reference = 0; reference < ReferenceCount; reference++)
            {
                referenceFrameIndices[reference] = (uint)this.remappedSlots[reference];
            }

            frameHeader.RefreshFrameFlags = 1U << this.remappedSlots[(int)Av1ReferenceFrameType.Last - 1];
        }
        else
        {
            Span<int> remapped = stackalloc int[SlotCount];
            GetReferenceFrames(pairOrder, pairLevel, this.displayOrder, remapped);
            for (int reference = 0; reference < ReferenceCount; reference++)
            {
                referenceFrameIndices[reference] = (uint)remapped[reference];
                this.remappedSlots[reference] = remapped[reference];
            }

            frameHeader.RefreshFrameFlags = keyFrame ? byte.MaxValue : GetRefreshFrameFlags(pairOrder, pairLevel, this.displayOrder);
        }

        frameHeader.PrimaryReferenceFrame = this.ChoosePrimaryReferenceFrame(frameHeader);
    }

    /// <summary>
    /// Chooses the reference slots, the refreshed slots, and the primary reference of a frame of a lookahead golden
    /// group. Reference: the reference setup of av1_encode_strategy(), with av1_get_ref_frames(),
    /// av1_get_refresh_frame_flags(), and choose_primary_ref_frame().
    /// </summary>
    /// <param name="frameHeader">The frame header that receives the slots and the primary reference.</param>
    /// <param name="frame">The role of the frame in its golden group.</param>
    /// <param name="skipFrameRefresh">The display orders that the frame must not replace. Reference:
    /// gf_group->skip_frame_refresh[gf_index].</param>
    public void ConfigureLagged(ObuFrameHeader frameHeader, in GroupFrame frame, ReadOnlySpan<int> skipFrameRefresh)
    {
        bool keyFrame = frameHeader.FrameType == ObuFrameType.KeyFrame;
        this.displayOrder = frame.DisplayOrder;
        this.UpdateType = frame.UpdateType;
        this.layerDepth = frame.LayerDepth;
        this.pyramidLevel = GetTruePyramidLevel(this.layerDepth, this.displayOrder, frame.MaximumLayerDepth);

        Span<int> pairOrder = stackalloc int[SlotCount];
        Span<int> pairLevel = stackalloc int[SlotCount];
        this.InitializeReferenceMapPairs(keyFrame, pairOrder, pairLevel);

        Span<int> remapped = stackalloc int[SlotCount];
        GetReferenceFrames(pairOrder, pairLevel, this.displayOrder, remapped);
        Span<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        for (int reference = 0; reference < ReferenceCount; reference++)
        {
            referenceFrameIndices[reference] = (uint)remapped[reference];
            this.remappedSlots[reference] = remapped[reference];
        }

        frameHeader.RefreshFrameFlags = frame.IsNonReference
            ? 0
            : GetLaggedRefreshFrameFlags(pairOrder, pairLevel, this.displayOrder, in frame, skipFrameRefresh);

        frameHeader.PrimaryReferenceFrame = frame.IsNonReference
            ? Av1Constants.PrimaryReferenceFrameNone
            : this.ChoosePrimaryReferenceFrame(frameHeader);
    }

    /// <summary>
    /// Fills the display order and pyramid level of the frame in each slot, as the frame about to be coded sees them.
    /// Reference: init_ref_map_pair().
    /// </summary>
    /// <param name="keyFrame">Whether the frame is a key frame, which sees no references.</param>
    /// <param name="pairOrder">Receives the display order of each slot, or -1.</param>
    /// <param name="pairLevel">Receives the pyramid level of each slot, or -1.</param>
    public void GetReferenceMapPairs(bool keyFrame, Span<int> pairOrder, Span<int> pairLevel)
        => this.InitializeReferenceMapPairs(keyFrame, pairOrder, pairLevel);

    /// <summary>
    /// Maps the named references of a frame to slots. Reference: av1_get_ref_frames().
    /// </summary>
    /// <param name="pairOrder">The display order of each slot, or -1.</param>
    /// <param name="pairLevel">The pyramid level of each slot, or -1.</param>
    /// <param name="displayOrder">The display order of the frame.</param>
    /// <param name="remapped">Receives the slot of each named reference, LAST first.</param>
    public static void MapReferences(ReadOnlySpan<int> pairOrder, ReadOnlySpan<int> pairLevel, int displayOrder, Span<int> remapped)
        => GetReferenceFrames(pairOrder, pairLevel, displayOrder, remapped);

    /// <summary>
    /// Chooses the slots a frame of a lookahead golden group refreshes. Reference: av1_get_refresh_frame_flags().
    /// </summary>
    /// <param name="pairOrder">The display order of each slot, or -1.</param>
    /// <param name="pairLevel">The pyramid level of each slot, or -1.</param>
    /// <param name="frame">The role of the frame in its golden group.</param>
    /// <returns>The refresh mask, one bit per slot.</returns>
    public static int GetRefreshFlags(ReadOnlySpan<int> pairOrder, ReadOnlySpan<int> pairLevel, in GroupFrame frame)
        => frame.IsNonReference ? 0 : (int)GetLaggedRefreshFrameFlags(pairOrder, pairLevel, frame.DisplayOrder, in frame, []);

    /// <summary>
    /// Returns the display order of the frame in a slot, or -1 for an empty slot.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The display order.</returns>
    public int GetSlotDisplayOrder(int slot) => this.slotDisplayOrder[slot];

    /// <summary>
    /// Returns the slot of the frame that a show-existing frame displays. Reference: the existing_fb_idx_to_show
    /// search of av1_encode_strategy().
    /// </summary>
    /// <param name="displayOrder">The display order of the frame to show.</param>
    /// <returns>The last slot that holds the frame, or -1.</returns>
    public int GetExistingFrameSlot(int displayOrder)
    {
        int slot = InvalidIndex;
        for (int index = 0; index < SlotCount; index++)
        {
            if (this.slotBuffer[index] >= 0 && this.slotDisplayOrder[index] == displayOrder)
            {
                slot = index;
            }
        }

        return slot;
    }

    /// <summary>
    /// Records the coded frame in its refreshed slots and advances the golden group. Reference: the
    /// ref_frame_map update of av1_update_ref_frame_map() and update_fb_of_context_type().
    /// </summary>
    /// <param name="frameHeader">The header of the coded frame.</param>
    public void Complete(ObuFrameHeader frameHeader)
    {
        int referenceType = GetReferenceType(this.layerDepth);
        if (frameHeader.IsIntra || frameHeader.ErrorResilientMode)
        {
            this.contextTypeSlots.AsSpan().Fill(-1);

            // A shown intra frame records the slot of GOLDEN and a hidden one the slot of ALTREF. A key frame maps
            // every reference to slot 0.
            Av1ReferenceFrameType recorded = frameHeader.ShowFrame ? Av1ReferenceFrameType.Golden : Av1ReferenceFrameType.Alternate;
            this.contextTypeSlots[referenceType] = (int)frameHeader.GetReferenceFrameIndices()[(int)recorded - 1];
        }

        if (frameHeader.FrameType == ObuFrameType.KeyFrame)
        {
            // Every slot is refreshed, so slot 0 lives as long as any.
            this.contextTypeSlots[referenceType] = 0;
        }
        else
        {
            // The first refreshed slot. A frame that refreshes no slot keeps the previous one.
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if ((frameHeader.RefreshFrameFlags & (1U << slot)) != 0)
                {
                    this.contextTypeSlots[referenceType] = slot;
                    break;
                }
            }
        }

        int buffer = this.nextBuffer++;
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if ((frameHeader.RefreshFrameFlags & (1U << slot)) != 0)
            {
                this.slotDisplayOrder[slot] = this.displayOrder;
                this.slotPyramidLevel[slot] = this.pyramidLevel;
                this.slotBuffer[slot] = buffer;
            }
        }

        this.groupIndex++;
    }

    /// <summary>
    /// Gets the layer depth of a frame in a low-delay pyramid. The depth falls by one for each trailing zero bit of
    /// the frame's index. Reference: set_ld_layer_depth().
    /// </summary>
    /// <param name="groupLength">The number of frames in the group.</param>
    /// <param name="index">The frame's index in the group.</param>
    /// <param name="maximumLayerDepth">Receives the largest depth of the group. Reference: max_layer_depth.</param>
    /// <returns>The layer depth.</returns>
    private static int GetLayerDepth(int groupLength, int index, out int maximumLayerDepth)
    {
        int logLength = 0;
        while ((1 << logLength) < groupLength)
        {
            logLength++;
        }

        int count = 0;
        for (; count < MaximumLayers; count++)
        {
            if (((index >> count) & 1) != 0)
            {
                break;
            }
        }

        maximumLayerDepth = Math.Min(logLength, MaximumLayers);
        return Math.Max(logLength - count, 0);
    }

    /// <summary>
    /// Gets the pyramid level that ranks a frame for reference mapping. Reference: get_true_pyr_level().
    /// </summary>
    private static int GetTruePyramidLevel(int frameLevel, int frameOrder, int maximumLayerDepth)
    {
        if (frameOrder == 0)
        {
            return MinimumPyramidLevel;
        }

        if (frameLevel == MaximumLayers)
        {
            return maximumLayerDepth;
        }

        return frameLevel == MaximumLayers + 1 ? MinimumPyramidLevel : Math.Max(MinimumPyramidLevel, frameLevel);
    }

    /// <summary>
    /// Gets the reference type that selects the primary reference context. Reference: get_current_frame_ref_type().
    /// </summary>
    private static int GetReferenceType(int layerDepth) => layerDepth switch
    {
        0 => 0,
        1 => 1,
        MaximumLayers or MaximumLayers + 1 => 4,
        _ => 7
    };

    /// <summary>
    /// Records the display order and pyramid level of each slot. A key frame empties every slot, and a buffer that
    /// several slots hold counts once, in its first slot. Reference: init_ref_map_pair().
    /// </summary>
    private void InitializeReferenceMapPairs(bool keyFrame, Span<int> pairOrder, Span<int> pairLevel)
    {
        if (keyFrame)
        {
            pairOrder.Fill(-1);
            pairLevel.Fill(-1);
            return;
        }

        pairOrder.Clear();
        pairLevel.Clear();
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (pairOrder[slot] == -1)
            {
                continue;
            }

            int buffer = this.slotBuffer[slot];
            if (buffer < 0)
            {
                pairOrder[slot] = -1;
                pairLevel[slot] = -1;
                continue;
            }

            for (int later = slot + 1; later < SlotCount; later++)
            {
                if (this.slotBuffer[later] == buffer)
                {
                    pairOrder[later] = -1;
                    pairLevel[later] = -1;
                }
            }

            pairOrder[slot] = this.slotDisplayOrder[slot];
            pairLevel[slot] = this.slotPyramidLevel[slot];
        }
    }

    /// <summary>
    /// Maps the named references to slots. GOLDEN takes the newest past frame at the lowest pyramid level, LAST to
    /// LAST3 take the remaining past frames newest first, and every empty reference falls back to slot 0.
    /// Reference: av1_get_ref_frames() without external maps or parallel coding.
    /// </summary>
    private static void GetReferenceFrames(ReadOnlySpan<int> pairOrder, ReadOnlySpan<int> pairLevel, int currentOrder, Span<int> remapped)
    {
        remapped.Fill(InvalidIndex);

        // Collect the distinct buffers in ascending display order.
        Span<int> mapSlot = stackalloc int[SlotCount];
        Span<int> mapOrder = stackalloc int[SlotCount];
        Span<int> mapLevel = stackalloc int[SlotCount];
        Span<bool> used = stackalloc bool[SlotCount];
        int bufferCount = 0;
        int minimumLevel = MaximumLayers;
        for (int slot = 0; slot < SlotCount; slot++)
        {
            int order = pairOrder[slot];
            if (order == -1 || mapOrder[..bufferCount].Contains(order))
            {
                continue;
            }

            minimumLevel = Math.Min(minimumLevel, pairLevel[slot]);

            // compare_map_idx_pair_asc() is a stable insertion by display order for the distinct orders here.
            int position = bufferCount;
            while (position > 0 && mapOrder[position - 1] > order)
            {
                mapSlot[position] = mapSlot[position - 1];
                mapOrder[position] = mapOrder[position - 1];
                mapLevel[position] = mapLevel[position - 1];
                position--;
            }

            mapSlot[position] = slot;
            mapOrder[position] = order;
            mapLevel[position] = pairLevel[slot];
            bufferCount++;
        }

        int minimumLevelCount = 0;
        int closestPast = -1;
        int golden = -1;
        int alternate = -1;
        for (int i = bufferCount - 1; i >= 0; i--)
        {
            if (mapLevel[i] == minimumLevel)
            {
                minimumLevelCount++;
                if (mapOrder[i] < currentOrder && golden == -1 && remapped[(int)Av1ReferenceFrameType.Golden - 1] == InvalidIndex)
                {
                    golden = i;
                }
                else if (mapOrder[i] > currentOrder && alternate == -1 && remapped[(int)Av1ReferenceFrameType.Alternate - 1] == InvalidIndex)
                {
                    alternate = i;
                }
            }
            else if (mapOrder[i] == currentOrder)
            {
                AddToSlot(mapSlot, used, i, remapped, Av1ReferenceFrameType.Backward);
            }

            if (mapOrder[i] < currentOrder && closestPast < 0)
            {
                closestPast = i;
            }
        }

        // GOLDEN and ALTREF follow the pyramid level only while the levels differ.
        if (minimumLevelCount < bufferCount)
        {
            if (golden > -1)
            {
                AddToSlot(mapSlot, used, golden, remapped, Av1ReferenceFrameType.Golden);
            }

            if (alternate > -1)
            {
                AddToSlot(mapSlot, used, alternate, remapped, Av1ReferenceFrameType.Alternate);
            }
        }

        SetUnmappedReference(mapOrder[..bufferCount], mapLevel[..bufferCount], used, minimumLevelCount, minimumLevel, currentOrder);

        // Past frames fill LAST, LAST2, and LAST3 in decreasing display order.
        for (Av1ReferenceFrameType frame = Av1ReferenceFrameType.Last; frame < Av1ReferenceFrameType.Golden; frame++)
        {
            if (remapped[(int)frame - 1] != InvalidIndex)
            {
                continue;
            }

            int next = -1;
            int nextOrder = int.MinValue;
            for (int i = bufferCount - 1; i >= 0; i--)
            {
                if (!used[i] && mapOrder[i] < currentOrder && mapOrder[i] > nextOrder)
                {
                    nextOrder = mapOrder[i];
                    next = i;
                }
            }

            if (next < 0)
            {
                break;
            }

            AddToSlot(mapSlot, used, next, remapped, frame);
        }

        // Future frames fill BWDREF and ALTREF2 in increasing display order.
        for (Av1ReferenceFrameType frame = Av1ReferenceFrameType.Backward; frame <= Av1ReferenceFrameType.Alternate; frame++)
        {
            if (remapped[(int)frame - 1] != InvalidIndex)
            {
                continue;
            }

            int next = -1;
            int nextOrder = int.MaxValue;
            for (int i = bufferCount - 1; i >= 0; i--)
            {
                if (!used[i] && mapOrder[i] > currentOrder && mapOrder[i] < nextOrder)
                {
                    nextOrder = mapOrder[i];
                    next = i;
                }
            }

            if (next < 0)
            {
                break;
            }

            AddToSlot(mapSlot, used, next, remapped, frame);
        }

        // The remaining past frames take the first empty references.
        int index = closestPast;
        for (Av1ReferenceFrameType frame = Av1ReferenceFrameType.Last; frame <= Av1ReferenceFrameType.Alternate; frame++)
        {
            if (remapped[(int)frame - 1] != InvalidIndex)
            {
                continue;
            }

            for (; index >= 0; index--)
            {
                if (!used[index])
                {
                    break;
                }
            }

            if (index < 0)
            {
                break;
            }

            AddToSlot(mapSlot, used, index, remapped, frame);
        }

        // The remaining future frames take the last empty references.
        index = bufferCount - 1;
        for (Av1ReferenceFrameType frame = Av1ReferenceFrameType.Alternate; frame >= Av1ReferenceFrameType.Last; frame--)
        {
            if (remapped[(int)frame - 1] != InvalidIndex)
            {
                continue;
            }

            for (; index > closestPast; index--)
            {
                if (!used[index])
                {
                    break;
                }
            }

            if (index < 0 || used[index])
            {
                break;
            }

            AddToSlot(mapSlot, used, index, remapped, frame);
        }

        for (int i = 0; i < SlotCount; i++)
        {
            if (remapped[i] == InvalidIndex)
            {
                remapped[i] = 0;
            }
        }
    }

    /// <summary>
    /// Maps one buffer to a named reference. Reference: add_ref_to_slot().
    /// </summary>
    private static void AddToSlot(ReadOnlySpan<int> mapSlot, Span<bool> used, int index, Span<int> remapped, Av1ReferenceFrameType frame)
    {
        remapped[(int)frame - 1] = mapSlot[index];
        used[index] = true;
    }

    /// <summary>
    /// Leaves one buffer out of the named mapping when there are more buffers than named references. The farthest
    /// buffer above the lowest level goes first. Reference: set_unmapped_ref() with LOW_LEVEL_FRAMES_TR of 5.
    /// </summary>
    private static void SetUnmappedReference(
        ReadOnlySpan<int> mapOrder,
        ReadOnlySpan<int> mapLevel,
        Span<bool> used,
        int minimumLevelCount,
        int minimumLevel,
        int currentOrder)
    {
        if (mapOrder.Length <= ReferenceCount)
        {
            return;
        }

        int maximumDistance = 0;
        int unmapped = -1;
        for (int i = 0; i < mapOrder.Length; i++)
        {
            if (used[i])
            {
                continue;
            }

            if (mapLevel[i] != minimumLevel || minimumLevelCount >= 5)
            {
                int distance = Math.Abs(currentOrder - mapOrder[i]);
                if (distance > maximumDistance)
                {
                    maximumDistance = distance;
                    unmapped = i;
                }
            }
        }

        used[unmapped] = true;
    }

    /// <summary>
    /// Chooses the slot that the frame refreshes: the first empty slot, or else the oldest frame outside the three
    /// newest past frames, preferring frames above the lowest pyramid level. Reference: av1_get_refresh_frame_flags()
    /// and get_refresh_idx() for a frame that is not an alternate reference.
    /// </summary>
    private static uint GetRefreshFrameFlags(ReadOnlySpan<int> pairOrder, ReadOnlySpan<int> pairLevel, int currentOrder)
    {
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (pairOrder[slot] == -1)
            {
                return 1U << slot;
            }
        }

        int oldestAlternateOrder = int.MaxValue;
        int oldestAlternate = -1;
        int oldestOrder = int.MaxValue;
        int oldest = -1;
        for (int slot = 0; slot < SlotCount; slot++)
        {
            int order = pairOrder[slot];
            if (order == -1 || order > currentOrder - 3)
            {
                continue;
            }

            if (pairLevel[slot] == 1)
            {
                if (order < oldestAlternateOrder)
                {
                    oldestAlternateOrder = order;
                    oldestAlternate = slot;
                }

                continue;
            }

            if (order < oldestOrder)
            {
                oldestOrder = order;
                oldest = slot;
            }
        }

        return 1U << (oldest >= 0 ? oldest : oldestAlternate);
    }

    /// <summary>
    /// Chooses the slots that a frame of a lookahead golden group refreshes. Reference: av1_get_refresh_frame_flags().
    /// </summary>
    private static uint GetLaggedRefreshFrameFlags(
        ReadOnlySpan<int> pairOrder,
        ReadOnlySpan<int> pairLevel,
        int currentOrder,
        in GroupFrame frame,
        ReadOnlySpan<int> skipFrameRefresh)
    {
        if (frame.ResetsReferences)
        {
            return byte.MaxValue;
        }

        if (frame.ShowExisting ||
            frame.UpdateType is Av1FrameUpdateType.Overlay or Av1FrameUpdateType.IntermediateOverlay)
        {
            return 0;
        }

        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (pairOrder[slot] == -1)
            {
                return 1U << slot;
            }
        }

        int refreshIndex = GetRefreshIndex(
            pairOrder, pairLevel, frame.UpdateType == Av1FrameUpdateType.Alternate, currentOrder, skipFrameRefresh);

        return 1U << refreshIndex;
    }

    /// <summary>
    /// Chooses the slot to replace: the oldest frame outside the three newest past frames and the frames the group
    /// keeps, preferring frames above the lowest pyramid level. An alternate reference replaces the oldest lowest-level
    /// frame when more than two are held. Reference: get_refresh_idx() with enable_refresh_skip.
    /// </summary>
    private static int GetRefreshIndex(
        ReadOnlySpan<int> pairOrder,
        ReadOnlySpan<int> pairLevel,
        bool updateAlternate,
        int currentOrder,
        ReadOnlySpan<int> skipFrameRefresh)
    {
        int alternateCount = 0;
        int oldestAlternateOrder = int.MaxValue;
        int oldestAlternate = -1;
        int oldestOrder = int.MaxValue;
        int oldest = -1;
        for (int slot = 0; slot < SlotCount; slot++)
        {
            int order = pairOrder[slot];
            if (order == -1 || order > currentOrder - 3)
            {
                continue;
            }

            bool skip = false;
            for (int i = 0; i < skipFrameRefresh.Length && skipFrameRefresh[i] != InvalidIndex; i++)
            {
                if (order == skipFrameRefresh[i])
                {
                    skip = true;
                    break;
                }
            }

            if (skip)
            {
                continue;
            }

            if (pairLevel[slot] == 1)
            {
                if (order < oldestAlternateOrder)
                {
                    oldestAlternateOrder = order;
                    oldestAlternate = slot;
                }

                alternateCount++;
                continue;
            }

            if (order < oldestOrder)
            {
                oldestOrder = order;
                oldest = slot;
            }
        }

        if (updateAlternate && alternateCount > 2)
        {
            return oldestAlternate;
        }

        return oldest >= 0 ? oldest : oldestAlternate;
    }

    /// <summary>
    /// Chooses the reference whose slot holds the most recent frame of the current frame's reference type.
    /// Reference: choose_primary_ref_frame().
    /// </summary>
    private uint ChoosePrimaryReferenceFrame(ObuFrameHeader frameHeader)
    {
        if (frameHeader.IsIntra || frameHeader.ErrorResilientMode)
        {
            return Av1Constants.PrimaryReferenceFrameNone;
        }

        int wanted = this.contextTypeSlots[GetReferenceType(this.layerDepth)];
        uint primary = Av1Constants.PrimaryReferenceFrameNone;
        ReadOnlySpan<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
        for (int reference = 0; reference < ReferenceCount; reference++)
        {
            if ((int)referenceFrameIndices[reference] == wanted)
            {
                primary = (uint)reference;
            }
        }

        return primary;
    }

    /// <summary>
    /// The role of one frame in a lookahead golden group. Reference: the entries of GF_GROUP at gf_index.
    /// </summary>
    public readonly struct GroupFrame
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GroupFrame"/> struct.
        /// </summary>
        /// <param name="updateType">The update type. Reference: update_type.</param>
        /// <param name="layerDepth">The layer depth. Reference: layer_depth.</param>
        /// <param name="maximumLayerDepth">The largest layer depth of the group. Reference: max_layer_depth.</param>
        /// <param name="displayOrder">The display order of the frame. Reference: cur_frame_disp.</param>
        /// <param name="resetsReferences">Whether the frame refreshes every slot. Reference: REFBUF_RESET.</param>
        /// <param name="isNonReference">Whether no later frame references the frame. Reference: is_frame_non_ref.</param>
        /// <param name="showExisting">Whether the frame shows a frame already coded. Reference: show_existing_frame.</param>
        public GroupFrame(
            Av1FrameUpdateType updateType,
            int layerDepth,
            int maximumLayerDepth,
            int displayOrder,
            bool resetsReferences,
            bool isNonReference,
            bool showExisting)
        {
            this.UpdateType = updateType;
            this.LayerDepth = layerDepth;
            this.MaximumLayerDepth = maximumLayerDepth;
            this.DisplayOrder = displayOrder;
            this.ResetsReferences = resetsReferences;
            this.IsNonReference = isNonReference;
            this.ShowExisting = showExisting;
        }

        /// <summary>Gets the update type.</summary>
        public Av1FrameUpdateType UpdateType { get; }

        /// <summary>Gets the layer depth.</summary>
        public int LayerDepth { get; }

        /// <summary>Gets the largest layer depth of the group.</summary>
        public int MaximumLayerDepth { get; }

        /// <summary>Gets the display order of the frame.</summary>
        public int DisplayOrder { get; }

        /// <summary>Gets a value indicating whether the frame refreshes every slot.</summary>
        public bool ResetsReferences { get; }

        /// <summary>Gets a value indicating whether no later frame references the frame.</summary>
        public bool IsNonReference { get; }

        /// <summary>Gets a value indicating whether the frame shows a frame already coded.</summary>
        public bool ShowExisting { get; }
    }
}
