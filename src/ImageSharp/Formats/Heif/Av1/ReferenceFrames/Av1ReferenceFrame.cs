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
    /// The independently retained entropy snapshot while this frame owner remains alive.
    /// </summary>
    private Av1FrameEntropyContext? entropyContext;

    /// <summary>
    /// The decoder-session owner that receives <see cref="entropyContext"/> when this frame is released.
    /// </summary>
    private Av1FrameEntropyContexts? entropyContextOwner;

    /// <summary>
    /// The shared decoded per-block state while this frame owns one lifetime lease.
    /// </summary>
    private Av1FrameInfo? frameInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ReferenceFrame"/> class and takes ownership of the decoded
    /// sample buffer.
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
    public Av1ReferenceFrame(Av1FrameBuffer<byte> frameBuffer, ObuFrameHeader frameHeader, Av1FrameInfo frameInfo)
    {
        this.frameBuffer = frameBuffer;
        this.FrameHeader = frameHeader;
        this.frameInfo = frameInfo;
        frameInfo.AddOwner();
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
        : this(frameBuffer, frameHeader, frameInfo)
    {
        this.entropyContext = entropyContext;
        this.entropyContextOwner = entropyContextOwner;
    }

    /// <summary>
    /// Gets the completed sample buffer owned by this frame.
    /// </summary>
    public Av1FrameBuffer<byte> FrameBuffer => this.frameBuffer!;

    /// <summary>
    /// Gets the completed header that describes the retained frame.
    /// </summary>
    public ObuFrameHeader FrameHeader { get; }

    /// <summary>
    /// Gets the decoded per-block mode, motion, transform, and filter state associated with the retained frame.
    /// </summary>
    public Av1FrameInfo FrameInfo => this.frameInfo!;

    /// <summary>
    /// Gets the entropy context retained for primary-reference use, or <see langword="null"/> for a presentation-only
    /// frame.
    /// </summary>
    public Av1FrameEntropyContext? EntropyContext => this.entropyContext;

    /// <summary>
    /// Restores the retained frame context to the normative defaults selected by this frame's quantizer band.
    /// </summary>
    public void ResetEntropyContext()
        => this.entropyContext!.ResetToDefaults(this.FrameHeader.QuantizationParameters.BaseQIndex);

    /// <summary>
    /// Transfers the completed sample planes out of this frame owner.
    /// </summary>
    /// <returns>The completed sample planes now owned by the caller.</returns>
    public Av1FrameBuffer<byte> TakeFrameBuffer()
    {
        Av1FrameBuffer<byte> result = this.frameBuffer!;
        this.frameBuffer = null;
        return result;
    }

    /// <summary>
    /// Releases the owned completed sample planes and returns any retained entropy snapshot to its decoder session.
    /// </summary>
    public void Dispose()
    {
        Av1FrameEntropyContext? context = this.entropyContext;
        this.entropyContext = null;
        if (context is not null)
        {
            // Nulling the field before returning the graph makes repeated disposal harmless and guarantees that one
            // shared frame owner occupying multiple reference slots returns its snapshot exactly once.
            this.entropyContextOwner!.ReturnSnapshot(context);
            this.entropyContextOwner = null;
        }

        this.frameBuffer?.Dispose();
        this.frameBuffer = null;
        this.frameInfo?.ReleaseOwner();
        this.frameInfo = null;
    }
}
