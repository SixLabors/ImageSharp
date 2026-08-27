// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;

/// <summary>
/// Owns the reference map and selected presentation output for one bounded AV1 decoder session.
/// </summary>
/// <remarks>
/// Several slots and the selected output may identify the same <see cref="Av1ReferenceFrame"/>. The store preserves
/// that sharing without allocating reference-count objects and releases a frame only after its final owning reference
/// has been replaced or cleared. This type is not thread safe; one decoder session serializes commit and disposal.
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
    /// <returns>The retained frame, or <see langword="null"/> when the slot has not been populated.</returns>
    public Av1ReferenceFrame? Resolve(int slot) => this.frames[slot];

    /// <summary>
    /// Writes whether each reference-map slot currently owns a reconstructed frame.
    /// </summary>
    /// <param name="destination">The eight-entry destination receiving the current slot occupancy.</param>
    public void FillOccupancy(Span<bool> destination)
    {
        // Physical ownership is intentionally independent from frame-ID validity. Short reference signaling sorts
        // every occupied slot first, then the uncompressed-header parser validates each derived role separately.
        for (int slot = 0; slot < SlotCount; slot++)
        {
            destination[slot] = this.frames[slot] is not null;
        }
    }

    /// <summary>
    /// Commits a completed frame to the reference map and, when shown, retains it for presentation.
    /// </summary>
    /// <param name="refreshFrameFlags">
    /// The mask whose bit <c>n</c> replaces reference-map slot <c>n</c>. Only the low eight bits describe AV1 slots.
    /// </param>
    /// <param name="frame">The completed frame to retain in every selected ownership role.</param>
    /// <param name="showFrame">Whether the completed frame replaces the previously retained presentation output.</param>
    /// <returns>
    /// <see langword="true"/> when the frame is retained as a reference or presentation output and ownership transfers
    /// to this store; otherwise <see langword="false"/>, in which case no state changes and the caller retains ownership.
    /// </returns>
    /// <remarks>
    /// The caller must invoke this method only after reconstruction and all normative in-loop filters have completed.
    /// Once ownership transfers, the caller must not dispose the frame. A frame passed here must not already be owned by
    /// this store.
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

        // Capture displaced owners in inline storage, then publish the complete slot and output transition before
        // releasing anything. A shown frame may also occupy reference slots, so both ownership domains must change as
        // one operation.
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if ((refreshFrameFlags & (1U << slot)) != 0)
            {
                replacedFrames[slot] = this.frames[slot];
                this.frames[slot] = frame;
            }
        }

        if (showFrame)
        {
            this.outputFrame = frame;
        }

        for (int replacedIndex = 0; replacedIndex < SlotCount; replacedIndex++)
        {
            Av1ReferenceFrame? replacedFrame = replacedFrames[replacedIndex];

            if (replacedFrame is null)
            {
                continue;
            }

            if (ReferenceEquals(replacedFrame, replacedOutputFrame))
            {
                // Let the displaced-output path release this shared owner after every slot candidate has been removed.
                replacedFrames[replacedIndex] = null;
                continue;
            }

            // A displaced frame remains owned when any unrefreshed slot or the selected output still references it.
            // Eight fixed slots make the bounded identity scan cheaper than allocated reference-count state.
            if (this.IsRetained(replacedFrame))
            {
                replacedFrames[replacedIndex] = null;
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
    /// This path is used when film grain requires presentation samples to differ from the ungrained reconstruction
    /// retained by the reference map.
    /// </remarks>
    public void CommitOutput(Av1ReferenceFrame frame)
    {
        Av1ReferenceFrame? replacedFrame = this.outputFrame;
        this.outputFrame = frame;

        // The previous output may still be retained by one or more reference slots. Release it only after publishing
        // the new output and confirming that no reference-map identity remains.
        if (replacedFrame is not null && !this.IsRetained(replacedFrame))
        {
            replacedFrame.Dispose();
        }
    }

    /// <summary>
    /// Transfers the selected presentation frame out of this store and releases every other retained frame.
    /// </summary>
    /// <returns>The selected presentation frame now owned by the caller.</returns>
    public Av1ReferenceFrame TakeOutput()
    {
        Av1ReferenceFrame result = this.outputFrame!;
        this.outputFrame = null;

        // The caller becomes the sole owner of the selected output. Remove all slot aliases before Reset releases the
        // remaining session references so the sample buffer can transfer without copying.
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (ReferenceEquals(this.frames[slot], result))
            {
                this.frames[slot] = null;
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

        // Clear the live map before disposal so the store cannot expose a partially reset ownership state. When the
        // output aliases a slot, let the output path perform the single release after the duplicate slot is removed.
        if (releasedOutputFrame is not null)
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (ReferenceEquals(releasedFrames[slot], releasedOutputFrame))
                {
                    releasedFrames[slot] = null;
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

        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (ReferenceEquals(this.frames[slot], frame))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Releases each distinct frame owner in a fixed-size set exactly once.
    /// </summary>
    /// <param name="frames">The inline set of frame references to release.</param>
    private static void DisposeUnique(ref InlineArray8<Av1ReferenceFrame?> frames)
    {
        for (int frameIndex = 0; frameIndex < SlotCount; frameIndex++)
        {
            Av1ReferenceFrame? frame = frames[frameIndex];

            if (frame is null)
            {
                continue;
            }

            // Null every later alias before disposal. The store intentionally represents shared slot ownership through
            // object identity, so no separately allocated reference-count state is needed for the eight-entry map.
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
