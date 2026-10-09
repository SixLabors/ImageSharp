// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.SuperResolution;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Reconstructs the coded blocks of one AV1 image frame into planar sample buffers.
/// </summary>
internal sealed class Av1FrameDecoder : IAv1FrameDecoder
{
    /// <summary>
    /// The sequence-level superblock and color configuration.
    /// </summary>
    private readonly ObuSequenceHeader sequenceHeader;

    /// <summary>
    /// The frame-level tile, quantization, and reconstruction configuration.
    /// </summary>
    private readonly ObuFrameHeader frameHeader;

    /// <summary>
    /// The parsed superblock and block-mode information for the frame.
    /// </summary>
    private readonly Av1FrameInfo frameInfo;

    /// <summary>
    /// The destination planar sample buffers for reconstructed pixels.
    /// </summary>
    private readonly Av1FrameBuffer<byte> frameBuffer;

    /// <summary>
    /// The retained reconstructed frames addressable by inter prediction.
    /// </summary>
    private readonly Av1ReferenceFrameStore referenceFrames;

    /// <summary>
    /// The block reconstruction stage that applies prediction and inverse transforms.
    /// </summary>
    private readonly Av1BlockDecoder blockDecoder;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameDecoder"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The parsed AV1 sequence header.</param>
    /// <param name="frameHeader">The parsed AV1 frame header.</param>
    /// <param name="frameInfo">The parsed superblock and block-mode information.</param>
    /// <param name="frameBuffer">The destination planar sample buffers.</param>
    /// <param name="referenceFrames">The retained reconstructed frames selected by inter blocks.</param>
    /// <param name="reconstructionWorkspace">The reconstruction storage available for the lifetime of this frame.</param>
    /// <param name="paletteColorIndexMaps">The complete decoder-session palette map state.</param>
    public Av1FrameDecoder(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameInfo frameInfo,
        Av1FrameBuffer<byte> frameBuffer,
        Av1ReferenceFrameStore referenceFrames,
        Memory<short> reconstructionWorkspace,
        Av1TileReader.PaletteColorIndexMaps? paletteColorIndexMaps = null)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameInfo = frameInfo;
        this.frameBuffer = frameBuffer;
        this.referenceFrames = referenceFrames;
        this.blockDecoder = new(
            this.sequenceHeader,
            this.frameHeader,
            this.frameBuffer,
            this.referenceFrames,
            reconstructionWorkspace,
            paletteColorIndexMaps);
    }

    /// <inheritdoc/>
    public Span<short> Workspace => this.blockDecoder.Workspace;

    /// <inheritdoc/>
    public Span<byte> GetFramePlane(Av1Plane plane) => this.blockDecoder.GetFramePlane(plane);

    /// <summary>
    /// Applies the in-loop frame stages after the decoder reconstructs every superblock.
    /// </summary>
    /// <param name="cdefDecoder">The session-owned CDEF stage.</param>
    /// <param name="restorationBoundary">The session-owned restoration boundary rows.</param>
    /// <param name="restorationDecoder">The session-owned restoration stage.</param>
    public void CompleteFrame(
        Av1CdefDecoder cdefDecoder,
        Av1LoopRestorationBoundary restorationBoundary,
        Av1LoopRestorationDecoder restorationDecoder)
    {
        bool doLoopRestoration = this.frameHeader.LoopRestorationParameters.UsesLoopRestoration;

        Av1LoopFilterDecoder loopFilterDecoder = new(
            this.sequenceHeader,
            this.frameHeader,
            this.frameInfo,
            this.frameBuffer);

        loopFilterDecoder.DecodeFrame();

        if (doLoopRestoration)
        {
            restorationBoundary.SaveDeblockedRows(this.sequenceHeader, this.frameHeader, this.frameBuffer);
        }

        cdefDecoder.DecodeFrame(this.sequenceHeader, this.frameHeader, this.frameInfo, this.frameBuffer);

        Av1SuperResolutionDecoder superResolutionDecoder = new(this.sequenceHeader, this.frameHeader, this.frameBuffer);
        superResolutionDecoder.DecodeFrame();

        if (doLoopRestoration)
        {
            restorationBoundary.SaveFrameEdgeRows(this.sequenceHeader, this.frameHeader, this.frameBuffer);
            restorationDecoder.DecodeFrame(
                this.sequenceHeader,
                this.frameHeader,
                this.frameInfo,
                this.frameBuffer,
                restorationBoundary);
        }

        // This stage does not apply film grain, because this buffer is the normative reference after restoration.
        // The owning decoder applies grain only to the presentation buffer, after it stores the reference.
    }

    /// <summary>
    /// Begins reconstruction of a superblock before its first coding block is parsed.
    /// </summary>
    /// <param name="superblockInfo">The transform and coefficient storage belonging to the superblock.</param>
    public void BeginSuperblock(Av1SuperblockInfo superblockInfo) => this.blockDecoder.UpdateSuperblock(superblockInfo);

    /// <summary>
    /// Prepares a published coding block before its residual syntax is read.
    /// </summary>
    /// <param name="partitionInfo">The current block modes, geometry, and available neighbors.</param>
    /// <param name="workspace">The inverse-transform and prediction storage, from <see cref="Workspace"/>.</param>
    /// <param name="frameLuma">The luma samples of the reconstructed frame, from <see cref="GetFramePlane"/>.</param>
    /// <param name="frameBlue">The blue-difference samples of the reconstructed frame, from <see cref="GetFramePlane"/>.</param>
    /// <param name="frameRed">The red-difference samples of the reconstructed frame, from <see cref="GetFramePlane"/>.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    public void BeginBlock(
        ref Av1PartitionInfo partitionInfo,
        Span<short> workspace,
        Span<byte> frameLuma,
        Span<byte> frameBlue,
        Span<byte> frameRed,
        Av1TileInfo tileInfo)
        => this.blockDecoder.BeginBlock(ref partitionInfo, workspace, frameLuma, frameBlue, frameRed, tileInfo);

    /// <summary>
    /// Reconstructs a parsed transform before the following transform's coefficients are read.
    /// </summary>
    /// <param name="partitionInfo">The current block modes, geometry, and available neighbors.</param>
    /// <param name="plane">The zero-based color-plane index.</param>
    /// <param name="transformInfo">The parsed transform geometry and residual metadata.</param>
    /// <param name="workspace">The inverse-transform and prediction storage, from <see cref="Workspace"/>.</param>
    /// <param name="frameLuma">The luma samples of the reconstructed frame, from <see cref="GetFramePlane"/>.</param>
    /// <param name="frameBlue">The blue-difference samples of the reconstructed frame, from <see cref="GetFramePlane"/>.</param>
    /// <param name="frameRed">The red-difference samples of the reconstructed frame, from <see cref="GetFramePlane"/>.</param>
    /// <param name="planeCoefficients">The coefficient scratch of the plane in the superblock, read once by the caller.</param>
    /// <param name="tileInfo">The active tile boundaries.</param>
    public void DecodeTransform(
        ref Av1PartitionInfo partitionInfo,
        int plane,
        ref Av1TransformInfo transformInfo,
        Span<short> workspace,
        Span<byte> frameLuma,
        Span<byte> frameBlue,
        Span<byte> frameRed,
        Span<int> planeCoefficients,
        Av1TileInfo tileInfo)
        => this.blockDecoder.DecodeTransform(
            ref partitionInfo,
            plane,
            ref transformInfo,
            workspace,
            frameLuma,
            frameBlue,
            frameRed,
            planeCoefficients,
            tileInfo);

    /// <inheritdoc/>
    public void EndBlock(ref Av1PartitionInfo partitionInfo, Span<short> workspace, Span<byte> frameLuma)
        => this.blockDecoder.EndBlock(ref partitionInfo, workspace, frameLuma);
}
