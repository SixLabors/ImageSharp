// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.IntraBlockCopy;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Reconstructs AV1 transform blocks by combining prediction, inverse quantization, and inverse transforms.
/// </summary>
internal sealed class Av1BlockDecoder : IDisposable
{
    /// <summary>
    /// The sequence-level syntax that determines superblock size, plane layout, and sample depth.
    /// </summary>
    private readonly ObuSequenceHeader sequenceHeader;

    /// <summary>
    /// The current frame syntax that determines quantization, lossless segments, and reconstruction geometry.
    /// </summary>
    private readonly ObuFrameHeader frameHeader;

    /// <summary>
    /// The reconstructed Y, U, and V sample planes receiving prediction and residual output.
    /// </summary>
    private readonly Av1FrameBuffer<byte> frameBuffer;

    /// <summary>
    /// The per-plane transform-size map consumed after reconstruction by the deblocking stage.
    /// </summary>
    private readonly Av1LoopFilterContext loopFilterContext;

    /// <summary>
    /// The frame-owned inverse quantizer carrying the active superblock delta-Q state.
    /// </summary>
    private readonly Av1InverseQuantizer inverseQuantizer;

    /// <summary>
    /// The retained reconstructed frames addressable by inter prediction.
    /// </summary>
    private readonly Av1ReferenceFrameStore referenceFrames;

    /// <summary>
    /// Owns the reusable raster-order inverse-quantization buffer.
    /// </summary>
    private readonly IMemoryOwner<int> inverseQuantizationOwner;

    /// <summary>
    /// Owns the reusable two-dimensional inverse-transform workspace.
    /// </summary>
    private readonly IMemoryOwner<int> transformWorkspaceOwner;

    /// <summary>
    /// Owns the reusable directional and filter-intra prediction workspace.
    /// </summary>
    private readonly IMemoryOwner<short> predictionScratchOwner;

    /// <summary>
    /// The reusable predictor portion of <see cref="predictionScratchOwner"/>, excluding compound and chroma-from-luma storage.
    /// </summary>
    private readonly int predictorWorkingLength;

    /// <summary>
    /// Reconstructs intra-predicted blocks using the frame-owned prediction workspace.
    /// </summary>
    private readonly Av1PredictionDecoder predictionDecoder;

    /// <summary>
    /// Indicates whether transform traversal must also populate loop-filter parameters.
    /// </summary>
    private readonly bool isLoopFilterEnabled;

    /// <summary>
    /// The next packed coefficient position for each plane in the current superblock.
    /// </summary>
    private InlineArray4<int> currentCoefficientIndex;

    /// <summary>
    /// Accumulates reconstructed luma samples until a chroma-from-luma prediction block can consume them.
    /// </summary>
    private readonly Av1ChromaFromLumaContext chromaFromLumaContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1BlockDecoder"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The decoded sequence header.</param>
    /// <param name="frameHeader">The decoded frame header.</param>
    /// <param name="frameBuffer">The frame buffer receiving reconstructed samples.</param>
    /// <param name="loopFilterContext">The transform-size map populated while reconstructing blocks.</param>
    /// <param name="inverseQuantizer">The inverse quantizer carrying the active superblock delta-Q state.</param>
    /// <param name="referenceFrames">The retained reconstructed frames selected by inter blocks.</param>
    /// <param name="paletteColorIndexMaps">The complete decoder-session palette map state.</param>
    public Av1BlockDecoder(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameBuffer<byte> frameBuffer,
        Av1LoopFilterContext loopFilterContext,
        Av1InverseQuantizer inverseQuantizer,
        Av1ReferenceFrameStore referenceFrames,
        Av1TileReader.PaletteColorIndexMaps? paletteColorIndexMaps = null)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameBuffer = frameBuffer;
        this.loopFilterContext = loopFilterContext;
        this.inverseQuantizer = inverseQuantizer;
        this.referenceFrames = referenceFrames;
        int ySize = (1 << this.sequenceHeader.SuperblockSizeLog2) * (1 << this.sequenceHeader.SuperblockSizeLog2);

        // One scratch plane is reused for every transform unit. Its maximum size must cover a complete superblock
        // across all coded planes, with chroma dimensions reduced independently by their subsampling axes.
        int inverseQuantizationSize = ySize +
            (this.sequenceHeader.ColorConfig.SubSamplingX ? ySize >> 2 : ySize) +
            (this.sequenceHeader.ColorConfig.SubSamplingY ? ySize >> 2 : ySize);

