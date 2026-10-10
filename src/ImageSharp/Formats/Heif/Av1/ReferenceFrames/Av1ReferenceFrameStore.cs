// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;

/// <summary>
/// Owns the reference map and selected presentation output for one bounded AV1 decoder session.
/// </summary>
/// <remarks>
/// Several slots and the selected output can identify the same <see cref="Av1ReferenceFrame"/>. The store keeps that sharing without reference-count objects.
/// It releases a frame only after its final owning reference is replaced or cleared. This type is not thread safe. One decoder session serializes commit and
/// disposal.
/// </remarks>
internal sealed class Av1ReferenceFrameStore : IDisposable
{
    /// <summary>
    /// The number of reference slots defined by the AV1 uncompressed frame header.
    /// </summary>
    private const int SlotCount = Av1Constants.ReferenceFrameCount;

    /// <summary>
    /// Stores the frame owner selected by each reference-map slot without allocating a managed array.
    /// </summary>
    private InlineArray8<Av1ReferenceFrame?> frames;

    /// <summary>
    /// The most recent shown frame retained for presentation at the end of the bounded image payload.
    /// </summary>
    private Av1ReferenceFrame? outputFrame;

    /// <summary>
    /// Gets the most recent shown frame retained for presentation.
    /// </summary>
    public Av1ReferenceFrame? OutputFrame => this.outputFrame;

    /// <summary>
    /// Resolves one reference-map slot.
    /// </summary>
    /// <param name="slot">The zero-based reference-map slot in the inclusive range 0 through 7.</param>
    /// <returns>The retained frame, or <see langword="null"/> when the slot is empty.</returns>
    public Av1ReferenceFrame? Resolve(int slot) => this.frames[slot];

    /// <summary>
    /// Resolves a reference-map slot that an earlier syntax boundary established as occupied.
    /// </summary>
    /// <param name="slot">The zero-based reference-map slot.</param>
    /// <returns>The retained frame in the selected slot.</returns>
    public Av1ReferenceFrame ResolveRequired(int slot)
    {
        return this.Resolve(slot)
            ?? throw new InvalidImageContentException($"The AV1 reference-map slot {slot} has not been populated.");
    }

    /// <summary>
    /// Resolves the presentation output established by the completed bounded payload.
    /// </summary>
    /// <returns>The retained frame selected for presentation.</returns>
    public Av1ReferenceFrame ResolveOutput()
    {
        return this.outputFrame
            ?? throw new InvalidImageContentException("The AV1 payload did not produce a shown frame.");
    }

    /// <summary>
    /// Writes whether each reference-map slot currently owns a reconstructed frame.
    /// </summary>
    /// <param name="destination">The eight-entry destination receiving the current slot occupancy.</param>
    public void FillOccupancy(Span<bool> destination)
    {
        // Physical ownership is intentionally independent from frame-ID validity. Short reference signaling sorts every occupied slot first. Then the
        // uncompressed-header parser validates each derived role separately.
        ReadOnlySpan<Av1ReferenceFrame?> frames = this.frames;
        for (int slot = 0; slot < SlotCount; slot++)
        {
            destination[slot] = frames[slot] is not null;
        }
    }

