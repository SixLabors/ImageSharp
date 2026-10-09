// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Stores the uncompressed-header reference state retained by one AV1 OBU reader session.
/// </summary>
/// <remarks>
/// This state describes the eight reference-map slots. It does not own reconstructed sample buffers. The reference-frame store of the decoder
/// owns the pixels and commits them before this syntax state is complete. The CDF, segmentation, loop-filter, motion and layer data also stay
/// with that frame owner. The primary reference slot of the current header selects that owner, so this state does not copy those values.
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

        // Only the three tables of the eight slots cross a frame boundary. Each frame signals or derives its seven inter references again.
        // The primary context source comes from that mapping of the frame.
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

        // The refresh occurs only when a frame completes successfully. The code updates the completed header first. The reconstructed frame
        // owner keeps that same header object, so it stays equal to the state that the next frame header copies.
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
            // The reader session keeps one current frame identifier. The next frame header copies this value as its previous identifier before
            // it reads its own `current_frame_id` syntax element.
            this.CurrentFrameId = frameHeader.CurrentFrameId;
            this.HasCurrentFrameId = true;
        }
    }

    /// <summary>
    /// Clears the completed frame identifier and every retained reference-map slot.
    /// </summary>
    public void Reset() => this = default;
}
