// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.SuperResolution;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Reconstructs the coded blocks of one AV1 image frame into planar sample buffers.
/// </summary>
internal sealed class Av1FrameDecoder : IAv1FrameDecoder, IDisposable
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
    private readonly Av1ReferenceFrameStore? referenceFrames;

    /// <summary>
    /// The coefficient inverse-quantization stage shared across superblocks.
    /// </summary>
    private readonly Av1InverseQuantizer inverseQuantizer;

    /// <summary>
    /// The frame's base per-segment and per-plane dequantization values.
    /// </summary>
    private readonly Av1DeQuantizationContext deQuants;

    /// <summary>
    /// The transform-size map populated during reconstruction and consumed by deblocking.
    /// </summary>
    private readonly Av1LoopFilterContext loopFilterContext;

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
    /// <param name="referenceFrames">
    /// The retained reconstructed frames selected by inter blocks, or <see langword="null"/> for intra-only reconstruction.
    /// </param>
    public Av1FrameDecoder(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameInfo frameInfo,
        Av1FrameBuffer<byte> frameBuffer,
        Av1ReferenceFrameStore? referenceFrames = null)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameInfo = frameInfo;
        this.frameBuffer = frameBuffer;
        this.referenceFrames = referenceFrames;
        this.inverseQuantizer = new(sequenceHeader, frameHeader);
        this.deQuants = new(sequenceHeader, frameHeader);
        this.loopFilterContext = new(sequenceHeader);
        this.blockDecoder = new(
            this.sequenceHeader,
            this.frameHeader,
            this.frameBuffer,
            this.loopFilterContext,
            this.inverseQuantizer,
            this.referenceFrames);
    }

    /// <summary>
    /// Releases the pooled block-reconstruction workspaces owned by this decoder.
    /// </summary>
    public void Dispose() => this.blockDecoder.Dispose();

    /// <summary>
    /// Applies the in-loop frame stages after every superblock has been reconstructed.
    /// </summary>
    public void CompleteFrame()
    {
        bool doLoopRestoration = this.frameHeader.LoopRestorationParameters.UsesLoopRestoration;

        Av1LoopFilterDecoder loopFilterDecoder = new(
            this.sequenceHeader,
            this.frameHeader,
            this.frameInfo,
            this.frameBuffer,
            this.loopFilterContext);

        loopFilterDecoder.DecodeFrame();

        using Av1LoopRestorationBoundary? restorationBoundary = doLoopRestoration
            ? new(this.sequenceHeader, this.frameHeader, this.frameBuffer)
            : null;

        if (restorationBoundary is not null)
        {
            restorationBoundary.SaveDeblockedRows();
        }

        Av1CdefDecoder cdefDecoder = new(this.sequenceHeader, this.frameHeader, this.frameInfo, this.frameBuffer);
        cdefDecoder.DecodeFrame();

        Av1SuperResolutionDecoder superResolutionDecoder = new(this.sequenceHeader, this.frameHeader, this.frameBuffer);
        superResolutionDecoder.DecodeFrame();

        if (restorationBoundary is not null)
        {
            restorationBoundary.SaveFrameEdgeRows();
            Av1LoopRestorationDecoder loopRestorationDecoder = new(
                this.sequenceHeader,
                this.frameHeader,
                this.frameInfo,
                this.frameBuffer,
                restorationBoundary);

            loopRestorationDecoder.DecodeFrame();
        }

        // Film grain is deliberately excluded here because this buffer is the normative post-restoration reference.
        // The owning decoder applies grain only to the presentation buffer after reference ownership is established.
    }

    /// <summary>
    /// Reconstructs one superblock after applying its block state and delta-Q context.
    /// </summary>
    /// <param name="modeInfoPosition">The superblock's top-left position in 4x4 mode-info units.</param>
    /// <param name="superblockInfo">The decoded syntax and block modes for the superblock.</param>
    /// <param name="tileInfo">The tile that contains the superblock.</param>
    public void DecodeSuperblock(Point modeInfoPosition, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo)
    {
        this.blockDecoder.UpdateSuperblock(superblockInfo);
        this.inverseQuantizer.UpdateDequant(this.deQuants, superblockInfo);
        this.DecodePartition(modeInfoPosition, superblockInfo, tileInfo);
    }

    /// <summary>
    /// Reconstructs each decoded block in a superblock partition.
    /// </summary>
    /// <param name="modeInfoPosition">The superblock's frame-relative origin in 4x4 mode-info units.</param>
    /// <param name="superblockInfo">The superblock whose block modes are traversed.</param>
    /// <param name="tileInfo">The tile boundary information used by intra prediction.</param>
    /// <remarks>Traverses the depth-first block order produced by tile parsing.</remarks>
    private void DecodePartition(Point modeInfoPosition, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo)
    {
        foreach (ref Av1BlockModeInfo modeInfo in superblockInfo.GetModeInfos())
        {
            Point subPosition = modeInfo.PositionInSuperblock;
            Av1BlockSize subSize = modeInfo.BlockSize;
            Point globalPosition = new(modeInfoPosition.X, modeInfoPosition.Y);

            // Block positions are stored relative to the superblock; prediction and reconstruction require frame-relative mode-info coordinates.
            globalPosition.Offset(subPosition);
            this.blockDecoder.DecodeBlock(modeInfo, globalPosition, subSize, superblockInfo, tileInfo);

            // Palette maps are decoder-session scratch. Retained mode information must not keep views after the block
            // has consumed them because the next superblock reuses the same storage.
            modeInfo.SetPaletteColorIndexMap(Av1PlaneType.Y, default);
            modeInfo.SetPaletteColorIndexMap(Av1PlaneType.Uv, default);
        }
    }
}