    /// <summary>
    /// Commits a completed frame to the reference map and, when shown, retains it for presentation.
    /// </summary>
    /// <param name="refreshFrameFlags">The mask whose bit <c>n</c> replaces reference-map slot <c>n</c>. Only the low eight bits describe AV1 slots.</param>
    /// <param name="frame">The completed frame to retain in every selected ownership role.</param>
    /// <param name="showFrame">Whether the completed frame replaces the previously retained presentation output.</param>
    /// <returns>
    /// <see langword="true"/> when the frame is retained as a reference or presentation output and ownership transfers to this store. Otherwise
    /// <see langword="false"/>. In that case, no state changes and the caller keeps ownership.
    /// </returns>
    /// <remarks>
    /// The caller must invoke this method only when reconstruction and all normative in-loop filters are complete. After the ownership transfer, the caller
    /// must not dispose the frame. A frame passed here must not already be owned by this store.
    /// </remarks>
    public bool Commit(uint refreshFrameFlags, Av1ReferenceFrame frame, bool showFrame)
    {
        refreshFrameFlags &= byte.MaxValue;
        if (refreshFrameFlags == 0 && !showFrame)
        {
            // A hidden frame with a zero refresh mask has no remaining role in an image-decoder session.
            return false;
        }

        InlineArray8<Av1ReferenceFrame?> replacedFrames = default;
        Av1ReferenceFrame? replacedOutputFrame = showFrame ? this.outputFrame : null;

        // The code captures displaced owners in inline storage. Then it publishes the full slot and output transition before it releases anything. A shown
        // frame can also occupy reference slots, so both ownership domains must change as one operation.
        Span<Av1ReferenceFrame?> frames = this.frames;
        Span<Av1ReferenceFrame?> replaced = replacedFrames;
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if ((refreshFrameFlags & (1U << slot)) != 0)
            {
                replaced[slot] = frames[slot];
                frames[slot] = frame;
            }
        }

        if (showFrame)
        {
            this.outputFrame = frame;
        }

        for (int replacedIndex = 0; replacedIndex < SlotCount; replacedIndex++)
        {
            Av1ReferenceFrame? replacedFrame = replaced[replacedIndex];

            if (replacedFrame is null)
            {
                continue;
            }

            if (ReferenceEquals(replacedFrame, replacedOutputFrame))
            {
                // The displaced-output path releases this shared owner after this loop removes every slot candidate.
                replaced[replacedIndex] = null;
                continue;
            }

            // A displaced frame remains owned when any unrefreshed slot or the selected output still references it. Eight fixed slots make the bounded identity
            // scan cheaper than allocated reference-count state.
            if (this.IsRetained(replacedFrame))
            {
                replaced[replacedIndex] = null;
            }
        }

        DisposeUnique(ref replacedFrames);

        if (replacedOutputFrame is not null && !this.IsRetained(replacedOutputFrame))
        {
            replacedOutputFrame.Dispose();
        }