        IMemoryOwner<int>? inverseQuantizationOwner = null;
        IMemoryOwner<int>? transformWorkspaceOwner = null;
        IMemoryOwner<short>? predictionScratchOwner = null;
        try
        {
            inverseQuantizationOwner = this.frameBuffer.MemoryAllocator.Allocate<int>(inverseQuantizationSize);
            transformWorkspaceOwner = this.frameBuffer.MemoryAllocator.Allocate<int>(Av1TransformWorkspace.MaximumLength);
            int maximumBlockLength = 1 << sequenceHeader.SuperblockSizeLog2;
            int maximumBlockArea = maximumBlockLength * maximumBlockLength;
            int predictorWorkingLength = Math.Max(
                Av1PredictionDecoder.ScratchLength,
                Math.Max(
                    Av1TranslationalInterPredictor.GetScratchLength(maximumBlockLength, maximumBlockLength),
                    Av1ScaledInterPredictor.GetMaximumScaledScratchLength(maximumBlockLength, maximumBlockLength)));

            int compoundMaskLength = (maximumBlockArea + 1) >> 1;
            int predictorWorkingOffset = (2 * maximumBlockArea) + compoundMaskLength;
            int chromaFromLumaOffset = predictorWorkingOffset + predictorWorkingLength;

            // Compound prediction retains both high-precision reference planes plus the full-resolution luma mask.
            // Keeping those planes, convolution workspace, and CfL surface in one owner avoids independent managed
            // buffers while ensuring the two scratch consumers never overlap.
            int predictionScratchLength = chromaFromLumaOffset + Av1ChromaFromLumaContext.BufferLength;
            predictionScratchOwner = this.frameBuffer.MemoryAllocator.Allocate<short>(predictionScratchLength);

            this.inverseQuantizationOwner = inverseQuantizationOwner;
            this.transformWorkspaceOwner = transformWorkspaceOwner;
            this.predictionScratchOwner = predictionScratchOwner;
            this.predictorWorkingLength = predictorWorkingLength;
            this.predictionDecoder = new(
                sequenceHeader,
                frameHeader,
                predictionScratchOwner.Memory.Slice(predictorWorkingOffset, predictorWorkingLength),
                paletteColorIndexMaps);
            this.isLoopFilterEnabled = frameHeader.LoopFilterParameters.FilterLevel[0] != 0 ||
                frameHeader.LoopFilterParameters.FilterLevel[1] != 0;

            this.chromaFromLumaContext = new(
                sequenceHeader.ColorConfig,
                predictionScratchOwner.Memory.Slice(chromaFromLumaOffset, Av1ChromaFromLumaContext.BufferLength));
        }
        catch
        {
            // A constructor that does not return transfers no ownership to its caller. Unwind successful rents in
            // reverse order so allocator diagnostics and pooled buffers remain balanced after any later allocation.
            predictionScratchOwner?.Dispose();
            transformWorkspaceOwner?.Dispose();
            inverseQuantizationOwner?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the reusable raster-order coefficient buffer populated by inverse quantization.
    /// </summary>
    public Span<int> CurrentInverseQuantizationCoefficients => this.inverseQuantizationOwner.Memory.Span;

    /// <summary>
    /// Releases the pooled reconstruction workspaces owned by this decoder.
    /// </summary>
    public void Dispose()
    {
        this.predictionScratchOwner.Dispose();
        this.transformWorkspaceOwner.Dispose();
        this.inverseQuantizationOwner.Dispose();
    }

    /// <summary>
    /// Resets the per-plane packed coefficient cursors before reconstructing a superblock.
    /// </summary>
    /// <param name="superblockInfo">The superblock whose coefficient streams will be consumed.</param>
    public void UpdateSuperblock(Av1SuperblockInfo superblockInfo)
    {
        // Each superblock owns independent packed coefficient streams for Y, U, and V. The first value for each
        // transform unit stores its coefficient count, so DecodeBlock advances a plane cursor as units are consumed.
        this.currentCoefficientIndex[0] = 0;
        this.currentCoefficientIndex[1] = 0;
        this.currentCoefficientIndex[2] = 0;
    }

    /// <summary>
    /// Reconstructs every luma and chroma transform unit belonging to one decoded AV1 block.
    /// </summary>
    /// <param name="modeInfo">The decoded prediction, segmentation, skip, and transform state.</param>
    /// <param name="modeInfoPosition">The block origin in units of four luma samples.</param>
    /// <param name="blockSize">The decoded block size.</param>
    /// <param name="superblockInfo">The owning superblock's transform and coefficient storage.</param>
    /// <param name="tileInfo">The tile boundaries used to determine neighbor availability.</param>
    public void DecodeBlock(Av1BlockModeInfo modeInfo, Point modeInfoPosition, Av1BlockSize blockSize, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo)
    {
        Span<int> transformWorkspace = this.transformWorkspaceOwner.Memory.Span;

        ObuColorConfig colorConfig = this.sequenceHeader.ColorConfig;
        Av1TransformType transformType;
        Av1TransformSize transformSize;
        int transformUnitCount;
        bool hasChroma = Av1TileReader.HasChroma(this.sequenceHeader, modeInfoPosition, blockSize);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, hasChroma, modeInfo.PartitionType)
        {
            ColumnIndex = modeInfoPosition.X,
            RowIndex = modeInfoPosition.Y,
            ChromaFromLumaContext = this.chromaFromLumaContext
        };

        partitionInfo.ComputeBoundaryOffsets(this.sequenceHeader, this.frameHeader, tileInfo);

        if (hasChroma)
        {
            // A one-unit luma edge maps to the same chroma sample as the adjacent unit on a subsampled axis. In that
            // case the usable chroma neighbor is two mode-info units away rather than immediately above or left.
            if (colorConfig.SubSamplingY && blockSize.Get4x4HighCount() == 1)
            {
                partitionInfo.AvailableAboveForChroma = modeInfoPosition.Y - 2 >= tileInfo.ModeInfoRowStart;
            }

            if (colorConfig.SubSamplingX && blockSize.Get4x4WideCount() == 1)
            {
                partitionInfo.AvailableLeftForChroma = modeInfoPosition.X - 2 >= tileInfo.ModeInfoColumnStart;
            }
        }

        partitionInfo.PopulateModeInfoNeighbors(colorConfig);

        int maxBlocksWide = partitionInfo.GetMaxBlockWide(blockSize, false);
        int maxBlocksHigh = partitionInfo.GetMaxBlockHigh(blockSize, false);

        bool isLossless = this.frameHeader.LosslessArray[modeInfo.SegmentId];
        bool isLosslessBlock = isLossless && ((blockSize >= Av1BlockSize.Block64x64) && (blockSize <= Av1BlockSize.Block128x128));
        int chromaTransformUnitCount = isLosslessBlock
            ? (maxBlocksWide * maxBlocksHigh) >> ((colorConfig.SubSamplingX ? 1 : 0) + (colorConfig.SubSamplingY ? 1 : 0))
            : modeInfo.GetTransformUnitCount(Av1Plane.U);

        bool isInterBlock = modeInfo.ReferenceFrames[0] >= Av1ReferenceFrameType.Last;
        InterReferenceBuffers? interReferenceBuffers = null;
        bool isCompound = modeInfo.ReferenceFrames[1] > Av1ReferenceFrameType.Intra;
        bool isInterIntra = modeInfo.ReferenceFrames[1] == Av1ReferenceFrameType.Intra;
        int firstCompoundWeight = 8;
        int secondCompoundWeight = 8;
        if (isInterBlock)
        {
            Av1FrameBuffer<byte> primaryReferenceFrameBuffer = this.ResolveReferenceFrame(modeInfo.ReferenceFrames[0]);
            Av1FrameBuffer<byte> secondaryReferenceFrameBuffer = primaryReferenceFrameBuffer;
            if (isCompound)
            {
                secondaryReferenceFrameBuffer = this.ResolveReferenceFrame(modeInfo.ReferenceFrames[1]);
                if (modeInfo.CompoundType == Av1CompoundType.DistanceWeighted)
                {
                    Av1CompoundDistanceWeights.Derive(
                        this.sequenceHeader.OrderHintInfo,
                        this.frameHeader,
                        modeInfo.ReferenceFrames[0],
                        modeInfo.ReferenceFrames[1],
                        out firstCompoundWeight,
                        out secondCompoundWeight);
                }
            }

            // A non-compound block aliases the unused secondary slot to its required primary frame. This keeps the
            // published inter state complete without manufacturing a nullable second half.
            interReferenceBuffers = new(primaryReferenceFrameBuffer, secondaryReferenceFrameBuffer);
        }

        bool highBitDepth = this.frameBuffer.BytesPerSample == 2;
        for (int plane = 0; plane < colorConfig.PlaneCount; plane++)
        {
            int subX = (plane > 0) && colorConfig.SubSamplingX ? 1 : 0;
            int subY = (plane > 0) && colorConfig.SubSamplingY ? 1 : 0;

            if (plane != 0 && !partitionInfo.IsChroma)
            {
                continue;
            }

            // Luma transform descriptors occupy their own stream. U and V share one stream, with the V descriptors
            // following the U descriptors for this block, so the V base includes the complete U transform-unit count.
            int transformInfoIndex = plane switch
            {
                2 => modeInfo.GetFirstTransformLocation(Av1Plane.V) + chromaTransformUnitCount,
                1 => modeInfo.GetFirstTransformLocation(Av1Plane.U),
                0 => modeInfo.GetFirstTransformLocation(Av1Plane.Y),
                _ => throw new InvalidImageContentException("Maximum of 3 color planes")
            };
            Span<Av1TransformInfo> transformInfo = superblockInfo.GetTransformInfo(plane)[transformInfoIndex..];

            if (isLosslessBlock)
            {
                Guard.IsTrue(transformInfo[0].Size == Av1TransformSize.Size4x4, nameof(transformInfo), "Lossless may only have 4x4 blocks.");
                transformUnitCount = (maxBlocksWide * maxBlocksHigh) >> (subX + subY);
            }
            else
            {
                transformUnitCount = modeInfo.GetTransformUnitCount((Av1Plane)plane);
            }

            Guard.IsFalse(transformUnitCount == 0, nameof(transformUnitCount), "Must have at least a single transform unit to decode.");

            Point pixelPosition = new(
                (modeInfoPosition.X >> subX) << Av1Constants.ModeInfoSizeLog2,
                (modeInfoPosition.Y >> subY) << Av1Constants.ModeInfoSizeLog2);

            Span<byte> blockReconstructionBuffer = default;
            Span<short> highBitDepthBlockReconstructionBuffer = default;
            int reconstructionStride;

            // Prediction reads the row immediately above the destination through negative-relative neighbor offsets.
            // The frame-buffer helpers therefore return a span beginning one logical sample row before the block.
            if (highBitDepth)
            {
                highBitDepthBlockReconstructionBuffer = this.frameBuffer.DeriveBlockPointer16((Av1Plane)plane, pixelPosition, subX, subY, out reconstructionStride);
            }
            else
            {
                blockReconstructionBuffer = this.frameBuffer.DeriveBlockPointer((Av1Plane)plane, pixelPosition, subX, subY, out reconstructionStride);
            }

            if (interReferenceBuffers is not null)
            {
                InterReferenceBuffers referenceBuffers = interReferenceBuffers.Value;
                Av1FrameBuffer<byte> primaryReferenceFrameBuffer = referenceBuffers.Primary;

                int predictionWidth = Math.Max(4, blockSize.GetWidth() >> subX);
                int predictionHeight = Math.Max(4, blockSize.GetHeight() >> subY);
                int maximumBlockLength = 1 << this.sequenceHeader.SuperblockSizeLog2;
                int maximumBlockArea = maximumBlockLength * maximumBlockLength;
                int compoundMaskStorageLength = (maximumBlockArea + 1) >> 1;
                Span<short> predictionStorage = this.predictionScratchOwner.Memory.Span;
                Span<short> secondPredictionStorage = predictionStorage[..maximumBlockArea];
                Span<ushort> firstCompoundPrediction = MemoryMarshal.Cast<short, ushort>(
                    predictionStorage.Slice(maximumBlockArea, maximumBlockArea));

                Span<byte> compoundMask = MemoryMarshal.AsBytes(
                    predictionStorage.Slice(2 * maximumBlockArea, compoundMaskStorageLength))[..(blockSize.GetWidth() * blockSize.GetHeight())];

                Span<short> predictionScratch =
                    predictionStorage.Slice(
                        (2 * maximumBlockArea) + compoundMaskStorageLength,
                        this.predictorWorkingLength);

                Span<byte> secondPrediction = MemoryMarshal.AsBytes(secondPredictionStorage)[..(predictionWidth * predictionHeight)];
                Span<ushort> highBitDepthSecondPrediction = MemoryMarshal.Cast<short, ushort>(secondPredictionStorage)[..(predictionWidth * predictionHeight)];
                bool usesSub8x8ChromaPrediction =
                    plane != 0 &&
                    !isCompound &&
                    this.TryPredictSub8x8Chroma(
                        ref partitionInfo,
                        modeInfoPosition,
                        blockSize,
                        plane,
                        subX,
                        subY,
                        pixelPosition,
                        predictionWidth,
                        predictionHeight,
                        blockReconstructionBuffer,
                        highBitDepthBlockReconstructionBuffer,
                        reconstructionStride,
                        predictionScratch);

                int referenceCount = usesSub8x8ChromaPrediction ? 0 : isCompound ? 2 : 1;

                // Every compound predictor is combined before its final rounding step. Warped prediction has its own
                // convolution kernels, but the reference decoder writes their output into the same unsigned no-round domain.
                bool useHighBitDepthCompoundIntermediates =
                    highBitDepth &&
                    modeInfo.CompoundType is (
                        Av1CompoundType.Average or
                        Av1CompoundType.DistanceWeighted or
                        Av1CompoundType.Wedge or
                        Av1CompoundType.DifferenceWeighted);

                bool useCompoundIntermediates =
                    isCompound &&
                    (!highBitDepth || useHighBitDepthCompoundIntermediates);

                for (int referenceIndex = 0; referenceIndex < referenceCount; referenceIndex++)
                {
                    Av1FrameBuffer<byte> activeReferenceFrameBuffer = referenceIndex == 0
                        ? referenceBuffers.Primary
                        : referenceBuffers.Secondary;

                    Av1MotionVector motionVector = modeInfo.MotionVectors[referenceIndex];
                    int destinationStride = useCompoundIntermediates
                        ? predictionWidth
                        : referenceIndex == 0 ? reconstructionStride : predictionWidth;
                    bool isScaledReference = activeReferenceFrameBuffer.Width != this.frameHeader.FrameSize.FrameWidth ||
                        activeReferenceFrameBuffer.Height != this.frameHeader.FrameSize.FrameHeight;

                    // Warped prediction is selected per plane. In subsampled frames an otherwise qualifying 8x8 luma
                    // block has a 4x4 chroma prediction, which the reference decoder deliberately reconstructs with the translational
                    // center motion vector. Scaled references and integer-only frames exclude both local and global warp.
                    bool canUseWarpedPrediction =
                        !isScaledReference &&
                        !this.frameHeader.ForceIntegerMotionVector &&
                        predictionWidth >= 8 &&
                        predictionHeight >= 8;

                    Av1GlobalMotionParameters warpedMotionParameters = modeInfo.WarpedMotionParameters;
                    bool useWarpedPrediction =
                        canUseWarpedPrediction &&
                        referenceIndex == 0 &&
                        modeInfo.MotionMode == Av1MotionMode.Warped &&
                        !warpedMotionParameters.IsInvalid;

                    if (canUseWarpedPrediction && !useWarpedPrediction)
                    {
                        bool usesGlobalMotion = modeInfo.YMode == Av1PredictionMode.GlobalGlobalMotionVector ||
                            (referenceIndex == 0 && modeInfo.YMode == Av1PredictionMode.GlobalMotionVector);

                        if (usesGlobalMotion)
                        {
                            int canonicalReferenceIndex =
                                (int)modeInfo.ReferenceFrames[referenceIndex] - (int)Av1ReferenceFrameType.Last;

                            Av1GlobalMotionParameters globalMotionParameters =
                                this.frameHeader.GetGlobalMotionParameters()[canonicalReferenceIndex];

                            // Identity and translation GLOBALMV modes use their derived center vector. Rotation/zoom and
                            // affine models use the complete matrix only when the decoded shear parameters are valid.
                            if (globalMotionParameters.Type > Av1GlobalMotionType.Translation &&
                                !globalMotionParameters.IsInvalid)
                            {
                                warpedMotionParameters = globalMotionParameters;
                                useWarpedPrediction = true;
                            }
                        }
                    }

                    if (useWarpedPrediction)
                    {
                        int referencePlaneWidth = Av1Math.DivideLog2Ceiling(activeReferenceFrameBuffer.Width, subX);
                        int referencePlaneHeight = Av1Math.DivideLog2Ceiling(activeReferenceFrameBuffer.Height, subY);
                        if (highBitDepth)
                        {
                            Span<ushort> source = activeReferenceFrameBuffer.GetPaddedPlaneSpan16(
                                (Av1Plane)plane,
                                subX,
                                subY,
                                out int sourceStride,
                                out Point sourceOrigin);

                            if (useCompoundIntermediates)
                            {
                                Span<ushort> destination = referenceIndex == 0
                                    ? firstCompoundPrediction
                                    : highBitDepthSecondPrediction;

                                Av1WarpedInterPredictor.PredictWarpedCompound(
                                    source,
                                    sourceStride,
                                    sourceOrigin,
                                    referencePlaneWidth,
                                    referencePlaneHeight,
                                    destination,
                                    destinationStride,
                                    pixelPosition,
                                    predictionWidth,
                                    predictionHeight,
                                    subX,
                                    subY,
                                    this.frameBuffer.BitDepth.GetBitCount(),
                                    warpedMotionParameters,
                                    predictionScratch);
                            }
                            else
                            {
                                Span<ushort> destination = MemoryMarshal.Cast<short, ushort>(
                                    referenceIndex == 0
                                        ? highBitDepthBlockReconstructionBuffer[reconstructionStride..]
                                        : secondPredictionStorage);

                                Av1WarpedInterPredictor.PredictWarped(
                                    source,
                                    sourceStride,
                                    sourceOrigin,
                                    referencePlaneWidth,
                                    referencePlaneHeight,
                                    destination,
                                    destinationStride,
                                    pixelPosition,
                                    predictionWidth,
                                    predictionHeight,
                                    subX,
                                    subY,
                                    this.frameBuffer.BitDepth.GetBitCount(),
                                    warpedMotionParameters,
                                    predictionScratch);
                            }
                        }
                        else
                        {
                            Span<byte> source = activeReferenceFrameBuffer.GetPaddedPlaneSpan(
                                (Av1Plane)plane,
                                subX,
                                subY,
                                out int sourceStride,
                                out Point sourceOrigin);

                            if (useCompoundIntermediates)
                            {
                                Span<ushort> destination = referenceIndex == 0
                                    ? firstCompoundPrediction
                                    : highBitDepthSecondPrediction;

                                Av1WarpedInterPredictor.PredictWarpedCompound(
                                    source,
                                    sourceStride,
                                    sourceOrigin,
                                    referencePlaneWidth,
                                    referencePlaneHeight,
                                    destination,
                                    destinationStride,
                                    pixelPosition,
                                    predictionWidth,
                                    predictionHeight,
                                    subX,
                                    subY,
                                    warpedMotionParameters,
                                    predictionScratch);
                            }
                            else
                            {
                                Span<byte> destination = referenceIndex == 0
                                    ? blockReconstructionBuffer[reconstructionStride..]
                                    : secondPrediction;

                                Av1WarpedInterPredictor.PredictWarped(
                                    source,
                                    sourceStride,
                                    sourceOrigin,
                                    referencePlaneWidth,
                                    referencePlaneHeight,
                                    destination,
                                    destinationStride,
                                    pixelPosition,
                                    predictionWidth,
                                    predictionHeight,
                                    subX,
                                    subY,
                                    warpedMotionParameters,
                                    predictionScratch);
                            }
                        }

                        continue;
                    }

                    if (isScaledReference)
                    {
                        Span<byte> scaledDestination = default;
                        Span<ushort> scaledHighBitDepthDestination = default;
                        Span<ushort> scaledCompoundDestination = default;
                        if (useCompoundIntermediates)
                        {
                            scaledCompoundDestination = referenceIndex == 0
                                ? firstCompoundPrediction
                                : highBitDepthSecondPrediction;
                        }
                        else if (highBitDepth)
                        {
                            scaledHighBitDepthDestination = referenceIndex == 0
                                ? MemoryMarshal.Cast<short, ushort>(highBitDepthBlockReconstructionBuffer[reconstructionStride..])
                                : highBitDepthSecondPrediction;
                        }
                        else
                        {
                            scaledDestination = referenceIndex == 0
                                ? blockReconstructionBuffer[reconstructionStride..]
                                : secondPrediction;
                        }

                        this.PredictScaledReference(
                            activeReferenceFrameBuffer,
                            motionVector,
                            plane,
                            subX,
                            subY,
                            pixelPosition,
                            predictionWidth,
                            predictionHeight,
                            modeInfo.InterpolationFilters[1],
                            modeInfo.InterpolationFilters[0],
                            scaledDestination,
                            scaledHighBitDepthDestination,
                            scaledCompoundDestination,
                            destinationStride,
                            predictionScratch);

                        continue;
                    }

                    // AV1 predicts the complete declared plane block even when its luma extent crosses the frame boundary.
                    // Subsampled dimensions retain the mandatory four-sample minimum used by set_plane_n4 in the reference decoder.
                    int horizontalMotionQ4 = motionVector.Column << (1 - subX);
                    int verticalMotionQ4 = motionVector.Row << (1 - subY);
                    int horizontalExtensionQ4 = (4 + predictionWidth) << 4;
                    int verticalExtensionQ4 = (4 + predictionHeight) << 4;
                    int horizontalEdgeScale = 1 << (1 - subX);
                    int verticalEdgeScale = 1 << (1 - subY);

                    // The UMV clamp is expressed in one-sixteenth plane-sample units. A 128-sample block can legally
                    // address 135 samples beyond an edge once its prediction extent and eight-tap filter support are
                    // included; the frame-owned prediction border keeps that source directly addressable.
                    horizontalMotionQ4 = Av1Math.Clip3(
                        (partitionInfo.ModeBlockToLeftEdge * horizontalEdgeScale) - horizontalExtensionQ4,
                        (partitionInfo.ModeBlockToRightEdge * horizontalEdgeScale) + horizontalExtensionQ4 - 16,
                        horizontalMotionQ4);

                    verticalMotionQ4 = Av1Math.Clip3(
                        (partitionInfo.ModeBlockToTopEdge * verticalEdgeScale) - verticalExtensionQ4,
                        (partitionInfo.ModeBlockToBottomEdge * verticalEdgeScale) + verticalExtensionQ4 - 16,
                        verticalMotionQ4);

                    int sourceColumnQ4 = (pixelPosition.X << 4) + horizontalMotionQ4;
                    int sourceRowQ4 = (pixelPosition.Y << 4) + verticalMotionQ4;

                    // Motion vectors use one-eighth luma-sample units. Shifting by one minus the plane subsampling converts
                    // them directly to the predictor's one-sixteenth-plane-sample phase; masking then preserves the signed
                    // floor used to select the integer source sample.
                    int horizontalPhase = sourceColumnQ4 & 15;
                    int verticalPhase = sourceRowQ4 & 15;

                    if (highBitDepth)
                    {
                        Span<ushort> source = activeReferenceFrameBuffer.GetPaddedPlaneSpan16(
                            (Av1Plane)plane,
                            subX,
                            subY,
                            out int sourceStride,
                            out Point sourceOrigin);

                        int sourceIndex =
                            ((sourceOrigin.Y + (sourceRowQ4 >> 4)) * sourceStride) + sourceOrigin.X + (sourceColumnQ4 >> 4);

                        if (useCompoundIntermediates)
                        {
                            Span<ushort> destination = referenceIndex == 0
                                ? firstCompoundPrediction
                                : highBitDepthSecondPrediction;

                            Av1CompoundInterPredictor.PredictCompound(
                                source,
                                sourceStride,
                                sourceIndex,
                                destination,
                                predictionWidth,
                                predictionWidth,
                                predictionHeight,
                                modeInfo.InterpolationFilters[1],
                                modeInfo.InterpolationFilters[0],
                                horizontalPhase,
                                verticalPhase,
                                this.frameBuffer.BitDepth.GetBitCount(),
                                predictionScratch);
                        }
                        else
                        {
                            Span<ushort> destination = referenceIndex == 0
                                ? MemoryMarshal.Cast<short, ushort>(highBitDepthBlockReconstructionBuffer[reconstructionStride..])
                                : highBitDepthSecondPrediction;

                            Av1TranslationalInterPredictor.Predict(
                                source,
                                sourceStride,
                                sourceIndex,
                                destination,
                                destinationStride,
                                predictionWidth,
                                predictionHeight,
                                modeInfo.InterpolationFilters[1],
                                modeInfo.InterpolationFilters[0],
                                horizontalPhase,
                                verticalPhase,
                                this.frameBuffer.BitDepth.GetBitCount(),
                                predictionScratch);
                        }
                    }
                    else
                    {
                        Span<byte> source = activeReferenceFrameBuffer.GetPaddedPlaneSpan(
                            (Av1Plane)plane,
                            subX,
                            subY,
                            out int sourceStride,
                            out Point sourceOrigin);

                        int sourceIndex =
                            ((sourceOrigin.Y + (sourceRowQ4 >> 4)) * sourceStride) + sourceOrigin.X + (sourceColumnQ4 >> 4);

                        if (useCompoundIntermediates)
                        {
                            Span<ushort> destination = referenceIndex == 0
                                ? firstCompoundPrediction
                                : highBitDepthSecondPrediction;

                            Av1CompoundInterPredictor.PredictCompound(
                                source,
                                sourceStride,
                                sourceIndex,
                                destination,
                                predictionWidth,
                                predictionWidth,
                                predictionHeight,
                                modeInfo.InterpolationFilters[1],
                                modeInfo.InterpolationFilters[0],
                                horizontalPhase,
                                verticalPhase,
                                predictionScratch);
                        }
                        else
                        {
                            Span<byte> destination = referenceIndex == 0
                                ? blockReconstructionBuffer[reconstructionStride..]
                                : secondPrediction;

                            Av1TranslationalInterPredictor.Predict(
                                source,
                                sourceStride,
                                sourceIndex,
                                destination,
                                destinationStride,
                                predictionWidth,
                                predictionHeight,
                                modeInfo.InterpolationFilters[1],
                                modeInfo.InterpolationFilters[0],
                                horizontalPhase,
                                verticalPhase,
                                predictionScratch);
                        }
                    }
                }

                if (isCompound)
                {
                    if (useCompoundIntermediates)
                    {
                        ReadOnlySpan<ushort> first = firstCompoundPrediction[..(predictionWidth * predictionHeight)];
                        if (highBitDepth)
                        {
                            Span<ushort> highBitDepthDestination = MemoryMarshal.Cast<short, ushort>(
                                highBitDepthBlockReconstructionBuffer[reconstructionStride..]);

                            if (modeInfo.CompoundType == Av1CompoundType.DistanceWeighted)
                            {
                                // Distance weighting must consume the no-round intermediates. Equal-averaging the
                                // already filtered references loses the decoded display-distance contribution.
                                Av1CompoundIntermediateDistanceWeightedPredictor.DistanceWeightedIntermediate(
                                    highBitDepthDestination,
                                    reconstructionStride,
                                    first,
                                    predictionWidth,
                                    highBitDepthSecondPrediction,
                                    predictionWidth,
                                    predictionWidth,
                                    predictionHeight,
                                    firstCompoundWeight,
                                    secondCompoundWeight,
                                    this.frameBuffer.BitDepth.GetBitCount());
                            }
                            else if (modeInfo.CompoundType == Av1CompoundType.Wedge)
                            {
                                Av1WedgeMask.Fill(
                                    compoundMask,
                                    predictionWidth,
                                    blockSize,
                                    modeInfo.CompoundWedgeIndex,
                                    modeInfo.CompoundWedgeSign,
                                    subX,
                                    subY,
                                    invert: false);

                                // Masked compound prediction must blend the same no-round intermediates as the reference decoder's
                                // high-bit-depth d16 path so the mask is applied before the sole final rounding step.
                                Av1CompoundIntermediateMaskBlendPredictor.BlendIntermediate(
                                    highBitDepthDestination,
                                    reconstructionStride,
                                    first,
                                    predictionWidth,
                                    highBitDepthSecondPrediction,
                                    predictionWidth,
                                    compoundMask,
                                    predictionWidth,
                                    predictionWidth,
                                    predictionHeight,
                                    subX: 0,
                                    subY: 0,
                                    this.frameBuffer.BitDepth.GetBitCount());
                            }
                            else if (modeInfo.CompoundType == Av1CompoundType.DifferenceWeighted)
                            {
                                int lumaWidth = blockSize.GetWidth();
                                if (plane == 0)
                                {
                                    // Difference-weighted chroma reuses the luma-derived segment mask. Building it
                                    // only for plane zero preserves that decoded contract before chroma subsampling.
                                    Av1CompoundIntermediateDifferenceWeightedMaskBuilder.FillDifferenceWeightedIntermediateMask(
                                        compoundMask,
                                        lumaWidth,
                                        first,
                                        predictionWidth,
                                        highBitDepthSecondPrediction,
                                        predictionWidth,
                                        predictionWidth,
                                        predictionHeight,
                                        this.frameBuffer.BitDepth.GetBitCount(),
                                        modeInfo.DifferenceWeightedMaskType);
                                }

                                // The d16 mask and final blend consume the same no-round intermediates. Rounding
                                // either reference first changes both the derived mask and the reconstructed sample.
                                Av1CompoundIntermediateMaskBlendPredictor.BlendIntermediate(
                                    highBitDepthDestination,
                                    reconstructionStride,
                                    first,
                                    predictionWidth,
                                    highBitDepthSecondPrediction,
                                    predictionWidth,
                                    compoundMask,
                                    lumaWidth,
                                    predictionWidth,
                                    predictionHeight,
                                    subX,
                                    subY,
                                    this.frameBuffer.BitDepth.GetBitCount());
                            }
                            else
                            {
                                Av1CompoundIntermediateAveragePredictor.AverageIntermediate(
                                    highBitDepthDestination,
                                    reconstructionStride,
                                    first,
                                    predictionWidth,
                                    highBitDepthSecondPrediction,
                                    predictionWidth,
                                    predictionWidth,
                                    predictionHeight,
                                    this.frameBuffer.BitDepth.GetBitCount());
                            }
                        }
                        else
                        {
                            Span<byte> destination = blockReconstructionBuffer[reconstructionStride..];
                            switch (modeInfo.CompoundType)
                            {
                                case Av1CompoundType.Average:
                                    Av1CompoundIntermediateAveragePredictor.AverageIntermediate(
                                        destination,
                                        reconstructionStride,
                                        first,
                                        predictionWidth,
                                        highBitDepthSecondPrediction,
                                        predictionWidth,
                                        predictionWidth,
                                        predictionHeight,
                                        bitDepth: 8);

                                    break;
                                case Av1CompoundType.DistanceWeighted:
                                    Av1CompoundIntermediateDistanceWeightedPredictor.DistanceWeightedIntermediate(
                                        destination,
                                        reconstructionStride,
                                        first,
                                        predictionWidth,
                                        highBitDepthSecondPrediction,
                                        predictionWidth,
                                        predictionWidth,
                                        predictionHeight,
                                        firstCompoundWeight,
                                        secondCompoundWeight,
                                        bitDepth: 8);

                                    break;
                                case Av1CompoundType.Wedge:
                                    Av1WedgeMask.Fill(
                                        compoundMask,
                                        predictionWidth,
                                        blockSize,
                                        modeInfo.CompoundWedgeIndex,
                                        modeInfo.CompoundWedgeSign,
                                        subX,
                                        subY,
                                        invert: false);

                                    Av1CompoundIntermediateMaskBlendPredictor.BlendIntermediate(
                                        destination,
                                        reconstructionStride,
                                        first,
                                        predictionWidth,
                                        highBitDepthSecondPrediction,
                                        predictionWidth,
                                        compoundMask,
                                        predictionWidth,
                                        predictionWidth,
                                        predictionHeight,
                                        subX: 0,
                                        subY: 0,
                                        bitDepth: 8);

                                    break;
                                default:
                                    int lumaWidth = blockSize.GetWidth();
                                    if (plane == 0)
                                    {
                                        Av1CompoundIntermediateDifferenceWeightedMaskBuilder.FillDifferenceWeightedIntermediateMask(
                                            compoundMask,
                                            lumaWidth,
                                            first,
                                            predictionWidth,
                                            highBitDepthSecondPrediction,
                                            predictionWidth,
                                            predictionWidth,
                                            predictionHeight,
                                            bitDepth: 8,
                                            modeInfo.DifferenceWeightedMaskType);
                                    }

                                    Av1CompoundIntermediateMaskBlendPredictor.BlendIntermediate(
                                        destination,
                                        reconstructionStride,
                                        first,
                                        predictionWidth,
                                        highBitDepthSecondPrediction,
                                        predictionWidth,
                                        compoundMask,
                                        lumaWidth,
                                        predictionWidth,
                                        predictionHeight,
                                        subX,
                                        subY,
                                        bitDepth: 8);

                                    break;
                            }
                        }
                    }
                    else if (highBitDepth)
                    {
                        Span<ushort> destination = MemoryMarshal.Cast<short, ushort>(
                            highBitDepthBlockReconstructionBuffer[reconstructionStride..]);

                        switch (modeInfo.CompoundType)
                        {
                            case Av1CompoundType.Average:
                                Av1CompoundAveragePredictor.Average(
                                    destination,
                                    reconstructionStride,
                                    highBitDepthSecondPrediction,
                                    predictionWidth,
                                    predictionWidth,
                                    predictionHeight);

                                break;
                            case Av1CompoundType.DistanceWeighted:
                                Av1CompoundDistanceWeightedPredictor.DistanceWeighted(
                                    destination,
                                    reconstructionStride,
                                    highBitDepthSecondPrediction,
                                    predictionWidth,
                                    predictionWidth,
                                    predictionHeight,
                                    firstCompoundWeight,
                                    secondCompoundWeight);

                                break;
                            case Av1CompoundType.Wedge:
                                Av1WedgeMask.Fill(
                                    compoundMask,
                                    predictionWidth,
                                    blockSize,
                                    modeInfo.CompoundWedgeIndex,
                                    modeInfo.CompoundWedgeSign,
                                    subX,
                                    subY,
                                    invert: false);

                                Av1CompoundMaskBlendPredictor.Blend(
                                    destination,
                                    reconstructionStride,
                                    highBitDepthSecondPrediction,
                                    predictionWidth,
                                    compoundMask,
                                    predictionWidth,
                                    predictionWidth,
                                    predictionHeight);

                                break;
                            default:
                                Av1DifferenceWeightedMaskBuilder.FillDifferenceWeightedMask(
                                    compoundMask,
                                    predictionWidth,
                                    destination,
                                    reconstructionStride,
                                    highBitDepthSecondPrediction,
                                    predictionWidth,
                                    predictionWidth,
                                    predictionHeight,
                                    this.frameBuffer.BitDepth.GetBitCount(),
                                    modeInfo.DifferenceWeightedMaskType);

                                Av1CompoundMaskBlendPredictor.Blend(
                                    destination,
                                    reconstructionStride,
                                    highBitDepthSecondPrediction,
                                    predictionWidth,
                                    compoundMask,
                                    predictionWidth,
                                    predictionWidth,
                                    predictionHeight);

                                break;
                        }
                    }
                    else
                    {
                        Span<byte> destination = blockReconstructionBuffer[reconstructionStride..];
                        switch (modeInfo.CompoundType)
                        {
                            case Av1CompoundType.Average:
                                Av1CompoundAveragePredictor.Average(
                                    destination,
                                    reconstructionStride,
                                    secondPrediction,
                                    predictionWidth,
                                    predictionWidth,
                                    predictionHeight);

                                break;
                            case Av1CompoundType.DistanceWeighted:
                                Av1CompoundDistanceWeightedPredictor.DistanceWeighted(
                                    destination,
                                    reconstructionStride,
                                    secondPrediction,
                                    predictionWidth,
                                    predictionWidth,
                                    predictionHeight,
                                    firstCompoundWeight,
                                    secondCompoundWeight);

                                break;
                            case Av1CompoundType.Wedge:
                                Av1WedgeMask.Fill(
                                    compoundMask,
                                    predictionWidth,
                                    blockSize,
                                    modeInfo.CompoundWedgeIndex,
                                    modeInfo.CompoundWedgeSign,
                                    subX,
                                    subY,
                                    invert: false);

                                Av1CompoundMaskBlendPredictor.Blend(
                                    destination,
                                    reconstructionStride,
                                    secondPrediction,
                                    predictionWidth,
                                    compoundMask,
                                    predictionWidth,
                                    predictionWidth,
                                    predictionHeight);

                                break;
                            default:
                                Av1DifferenceWeightedMaskBuilder.FillDifferenceWeightedMask(
                                    compoundMask,
                                    predictionWidth,
                                    destination,
                                    reconstructionStride,
                                    secondPrediction,
                                    predictionWidth,
                                    predictionWidth,
                                    predictionHeight,
                                    modeInfo.DifferenceWeightedMaskType);

                                Av1CompoundMaskBlendPredictor.Blend(
                                    destination,
                                    reconstructionStride,
                                    secondPrediction,
                                    predictionWidth,
                                    compoundMask,
                                    predictionWidth,
                                    predictionWidth,
                                    predictionHeight);

                                break;
                        }
                    }
                }
                else if (isInterIntra)
                {
                    if (highBitDepth)
                    {
                        this.predictionDecoder.DecodeInterIntra(
                            ref partitionInfo,
                            (Av1Plane)plane,
                            tileInfo,
                            highBitDepthBlockReconstructionBuffer,
                            reconstructionStride,
                            secondPredictionStorage[..(predictionWidth * predictionHeight)],
                            predictionWidth,
                            this.frameBuffer.BitDepth);

                        if (modeInfo.UseInterIntraWedge)
                        {
                            Av1WedgeMask.Fill(
                                compoundMask,
                                predictionWidth,
                                blockSize,
                                modeInfo.InterIntraWedgeIndex,
                                wedgeSign: false,
                                subX,
                                subY,
                                invert: true);
                        }
                        else
                        {
                            Av1InterIntraMaskBuilder.FillInterIntraMask(
                                compoundMask,
                                predictionWidth,
                                predictionWidth,
                                predictionHeight,
                                modeInfo.InterIntraMode,
                                invert: true);
                        }

                        Av1CompoundMaskBlendPredictor.Blend(
                            MemoryMarshal.Cast<short, ushort>(highBitDepthBlockReconstructionBuffer[reconstructionStride..]),
                            reconstructionStride,
                            highBitDepthSecondPrediction,
                            predictionWidth,
                            compoundMask,
                            predictionWidth,
                            predictionWidth,
                            predictionHeight);
                    }
                    else
                    {
                        this.predictionDecoder.DecodeInterIntra(
                            ref partitionInfo,
                            (Av1Plane)plane,
                            tileInfo,
                            blockReconstructionBuffer,
                            reconstructionStride,
                            secondPrediction,
                            predictionWidth,
                            this.frameBuffer.BitDepth);

                        if (modeInfo.UseInterIntraWedge)
                        {
                            Av1WedgeMask.Fill(
                                compoundMask,
                                predictionWidth,
                                blockSize,
                                modeInfo.InterIntraWedgeIndex,
                                wedgeSign: false,
                                subX,
                                subY,
                                invert: true);
                        }
                        else
                        {
                            Av1InterIntraMaskBuilder.FillInterIntraMask(
                                compoundMask,
                                predictionWidth,
                                predictionWidth,
                                predictionHeight,
                                modeInfo.InterIntraMode,
                                invert: true);
                        }

                        Av1CompoundMaskBlendPredictor.Blend(
                            blockReconstructionBuffer[reconstructionStride..],
                            reconstructionStride,
                            secondPrediction,
                            predictionWidth,
                            compoundMask,
                            predictionWidth,
                            predictionWidth,
                            predictionHeight);
                    }
                }
                else if (modeInfo.MotionMode == Av1MotionMode.Obmc)
                {
                    this.ApplyOverlappedMotionCompensation(
                        ref partitionInfo,
                        plane,
                        subX,
                        subY,
                        predictionWidth,
                        predictionHeight,
                        blockReconstructionBuffer,
                        highBitDepthBlockReconstructionBuffer,
                        reconstructionStride,
                        secondPrediction,
                        highBitDepthSecondPrediction,
                        compoundMask,
                        predictionScratch);
                }
            }

            for (int tu = 0; tu < transformUnitCount; tu++)
            {
                Span<byte> transformBlockReconstructionBuffer = default;
                Span<short> highBitDepthTransformBlockReconstructionBuffer = default;
                int transformBlockOffset;

                transformSize = transformInfo[0].Size;
                Span<int> coefficients = superblockInfo.GetCoefficients((Av1Plane)plane)[this.currentCoefficientIndex[plane]..];

                // Transform offsets are stored in mode-info units. Reconstruction strides are expressed in logical
                // samples for both storage pipelines, so no byte scaling is applied to the high-bit-depth offset.
                transformBlockOffset = ((transformInfo[0].OffsetY * reconstructionStride) + transformInfo[0].OffsetX) << Av1Constants.ModeInfoSizeLog2;
                if (highBitDepth)
                {
                    highBitDepthTransformBlockReconstructionBuffer = highBitDepthBlockReconstructionBuffer[transformBlockOffset..];
                }
                else
                {
                    transformBlockReconstructionBuffer = blockReconstructionBuffer[transformBlockOffset..];
                }

                if (this.isLoopFilterEnabled)
                {
                    // U and V share transform geometry. Store the chroma map once so the later plane passes consume
                    // identical sizes without retaining duplicate state.
                    if (plane != 2)
                    {
                        Point transformPosition = new(
                            (modeInfoPosition.X >> subX) + transformInfo[0].OffsetX,
                            (modeInfoPosition.Y >> subY) + transformInfo[0].OffsetY);

                        this.loopFilterContext.SetTransformSize((Av1Plane)plane, transformPosition, transformSize);
                    }
                }

                // Intra-block copy is signaled on an intra-only frame but follows AV1's inter prediction and transform
                // rules. Its validated displacement always references an earlier reconstructed region of this frame.
                if (modeInfo.UseIntraBlockCopy)
                {
                    // the reference decoder predicts the complete coding block before traversing its residual transforms. The mandatory
                    // 256-pixel source delay prevents overlap, and the two-tap interpolation is translation-invariant,
                    // so predicting the matching source rectangle for each transform unit produces the same samples.
                    Point transformPixelPosition = new(
                        pixelPosition.X + (transformInfo[0].OffsetX << Av1Constants.ModeInfoSizeLog2),
                        pixelPosition.Y + (transformInfo[0].OffsetY << Av1Constants.ModeInfoSizeLog2));

                    // Displacement vectors use one-eighth luma-sample units. Converting them to the plane's q4 grid
                    // leaves luma on an integer sample and can leave subsampled chroma exactly at phase eight.
                    int sourceColumnQ4 = (transformPixelPosition.X << 4) +
                        (modeInfo.DisplacementVector.Column << (1 - subX));

                    int sourceRowQ4 = (transformPixelPosition.Y << 4) +
                        (modeInfo.DisplacementVector.Row << (1 - subY));

                    int sourcePhaseX = sourceColumnQ4 & 15;
                    int sourcePhaseY = sourceRowQ4 & 15;
                    DebugGuard.IsTrue(sourcePhaseX is 0 or 8, "Intra-block-copy horizontal phase must be an integer or half sample.");
                    DebugGuard.IsTrue(sourcePhaseY is 0 or 8, "Intra-block-copy vertical phase must be an integer or half sample.");

                    Point sourcePixelPosition = new(sourceColumnQ4 >> 4, sourceRowQ4 >> 4);
                    int transformWidth = transformSize.GetWidth();
                    int transformHeight = transformSize.GetHeight();

                    if (highBitDepth)
                    {
                        Span<short> source = this.frameBuffer.DeriveBlockPointer16(
                            (Av1Plane)plane,
                            sourcePixelPosition,
                            subX,
                            subY,
                            out int sourceStride);

                        Av1IntraBlockCopyPredictor.Predict(
                            source[sourceStride..],
                            sourceStride,
                            highBitDepthTransformBlockReconstructionBuffer[reconstructionStride..],
                            reconstructionStride,
                            transformWidth,
                            transformHeight,
                            sourcePhaseX != 0,
                            sourcePhaseY != 0);
                    }
                    else
                    {
                        Span<byte> source = this.frameBuffer.DeriveBlockPointer(
                            (Av1Plane)plane,
                            sourcePixelPosition,
                            subX,
                            subY,
                            out int sourceStride);

                        Av1IntraBlockCopyPredictor.Predict(
                            source[sourceStride..],
                            sourceStride,
                            transformBlockReconstructionBuffer[reconstructionStride..],
                            reconstructionStride,
                            transformWidth,
                            transformHeight,
                            sourcePhaseX != 0,
                            sourcePhaseY != 0);
                    }
                }
                else if (!isInterBlock)
                {
                    // Conventional intra prediction consumes the reference-prefixed destination span before the
                    // transform residual is reconstructed over its first output row.
                    if (highBitDepth)
                    {
                        this.predictionDecoder.Decode(
                            ref partitionInfo,
                            (Av1Plane)plane,
                            transformSize,
                            tileInfo,
                            highBitDepthTransformBlockReconstructionBuffer,
                            reconstructionStride,
                            this.frameBuffer.BitDepth,
                            transformInfo[0].OffsetX,
                            transformInfo[0].OffsetY);
                    }
                    else
                    {
                        this.predictionDecoder.Decode(
                            ref partitionInfo,
                            (Av1Plane)plane,
                            transformSize,
                            tileInfo,
                            transformBlockReconstructionBuffer,
                            reconstructionStride,
                            this.frameBuffer.BitDepth,
                            transformInfo[0].OffsetX,
                            transformInfo[0].OffsetY);
                    }
                }

                int numberOfCoefficients = 0;

                if (!modeInfo.Skip && transformInfo[0].CodeBlockFlag)
                {
                    Span<int> quantizationCoefficients = this.CurrentInverseQuantizationCoefficients;
                    int inverseQuantizationSize = transformSize.GetWidth() * transformSize.GetHeight();
                    quantizationCoefficients[..inverseQuantizationSize].Clear();
                    transformType = transformInfo[0].Type;

                    // Inverse quantization writes raster coefficients into the reusable superblock scratch plane.
                    numberOfCoefficients = this.inverseQuantizer.InverseQuantize(
                        modeInfo, coefficients, quantizationCoefficients, transformType, transformSize, (Av1Plane)plane);
                    if (numberOfCoefficients != 0)
                    {
                        // The packed coefficient stream prefixes every transform unit with its decoded coefficient
                        // count. Advance past that prefix as well as the coefficient values before the next unit.
                        this.currentCoefficientIndex[plane] += numberOfCoefficients + 1;

                        if (highBitDepth)
                        {
                            // Prediction receives a reference-prefixed span beginning on the previous row. Inverse
                            // reconstruction operates on the transform itself, so advance to the first destination row.
                            Av1InverseTransformer.ReconstructHighBitDepth(
                                quantizationCoefficients,
                                highBitDepthTransformBlockReconstructionBuffer[reconstructionStride..],
                                reconstructionStride,
                                transformSize,
                                transformType,
                                plane,
                                numberOfCoefficients,
                                isLossless,
                                this.frameBuffer.BitDepth,
                                transformWorkspace);
                        }
                        else
                        {
                            // Keep the reference-prefix convention local to prediction; residuals are added at the
                            // first reconstructed row rather than the top-neighbor row.
                            Av1InverseTransformer.Reconstruct8Bit(
                                quantizationCoefficients,
                                transformBlockReconstructionBuffer[reconstructionStride..],
                                reconstructionStride,
                                transformSize,
                                transformType,
                                plane,
                                numberOfCoefficients,
                                isLossless,
                                transformWorkspace);
                        }
                    }
                }

                // Store Luma for CFL if required!
                if (plane == (int)Av1Plane.Y && StoreChromaFromLumaRequired(colorConfig, ref partitionInfo))
                {
                    // The predictor span begins on the previous row; CFL storage consumes reconstructed samples from
                    // the transform block itself, hence the explicit one-stride advance for both sample pipelines.
                    if (highBitDepth)
                    {
                        this.chromaFromLumaContext.Store(
                            highBitDepthTransformBlockReconstructionBuffer[reconstructionStride..],
                            reconstructionStride,
                            transformInfo[0].OffsetY,
                            transformInfo[0].OffsetX,
                            transformSize,
                            blockSize,
                            modeInfoPosition.Y,
                            modeInfoPosition.X);
                    }
                    else
                    {
                        this.chromaFromLumaContext.Store(
                            transformBlockReconstructionBuffer[reconstructionStride..],
                            reconstructionStride,
                            transformInfo[0].OffsetY,
                            transformInfo[0].OffsetX,
                            transformSize,
                            blockSize,
                            modeInfoPosition.Y,
                            modeInfoPosition.X);
                    }
                }

                // Transform descriptors are stored in the same traversal order as their packed coefficient groups.
                transformInfo = transformInfo[1..];
            }
        }
    }

