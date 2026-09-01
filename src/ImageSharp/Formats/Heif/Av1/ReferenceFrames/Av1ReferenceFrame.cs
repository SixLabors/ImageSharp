// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;

/// <summary>
/// Owns the completed decoded state retained for one AV1 reference or presentation frame.
/// </summary>
/// <remarks>
/// Reference-map owners contain reconstruction samples after the normative in-loop filters and before film-grain
/// synthesis. A presentation-only owner may instead contain the independently synthesized grained output.
/// </remarks>
internal sealed class Av1ReferenceFrame : IDisposable
{
    /// <summary>
    /// The completed sample planes while this instance owns them.
    /// </summary>
    private Av1FrameBuffer<byte>? frameBuffer;

    /// <summary>
    /// The compact reference state and optional entropy snapshot retained while this frame occupies the reference map.
    /// </summary>
    private ReferenceOwnership? referenceOwnership;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ReferenceFrame"/> class for presentation-only ownership.
    /// </summary>
    /// <param name="frameBuffer">
    /// The completed sample buffer. Ownership transfers to this instance when construction succeeds.
    /// </param>
    /// <param name="frameHeader">
    /// The completed frame header associated with the reconstructed samples. The caller must not mutate the header
    /// after transferring it to this instance.
    /// </param>
    public Av1ReferenceFrame(Av1FrameBuffer<byte> frameBuffer, ObuFrameHeader frameHeader)
    {
        this.frameBuffer = frameBuffer;
        this.FrameHeader = frameHeader;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ReferenceFrame"/> class with retained compact reference state.
    /// </summary>
    /// <param name="frameBuffer">
    /// The completed sample buffer. Ownership transfers to this instance when construction succeeds.
    /// </param>
    /// <param name="frameHeader">
    /// The completed frame header associated with the reconstructed samples. The caller must not mutate the header
    /// after transferring it to this instance.
    /// </param>
    /// <param name="frameInfo">The completed reconstruction state from which reference syntax is retained.</param>
    public Av1ReferenceFrame(Av1FrameBuffer<byte> frameBuffer, ObuFrameHeader frameHeader, Av1FrameInfo frameInfo)
        : this(frameBuffer, frameHeader)
    {
        this.referenceOwnership = new(frameInfo.AcquireReferenceState(), null);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ReferenceFrame"/> class and takes ownership of decoded samples
    /// and the entropy snapshot retained by a refreshed reference frame.
    /// </summary>
    /// <param name="frameBuffer">
    /// The completed sample buffer. Ownership transfers to this instance when construction succeeds.
    /// </param>
    /// <param name="frameHeader">
    /// The completed frame header associated with the reconstructed samples. The caller must not mutate the header
    /// after transferring it to this instance.
    /// </param>
    /// <param name="frameInfo">
    /// The completed per-block state associated with the reconstructed samples. The caller must not mutate the state
    /// after transferring it to this instance.
    /// </param>
    /// <param name="entropyContext">The completed entropy snapshot selected for later primary-reference use.</param>
    /// <param name="entropyContextOwner">The decoder-session owner to which the snapshot is returned.</param>
    public Av1ReferenceFrame(
        Av1FrameBuffer<byte> frameBuffer,
        ObuFrameHeader frameHeader,
        Av1FrameInfo frameInfo,
        Av1FrameEntropyContext entropyContext,
        Av1FrameEntropyContexts entropyContextOwner)
        : this(frameBuffer, frameHeader)
    {
        this.referenceOwnership = new(
            frameInfo.AcquireReferenceState(),
            new EntropyOwnership(entropyContext, entropyContextOwner));
    }

    /// <summary>
    /// Gets the completed sample buffer owned by this frame.
    /// </summary>
    public Av1FrameBuffer<byte> FrameBuffer
    {
        get
        {
            return this.frameBuffer
                ?? throw new ObjectDisposedException(nameof(Av1ReferenceFrame));
        }
    }

    /// <summary>
    /// Gets the completed header that describes the retained frame.
    /// </summary>
    public ObuFrameHeader FrameHeader { get; }

    /// <summary>
    /// Gets the compact segment and motion state associated with the retained frame.
    /// </summary>
    public Av1FrameInfo.ReferenceState ReferenceState
    {
        get
        {
            ReferenceOwnership? ownership = this.referenceOwnership;
            if (ownership is null)
            {
                throw new InvalidOperationException("A presentation-only AV1 frame has no retained reference state.");
            }

            return ownership.Value.ReferenceState;
        }
    }

    /// <summary>
    /// Gets the entropy context retained for primary-reference use, or <see langword="null"/> for a presentation-only
    /// frame.
    /// </summary>
    public Av1FrameEntropyContext? EntropyContext => this.referenceOwnership?.Entropy?.Context;

    /// <summary>
    /// Restores the retained frame context to the normative defaults selected by this frame's quantizer band.
    /// </summary>
    public void ResetEntropyContext()
    {
        EntropyOwnership? entropy = this.referenceOwnership?.Entropy;
        if (entropy is null)
        {
            throw new InvalidOperationException("The AV1 reference frame has no retained entropy context.");
        }

        entropy.Value.Context.ResetToDefaults(this.FrameHeader.QuantizationParameters.BaseQIndex);
    }

    /// <summary>
    /// Transfers the completed sample planes out of this frame owner.
    /// </summary>
    /// <returns>The completed sample planes now owned by the caller.</returns>
    public Av1FrameBuffer<byte> TakeFrameBuffer()
    {
        Av1FrameBuffer<byte> result = this.frameBuffer
            ?? throw new ObjectDisposedException(nameof(Av1ReferenceFrame));

        this.frameBuffer = null;
        return result;
    }

    /// <summary>
    /// Releases the owned completed sample planes and returns any retained entropy snapshot to its decoder session.
    /// </summary>
    public void Dispose()
    {
        ReferenceOwnership? ownership = this.referenceOwnership;
        this.referenceOwnership = null;
        if (ownership is not null)
        {
            // Clearing the complete ownership state before returning either resource makes repeated disposal harmless
            // when one frame owner occupies multiple reference-map slots.
            ReferenceOwnership activeOwnership = ownership.Value;
            EntropyOwnership? entropy = activeOwnership.Entropy;
            if (entropy is not null)
            {
                EntropyOwnership activeEntropy = entropy.Value;
                activeEntropy.Owner.ReturnSnapshot(activeEntropy.Context);
            }

            activeOwnership.ReferenceState.ReleaseOwner();
        }

        this.frameBuffer?.Dispose();
        this.frameBuffer = null;
    }

    /// <summary>
    /// Carries the complete state retained only by frames that can be selected as references.
    /// </summary>
    private readonly struct ReferenceOwnership(
        Av1FrameInfo.ReferenceState referenceState,
        EntropyOwnership? entropy)
    {
        /// <summary>
        /// Gets the retained segment and motion state.
        /// </summary>
        public Av1FrameInfo.ReferenceState ReferenceState { get; } = referenceState;

        /// <summary>
        /// Gets the retained entropy snapshot and its return owner when one was published.
        /// </summary>
        public EntropyOwnership? Entropy { get; } = entropy;
    }

    /// <summary>
    /// Pairs a retained entropy snapshot with the decoder-session owner that must receive it on release.
    /// </summary>
    private readonly struct EntropyOwnership(
        Av1FrameEntropyContext context,
        Av1FrameEntropyContexts owner)
    {
        /// <summary>
        /// Gets the retained entropy snapshot.
        /// </summary>
        public Av1FrameEntropyContext Context { get; } = context;

        /// <summary>
        /// Gets the decoder-session owner that receives the snapshot.
        /// </summary>
        public Av1FrameEntropyContexts Owner { get; } = owner;
    }
}