        return true;
    }

    /// <summary>
    /// Replaces the selected presentation output with an independently owned completed frame.
    /// </summary>
    /// <param name="frame">The completed presentation frame whose ownership transfers to this store.</param>
    /// <remarks>
    /// The decoder uses this path when film grain makes the presentation samples differ from the ungrained reconstruction retained by the reference map.
    /// </remarks>
    public void CommitOutput(Av1ReferenceFrame frame)
    {
        Av1ReferenceFrame? replacedFrame = this.outputFrame;
        this.outputFrame = frame;

        // One or more reference slots can still retain the previous output. The code releases it only after it publishes the new output and finds no remaining
        // reference-map identity.
        if (replacedFrame is not null && !this.IsRetained(replacedFrame))
        {
            replacedFrame.Dispose();
        }
    }

    /// <summary>
    /// Selects one retained reference for presentation and applies the key-frame reference-map reset when required.
    /// </summary>
    /// <param name="slot">The zero-based reference-map slot selected by the frame header.</param>
    /// <returns>The retained frame selected for presentation.</returns>
    public Av1ReferenceFrame ShowExisting(int slot)
    {
        Av1ReferenceFrame selectedFrame = this.ResolveRequired(slot);
        Av1ReferenceFrame? replacedOutputFrame = this.outputFrame;
        this.outputFrame = selectedFrame;

        if (selectedFrame.FrameHeader.FrameType == ObuFrameType.KeyFrame)
        {
            InlineArray8<Av1ReferenceFrame?> replacedFrames = this.frames;
            Span<Av1ReferenceFrame?> frames = this.frames;
            Span<Av1ReferenceFrame?> replaced = replacedFrames;

            // Showing a hidden key frame starts a new coded-video-sequence state. All eight reference-map slots now identify that same reconstructed owner. The
            // code publishes every alias before it releases displaced frames.
            for (int mapSlot = 0; mapSlot < SlotCount; mapSlot++)
            {
                frames[mapSlot] = selectedFrame;
                if (ReferenceEquals(replaced[mapSlot], selectedFrame))
                {
                    replaced[mapSlot] = null;
                }
            }

            // A key frame can be presented through show_existing_frame only once. The retained owner carries this conformance state, because every slot alias
            // must see the transition.
            selectedFrame.FrameHeader.ShowableFrame = false;

            if (replacedOutputFrame is not null)
            {
                // The displaced-output path releases a detached shared owner. This loop first removes all old slot aliases of that owner from the replacement
                // set.
                for (int mapSlot = 0; mapSlot < SlotCount; mapSlot++)
                {
                    if (ReferenceEquals(replaced[mapSlot], replacedOutputFrame))
                    {
                        replaced[mapSlot] = null;
                    }
                }
            }

            DisposeUnique(ref replacedFrames);
        }

        if (replacedOutputFrame is not null && !this.IsRetained(replacedOutputFrame))
        {
            replacedOutputFrame.Dispose();
        }

        return selectedFrame;
    }

    /// <summary>
    /// Transfers the selected presentation frame out of this store and releases every other retained frame.
    /// </summary>
    /// <returns>The selected presentation frame now owned by the caller.</returns>
    public Av1ReferenceFrame TakeOutput()
    {
        Av1ReferenceFrame result = this.ResolveOutput();
        this.outputFrame = null;

        // The caller becomes the sole owner of the selected output. The code removes all slot aliases before Reset releases the remaining session references,
        // so the sample buffer transfers without a copy.
        Span<Av1ReferenceFrame?> frames = this.frames;
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (ReferenceEquals(frames[slot], result))
            {
                frames[slot] = null;
            }
        }

        this.Reset();
        return result;
    }

    /// <summary>
    /// Clears all reference-map slots and releases every uniquely retained frame.
    /// </summary>
    public void Reset()
    {
        InlineArray8<Av1ReferenceFrame?> releasedFrames = this.frames;
        this.frames = default;
        Av1ReferenceFrame? releasedOutputFrame = this.outputFrame;
        this.outputFrame = null;

        // The live map is clear before disposal, so the store cannot expose a partially reset ownership state. When the output aliases a slot, the output path
        // performs the single release after this loop removes the duplicate slot.
        if (releasedOutputFrame is not null)
        {
            Span<Av1ReferenceFrame?> released = releasedFrames;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (ReferenceEquals(released[slot], releasedOutputFrame))
                {
                    released[slot] = null;
                }
            }
        }

        DisposeUnique(ref releasedFrames);
        releasedOutputFrame?.Dispose();
    }

    /// <summary>
    /// Releases every uniquely retained frame and clears all reference-map slots.
    /// </summary>
    public void Dispose() => this.Reset();

    /// <summary>
    /// Determines whether the live reference map or presentation output retains a frame.
    /// </summary>
    /// <param name="frame">The frame whose ownership is queried.</param>
    /// <returns><see langword="true"/> when the store still owns the frame.</returns>
    private bool IsRetained(Av1ReferenceFrame frame)
    {
        if (ReferenceEquals(this.outputFrame, frame))
        {
            return true;
        }

        ReadOnlySpan<Av1ReferenceFrame?> frames = this.frames;
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (ReferenceEquals(frames[slot], frame))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Releases each distinct frame owner in a fixed-size set exactly once.
    /// </summary>
    /// <param name="frameStorage">The inline set of frame references to release.</param>
    private static void DisposeUnique(ref InlineArray8<Av1ReferenceFrame?> frameStorage)
    {
        Span<Av1ReferenceFrame?> frames = frameStorage;
        for (int frameIndex = 0; frameIndex < SlotCount; frameIndex++)
        {
            Av1ReferenceFrame? frame = frames[frameIndex];

            if (frame is null)
            {
                continue;
            }

            // The loop sets every later alias to null before disposal. The store represents shared slot ownership through object identity, so the eight-entry
            // map needs no separately allocated reference-count state.
            for (int duplicateIndex = frameIndex + 1; duplicateIndex < SlotCount; duplicateIndex++)
            {
                if (ReferenceEquals(frames[duplicateIndex], frame))
                {
                    frames[duplicateIndex] = null;
                }
            }

            frames[frameIndex] = null;
            frame.Dispose();
        }
    }
}