    /// <summary>
    /// Reconstructs a subsampled chroma block assembled from multiple neighboring luma inter blocks.
    /// </summary>
    private bool TryPredictSub8x8Chroma(
        ref Av1PartitionInfo partitionInfo,
        Point modeInfoPosition,
        Av1BlockSize blockSize,
        int plane,
        int subX,
        int subY,
        Point pixelPosition,
        int predictionWidth,
        int predictionHeight,
        Span<byte> blockReconstructionBuffer,
        Span<short> highBitDepthBlockReconstructionBuffer,
        int reconstructionStride,
        Span<short> predictionScratch)
    {
        bool isSub4X = blockSize.GetWidth() == 4 && subX != 0;
        bool isSub4Y = blockSize.GetHeight() == 4 && subY != 0;
        if (!isSub4X && !isSub4Y)
        {
            return false;
        }

        int rowStart = isSub4Y ? -1 : 0;
        int columnStart = isSub4X ? -1 : 0;

        // One chroma block can cover two or four independently decoded luma blocks. the reference decoder enters this path only
        // when every contributing owner is a conventional inter block; otherwise the current block supplies the
        // complete chroma prediction through the ordinary path.
        for (int row = rowStart; row <= 0; row++)
        {
            for (int column = columnStart; column <= 0; column++)
            {
                Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(
                    new Point(modeInfoPosition.X + column, modeInfoPosition.Y + row));

                if (candidate.ReferenceFrames[0] < Av1ReferenceFrameType.Last || candidate.UseIntraBlockCopy)
                {
                    return false;
                }
            }
        }

        int subPredictionWidth = blockSize.GetWidth() >> subX;
        int subPredictionHeight = blockSize.GetHeight() >> subY;
        int modeRow = rowStart;

        // Chroma ownership is assigned to the bottom-right luma mode record on each subsampled axis. Consequently
        // pixelPosition is already the top-left of this assembled plane block even when its first luma owner is at
        // row or column -1. Each subprediction writes directly into its final rectangle without a staging copy.
        for (int y = 0; y < predictionHeight; y += subPredictionHeight)
        {
            int modeColumn = columnStart;
            for (int x = 0; x < predictionWidth; x += subPredictionWidth)
            {
                Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(
                    new Point(modeInfoPosition.X + modeColumn, modeInfoPosition.Y + modeRow));

                Av1FrameBuffer<byte> referenceFrameBuffer = this.ResolveReferenceFrame(candidate.ReferenceFrames[0]);
                Av1MotionVector motionVector = candidate.MotionVectors[0];
                Point subPredictionOrigin = new(pixelPosition.X + x, pixelPosition.Y + y);
                int destinationOffset = reconstructionStride + (y * reconstructionStride) + x;
                bool isScaledReference =
                    referenceFrameBuffer.Width != this.frameHeader.FrameSize.FrameWidth ||
                    referenceFrameBuffer.Height != this.frameHeader.FrameSize.FrameHeight;

                if (isScaledReference)
                {
                    Span<byte> scaledDestination = default;
                    Span<ushort> scaledHighBitDepthDestination = default;
                    if (this.frameBuffer.BytesPerSample == 2)
                    {
                        scaledHighBitDepthDestination = MemoryMarshal.Cast<short, ushort>(
                            highBitDepthBlockReconstructionBuffer[destinationOffset..]);
                    }
                    else
                    {
                        scaledDestination = blockReconstructionBuffer[destinationOffset..];
                    }

                    this.PredictScaledReference(
                        referenceFrameBuffer,
                        motionVector,
                        plane,
                        subX,
                        subY,
                        subPredictionOrigin,
                        subPredictionWidth,
                        subPredictionHeight,
                        candidate.InterpolationFilters[1],
                        candidate.InterpolationFilters[0],
                        scaledDestination,
                        scaledHighBitDepthDestination,
                        default,
                        reconstructionStride,
                        predictionScratch);
                }
                else
                {
                    int horizontalMotionQ4 = motionVector.Column << (1 - subX);
                    int verticalMotionQ4 = motionVector.Row << (1 - subY);
                    int horizontalExtensionQ4 = (4 + subPredictionWidth) << 4;
                    int verticalExtensionQ4 = (4 + subPredictionHeight) << 4;
                    int horizontalEdgeScale = 1 << (1 - subX);
                    int verticalEdgeScale = 1 << (1 - subY);

                    // The block-relative UMV edges belong to the current coding block, while each contributing luma
                    // owner supplies only its motion vector and interpolation filters. This is the same split used by
                    // the reference decoder's sub-8x8 chroma builder.
                    horizontalMotionQ4 = Av1Math.Clip3(
                        (partitionInfo.ModeBlockToLeftEdge * horizontalEdgeScale) - horizontalExtensionQ4,
                        (partitionInfo.ModeBlockToRightEdge * horizontalEdgeScale) + horizontalExtensionQ4 - 16,
                        horizontalMotionQ4);

                    verticalMotionQ4 = Av1Math.Clip3(
                        (partitionInfo.ModeBlockToTopEdge * verticalEdgeScale) - verticalExtensionQ4,
                        (partitionInfo.ModeBlockToBottomEdge * verticalEdgeScale) + verticalExtensionQ4 - 16,
                        verticalMotionQ4);

                    int sourceColumnQ4 = (subPredictionOrigin.X << 4) + horizontalMotionQ4;
                    int sourceRowQ4 = (subPredictionOrigin.Y << 4) + verticalMotionQ4;
                    int horizontalPhase = sourceColumnQ4 & 15;
                    int verticalPhase = sourceRowQ4 & 15;

                    if (this.frameBuffer.BytesPerSample == 2)
                    {
                        Span<ushort> source = referenceFrameBuffer.GetPaddedPlaneSpan16(
                            (Av1Plane)plane,
                            subX,
                            subY,
                            out int sourceStride,
                            out Point sourceOrigin);

                        int sourceIndex =
                            ((sourceOrigin.Y + (sourceRowQ4 >> 4)) * sourceStride) +
                            sourceOrigin.X +
                            (sourceColumnQ4 >> 4);

                        Span<ushort> destination = MemoryMarshal.Cast<short, ushort>(
                            highBitDepthBlockReconstructionBuffer[destinationOffset..]);

                        Av1TranslationalInterPredictor.Predict(
                            source,
                            sourceStride,
                            sourceIndex,
                            destination,
                            reconstructionStride,
                            subPredictionWidth,
                            subPredictionHeight,
                            candidate.InterpolationFilters[1],
                            candidate.InterpolationFilters[0],
                            horizontalPhase,
                            verticalPhase,
                            this.frameBuffer.BitDepth.GetBitCount(),
                            predictionScratch);
                    }
                    else
                    {
                        Span<byte> source = referenceFrameBuffer.GetPaddedPlaneSpan(
                            (Av1Plane)plane,
                            subX,
                            subY,
                            out int sourceStride,
                            out Point sourceOrigin);

                        int sourceIndex =
                            ((sourceOrigin.Y + (sourceRowQ4 >> 4)) * sourceStride) +
                            sourceOrigin.X +
                            (sourceColumnQ4 >> 4);

                        Av1TranslationalInterPredictor.Predict(
                            source,
                            sourceStride,
                            sourceIndex,
                            blockReconstructionBuffer[destinationOffset..],
                            reconstructionStride,
                            subPredictionWidth,
                            subPredictionHeight,
                            candidate.InterpolationFilters[1],
                            candidate.InterpolationFilters[0],
                            horizontalPhase,
                            verticalPhase,
                            predictionScratch);
                    }
                }

                modeColumn++;
            }

            modeRow++;
        }

        return true;
    }

