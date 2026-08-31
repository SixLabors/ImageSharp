// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;

/// <summary>
/// Derives the seven AV1 inter-reference map indices from short reference signaling.
/// </summary>
internal static class Av1ReferenceFrameDerivation
{
    /// <summary>
    /// The shifted-order sentinel used for a reference-map slot that is not available to the current frame.
    /// </summary>
    private const int UnavailableSortIndex = -1;

    /// <summary>
    /// The reference-type value subtracted when indexing the seven-entry inter-reference map.
    /// </summary>
    private const int ReferenceIndexOffset = (int)Av1ReferenceFrameType.Last;

    /// <summary>
    /// Gets the order in which unassigned backward roles are replaced by forward references.
    /// </summary>
    private static ReadOnlySpan<Av1ReferenceFrameType> RemainingReferenceOrder =>
        [
            Av1ReferenceFrameType.Last2,
            Av1ReferenceFrameType.Last3,
            Av1ReferenceFrameType.Backward,
            Av1ReferenceFrameType.Alternate2,
            Av1ReferenceFrameType.Alternate,
        ];

    /// <summary>
    /// Derives the reference-map slot selected for each inter-reference type when an AV1 frame uses short reference
    /// signaling.
    /// </summary>
    /// <param name="currentOrderHint">The current frame order hint in the active modulo order-hint domain.</param>
    /// <param name="orderHintBitWidth">The number of bits in the active order-hint domain.</param>
    /// <param name="lastFrameIndex">The explicitly signaled reference-map slot for <see cref="Av1ReferenceFrameType.Last"/>.</param>
    /// <param name="goldenFrameIndex">The explicitly signaled reference-map slot for <see cref="Av1ReferenceFrameType.Golden"/>.</param>
    /// <param name="slotOrderHints">The eight persisted reference-map order hints.</param>
    /// <param name="slotOccupancy">
    /// The eight values indicating whether each persisted reference-map slot owns a decoded frame. An empty slot is
    /// excluded from derivation.
    /// </param>
    /// <param name="referenceFrameIndices">
    /// The destination for seven slot indices ordered from <see cref="Av1ReferenceFrameType.Last"/> through
    /// <see cref="Av1ReferenceFrameType.Alternate"/>.
    /// </param>
    /// <exception cref="InvalidImageContentException">
    /// The signaled LAST or GOLDEN slot is empty, refers to the current frame, or refers to a future frame.
    /// </exception>
    /// <remarks>
    /// The caller owns the fixed AV1 table-size invariants: <paramref name="slotOrderHints"/> and
    /// <paramref name="slotOccupancy"/> contain eight entries, while <paramref name="referenceFrameIndices"/> contains
    /// seven entries. The signaled indices and order hints have already been read from their bounded bit fields. Every
    /// destination entry is overwritten on success, and multiple reference types may select the same slot. Frame-ID
    /// validity is a separate conformance state that the caller checks for every resolved reference after derivation.
    /// </remarks>
    public static void DeriveShortSignaledReferences(
        uint currentOrderHint,
        int orderHintBitWidth,
        uint lastFrameIndex,
        uint goldenFrameIndex,
        ReadOnlySpan<uint> slotOrderHints,
        ReadOnlySpan<bool> slotOccupancy,
        Span<uint> referenceFrameIndices)
    {
        int lastMapIndex = (int)lastFrameIndex;
        int goldenMapIndex = (int)goldenFrameIndex;

        if (!slotOccupancy[lastMapIndex])
        {
            // Unlike an unused empty slot, the explicitly signaled LAST slot must own a decoded frame before any
            // derived mapping can be consumed. the reference decoder rejects the missing reference at this frame-header boundary.
            throw new InvalidImageContentException("An AV1 inter frame requests an unavailable LAST reference.");
        }

        if (!slotOccupancy[goldenMapIndex])
        {
            // GOLDEN is the other explicitly signaled slot and has the same ownership requirement as LAST.
            throw new InvalidImageContentException("An AV1 inter frame requests an unavailable GOLDEN reference.");
        }

        int currentFrameSortIndex = 1 << (orderHintBitWidth - 1);
        int orderHintMask = currentFrameSortIndex - 1;
        InlineArray8<ReferenceFrameInfo> referenceInfo = default;
        int lastFrameSortIndex = UnavailableSortIndex;
        int goldenFrameSortIndex = UnavailableSortIndex;

        for (int mapIndex = 0; mapIndex < Av1Constants.ReferenceFrameCount; mapIndex++)
        {
            ref ReferenceFrameInfo info = ref referenceInfo[mapIndex];
            info.MapIndex = mapIndex;
            info.SortIndex = UnavailableSortIndex;

            if (!slotOccupancy[mapIndex])
            {
                // the reference decoder gives absent reference buffers sort index -1. Keeping empty managed slots in the same
                // leading partition prevents their stale order hints from participating in temporal selection.
                continue;
            }

            int difference = (int)slotOrderHints[mapIndex] - (int)currentOrderHint;

            // get_relative_dist folds the unsigned order-hint difference into the signed half-open interval
            // [-2^(bits-1), 2^(bits-1)). Adding the half-range makes -1 available as the absence sentinel while valid
            // entries sort from zero through the complete modulo domain.
            difference = (difference & orderHintMask) - (difference & currentFrameSortIndex);
            info.SortIndex = currentFrameSortIndex + difference;

            if (mapIndex == lastMapIndex)
            {
                lastFrameSortIndex = info.SortIndex;
            }

            if (mapIndex == goldenMapIndex)
            {
                goldenFrameSortIndex = info.SortIndex;
            }
        }

        if (lastFrameSortIndex >= currentFrameSortIndex)
        {
            throw new InvalidImageContentException("An AV1 inter frame requests a current or future frame as LAST.");
        }

        if (goldenFrameSortIndex >= currentFrameSortIndex)
        {
            throw new InvalidImageContentException("An AV1 inter frame requests a current or future frame as GOLDEN.");
        }

        // the reference decoder sorts first by shifted output order and then by reference-map index. The explicit tie break is
        // normative: equal order hints select the highest map index for latest references and the lowest for earliest
        // references. Insertion sort is bounded to eight inline entries and does not allocate or require general sort
        // infrastructure at the frame-header boundary.
        for (int index = 1; index < Av1Constants.ReferenceFrameCount; index++)
        {
            ReferenceFrameInfo current = referenceInfo[index];
            int insertionIndex = index;

            while (insertionIndex > 0)
            {
                ReferenceFrameInfo previous = referenceInfo[insertionIndex - 1];
                if (previous.SortIndex < current.SortIndex ||
                    (previous.SortIndex == current.SortIndex && previous.MapIndex <= current.MapIndex))
                {
                    break;
                }

                referenceInfo[insertionIndex] = previous;
                insertionIndex--;
            }

            referenceInfo[insertionIndex] = current;
        }

        InlineArray8<bool> assignedReferences = default;
        int lastReferenceIndex = (int)Av1ReferenceFrameType.Last - ReferenceIndexOffset;
        int goldenReferenceIndex = (int)Av1ReferenceFrameType.Golden - ReferenceIndexOffset;
        referenceFrameIndices[lastReferenceIndex] = lastFrameIndex;
        referenceFrameIndices[goldenReferenceIndex] = goldenFrameIndex;
        assignedReferences[lastReferenceIndex] = true;
        assignedReferences[goldenReferenceIndex] = true;

        int forwardStartIndex = 0;
        int forwardEndIndex = Av1Constants.ReferenceFrameCount - 1;

        // Empty entries sort before every occupied shifted hint. The first current-or-future entry then divides the
        // remaining sorted table into forward references on the left and backward references on the right.
        for (int index = 0; index < Av1Constants.ReferenceFrameCount; index++)
        {
            if (referenceInfo[index].SortIndex == UnavailableSortIndex)
            {
                forwardStartIndex++;
                continue;
            }

            if (referenceInfo[index].SortIndex >= currentFrameSortIndex)
            {
                forwardEndIndex = index - 1;
                break;
            }
        }

        int backwardStartIndex = forwardEndIndex + 1;
        int backwardEndIndex = Av1Constants.ReferenceFrameCount - 1;
        int alternateReferenceIndex = (int)Av1ReferenceFrameType.Alternate - ReferenceIndexOffset;
        int backwardReferenceIndex = (int)Av1ReferenceFrameType.Backward - ReferenceIndexOffset;
        int alternate2ReferenceIndex = (int)Av1ReferenceFrameType.Alternate2 - ReferenceIndexOffset;

        if (backwardStartIndex <= backwardEndIndex)
        {
            // ALTREF receives the frame farthest into the future. The sorted-map-index tie break selects the highest
            // slot when multiple frames share that order hint, matching both the specification and the reference decoder.
            referenceFrameIndices[alternateReferenceIndex] = (uint)referenceInfo[backwardEndIndex].MapIndex;
            assignedReferences[alternateReferenceIndex] = true;
            backwardEndIndex--;
        }

        if (backwardStartIndex <= backwardEndIndex)
        {
            // BWDREF receives the nearest future frame and therefore consumes the low end of the backward partition.
            referenceFrameIndices[backwardReferenceIndex] = (uint)referenceInfo[backwardStartIndex].MapIndex;
            assignedReferences[backwardReferenceIndex] = true;
            backwardStartIndex++;
        }

        if (backwardStartIndex <= backwardEndIndex)
        {
            // ALTREF2 receives the next-nearest remaining future frame. No further backward lookup follows, so the
            // lower boundary does not need to advance after this assignment.
            referenceFrameIndices[alternate2ReferenceIndex] = (uint)referenceInfo[backwardStartIndex].MapIndex;
            assignedReferences[alternate2ReferenceIndex] = true;
        }

        ReadOnlySpan<Av1ReferenceFrameType> remainingReferenceOrder = RemainingReferenceOrder;
        int remainingIndex;

        for (remainingIndex = 0; remainingIndex < remainingReferenceOrder.Length; remainingIndex++)
        {
            int referenceIndex = (int)remainingReferenceOrder[remainingIndex] - ReferenceIndexOffset;
            if (assignedReferences[referenceIndex])
            {
                continue;
            }

            // LAST and GOLDEN were already assigned explicitly and cannot be reused while an unassigned forward slot
            // remains. Moving from the high end chooses the remaining frames in anti-chronological order.
            while (forwardStartIndex <= forwardEndIndex &&
                (referenceInfo[forwardEndIndex].MapIndex == lastMapIndex ||
                 referenceInfo[forwardEndIndex].MapIndex == goldenMapIndex))
            {
                forwardEndIndex--;
            }

            if (forwardStartIndex > forwardEndIndex)
            {
                break;
            }

            referenceFrameIndices[referenceIndex] = (uint)referenceInfo[forwardEndIndex].MapIndex;
            assignedReferences[referenceIndex] = true;
            forwardEndIndex--;
        }

        for (; remainingIndex < remainingReferenceOrder.Length; remainingIndex++)
        {
            int referenceIndex = (int)remainingReferenceOrder[remainingIndex] - ReferenceIndexOffset;
            if (assignedReferences[referenceIndex])
            {
                continue;
            }

            // AV1 requires every unfilled role to reuse the earliest available forward frame. At least LAST and GOLDEN
            // are occupied forward references, so forwardStartIndex always identifies a usable slot at this point.
            referenceFrameIndices[referenceIndex] = (uint)referenceInfo[forwardStartIndex].MapIndex;
            assignedReferences[referenceIndex] = true;
        }
    }

    /// <summary>
    /// Stores one reference-map slot and its shifted order for fixed-size sorting.
    /// </summary>
    private struct ReferenceFrameInfo
    {
        /// <summary>
        /// Gets or sets the zero-based slot in the eight-entry persisted reference map.
        /// </summary>
        public int MapIndex { get; set; }

        /// <summary>
        /// Gets or sets the order hint shifted around the current frame, or <see cref="UnavailableSortIndex"/> when unavailable.
        /// </summary>
        public int SortIndex { get; set; }
    }
}
