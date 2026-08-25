// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantification;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

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
    /// Owns the reusable raster-order inverse-quantization buffer.
    /// </summary>
    private readonly IMemoryOwner<int> inverseQuantizationOwner;

    /// <summary>
    /// Owns the reusable two-dimensional inverse-transform workspace.
    /// </summary>
    private readonly IMemoryOwner<int> transformWorkspaceOwner;

    /// <summary>
    /// Indicates whether transform traversal must also populate loop-filter parameters.
    /// </summary>
    private readonly bool isLoopFilterEnabled;

    /// <summary>
    /// The next packed coefficient position for each plane in the current superblock.
    /// </summary>
    private readonly int[] currentCoefficientIndex;

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
    public Av1BlockDecoder(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameBuffer<byte> frameBuffer,
        Av1LoopFilterContext loopFilterContext,
        Av1InverseQuantizer inverseQuantizer)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameBuffer = frameBuffer;
        this.loopFilterContext = loopFilterContext;
        this.inverseQuantizer = inverseQuantizer;
        int ySize = (1 << this.sequenceHeader.SuperblockSizeLog2) * (1 << this.sequenceHeader.SuperblockSizeLog2);

        // One scratch plane is reused for every transform unit. Its maximum size must cover a complete superblock
        // across all coded planes, with chroma dimensions reduced independently by their subsampling axes.
        int inverseQuantizationSize = ySize +
            (this.sequenceHeader.ColorConfig.SubSamplingX ? ySize >> 2 : ySize) +
            (this.sequenceHeader.ColorConfig.SubSamplingY ? ySize >> 2 : ySize);

        this.inverseQuantizationOwner = this.frameBuffer.MemoryAllocator.Allocate<int>(inverseQuantizationSize);
        this.transformWorkspaceOwner = this.frameBuffer.MemoryAllocator.Allocate<int>(Av1TransformWorkspace.MaximumLength);
        this.isLoopFilterEnabled = frameHeader.LoopFilterParameters.FilterLevel[0] != 0 ||
            frameHeader.LoopFilterParameters.FilterLevel[1] != 0;

        this.currentCoefficientIndex = new int[3];
        this.chromaFromLumaContext = new(sequenceHeader.ColorConfig);
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

        bool highBitDepth = this.frameBuffer.BytesPerSample == 2;
        Av1PredictionDecoder predictionDecoder = new(this.sequenceHeader, this.frameHeader);
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
                2 => superblockInfo.TransformInfoIndexUv + modeInfo.GetFirstTransformLocation(Av1Plane.V) + chromaTransformUnitCount,
                1 => superblockInfo.TransformInfoIndexUv + modeInfo.GetFirstTransformLocation(Av1Plane.U),
                0 => superblockInfo.TransformInfoIndexY + modeInfo.GetFirstTransformLocation(Av1Plane.Y),
                _ => throw new InvalidImageContentException("Maximum of 3 color planes")
            };
            Span<Av1TransformInfo> transformInfo = superblockInfo.GetTransformInfo(plane)[transformInfoIndex..];
            Guard.NotNull(transformInfo[0]);

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

                // The bounded image-item decoder reconstructs intra-only AV1 still pictures, so every transform unit
                // predicts its samples before any coded residual is added.
                if (highBitDepth)
                {
                    predictionDecoder.Decode(
                        partitionInfo,
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
                    predictionDecoder.Decode(
                        partitionInfo,
                        (Av1Plane)plane,
                        transformSize,
                        tileInfo,
                        transformBlockReconstructionBuffer,
                        reconstructionStride,
                        this.frameBuffer.BitDepth,
                        transformInfo[0].OffsetX,
                        transformInfo[0].OffsetY);
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
                if (plane == (int)Av1Plane.Y && StoreChromaFromLumaRequired(colorConfig, partitionInfo))
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
    /// Derives a byte-addressed reconstruction span beginning one row before a block.
    /// </summary>
    /// <param name="frameBuffer">The frame buffer containing the destination planes.</param>
    /// <param name="plane">The zero-based Y, U, or V plane index.</param>
    /// <param name="blockColumnInPixels">The horizontal block origin in plane samples.</param>
    /// <param name="blockRowInPixels">The vertical block origin in plane samples.</param>
    /// <param name="blockReconstructionBuffer">The resulting span beginning one row before the block.</param>
    /// <param name="reconstructionStride">The number of logical samples between rows.</param>
    /// <param name="subX">The chroma horizontal subsampling shift.</param>
    /// <param name="subY">The chroma vertical subsampling shift.</param>
    private static void DeriveBlockPointers(
        Av1FrameBuffer<byte> frameBuffer,
        int plane,
        int blockColumnInPixels,
        int blockRowInPixels,
        out Span<byte> blockReconstructionBuffer,
        out int reconstructionStride,
        int subX,
        int subY)
    {
        int blockOffset;

        switch (plane)
        {
            case 0:
                reconstructionStride = frameBuffer.BufferY!.Width;
                blockOffset = ((frameBuffer.OriginY + blockRowInPixels) * reconstructionStride) +
                    (frameBuffer.OriginX + blockColumnInPixels);
                break;
            case 1:
                reconstructionStride = frameBuffer.BufferCb!.Width;
                blockOffset = (((frameBuffer.OriginY >> subY) + blockRowInPixels) * reconstructionStride) +
                    ((frameBuffer.OriginX >> subX) + blockColumnInPixels);
                break;
            default:
                reconstructionStride = frameBuffer.BufferCr!.Width;
                blockOffset = (((frameBuffer.OriginY >> subY) + blockRowInPixels) * reconstructionStride) +
                    ((frameBuffer.OriginX >> subX) + blockColumnInPixels);
                break;
        }

        // Prediction addresses above samples relative to the returned span, so expose the previous row as index zero.
        blockOffset -= reconstructionStride;
        Guard.MustBeGreaterThanOrEqualTo(blockOffset, 0, nameof(blockOffset));

        if (frameBuffer.BitDepth != Av1BitDepth.EightBit || frameBuffer.Is16BitPipeline)
        {
            // The legacy byte view represents each high-bit-depth sample with two adjacent storage elements.
            blockOffset *= 2;
            if (plane == 0)
            {
                blockReconstructionBuffer = frameBuffer.BufferY!.DangerousGetSingleSpan()[blockOffset..];
            }
            else if (plane == 1)
            {
                blockReconstructionBuffer = frameBuffer.BufferCb!.DangerousGetSingleSpan()[blockOffset..];
            }
            else
            {
                blockReconstructionBuffer = frameBuffer.BufferCr!.DangerousGetSingleSpan()[blockOffset..];
            }
        }
        else
        {
            if (plane == 0)
            {
                blockReconstructionBuffer = frameBuffer.BufferY!.DangerousGetSingleSpan()[blockOffset..];
            }
            else if (plane == 1)
            {
                blockReconstructionBuffer = frameBuffer.BufferCb!.DangerousGetSingleSpan()[blockOffset..];
            }
            else
            {
                blockReconstructionBuffer = frameBuffer.BufferCr!.DangerousGetSingleSpan()[blockOffset..];
            }
        }
    }

    /// <summary>
    /// Determines whether reconstructed luma samples must be retained for a later chroma-from-luma prediction.
    /// </summary>
    /// <param name="colorConfig">The sequence color-plane configuration.</param>
    /// <param name="partitionInfo">The current block and its prediction modes.</param>
    /// <returns>
    /// <see langword="true"/> when chroma is present and the current luma block can contribute to a chroma-from-luma block.
    /// </returns>
    private static bool StoreChromaFromLumaRequired(ObuColorConfig colorConfig, Av1PartitionInfo partitionInfo)
        => !colorConfig.IsMonochrome &&
            (!partitionInfo.IsChroma || partitionInfo.ModeInfo.UvMode == Av1PredictionMode.UvChromaFromLuma);
}