    /// <summary>
    /// Resolves one canonical retained reference frame.
    /// </summary>
    private Av1FrameBuffer<byte> ResolveReferenceFrame(Av1ReferenceFrameType referenceFrame)
    {
        int canonicalReferenceIndex = (int)referenceFrame - (int)Av1ReferenceFrameType.Last;
        uint referenceSlot = this.frameHeader.GetReferenceFrameIndices()[canonicalReferenceIndex];

        // The uncompressed-header parser validates each selected slot and the reference store remains unchanged
        // until frame reconstruction completes, so every parsed inter block resolves the same retained owner.
        return this.referenceFrames.ResolveRequired((int)referenceSlot).FrameBuffer;
    }

    /// <summary>
    /// Predicts one block from a retained reference whose visible dimensions differ from the current coded frame.
    /// </summary>
    private void PredictScaledReference(
        Av1FrameBuffer<byte> referenceFrameBuffer,
        Av1MotionVector motionVector,
        int plane,
        int subX,
        int subY,
        Point predictionOrigin,
        int predictionWidth,
        int predictionHeight,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        Span<byte> destination,
        Span<ushort> highBitDepthDestination,
        Span<ushort> compoundDestination,
        int destinationStride,
        Span<short> predictionScratch)
    {
        Av1ReferenceScale scale = new(
            referenceFrameBuffer.Width,
            referenceFrameBuffer.Height,
            this.frameHeader.FrameSize.FrameWidth,
            this.frameHeader.FrameSize.FrameHeight);

        int currentColumnQ4 = (predictionOrigin.X << 4) + (motionVector.Column << (1 - subX));
        int currentRowQ4 = (predictionOrigin.Y << 4) + (motionVector.Row << (1 - subY));
        int sourceColumnQ10 = scale.ScaleHorizontal(currentColumnQ4) + Av1ReferenceScale.ExtraOffset;
        int sourceRowQ10 = scale.ScaleVertical(currentRowQ4) + Av1ReferenceScale.ExtraOffset;
        int referencePlaneWidth = Av1Math.DivideLog2Ceiling(referenceFrameBuffer.Width, subX);
        int referencePlaneHeight = Av1Math.DivideLog2Ceiling(referenceFrameBuffer.Height, subY);
        int horizontalMargin = (Av1FrameBuffer<byte>.DecoderPaddingValue >> subX) - 4;
        int verticalMargin = (Av1FrameBuffer<byte>.DecoderPaddingValue >> subY) - 4;

        // The scaled coordinate clamp is intentionally wider than the ordinary block-relative UMV clamp. The retained
        // frame owns the normative border, so every variable-phase eight-tap source remains directly addressable.
        sourceColumnQ10 = Av1Math.Clip3(
            -horizontalMargin << Av1ReferenceScale.SubpixelBits,
            (referencePlaneWidth + 4) << Av1ReferenceScale.SubpixelBits,
            sourceColumnQ10);

        sourceRowQ10 = Av1Math.Clip3(
            -verticalMargin << Av1ReferenceScale.SubpixelBits,
            (referencePlaneHeight + 4) << Av1ReferenceScale.SubpixelBits,
            sourceRowQ10);

        int horizontalPhase = sourceColumnQ10 & Av1ReferenceScale.SubpixelMask;
        int verticalPhase = sourceRowQ10 & Av1ReferenceScale.SubpixelMask;
        if (this.frameBuffer.BytesPerSample == 2)
        {
            Span<ushort> source = referenceFrameBuffer.GetPaddedPlaneSpan16(
                (Av1Plane)plane,
                subX,
                subY,
                out int sourceStride,
                out Point sourceOrigin);

            int sourceIndex =
                ((sourceOrigin.Y + (sourceRowQ10 >> Av1ReferenceScale.SubpixelBits)) * sourceStride) +
                sourceOrigin.X +
                (sourceColumnQ10 >> Av1ReferenceScale.SubpixelBits);

            if (compoundDestination.IsEmpty)
            {
                Av1ScaledInterPredictor.PredictScaled(
                    source,
                    sourceStride,
                    sourceIndex,
                    highBitDepthDestination,
                    destinationStride,
                    predictionWidth,
                    predictionHeight,
                    horizontalFilter,
                    verticalFilter,
                    horizontalPhase,
                    scale.HorizontalStep,
                    verticalPhase,
                    scale.VerticalStep,
                    this.frameBuffer.BitDepth.GetBitCount(),
                    predictionScratch);
            }
            else
            {
                Av1ScaledInterPredictor.PredictScaledCompound(
                    source,
                    sourceStride,
                    sourceIndex,
                    compoundDestination,
                    destinationStride,
                    predictionWidth,
                    predictionHeight,
                    horizontalFilter,
                    verticalFilter,
                    horizontalPhase,
                    scale.HorizontalStep,
                    verticalPhase,
                    scale.VerticalStep,
                    this.frameBuffer.BitDepth.GetBitCount(),
                    predictionScratch);
            }
        }
        else
        {
            Span<byte> source = referenceFrameBuffer.GetPaddedPlaneSpan(
                (Av1Plane)plane,
                subX,
                subY,
                out int sourceStride,
                out Point sourceOrigin);

            int sourceIndex =
                ((sourceOrigin.Y + (sourceRowQ10 >> Av1ReferenceScale.SubpixelBits)) * sourceStride) +
                sourceOrigin.X +
                (sourceColumnQ10 >> Av1ReferenceScale.SubpixelBits);

            if (compoundDestination.IsEmpty)
            {
                Av1ScaledInterPredictor.PredictScaled(
                    source,
                    sourceStride,
                    sourceIndex,
                    destination,
                    destinationStride,
                    predictionWidth,
                    predictionHeight,
                    horizontalFilter,
                    verticalFilter,
                    horizontalPhase,
                    scale.HorizontalStep,
                    verticalPhase,
                    scale.VerticalStep,
                    predictionScratch);
            }
            else
            {
                Av1ScaledInterPredictor.PredictScaledCompound(
                    source,
                    sourceStride,
                    sourceIndex,
                    compoundDestination,
                    destinationStride,
                    predictionWidth,
                    predictionHeight,
                    horizontalFilter,
                    verticalFilter,
                    horizontalPhase,
                    scale.HorizontalStep,
                    verticalPhase,
                    scale.VerticalStep,
                    predictionScratch);
            }
        }
    }

