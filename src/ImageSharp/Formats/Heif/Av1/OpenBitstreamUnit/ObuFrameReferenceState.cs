// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Stores the uncompressed-header reference state retained by one AV1 OBU reader session.
/// </summary>
/// <remarks>
/// This state describes the eight reference-map slots but does not own reconstructed sample buffers. Pixel ownership
/// remains with the decoder's reference-frame store and is committed before this syntax state is completed. CDF,
/// segmentation, loop-filter, motion, and layer metadata remain on that retained frame owner; the current header's
/// resolved primary-reference slot selects the shared owner instead of duplicating those values here.
/// </remarks>
internal struct ObuFrameReferenceState
{
    /// <summary>
    /// Stores whether each of the eight reference-map slots can be selected by a later frame.
    /// </summary>
    private InlineArray8<bool> referenceValidity;

    /// <summary>
    /// Stores the frame identifier associated with each of the eight reference-map slots.
    /// </summary>
    private InlineArray8<uint> referenceFrameIds;

    /// <summary>
    /// Stores the order hint associated with each of the eight reference-map slots.
    /// </summary>
    private InlineArray8<uint> referenceOrderHints;

    /// <summary>
    /// Gets a value indicating whether a completed frame identifier is available for the next header.
    /// </summary>
    public bool HasCurrentFrameId { get; private set; }

    /// <summary>
    /// Gets the frame identifier of the most recently completed frame.
    /// </summary>
    public uint CurrentFrameId { get; private set; }

    /// <summary>
    /// Copies the completed reference-map state into a newly created frame header.
    /// </summary>
    /// <param name="frameHeader">The frame header that will parse and derive state from the retained map.</param>
    public void InitializeFrameHeader(ObuFrameHeader frameHeader)
    {
        ReadOnlySpan<bool> referenceValidity = this.referenceValidity;
        ReadOnlySpan<uint> referenceFrameIds = this.referenceFrameIds;
        ReadOnlySpan<uint> referenceOrderHints = this.referenceOrderHints;

        // Only the eight retained-slot tables cross a frame boundary. The seven inter-reference roles are signaled or
        // derived afresh for each frame, and the primary context source is resolved from that per-frame mapping.
        referenceValidity.CopyTo(frameHeader.GetReferenceValidity());
        referenceFrameIds.CopyTo(frameHeader.GetReferenceFrameIds());
        referenceOrderHints.CopyTo(frameHeader.GetReferenceOrderHints());
    }

    /// <summary>
    /// Publishes the reference-map transition produced by a successfully completed frame.
    /// </summary>
    /// <param name="frameHeader">The completed frame header whose refresh mask selects the replaced slots.</param>
    /// <param name="frameIdNumbersPresent">
    /// A value indicating whether the sequence carries modulo frame identifiers.
    /// </param>
    public void CompleteFrame(ObuFrameHeader frameHeader, bool frameIdNumbersPresent)
    {
        Span<bool> referenceValidity = frameHeader.GetReferenceValidity();
        Span<uint> referenceFrameIds = frameHeader.GetReferenceFrameIds();
        Span<uint> referenceOrderHints = frameHeader.GetReferenceOrderHints();

        // Refresh is published only at this successful completion boundary. Updating the completed header first keeps
        // the same object retained by the reconstructed frame owner synchronized with the next parser-session snapshot.
        for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
        {
            if ((frameHeader.RefreshFrameFlags & (1U << slot)) != 0)
            {
                referenceValidity[slot] = true;
                referenceFrameIds[slot] = frameHeader.CurrentFrameId;
                referenceOrderHints[slot] = frameHeader.OrderHint;
            }
        }

        referenceValidity.CopyTo(this.referenceValidity);
        referenceFrameIds.CopyTo(this.referenceFrameIds);
        referenceOrderHints.CopyTo(this.referenceOrderHints);

        if (frameIdNumbersPresent)
        {
            // libaom keeps one current_frame_id in decoder-session state. The following header snapshots this value as
            // its previous identifier before consuming its own current_frame_id syntax.
            this.CurrentFrameId = frameHeader.CurrentFrameId;
            this.HasCurrentFrameId = true;
        }
    }

    /// <summary>
    /// Clears the completed frame identifier and every retained reference-map slot.
    /// </summary>
    public void Reset() => this = default;
}