    /// <summary>
    /// Blends predictions from eligible above and left neighbors into one regular inter prediction.
    /// </summary>
    private void ApplyOverlappedMotionCompensation(
        ref Av1PartitionInfo partitionInfo,
        int plane,
        int subX,
        int subY,
        int predictionWidth,
        int predictionHeight,
        Span<byte> blockReconstructionBuffer,
        Span<short> highBitDepthBlockReconstructionBuffer,
        int reconstructionStride,
        Span<byte> neighborPrediction,
        Span<ushort> highBitDepthNeighborPrediction,
        Span<byte> maskStorage,
        Span<short> predictionScratch)
    {
        Av1BlockSize blockSize = partitionInfo.ModeInfo.BlockSize;
        int blockWidthInModeInfoUnits = blockSize.Get4x4WideCount();
        int blockHeightInModeInfoUnits = blockSize.Get4x4HighCount();
        int blockColumn = partitionInfo.ColumnIndex;
        int blockRow = partitionInfo.RowIndex;
        bool highBitDepth = this.frameBuffer.BytesPerSample == 2;

        // Chroma planes smaller than 8x8 use left overlap only. This is the AV1 bandwidth rule for 4x4,
        // 8x4, and 4x8 plane blocks; luma cannot reach those sizes when motion variation is selectable.
        bool skipAbove = (predictionWidth == 4 && predictionHeight <= 8) ||
            (predictionWidth == 8 && predictionHeight == 4);

        if (partitionInfo.AvailableAbove && !skipAbove)
        {
            int maximumNeighbors = Math.Min(4, blockSize.Get4x4WidthLog2());
            int endColumn = Math.Min(blockColumn + blockWidthInModeInfoUnits, this.frameHeader.ModeInfoColumnCount);
            int neighborCount = 0;
            for (int aboveColumn = blockColumn; aboveColumn < endColumn && neighborCount < maximumNeighbors;)
            {
                Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(aboveColumn, blockRow - 1));
                int step = Math.Min(candidate.BlockSize.Get4x4WideCount(), Av1BlockSize.Block64x64.Get4x4WideCount());
                if (step == 1)
                {
                    // A four-sample neighbor is one half of the chroma-bearing eight-sample pair. the reference decoder aligns
                    // the traversal to the pair start and reads prediction state from its second mode record.
                    aboveColumn &= ~1;
                    candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(aboveColumn + 1, blockRow - 1));
                    step = 2;
                }

                if (IsOverlappable(candidate))
                {
                    int relativeColumn = aboveColumn - blockColumn;
                    int neighborWidthInModeInfoUnits = Math.Min(blockWidthInModeInfoUnits, step);
                    int neighborWidth = (neighborWidthInModeInfoUnits << Av1Constants.ModeInfoSizeLog2) >> subX;
                    int neighborHeight = Math.Clamp(
                        blockSize.GetHeight() >> (subY + 1),
                        4,
                        Av1BlockSize.Block64x64.GetHeight() >> (subY + 1));

                    Point predictionOrigin = new(
                        (aboveColumn << Av1Constants.ModeInfoSizeLog2) >> subX,
                        (blockRow << Av1Constants.ModeInfoSizeLog2) >> subY);

                    this.PredictObmcNeighbor(
                        candidate,
                        plane,
                        subX,
                        subY,
                        predictionOrigin,
                        neighborWidth,
                        neighborHeight,
                        neighborPrediction,
                        highBitDepthNeighborPrediction,
                        predictionScratch);

                    int overlapHeight = (Math.Min(blockSize.GetHeight(), Av1BlockSize.Block64x64.GetHeight()) >> 1) >> subY;
                    int destinationColumn = (relativeColumn << Av1Constants.ModeInfoSizeLog2) >> subX;
                    ReadOnlySpan<byte> verticalMask = Av1ObmcMask.Get(overlapHeight);
                    Span<byte> expandedMask = maskStorage[..(neighborWidth * overlapHeight)];
                    for (int row = 0; row < overlapHeight; row++)
                    {
                        // The vertical mask has one alpha per row. Expanding it into the reusable scratch plane lets
                        // the existing SIMD masked blender process complete rows without a specialized duplicate path.
                        expandedMask.Slice(row * neighborWidth, neighborWidth).Fill(verticalMask[row]);
                    }

                    if (highBitDepth)
                    {
                        Av1CompoundMaskBlendPredictor.Blend(
                            MemoryMarshal.Cast<short, ushort>(highBitDepthBlockReconstructionBuffer[reconstructionStride..])[destinationColumn..],
                            reconstructionStride,
                            highBitDepthNeighborPrediction,
                            neighborWidth,
                            expandedMask,
                            neighborWidth,
                            neighborWidth,
                            overlapHeight);
                    }
                    else
                    {
                        Av1CompoundMaskBlendPredictor.Blend(
                            blockReconstructionBuffer[reconstructionStride..][destinationColumn..],
                            reconstructionStride,
                            neighborPrediction,
                            neighborWidth,
                            expandedMask,
                            neighborWidth,
                            neighborWidth,
                            overlapHeight);
                    }

                    neighborCount++;
                }

                aboveColumn += step;
            }
        }

        if (partitionInfo.AvailableLeft)
        {
            int maximumNeighbors = Math.Min(4, blockSize.Get4x4HeightLog2());
            int endRow = Math.Min(blockRow + blockHeightInModeInfoUnits, this.frameHeader.ModeInfoRowCount);
            int neighborCount = 0;
            for (int leftRow = blockRow; leftRow < endRow && neighborCount < maximumNeighbors;)
            {
                Av1BlockModeInfo candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(blockColumn - 1, leftRow));
                int step = Math.Min(candidate.BlockSize.Get4x4HighCount(), Av1BlockSize.Block64x64.Get4x4HighCount());
                if (step == 1)
                {
                    // The vertical traversal applies the corresponding pairing rule to four-sample-high blocks.
                    leftRow &= ~1;
                    candidate = partitionInfo.SuperblockInfo.GetModeInfoAt(new Point(blockColumn - 1, leftRow + 1));
                    step = 2;
                }

                if (IsOverlappable(candidate))
                {
                    int relativeRow = leftRow - blockRow;
                    int neighborHeightInModeInfoUnits = Math.Min(blockHeightInModeInfoUnits, step);
                    int neighborWidth = Math.Clamp(
                        blockSize.GetWidth() >> (subX + 1),
                        4,
                        Av1BlockSize.Block64x64.GetWidth() >> (subX + 1));

                    int neighborHeight = (neighborHeightInModeInfoUnits << Av1Constants.ModeInfoSizeLog2) >> subY;
                    Point predictionOrigin = new(
                        (blockColumn << Av1Constants.ModeInfoSizeLog2) >> subX,
                        (leftRow << Av1Constants.ModeInfoSizeLog2) >> subY);

                    this.PredictObmcNeighbor(
                        candidate,
                        plane,
                        subX,
                        subY,
                        predictionOrigin,
                        neighborWidth,
                        neighborHeight,
                        neighborPrediction,
                        highBitDepthNeighborPrediction,
                        predictionScratch);

                    int overlapWidth = (Math.Min(blockSize.GetWidth(), Av1BlockSize.Block64x64.GetWidth()) >> 1) >> subX;
                    int destinationRow = (relativeRow << Av1Constants.ModeInfoSizeLog2) >> subY;
                    ReadOnlySpan<byte> horizontalMask = Av1ObmcMask.Get(overlapWidth);
                    if (highBitDepth)
                    {
                        Av1CompoundMaskBlendPredictor.Blend(
                            MemoryMarshal.Cast<short, ushort>(highBitDepthBlockReconstructionBuffer[reconstructionStride..])[(destinationRow * reconstructionStride)..],
                            reconstructionStride,
                            highBitDepthNeighborPrediction,
                            neighborWidth,
                            horizontalMask,
                            0,
                            overlapWidth,
                            neighborHeight);
                    }
                    else
                    {
                        Av1CompoundMaskBlendPredictor.Blend(
                            blockReconstructionBuffer[reconstructionStride..][(destinationRow * reconstructionStride)..],
                            reconstructionStride,
                            neighborPrediction,
                            neighborWidth,
                            horizontalMask,
                            0,
                            overlapWidth,
                            neighborHeight);
                    }

                    neighborCount++;
                }

                leftRow += step;
            }
        }
    }

    /// <summary>
    /// Builds one neighboring block's primary translational predictor into the reusable OBMC workspace.
    /// </summary>
    private void PredictObmcNeighbor(
        Av1BlockModeInfo neighbor,
        int plane,
        int subX,
        int subY,
        Point predictionOrigin,
        int predictionWidth,
        int predictionHeight,
        Span<byte> destination,
        Span<ushort> highBitDepthDestination,
        Span<short> predictionScratch)
    {
        Av1FrameBuffer<byte> referenceFrameBuffer = this.ResolveReferenceFrame(neighbor.ReferenceFrames[0]);
        Av1MotionVector motionVector = neighbor.MotionVectors[0];
        bool isScaledReference = referenceFrameBuffer.Width != this.frameHeader.FrameSize.FrameWidth ||
            referenceFrameBuffer.Height != this.frameHeader.FrameSize.FrameHeight;

        if (isScaledReference)
        {
            this.PredictScaledReference(
                referenceFrameBuffer,
                motionVector,
                plane,
                subX,
                subY,
                predictionOrigin,
                predictionWidth,
                predictionHeight,
                neighbor.InterpolationFilters[1],
                neighbor.InterpolationFilters[0],
                destination,
                highBitDepthDestination,
                default,
                predictionWidth,
                predictionScratch);

            return;
        }

        int sourceColumnQ4 = (predictionOrigin.X << 4) + (motionVector.Column << (1 - subX));
        int sourceRowQ4 = (predictionOrigin.Y << 4) + (motionVector.Row << (1 - subY));
        int horizontalExtensionQ4 = (4 + predictionWidth) << 4;
        int verticalExtensionQ4 = (4 + predictionHeight) << 4;
        int framePlaneWidth = (this.frameHeader.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) >> subX;
        int framePlaneHeight = (this.frameHeader.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2) >> subY;

        // the reference decoder clamps the motion vector relative to each neighbor rectangle. Once the neighbor origin is added, the
        // prediction extent remains in the left/top limit but cancels from the right/bottom limit. Keeping this
        // asymmetry avoids counting the OBMC rectangle twice when the source lies beyond the far frame edge.
        sourceColumnQ4 = Av1Math.Clip3(
            -horizontalExtensionQ4,
            ((framePlaneWidth + 4) << 4) - 16,
            sourceColumnQ4);

        sourceRowQ4 = Av1Math.Clip3(
            -verticalExtensionQ4,
            ((framePlaneHeight + 4) << 4) - 16,
            sourceRowQ4);

        int horizontalPhase = sourceColumnQ4 & 15;
        int verticalPhase = sourceRowQ4 & 15;
        if (this.frameBuffer.BytesPerSample == 2)
        {
            Span<ushort> source = referenceFrameBuffer.GetPaddedPlaneSpan16(
                (Av1Plane)plane,
                subX,
                subY,
                out int sourceStride,
                out Point sourceOrigin);

            int sourceIndex =
                ((sourceOrigin.Y + (sourceRowQ4 >> 4)) * sourceStride) + sourceOrigin.X + (sourceColumnQ4 >> 4);

            Av1TranslationalInterPredictor.Predict(
                source,
                sourceStride,
                sourceIndex,
                highBitDepthDestination,
                predictionWidth,
                predictionWidth,
                predictionHeight,
                neighbor.InterpolationFilters[1],
                neighbor.InterpolationFilters[0],
                horizontalPhase,
                verticalPhase,
                this.frameBuffer.BitDepth.GetBitCount(),
                predictionScratch);
        }
        else
        {
            Span<byte> source = referenceFrameBuffer.GetPaddedPlaneSpan(
                (Av1Plane)plane,
                subX,
                subY,
                out int sourceStride,
                out Point sourceOrigin);

            int sourceIndex =
                ((sourceOrigin.Y + (sourceRowQ4 >> 4)) * sourceStride) + sourceOrigin.X + (sourceColumnQ4 >> 4);

            Av1TranslationalInterPredictor.Predict(
                source,
                sourceStride,
                sourceIndex,
                destination,
                predictionWidth,
                predictionWidth,
                predictionHeight,
                neighbor.InterpolationFilters[1],
                neighbor.InterpolationFilters[0],
                horizontalPhase,
                verticalPhase,
                predictionScratch);
        }
    }

    /// <summary>
    /// Determines whether a decoded neighbor supplies an inter predictor for OBMC.
    /// </summary>
    private static bool IsOverlappable(Av1BlockModeInfo candidate)
        => candidate.UseIntraBlockCopy || candidate.ReferenceFrames[0] > Av1ReferenceFrameType.Intra;

    /// <summary>
    /// Determines whether reconstructed luma samples must be retained for a later chroma-from-luma prediction.
    /// </summary>
    /// <param name="colorConfig">The sequence color-plane configuration.</param>
    /// <param name="partitionInfo">The current block and its prediction modes.</param>
    /// <returns>
    /// <see langword="true"/> when chroma is present and the current luma block can contribute to a chroma-from-luma block.
    /// </returns>
    private static bool StoreChromaFromLumaRequired(ObuColorConfig colorConfig, ref Av1PartitionInfo partitionInfo)
        => !colorConfig.IsMonochrome &&
            (!partitionInfo.IsChroma || partitionInfo.ModeInfo.UvMode == Av1ChromaPredictionMode.ChromaFromLuma);

    /// <summary>
    /// Carries a complete pair of retained buffers through the inter-only reconstruction branch.
    /// </summary>
    private readonly struct InterReferenceBuffers
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InterReferenceBuffers"/> struct.
        /// </summary>
        public InterReferenceBuffers(Av1FrameBuffer<byte> primary, Av1FrameBuffer<byte> secondary)
        {
            this.Primary = primary;
            this.Secondary = secondary;
        }

        /// <summary>
        /// Gets the primary retained frame.
        /// </summary>
        public Av1FrameBuffer<byte> Primary { get; }

        /// <summary>
        /// Gets the secondary retained frame.
        /// </summary>
        public Av1FrameBuffer<byte> Secondary { get; }
    }
}
