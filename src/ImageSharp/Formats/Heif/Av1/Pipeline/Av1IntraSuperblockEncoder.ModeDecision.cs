// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Provides live-probability final-block mode decisions for intra encoding.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// Gets the zero-angle luma modes in the order used by the reference encoder.
    /// </summary>
    private static ReadOnlySpan<Av1PredictionMode> LumaModeSearchOrder =>
    [
        Av1PredictionMode.DC,
        Av1PredictionMode.Horizontal,
        Av1PredictionMode.Vertical,
        Av1PredictionMode.Smooth,
        Av1PredictionMode.Paeth,
        Av1PredictionMode.SmoothVertical,
        Av1PredictionMode.SmoothHorizontal,
        Av1PredictionMode.Directional135Degrees,
        Av1PredictionMode.Directional203Degrees,
        Av1PredictionMode.Directional157Degrees,
        Av1PredictionMode.Directional67Degrees,
        Av1PredictionMode.Directional113Degrees,
        Av1PredictionMode.Directional45Degrees
    ];

    /// <summary>
    /// Gets the nonzero directional adjustments in the exhaustive order used by the reference encoder.
    /// </summary>
    private static ReadOnlySpan<sbyte> AngleDeltaSearchOrder => [-3, -2, -1, 1, 2, 3];

    /// <summary>
    /// Gets partition candidates in the evaluation order used by the reference encoder.
    /// </summary>
    private static ReadOnlySpan<Av1PartitionType> PartitionSearchOrder =>
    [
        Av1PartitionType.None,
        Av1PartitionType.Split,
        Av1PartitionType.Horizontal,
        Av1PartitionType.Vertical,
        Av1PartitionType.HorizontalA,
        Av1PartitionType.HorizontalB,
        Av1PartitionType.VerticalA,
        Av1PartitionType.VerticalB,
        Av1PartitionType.Horizontal4,
        Av1PartitionType.Vertical4
    ];

    /// <summary>
    /// Builds the fixed 8x8 partition skeleton consumed by interleaved mode decision and tile writing.
    /// </summary>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="superblock">The reusable partition and final-block decisions.</param>
    /// <param name="superblockOrigin">The absolute luma-sample origin of the superblock.</param>
    public static void Prepare(
        Av1PictureControlSet picture,
        Av1Superblock superblock,
        Point superblockOrigin)
    {
        superblock.Workspace.Reset();
        int partitionIndex = 0;
        PreparePartitionTree(
            picture,
            superblock,
            superblockOrigin,
            picture.Sequence.SequenceHeader.SuperblockSize,
            ref partitionIndex);
    }

    private static void PreparePartitionTree(
        Av1PictureControlSet picture,
        Av1Superblock superblock,
        Point blockOrigin,
        Av1BlockSize blockSize,
        ref int partitionIndex)
    {
        Av1EncoderCommon common = picture.Parent.Common;
        Point modeInfoPosition = blockOrigin >> Av1Constants.ModeInfoSizeLog2;
        if (modeInfoPosition.Y >= common.ModeInfoRowCount || modeInfoPosition.X >= common.ModeInfoColumnCount)
        {
            return;
        }

        if (blockSize == Av1BlockSize.Block8x8)
        {
            superblock.CodingUnitPartitionTypes[partitionIndex++] = (byte)Av1PartitionType.None;
            ref Av1MacroBlockModeInfo modeInfo = ref picture.GetMacroBlockModeInfo(modeInfoPosition);
            modeInfo.Block = new Av1EncoderBlockModeInfo
            {
                BlockSize = Av1BlockSize.Block8x8,
                PartitionType = Av1PartitionType.None
            };

            return;
        }

        superblock.CodingUnitPartitionTypes[partitionIndex++] = (byte)Av1PartitionType.Split;
        Av1BlockSize subSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
        int halfBlockSize = blockSize.GetWidth() >> 1;

        // The same preorder drives partition symbols, block decisions, and coefficient offsets.
        PreparePartitionTree(picture, superblock, blockOrigin, subSize, ref partitionIndex);
        PreparePartitionTree(picture, superblock, blockOrigin + new Size(halfBlockSize, 0), subSize, ref partitionIndex);
        PreparePartitionTree(picture, superblock, blockOrigin + new Size(0, halfBlockSize), subSize, ref partitionIndex);
        PreparePartitionTree(picture, superblock, blockOrigin + new Size(halfBlockSize, halfBlockSize), subSize, ref partitionIndex);
    }

    /// <summary>
    /// Produces one final block at a time against the tile state immediately preceding its syntax.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The type-specific block encoding operations.</typeparam>
    internal partial struct ModeDecision<TSample, TOperator> : Av1TileWriter.IBlockEncodingHandler
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        private readonly Av1EncoderFrame<TSample>.PlanarView source;
        private readonly Av1EncoderFrame<TSample>.PlanarView reconstruction;
        private readonly Av1PictureControlSet picture;
        private readonly Av1Superblock superblock;
        private readonly Av1EncoderCoefficientBuffer coefficientBuffer;
        private readonly Av1EncoderBlockWorkspace blockWorkspace;
        private readonly ObuQuantizationParameters quantization;
        private readonly Av1BitDepth bitDepth;
        private readonly int rateMultiplier;
        private readonly int effort;
        private int codedAreaLuma;
        private int codedAreaChroma;
        private long selectedBlockCost;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModeDecision{TSample, TOperator}"/> struct.
        /// </summary>
        /// <param name="source">The coded source frame.</param>
        /// <param name="reconstruction">The reconstructed frame updated by winning candidates.</param>
        /// <param name="picture">The frame coding and mode-information state.</param>
        /// <param name="superblock">The current superblock.</param>
        /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
        /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
        /// <param name="effort">The mode-search effort in the inclusive range zero through ten.</param>
        public ModeDecision(
            Av1EncoderFrame<TSample> source,
            Av1EncoderFrame<TSample> reconstruction,
            Av1PictureControlSet picture,
            Av1Superblock superblock,
            Av1EncoderCoefficientBuffer coefficientBuffer,
            Av1EncoderBlockWorkspace blockWorkspace,
            int effort)
        {
            this.source = source.CodedView;
            this.reconstruction = reconstruction.CodedView;
            this.picture = picture;
            this.superblock = superblock;
            this.coefficientBuffer = coefficientBuffer;
            this.blockWorkspace = blockWorkspace;
            this.quantization = picture.Parent.FrameHeader.QuantizationParameters;
            this.bitDepth = picture.Sequence.SequenceHeader.ColorConfig.BitDepth;
            this.rateMultiplier = Av1RateDistortion.GetKeyFrameRateMultiplier(this.quantization.QIndex[0], this.bitDepth);
            this.effort = effort;
            this.codedAreaLuma = 0;
            this.codedAreaChroma = 0;
            this.selectedBlockCost = 0;
        }

        /// <inheritdoc/>
        public Av1PartitionType SelectPartition(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            Av1PartitionType preparedPartition)
        {
            bool searchPartition = blockSize is Av1BlockSize.Block8x8 or Av1BlockSize.Block16x16 ||
                (this.effort == 10 &&
                    blockSize is Av1BlockSize.Block32x32 or Av1BlockSize.Block64x64 or Av1BlockSize.Block128x128);

            if (!searchPartition)
            {
                return preparedPartition;
            }

            Point modeInfoPosition = blockOrigin >> Av1Constants.ModeInfoSizeLog2;
            bool hasRows =
                modeInfoPosition.Y + blockSize.Get4x4HighCount() <= this.picture.Parent.Common.ModeInfoRowCount;

            bool hasColumns =
                modeInfoPosition.X + blockSize.Get4x4WideCount() <= this.picture.Parent.Common.ModeInfoColumnCount;

            if (!hasRows || !hasColumns)
            {
                // Coded dimensions are aligned to eight samples, so an incomplete searched node must retain
                // the prepared split tree rather than evaluating a block that extends beyond source storage.
                this.PreparePartitionGeometry(blockOrigin, blockSize, preparedPartition);
                return preparedPartition;
            }

            if (this.effort < 9)
            {
                return preparedPartition;
            }

            Av1PartitionType selectedPartition = this.SelectBestPartition(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                blockSize);

            // Trial reconstruction and mode entries need no copy-back. The selected branch is evaluated again
            // in raster order, overwriting each trial-local value before a later selected leaf can consume it.
            this.PreparePartitionGeometry(blockOrigin, blockSize, selectedPartition);
            return selectedPartition;
        }

        private Av1PartitionType SelectBestPartition(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize)
        {
            int savedLumaArea = this.codedAreaLuma;
            int savedChromaArea = this.codedAreaChroma;
            this.SavePartitionTrialContexts(blockOrigin, tileIndex, blockSize);
            long bestCost = long.MaxValue;
            Av1PartitionType selectedPartition = Av1PartitionType.None;
            ReadOnlySpan<Av1PartitionType> searchOrder = PartitionSearchOrder;
            int candidateCount = blockSize == Av1BlockSize.Block8x8 ? 4 : searchOrder.Length;
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Av1PartitionType partitionType = searchOrder[candidateIndex];
                if (!this.IsPartitionCandidateAllowed(blockSize, partitionType))
                {
                    continue;
                }

                long candidateCost = this.EvaluatePartitionCandidate(
                    writer,
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    blockSize,
                    partitionType,
                    publishFinalContexts: false);

                if (candidateCost < bestCost)
                {
                    bestCost = candidateCost;
                    selectedPartition = partitionType;
                }

                this.ResetPartitionTrial(
                    blockOrigin,
                    tileIndex,
                    blockSize,
                    savedLumaArea,
                    savedChromaArea);
            }

            return selectedPartition;
        }

        private long EvaluateSelectedPartitionTree(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            bool publishContexts)
        {
            Av1PartitionType selectedPartition = this.SelectBestPartition(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                blockSize);

            long cost = this.EvaluatePartitionCandidate(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                blockSize,
                selectedPartition,
                publishContexts);

            if (publishContexts)
            {
                Av1TileWriter.UpdatePartitionContexts(
                    this.picture.PartitionContexts[tileIndex],
                    blockOrigin,
                    selectedPartition.GetBlockSubSize(blockSize),
                    blockSize,
                    selectedPartition);
            }

            return cost;
        }

        private long EvaluatePartitionCandidate(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType,
            bool publishFinalContexts)
        {
            int rate = Av1TileWriter.GetPartitionCost(
                this.picture,
                writer,
                blockSize,
                partitionType,
                blockOrigin,
                this.picture.PartitionContexts[tileIndex]);

            long cost = Av1RateDistortion.GetCost(this.rateMultiplier, rate, 0);
            int leafCount = GetPartitionLeafCount(partitionType);

            // Child reconstruction and syntax contexts become input to the next child. Publishing only
            // the required leaves reproduces libaom's raster dry run without writing entropy symbols.
            for (int leafIndex = 0; leafIndex < leafCount; leafIndex++)
            {
                GetPartitionLeafGeometry(
                    blockOrigin,
                    blockSize,
                    partitionType,
                    leafIndex,
                    out Point leafOrigin,
                    out Av1BlockSize leafSize);

                bool publishContexts = leafIndex < leafCount - 1 || publishFinalContexts;
                cost += partitionType == Av1PartitionType.Split && blockSize > Av1BlockSize.Block8x8
                    ? this.EvaluateSelectedPartitionTree(
                        writer,
                        macroBlock,
                        leafOrigin,
                        tileIndex,
                        leafSize,
                        publishContexts)
                    : this.EvaluatePartitionLeaf(
                        writer,
                        macroBlock,
                        leafOrigin,
                        tileIndex,
                        leafSize,
                        publishContexts);
            }

            return cost;
        }

        private void ResetPartitionTrial(
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            int savedLumaArea,
            int savedChromaArea)
        {
            this.codedAreaLuma = savedLumaArea;
            this.codedAreaChroma = savedChromaArea;
            this.RestorePartitionTrialContexts(blockOrigin, tileIndex, blockSize);
        }

        private void PreparePartitionGeometry(
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType)
        {
            int leafCount = GetPartitionLeafCount(partitionType);
            for (int leafIndex = 0; leafIndex < leafCount; leafIndex++)
            {
                GetPartitionLeafGeometry(
                    blockOrigin,
                    blockSize,
                    partitionType,
                    leafIndex,
                    out Point leafOrigin,
                    out Av1BlockSize leafSize);

                if (this.IsBlockOriginInsideFrame(leafOrigin))
                {
                    this.SetBlockGeometry(leafOrigin, leafSize, Av1PartitionType.None);
                }
            }
        }

        private bool IsPartitionCandidateAllowed(
            Av1BlockSize blockSize,
            Av1PartitionType partitionType)
        {
            if (partitionType.GetBlockSubSize(blockSize) == Av1BlockSize.Invalid)
            {
                return false;
            }

            if (blockSize == Av1BlockSize.Block128x128 &&
                partitionType is Av1PartitionType.Horizontal4 or Av1PartitionType.Vertical4)
            {
                // AV1 excludes 128x32 and 32x128 leaves from the 128x128 partition alphabet.
                return false;
            }

            if (this.source.IsMonochrome)
            {
                return true;
            }

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int leafCount = GetPartitionLeafCount(partitionType);
            for (int leafIndex = 0; leafIndex < leafCount; leafIndex++)
            {
                GetPartitionLeafGeometry(
                    Point.Empty,
                    blockSize,
                    partitionType,
                    leafIndex,
                    out _,
                    out Av1BlockSize leafSize);

                if (leafSize.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY) ==
                    Av1BlockSize.Invalid)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsBlockOriginInsideFrame(Point blockOrigin)
        {
            Point modeInfoPosition = blockOrigin >> Av1Constants.ModeInfoSizeLog2;
            return modeInfoPosition.Y < this.picture.Parent.Common.ModeInfoRowCount &&
                modeInfoPosition.X < this.picture.Parent.Common.ModeInfoColumnCount;
        }

        private static int GetPartitionLeafCount(Av1PartitionType partitionType)
            => partitionType switch
            {
                Av1PartitionType.None => 1,
                Av1PartitionType.Horizontal or Av1PartitionType.Vertical => 2,
                Av1PartitionType.HorizontalA or
                    Av1PartitionType.HorizontalB or
                    Av1PartitionType.VerticalA or
                    Av1PartitionType.VerticalB => 3,
                _ => 4
            };

        private static void GetPartitionLeafGeometry(
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType,
            int leafIndex,
            out Point leafOrigin,
            out Av1BlockSize leafSize)
        {
            int halfWidth = blockSize.GetWidth() >> 1;
            int halfHeight = blockSize.GetHeight() >> 1;
            Av1BlockSize rectangularSize = partitionType.GetBlockSubSize(blockSize);
            Av1BlockSize splitSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
            switch (partitionType)
            {
                case Av1PartitionType.Horizontal:
                    leafOrigin = blockOrigin + new Size(0, leafIndex * halfHeight);
                    leafSize = rectangularSize;
                    return;
                case Av1PartitionType.Vertical:
                    leafOrigin = blockOrigin + new Size(leafIndex * halfWidth, 0);
                    leafSize = rectangularSize;
                    return;
                case Av1PartitionType.Split:
                    leafOrigin = blockOrigin + new Size(
                        (leafIndex & 1) * halfWidth,
                        (leafIndex >> 1) * halfHeight);

                    leafSize = splitSize;
                    return;
                case Av1PartitionType.HorizontalA:
                    leafOrigin = leafIndex < 2
                        ? blockOrigin + new Size(leafIndex * halfWidth, 0)
                        : blockOrigin + new Size(0, halfHeight);

                    leafSize = leafIndex < 2 ? splitSize : rectangularSize;
                    return;
                case Av1PartitionType.HorizontalB:
                    leafOrigin = leafIndex == 0
                        ? blockOrigin
                        : blockOrigin + new Size((leafIndex - 1) * halfWidth, halfHeight);

                    leafSize = leafIndex == 0 ? rectangularSize : splitSize;
                    return;
                case Av1PartitionType.VerticalA:
                    leafOrigin = leafIndex < 2
                        ? blockOrigin + new Size(0, leafIndex * halfHeight)
                        : blockOrigin + new Size(halfWidth, 0);

                    leafSize = leafIndex < 2 ? splitSize : rectangularSize;
                    return;
                case Av1PartitionType.VerticalB:
                    leafOrigin = leafIndex == 0
                        ? blockOrigin
                        : blockOrigin + new Size(halfWidth, (leafIndex - 1) * halfHeight);

                    leafSize = leafIndex == 0 ? rectangularSize : splitSize;
                    return;
                case Av1PartitionType.Horizontal4:
                    leafOrigin = blockOrigin + new Size(0, leafIndex * (blockSize.GetHeight() >> 2));
                    leafSize = rectangularSize;
                    return;
                case Av1PartitionType.Vertical4:
                    leafOrigin = blockOrigin + new Size(leafIndex * (blockSize.GetWidth() >> 2), 0);
                    leafSize = rectangularSize;
                    return;
                default:
                    leafOrigin = blockOrigin;
                    leafSize = blockSize;
                    return;
            }
        }

        /// <inheritdoc/>
        public void EncodeBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            Av1PartitionType partitionType = modeInfo.Block.PartitionType;
            Av1TransformSize maximumLumaTransformSize = this.picture.Parent.FrameHeader.CodedLossless
                ? Av1TransformSize.Size4x4
                : blockSize.GetMaximumTransformSize();

            int qIndex = this.quantization.QIndex[0];
            modeInfo.Block = new Av1EncoderBlockModeInfo
            {
                BlockSize = blockSize,
                PartitionType = partitionType,
                SegmentId = 0,
                TransformSize = maximumLumaTransformSize,
                Mode = Av1PredictionMode.DC,
                UvMode = Av1ChromaPredictionMode.DC
            };

            modeInfo.CdefStrength = 0;
            Point modeInfoPosition = blockOrigin >> Av1Constants.ModeInfoSizeLog2;
            block.HasChroma = !this.source.IsMonochrome &&
                Av1TileReader.HasChroma(this.picture.Sequence.SequenceHeader, modeInfoPosition, blockSize);

            block.QuantizationIndex = qIndex;
            block.SegmentId = 0;

            Span<int> lumaCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.Y);
            Span<Av1EncoderTransformBlockState> lumaTransformBlocks =
                this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.Y);

            int lumaTransformIndex = this.codedAreaLuma /
                Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            Span<Av1EncoderTransformBlockState> retainedLumaStates = lumaTransformBlocks[lumaTransformIndex..];
            modeInfo.Block.Mode = this.SelectLumaMode(
                writer,
                macroBlock,
                blockOrigin,
                blockSize,
                tileIndex,
                lumaCoefficients[this.codedAreaLuma..],
                retainedLumaStates,
                ref paletteInfo,
                out int lumaAngleDelta,
                out Av1FilterIntraMode filterIntraMode,
                out Av1TransformSize lumaTransformSize,
                out long lumaCost);

            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = (sbyte)lumaAngleDelta;
            block.FilterIntraMode = filterIntraMode;
            modeInfo.Block.TransformSize = lumaTransformSize;

            // Mode decision retains one state for every uniform transform tile in the coding block. The block
            // can skip coefficient syntax only when every retained transform has an empty end-of-block marker.
            int lumaTransformSampleCount = lumaTransformSize.GetSize2d();
            int lumaTransformBlockCount =
                (blockSize.GetWidth() * blockSize.GetHeight()) / lumaTransformSampleCount;

            int lumaStateStride =
                lumaTransformSampleCount / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            bool lumaTransformEmpty = true;
            for (int transformIndex = 0; transformIndex < lumaTransformBlockCount; transformIndex++)
            {
                lumaTransformEmpty &= retainedLumaStates[transformIndex * lumaStateStride].EndOfBlock == 0;
            }

            if (this.source.IsMonochrome)
            {
                int emptyTransformRate = lumaTransformEmpty
                    ? this.GetEmptyTransformRate(
                        writer,
                        this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                        Av1ComponentType.Luminance,
                        blockOrigin,
                        blockSize,
                        lumaTransformSize,
                        modeInfo.Block.Mode,
                        block.FilterIntraMode)
                    : 0;

                modeInfo.Block.Skip =
                    !this.picture.Parent.FrameHeader.CodedLossless &&
                    lumaTransformEmpty && Av1TileWriter.ShouldSkipCoefficients(
                        writer,
                        Av1TileWriter.GetSkipContext(macroBlock),
                        emptyTransformRate);

                bool allowIntraBlockCopy = blockSize == Av1BlockSize.Block8x8 &&
                    this.picture.Parent.FrameHeader.AllowIntraBlockCopy;

                long regularCost = this.GetRegularBlockCost(
                    writer,
                    macroBlock,
                    lumaCost,
                    emptyTransformRate,
                    modeInfo.Block.Skip,
                    allowIntraBlockCopy);

                this.selectedBlockCost = allowIntraBlockCopy
                    ? this.SelectIntraBlockCopy(
                        writer,
                        macroBlock,
                        blockOrigin,
                        tileIndex,
                        regularCost,
                        ref modeInfo,
                        ref block,
                        ref paletteInfo)
                    : regularCost;

                this.codedAreaLuma += blockSize.GetWidth() * blockSize.GetHeight();
                return;
            }

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            Point chromaOrigin = Av1TileWriter.GetChromaBlockOrigin(
                blockOrigin,
                subsamplingX,
                subsamplingY);

            Av1TransformSize chromaTransformSize = this.picture.Parent.FrameHeader.CodedLossless
                ? Av1TransformSize.Size4x4
                : blockSize.GetMaxUvTransformSize(
                    colorConfig.SubSamplingX,
                    colorConfig.SubSamplingY);

            Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            Span<int> blueCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.U);
            Span<int> redCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.V);
            Span<Av1EncoderTransformBlockState> blueTransformBlocks =
                this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.U);
            Span<Av1EncoderTransformBlockState> redTransformBlocks =
                this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.V);

            int chromaTransformIndex = this.codedAreaChroma /
                Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            Span<Av1EncoderTransformBlockState> retainedBlueStates = blueTransformBlocks[chromaTransformIndex..];
            Span<Av1EncoderTransformBlockState> retainedRedStates = redTransformBlocks[chromaTransformIndex..];
            long chromaCost = 0;
            if (block.HasChroma)
            {
                modeInfo.Block.UvMode = this.SelectChromaMode(
                    writer,
                    macroBlock,
                    modeInfo,
                    blockOrigin,
                    chromaOrigin,
                    blockSize,
                    tileIndex,
                    modeInfo.Block.Mode,
                    chromaTransformSize,
                    blueCoefficients[this.codedAreaChroma..],
                    redCoefficients[this.codedAreaChroma..],
                    retainedBlueStates,
                    retainedRedStates,
                    ref paletteInfo,
                    out int chromaAngleDelta,
                    out byte chromaFromLumaIndex,
                    out sbyte chromaFromLumaSigns,
                    out chromaCost);

                block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = (sbyte)chromaAngleDelta;
                block.PredictionUnit.ChromaFromLumaIndex = chromaFromLumaIndex;
                block.PredictionUnit.ChromaFromLumaSigns = chromaFromLumaSigns;
            }

            // Skip suppresses coefficient syntax for the entire coding block, not one plane independently.
            // Preserve normal coefficient coding when any selected luma or chroma transform is nonempty.
            int chromaTransformSampleCount = chromaTransformSize.GetSize2d();
            int chromaTransformBlockCount =
                (chromaBlockSize.GetWidth() * chromaBlockSize.GetHeight()) / chromaTransformSampleCount;

            int chromaStateStride =
                chromaTransformSampleCount / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            bool chromaTransformsEmpty = true;
            for (int transformIndex = 0; transformIndex < chromaTransformBlockCount; transformIndex++)
            {
                int stateIndex = transformIndex * chromaStateStride;
                chromaTransformsEmpty &= retainedBlueStates[stateIndex].EndOfBlock == 0 &&
                    retainedRedStates[stateIndex].EndOfBlock == 0;
            }

            bool allTransformsEmpty = lumaTransformEmpty && (!block.HasChroma || chromaTransformsEmpty);

            int regularEmptyTransformRate = 0;
            if (allTransformsEmpty)
            {
                regularEmptyTransformRate = this.GetEmptyTransformRate(
                    writer,
                    this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                    Av1ComponentType.Luminance,
                    blockOrigin,
                    blockSize,
                    lumaTransformSize,
                    modeInfo.Block.Mode,
                    block.FilterIntraMode);

                if (block.HasChroma)
                {
                    regularEmptyTransformRate += this.GetEmptyTransformRate(
                        writer,
                        this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                        Av1ComponentType.Chroma,
                        chromaOrigin,
                        chromaBlockSize,
                        chromaTransformSize,
                        modeInfo.Block.Mode,
                        Av1FilterIntraMode.AllFilterIntraModes);

                    regularEmptyTransformRate += this.GetEmptyTransformRate(
                        writer,
                        this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex],
                        Av1ComponentType.Chroma,
                        chromaOrigin,
                        chromaBlockSize,
                        chromaTransformSize,
                        modeInfo.Block.Mode,
                        Av1FilterIntraMode.AllFilterIntraModes);
                }

                modeInfo.Block.Skip = !this.picture.Parent.FrameHeader.CodedLossless &&
                    Av1TileWriter.ShouldSkipCoefficients(
                    writer,
                    Av1TileWriter.GetSkipContext(macroBlock),
                    regularEmptyTransformRate);
            }

            bool allowColorIntraBlockCopy = blockSize == Av1BlockSize.Block8x8 &&
                this.picture.Parent.FrameHeader.AllowIntraBlockCopy;

            long regularColorCost = this.GetRegularBlockCost(
                writer,
                macroBlock,
                lumaCost + chromaCost,
                regularEmptyTransformRate,
                modeInfo.Block.Skip,
                allowColorIntraBlockCopy);

            this.selectedBlockCost = allowColorIntraBlockCopy
                ? this.SelectIntraBlockCopy(
                    writer,
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    regularColorCost,
                    ref modeInfo,
                    ref block,
                    ref paletteInfo)
                : regularColorCost;

            this.codedAreaLuma += blockSize.GetWidth() * blockSize.GetHeight();
            if (block.HasChroma)
            {
                this.codedAreaChroma += chromaBlockSize.GetWidth() * chromaBlockSize.GetHeight();
            }
        }

        private long EvaluatePartitionLeaf(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            bool publishContexts)
        {
            this.SetBlockGeometry(blockOrigin, blockSize, Av1PartitionType.None);
            Point modeInfoPosition = blockOrigin >> Av1Constants.ModeInfoSizeLog2;
            Av1TileWriter.SetModeInfoRowAndColumn(
                this.picture,
                macroBlock,
                macroBlock.Tile,
                modeInfoPosition,
                blockSize,
                this.picture.Parent.Common.ModeInfoStride,
                this.picture.Parent.Common.ModeInfoRowCount,
                this.picture.Parent.Common.ModeInfoColumnCount);

            ref Av1MacroBlockModeInfo modeInfo = ref this.picture.GetMacroBlockModeInfo(modeInfoPosition);
            Av1EncoderBlockStruct block = default;
            Av1EncoderPaletteInfo paletteInfo = default;
            int lumaArea = this.codedAreaLuma;
            int chromaArea = this.codedAreaChroma;
            this.EncodeBlock(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                ref modeInfo,
                ref block,
                ref paletteInfo);

            if (publishContexts)
            {
                this.PublishPartitionLeafContexts(
                    blockOrigin,
                    tileIndex,
                    lumaArea,
                    chromaArea,
                    modeInfo,
                    block,
                    paletteInfo);
            }

            return this.selectedBlockCost;
        }

        private void SetBlockGeometry(
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType)
        {
            Point modeInfoPosition = blockOrigin >> Av1Constants.ModeInfoSizeLog2;
            ref Av1MacroBlockModeInfo modeInfo = ref this.picture.GetMacroBlockModeInfo(modeInfoPosition);
            modeInfo.Block = new Av1EncoderBlockModeInfo
            {
                BlockSize = blockSize,
                PartitionType = partitionType
            };

            this.picture.MapModeInfoBlock(modeInfoPosition, blockSize);
        }

        private void PublishPartitionLeafContexts(
            Point blockOrigin,
            ushort tileIndex,
            int lumaArea,
            int chromaArea,
            Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1EncoderPaletteInfo paletteInfo)
        {
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            Av1TransformSize transformSize = modeInfo.Block.TransformSize;
            Size blockDimensions = new(blockSize.GetWidth(), blockSize.GetHeight());
            Av1NeighborArrayUnit<byte> transformContexts = this.picture.TransformFunctionContexts[tileIndex];
            transformContexts.UnitModeWrite(
                (byte)transformSize.GetWidth(),
                blockOrigin,
                blockDimensions,
                Av1NeighborArrayUnit<byte>.UnitMask.Top);

            transformContexts.UnitModeWrite(
                (byte)transformSize.GetHeight(),
                blockOrigin,
                blockDimensions,
                Av1NeighborArrayUnit<byte>.UnitMask.Left);

            Span<Av1EncoderTransformBlockState> lumaStates =
                this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.Y);

            Span<int> lumaCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.Y);
            PublishCoefficientContexts(
                this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                blockOrigin,
                blockSize,
                transformSize,
                Av1BlockSize.Block64x64,
                lumaCoefficients[lumaArea..],
                lumaStates[(lumaArea / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount)..]);

            if (this.picture.Parent.FrameHeader.AllowScreenContentTools)
            {
                const Av1NeighborArrayUnit<Av1EncoderPaletteInfo>.UnitMask PaletteContextMask =
                    Av1NeighborArrayUnit<Av1EncoderPaletteInfo>.UnitMask.Top |
                    Av1NeighborArrayUnit<Av1EncoderPaletteInfo>.UnitMask.Left;

                this.picture.PaletteContexts[tileIndex].UnitModeWrite(
                    paletteInfo,
                    blockOrigin,
                    blockDimensions,
                    PaletteContextMask);
            }

            if (!block.HasChroma)
            {
                return;
            }

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            Point chromaOrigin = Av1TileWriter.GetChromaBlockOrigin(
                blockOrigin,
                subsamplingX,
                subsamplingY);

            Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            Av1TransformSize chromaTransformSize = blockSize.GetMaxUvTransformSize(
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            Av1BlockSize maximumChromaUnitBlockSize =
                Av1BlockSize.Block64x64.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);

            int chromaStateIndex =
                chromaArea / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            Span<Av1EncoderTransformBlockState> blueStates =
                this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.U);

            Span<Av1EncoderTransformBlockState> redStates =
                this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.V);

            Span<int> blueCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.U);
            Span<int> redCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.V);
            PublishCoefficientContexts(
                this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                chromaOrigin,
                chromaBlockSize,
                chromaTransformSize,
                maximumChromaUnitBlockSize,
                blueCoefficients[chromaArea..],
                blueStates[chromaStateIndex..]);

            PublishCoefficientContexts(
                this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex],
                chromaOrigin,
                chromaBlockSize,
                chromaTransformSize,
                maximumChromaUnitBlockSize,
                redCoefficients[chromaArea..],
                redStates[chromaStateIndex..]);
        }

        private static void PublishCoefficientContexts(
            Av1NeighborArrayUnit<byte> neighbors,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1TransformSize transformSize,
            Av1BlockSize maximumUnitBlockSize,
            ReadOnlySpan<int> coefficients,
            ReadOnlySpan<Av1EncoderTransformBlockState> states)
        {
            const Av1NeighborArrayUnit<byte>.UnitMask EdgeMask =
                Av1NeighborArrayUnit<byte>.UnitMask.Top |
                Av1NeighborArrayUnit<byte>.UnitMask.Left;

            int blockWidth = blockSize.GetWidth();
            int blockHeight = blockSize.GetHeight();
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int transformSampleCount = transformSize.GetSize2d();
            int maximumUnitWidth = Math.Min(maximumUnitBlockSize.GetWidth(), blockWidth);
            int maximumUnitHeight = Math.Min(maximumUnitBlockSize.GetHeight(), blockHeight);
            int transformStateOffset = 0;
            int coefficientOffset = 0;
            int transformStateStride =
                transformSampleCount / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            // Coefficients retain AV1's bounded-region order rather than unrestricted row-major order.
            // Publishing the same sequence pairs every state with the transform that produced it.
            for (int regionRow = 0; regionRow < blockHeight; regionRow += maximumUnitHeight)
            {
                int unitBottom = Math.Min(regionRow + maximumUnitHeight, blockHeight);
                for (int regionColumn = 0; regionColumn < blockWidth; regionColumn += maximumUnitWidth)
                {
                    int unitRight = Math.Min(regionColumn + maximumUnitWidth, blockWidth);
                    for (int row = regionRow; row < unitBottom; row += transformHeight)
                    {
                        for (int column = regionColumn; column < unitRight; column += transformWidth)
                        {
                            Av1EncoderTransformBlockState state = states[transformStateOffset];
                            byte context = Av1SymbolContextHelper.GetCoefficientContext(
                                coefficients[coefficientOffset..],
                                transformSize,
                                state.TransformType,
                                state.EndOfBlock);

                            neighbors.UnitModeWrite(
                                context,
                                blockOrigin + new Size(column, row),
                                new Size(transformWidth, transformHeight),
                                EdgeMask);

                            coefficientOffset += transformSampleCount;
                            transformStateOffset += transformStateStride;
                        }
                    }
                }
            }
        }

        private void SavePartitionTrialContexts(
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize)
        {
            Span<byte> storage = this.blockWorkspace.GetPartitionContextStorage(blockSize);
            int offset = 0;
            SaveNeighborEdges(
                this.picture.PartitionContexts[tileIndex],
                blockOrigin,
                blockSize.Get4x4WideCount(),
                blockSize.Get4x4HighCount(),
                storage,
                ref offset);

            SaveNeighborEdges(
                this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                blockOrigin,
                blockSize.Get4x4WideCount(),
                blockSize.Get4x4HighCount(),
                storage,
                ref offset);

            SaveNeighborEdges(
                this.picture.TransformFunctionContexts[tileIndex],
                blockOrigin,
                blockSize.Get4x4WideCount(),
                blockSize.Get4x4HighCount(),
                storage,
                ref offset);

            if (this.picture.Parent.FrameHeader.AllowScreenContentTools)
            {
                SaveNeighborEdges(
                    this.picture.PaletteContexts[tileIndex],
                    blockOrigin,
                    blockSize.Get4x4WideCount(),
                    blockSize.Get4x4HighCount(),
                    storage,
                    ref offset);
            }

            if (this.source.IsMonochrome)
            {
                return;
            }

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            Point chromaOrigin = Av1TileWriter.GetChromaBlockOrigin(
                blockOrigin,
                subsamplingX,
                subsamplingY);

            Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            SaveNeighborEdges(
                this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                chromaOrigin,
                chromaBlockSize.Get4x4WideCount(),
                chromaBlockSize.Get4x4HighCount(),
                storage,
                ref offset);

            SaveNeighborEdges(
                this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex],
                chromaOrigin,
                chromaBlockSize.Get4x4WideCount(),
                chromaBlockSize.Get4x4HighCount(),
                storage,
                ref offset);
        }

        private void RestorePartitionTrialContexts(
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize)
        {
            ReadOnlySpan<byte> storage = this.blockWorkspace.GetPartitionContextStorage(blockSize);
            int offset = 0;
            RestoreNeighborEdges(
                this.picture.PartitionContexts[tileIndex],
                blockOrigin,
                blockSize.Get4x4WideCount(),
                blockSize.Get4x4HighCount(),
                storage,
                ref offset);

            RestoreNeighborEdges(
                this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                blockOrigin,
                blockSize.Get4x4WideCount(),
                blockSize.Get4x4HighCount(),
                storage,
                ref offset);

            RestoreNeighborEdges(
                this.picture.TransformFunctionContexts[tileIndex],
                blockOrigin,
                blockSize.Get4x4WideCount(),
                blockSize.Get4x4HighCount(),
                storage,
                ref offset);

            if (this.picture.Parent.FrameHeader.AllowScreenContentTools)
            {
                RestoreNeighborEdges(
                    this.picture.PaletteContexts[tileIndex],
                    blockOrigin,
                    blockSize.Get4x4WideCount(),
                    blockSize.Get4x4HighCount(),
                    storage,
                    ref offset);
            }

            if (this.source.IsMonochrome)
            {
                return;
            }

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            Point chromaOrigin = Av1TileWriter.GetChromaBlockOrigin(
                blockOrigin,
                subsamplingX,
                subsamplingY);

            Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            RestoreNeighborEdges(
                this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                chromaOrigin,
                chromaBlockSize.Get4x4WideCount(),
                chromaBlockSize.Get4x4HighCount(),
                storage,
                ref offset);

            RestoreNeighborEdges(
                this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex],
                chromaOrigin,
                chromaBlockSize.Get4x4WideCount(),
                chromaBlockSize.Get4x4HighCount(),
                storage,
                ref offset);
        }

        private static void SaveNeighborEdges<T>(
            Av1NeighborArrayUnit<T> neighbors,
            Point blockOrigin,
            int width,
            int height,
            Span<byte> storage,
            ref int offset)
            where T : struct
        {
            Span<byte> top = MemoryMarshal.AsBytes(
                neighbors.Top.Slice(neighbors.GetTopIndex(blockOrigin), width));

            top.CopyTo(storage[offset..]);
            offset += top.Length;
            Span<byte> left = MemoryMarshal.AsBytes(
                neighbors.Left.Slice(neighbors.GetLeftIndex(blockOrigin), height));

            left.CopyTo(storage[offset..]);
            offset += left.Length;
        }

        private static void RestoreNeighborEdges<T>(
            Av1NeighborArrayUnit<T> neighbors,
            Point blockOrigin,
            int width,
            int height,
            ReadOnlySpan<byte> storage,
            ref int offset)
            where T : struct
        {
            Span<byte> top = MemoryMarshal.AsBytes(
                neighbors.Top.Slice(neighbors.GetTopIndex(blockOrigin), width));

            storage.Slice(offset, top.Length).CopyTo(top);
            offset += top.Length;
            Span<byte> left = MemoryMarshal.AsBytes(
                neighbors.Left.Slice(neighbors.GetLeftIndex(blockOrigin), height));

            storage.Slice(offset, left.Length).CopyTo(left);
            offset += left.Length;
        }

        private long GetRegularBlockCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            long modeCost,
            int emptyTransformRate,
            bool skip,
            bool allowIntraBlockCopy)
        {
            int rateAdjustment = writer.GetSkipCost(skip, Av1TileWriter.GetSkipContext(macroBlock));
            if (allowIntraBlockCopy)
            {
                rateAdjustment += writer.GetUseIntraBlockCopyCost(false);
            }

            if (skip)
            {
                // Mode search includes empty transform symbols, while block skip suppresses them from the bitstream.
                rateAdjustment -= emptyTransformRate;
            }

            return modeCost + Av1RateDistortion.GetCost(this.rateMultiplier, rateAdjustment, 0);
        }

        private int GetEmptyTransformRate(
            Av1SymbolEncoder writer,
            Av1NeighborArrayUnit<byte> coefficientNeighbors,
            Av1ComponentType componentType,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1TransformSize transformSize,
            Av1PredictionMode lumaMode,
            Av1FilterIntraMode filterIntraMode)
        {
            int blockWidth = blockSize.Get4x4WideCount();
            int blockHeight = blockSize.Get4x4HighCount();
            int transformWidth = transformSize.Get4x4WideCount();
            int transformHeight = transformSize.Get4x4HighCount();
            if (blockWidth == transformWidth && blockHeight == transformHeight)
            {
                Av1TransformBlockContext blockContext = Av1TileWriter.GetTransformBlockContexts(
                    componentType,
                    coefficientNeighbors,
                    blockOrigin,
                    blockSize,
                    transformSize);

                return writer.GetCoefficientCost(
                    transformSize,
                    Av1TransformType.DctDct,
                    lumaMode,
                    ReadOnlySpan<int>.Empty,
                    componentType,
                    blockContext,
                    0,
                    this.picture.Parent.FrameHeader.UseReducedTransformSet,
                    filterIntraMode,
                    usesInterTransformSet: false);
            }

            Span<byte> contexts = this.blockWorkspace
                .GetModeDecisionWorkspace<TSample>()
                .TransformContexts;

            Span<byte> topContexts = contexts[..blockWidth];
            Span<byte> leftContexts = contexts.Slice(blockWidth, blockHeight);
            int topIndex = coefficientNeighbors.GetTopIndex(blockOrigin);
            int leftIndex = coefficientNeighbors.GetLeftIndex(blockOrigin);
            coefficientNeighbors.Top.Slice(topIndex, blockWidth).CopyTo(topContexts);
            coefficientNeighbors.Left.Slice(leftIndex, blockHeight).CopyTo(leftContexts);
            int rate = 0;
            for (int blockRow = 0; blockRow < blockHeight; blockRow += transformHeight)
            {
                for (int blockColumn = 0; blockColumn < blockWidth; blockColumn += transformWidth)
                {
                    Av1TransformBlockContext blockContext = Av1TileWriter.GetTransformBlockContexts(
                        componentType,
                        topContexts.Slice(blockColumn, transformWidth),
                        leftContexts.Slice(blockRow, transformHeight),
                        blockSize,
                        transformSize);

                    rate += writer.GetCoefficientCost(
                        transformSize,
                        Av1TransformType.DctDct,
                        lumaMode,
                        ReadOnlySpan<int>.Empty,
                        componentType,
                        blockContext,
                        0,
                        this.picture.Parent.FrameHeader.UseReducedTransformSet,
                        filterIntraMode,
                        usesInterTransformSet: false);

                    topContexts.Slice(blockColumn, transformWidth).Clear();
                    leftContexts.Slice(blockRow, transformHeight).Clear();
                }
            }

            return rate;
        }

        private Av1PredictionMode SelectLumaMode(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Span<int> retainedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedStates,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out Av1FilterIntraMode selectedFilterIntraMode,
            out Av1TransformSize selectedTransformSize,
            out long selectedCost)
        {
            bool codedLossless = this.picture.Parent.FrameHeader.CodedLossless;
            Av1TransformSize transformSize = codedLossless
                ? Av1TransformSize.Size4x4
                : blockSize.GetMaximumTransformSize();

            int blockWidth = blockSize.GetWidth();
            int blockHeight = blockSize.GetHeight();
            Av1EncoderModeDecisionWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>();

            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Buffer2DRegion<TSample> reconstructionPlane = this.reconstruction.GetPlane(Av1Plane.Y);
            if (blockWidth > transformSize.GetWidth() || blockHeight > transformSize.GetHeight())
            {
                return this.SelectTiledLumaMode(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    transformSize,
                    retainedCoefficients,
                    retainedStates,
                    out selectedAngleDelta,
                    out selectedFilterIntraMode,
                    out selectedTransformSize,
                    out selectedCost);
            }

            bool hasLeft = macroBlock.IsLeftAvailable;
            bool hasAbove = macroBlock.IsUpAvailable;
            int modeInfoRow = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            bool rightAvailable = modeInfoColumn + transformSize.Get4x4WideCount() < macroBlock.Tile.ModeInfoColumnEnd;
            bool bottomAvailable = modeInfoRow + transformSize.Get4x4HighCount() < macroBlock.Tile.ModeInfoRowEnd;
            bool hasTopRight = Av1IntraReferenceAvailability.HasTopRight(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                blockSize,
                modeInfoRow,
                modeInfoColumn,
                hasAbove,
                rightAvailable,
                Av1PartitionType.None,
                transformSize,
                0,
                0,
                0,
                0);

            bool hasBottomLeft = Av1IntraReferenceAvailability.HasBottomLeft(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                blockSize,
                modeInfoRow,
                modeInfoColumn,
                bottomAvailable,
                hasLeft,
                Av1PartitionType.None,
                transformSize,
                0,
                0,
                0,
                0);

            Span<TSample> aboveStorage = workspace.GetReferenceSamples(0);
            Span<TSample> above = aboveStorage[1..];
            Span<TSample> leftStorage = workspace.GetReferenceSamples(1);
            Span<TSample> left = leftStorage[1..];

            if (hasAbove)
            {
                reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1)
                    .Slice(blockOrigin.X, blockWidth)
                    .CopyTo(above[..blockWidth]);
            }

            if (hasLeft)
            {
                for (int row = 0; row < blockHeight; row++)
                {
                    left[row] = reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y + row)[blockOrigin.X - 1];
                }
            }

            int midpoint = 128 << (this.bitDepth.GetBitCount() - 8);

            // A missing edge repeats the closest perpendicular sample. Only a block with neither edge
            // available uses the asymmetric midpoint offsets that distinguish top from left.
            if (!hasAbove)
            {
                above[..blockWidth].Fill(hasLeft ? left[0] : TOperator.CreateSample(midpoint - 1));
            }

            if (!hasLeft)
            {
                left[..blockHeight].Fill(hasAbove ? above[0] : TOperator.CreateSample(midpoint + 1));
            }

            if (hasTopRight)
            {
                reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1)
                    .Slice(blockOrigin.X + blockWidth, blockWidth)
                    .CopyTo(above.Slice(blockWidth, blockWidth));
            }
            else
            {
                above.Slice(blockWidth, blockWidth).Fill(above[blockWidth - 1]);
            }

            if (hasBottomLeft)
            {
                for (int row = blockHeight; row < blockHeight * 2; row++)
                {
                    left[row] = reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y + row)[blockOrigin.X - 1];
                }
            }
            else
            {
                left.Slice(blockHeight, blockHeight).Fill(left[blockHeight - 1]);
            }

            // Zone-two projection and Paeth address the common corner immediately before both prepared edges.
            // When an edge is unavailable AV1 derives that corner from the closest coded edge.
            TSample corner = hasAbove && hasLeft
                ? reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1)[blockOrigin.X - 1]
                : hasAbove
                    ? above[0]
                    : hasLeft
                        ? left[0]
                        : TOperator.CreateSample(midpoint);

            aboveStorage[0] = corner;
            leftStorage[0] = corner;

            Av1TransformBlockContext blockContext = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Luminance,
                this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                blockOrigin,
                blockSize,
                transformSize);

            int transformSizeContext = Av1TileWriter.GetTransformSizeContext(
                this.picture.TransformFunctionContexts[tileIndex],
                macroBlock,
                blockOrigin,
                blockSize);

            int largestTransformRate = this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select
                && blockSize > Av1BlockSize.Block4x4
                ? writer.GetTransformSizeCost(blockSize, transformSize, transformSizeContext)
                : 0;

            int paletteDisabledCost = 0;
            if (Av1TileWriter.IsPaletteAllowed(
                this.picture.Parent.FrameHeader.AllowScreenContentTools,
                blockSize))
            {
                Av1NeighborArrayUnit<Av1EncoderPaletteInfo> paletteContexts = this.picture.PaletteContexts[tileIndex];
                int blockSizeContext = Av1TileWriter.GetPaletteBlockSizeContext(blockSize);
                int neighborContext = Av1TileWriter.GetPaletteYModeContext(
                    paletteContexts,
                    macroBlock,
                    blockOrigin);

                paletteDisabledCost = writer.GetPaletteYModeCost(
                    false,
                    blockSizeContext,
                    neighborContext);
            }

            Span<TSample> candidateReconstruction = workspace.GetCandidateReconstruction(0);
            Span<int> candidateCoefficients = workspace.GetCandidateCoefficients(0);
            Span<TSample> prediction = workspace.Prediction;
            Span<short> residual = workspace.Residual;
            long bestCost = long.MaxValue;
            Av1PredictionMode bestMode = Av1PredictionMode.DC;
            selectedAngleDelta = 0;
            selectedFilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            selectedTransformSize = transformSize;
            int baseModeCount = LumaModeSearchOrder.Length;
            int deltaCount = AngleDeltaSearchOrder.Length;
            int directionalModeCount = (int)Av1PredictionMode.Directional67Degrees - (int)Av1PredictionMode.Vertical + 1;

            // Effort zero evaluates DC only, effort one adds every zero-angle mode, and higher levels add all directional adjustments.
            int candidateCount = this.effort switch
            {
                0 => 1,
                1 => baseModeCount,
                _ when blockSize >= Av1BlockSize.Block8x8 => baseModeCount + (directionalModeCount * deltaCount),
                _ => baseModeCount
            };

            bool useReducedTransformSet = this.picture.Parent.FrameHeader.UseReducedTransformSet;
            Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(
                transformSize,
                useReducedTransformSet);

            // Transform type and transform size are separate search axes. Splitting their effort thresholds
            // gives callers a useful intermediate tier without changing the fast default path.
            bool searchEveryTransformType = !codedLossless && this.effort >= 7;
            bool searchEveryTransformSize = !codedLossless &&
                this.effort >= 8 &&
                transformSize == Av1TransformSize.Size8x8;

            // Zero-angle modes precede groups of six nonzero adjustments for each directional mode.
            // A single index preserves that tie-breaking order without duplicating candidate evaluation.
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Av1PredictionMode mode;
                int angleDelta;
                if (candidateIndex < baseModeCount)
                {
                    mode = LumaModeSearchOrder[candidateIndex];
                    angleDelta = 0;
                }
                else
                {
                    int adjustedIndex = candidateIndex - baseModeCount;
                    mode = (Av1PredictionMode)((int)Av1PredictionMode.Vertical + (adjustedIndex / deltaCount));
                    angleDelta = AngleDeltaSearchOrder[adjustedIndex % deltaCount];
                }

                // Prediction and subtraction do not depend on transform type. Preparing them once keeps
                // exhaustive transform search from repeating the same pixel traversal for every candidate.
                TOperator.PrepareIntra(
                    this.blockWorkspace,
                    sourcePlane,
                    blockOrigin,
                    prediction,
                    above,
                    left,
                    hasLeft,
                    hasAbove,
                    mode,
                    angleDelta,
                    residual,
                    transformSize,
                    this.bitDepth);

                // Transform types are visited in AV1 enumeration order. A strict cost comparison below keeps
                // the first legal type on ties, while lower efforts visit only the mode-derived default.
                Av1TransformType firstTransformType = codedLossless
                    ? Av1TransformType.DctDct
                    : searchEveryTransformType
                        ? Av1TransformType.DctDct
                        : Av1SymbolContextHelper.GetDefaultIntraTransformType(
                            mode,
                            transformSize,
                            useReducedTransformSet);

                Av1TransformType transformTypeLimit = searchEveryTransformType
                    ? Av1TransformType.AllTransformTypes
                    : (Av1TransformType)((int)firstTransformType + 1);

                for (Av1TransformType transformType = firstTransformType;
                    transformType < transformTypeLimit;
                    transformType++)
                {
                    if (!transformType.IsExtendedSetUsed(transformSetType))
                    {
                        continue;
                    }

                    Av1EncoderTransformBlockState candidateState = default;
                    long candidateCost = this.GetLumaCandidateCost(
                        writer,
                        macroBlock,
                        sourcePlane,
                        blockOrigin,
                        prediction,
                        residual,
                        mode,
                        angleDelta,
                        blockSize,
                        transformSize,
                        transformType,
                        blockContext,
                        paletteDisabledCost,
                        largestTransformRate,
                        candidateReconstruction,
                        candidateCoefficients,
                        ref candidateState);

                    if (candidateCost < bestCost)
                    {
                        // The shared candidate spans are overwritten by the next transform. Copy only a
                        // global improvement into final block storage so no per-mode retained buffer is needed.
                        CopyCandidate(
                            candidateReconstruction,
                            candidateCoefficients,
                            reconstructionPlane,
                            blockOrigin,
                            retainedCoefficients,
                            transformSize,
                            candidateState,
                            ref retainedStates[0]);

                        bestCost = candidateCost;
                        bestMode = mode;
                        selectedAngleDelta = angleDelta;
                        selectedTransformSize = transformSize;
                    }
                }

                // At exhaustive effort, transform size belongs to this mode's RD result. Evaluate it
                // before advancing so an 8x8-only preliminary result cannot discard a better split mode.
                if (searchEveryTransformSize &&
                    this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select)
                {
                    long splitCost = this.GetSplitLumaCandidateCost(
                        writer,
                        macroBlock,
                        sourcePlane,
                        reconstructionPlane,
                        blockOrigin,
                        tileIndex,
                        mode,
                        angleDelta,
                        Av1FilterIntraMode.AllFilterIntraModes,
                        0,
                        ReadOnlySpan<ushort>.Empty,
                        0,
                        paletteDisabledCost,
                        transformSizeContext,
                        bestCost,
                        candidateReconstruction,
                        candidateCoefficients,
                        workspace.CandidateTransformBlocks);

                    if (splitCost < bestCost)
                    {
                        CopySplitCandidate(
                            candidateReconstruction,
                            candidateCoefficients,
                            workspace.CandidateTransformBlocks,
                            reconstructionPlane,
                            blockOrigin,
                            retainedCoefficients,
                            retainedStates);

                        bestCost = splitCost;
                        bestMode = mode;
                        selectedAngleDelta = angleDelta;
                        selectedTransformSize = Av1TransformSize.Size4x4;
                    }
                }
            }

            // Midrange effort refines the preliminary mode only. Higher effort already searched every
            // mode-transform pair above, so repeating the winning mode would add no candidates.
            long bestTransformCost = bestCost;
            if (!codedLossless && this.effort >= 3 && !searchEveryTransformType)
            {
                // The shared spans now contain the last mode visited above, so rebuild the preliminary
                // winner once before refining its transform types.
                TOperator.PrepareIntra(
                    this.blockWorkspace,
                    sourcePlane,
                    blockOrigin,
                    prediction,
                    above,
                    left,
                    hasLeft,
                    hasAbove,
                    bestMode,
                    selectedAngleDelta,
                    residual,
                    transformSize,
                    this.bitDepth);

                for (Av1TransformType transformType = Av1TransformType.DctDct;
                    transformType < Av1TransformType.AllTransformTypes;
                    transformType++)
                {
                    if (!transformType.IsExtendedSetUsed(transformSetType))
                    {
                        continue;
                    }

                    Av1EncoderTransformBlockState candidateState = default;
                    long candidateCost = this.GetLumaCandidateCost(
                        writer,
                        macroBlock,
                        sourcePlane,
                        blockOrigin,
                        prediction,
                        residual,
                        bestMode,
                        selectedAngleDelta,
                        blockSize,
                        transformSize,
                        transformType,
                        blockContext,
                        paletteDisabledCost,
                        largestTransformRate,
                        candidateReconstruction,
                        candidateCoefficients,
                        ref candidateState);

                    if (candidateCost < bestTransformCost)
                    {
                        CopyCandidate(
                            candidateReconstruction,
                            candidateCoefficients,
                            reconstructionPlane,
                            blockOrigin,
                            retainedCoefficients,
                            transformSize,
                            candidateState,
                            ref retainedStates[0]);

                        bestTransformCost = candidateCost;
                    }
                }
            }

            if (this.effort >= 4 &&
                Av1TileWriter.IsFilterIntraAllowedBlockSize(
                    this.picture.Sequence.SequenceHeader.EnableFilterIntra,
                    blockSize))
            {
                // Each recursive filter prediction and its source residual are independent of transform type.
                // Prepare them once per filter mode so all legal transforms reuse the same samples.
                for (Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
                    filterIntraMode < Av1FilterIntraMode.AllFilterIntraModes;
                    filterIntraMode++)
                {
                    TOperator.PrepareFilterIntra(
                        this.blockWorkspace,
                        sourcePlane,
                        blockOrigin,
                        prediction,
                        above,
                        left,
                        residual,
                        filterIntraMode,
                        transformSize,
                        this.bitDepth);

                    Av1TransformType filterTransformTypeLimit = codedLossless
                        ? (Av1TransformType)((int)Av1TransformType.DctDct + 1)
                        : Av1TransformType.AllTransformTypes;

                    for (Av1TransformType transformType = Av1TransformType.DctDct;
                        transformType < filterTransformTypeLimit;
                        transformType++)
                    {
                        if (!transformType.IsExtendedSetUsed(transformSetType))
                        {
                            continue;
                        }

                        Av1EncoderTransformBlockState candidateState = default;
                        long candidateCost = this.GetFilterIntraCandidateCost(
                            writer,
                            macroBlock,
                            sourcePlane,
                            blockOrigin,
                            prediction,
                            residual,
                            filterIntraMode,
                            blockSize,
                            transformSize,
                            transformType,
                            blockContext,
                            paletteDisabledCost,
                            largestTransformRate,
                            candidateReconstruction,
                            candidateCoefficients,
                            ref candidateState);

                        if (candidateCost < bestTransformCost)
                        {
                            CopyCandidate(
                                candidateReconstruction,
                                candidateCoefficients,
                                reconstructionPlane,
                                blockOrigin,
                                retainedCoefficients,
                                transformSize,
                                candidateState,
                                ref retainedStates[0]);

                            bestTransformCost = candidateCost;
                            bestMode = Av1PredictionMode.DC;
                            selectedAngleDelta = 0;
                            selectedFilterIntraMode = filterIntraMode;
                            selectedTransformSize = transformSize;
                        }
                    }

                    // Filter-intra mode and transform size form one candidate for RD comparison, just as
                    // ordinary spatial mode and transform size do in the exhaustive search above.
                    if (searchEveryTransformSize &&
                        this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select)
                    {
                        long splitCost = this.GetSplitLumaCandidateCost(
                            writer,
                            macroBlock,
                            sourcePlane,
                            reconstructionPlane,
                            blockOrigin,
                            tileIndex,
                            Av1PredictionMode.DC,
                            0,
                            filterIntraMode,
                            0,
                            ReadOnlySpan<ushort>.Empty,
                            0,
                            paletteDisabledCost,
                            transformSizeContext,
                            bestTransformCost,
                            candidateReconstruction,
                            candidateCoefficients,
                            workspace.CandidateTransformBlocks);

                        if (splitCost < bestTransformCost)
                        {
                            CopySplitCandidate(
                                candidateReconstruction,
                                candidateCoefficients,
                                workspace.CandidateTransformBlocks,
                                reconstructionPlane,
                                blockOrigin,
                                retainedCoefficients,
                                retainedStates);

                            bestTransformCost = splitCost;
                            bestMode = Av1PredictionMode.DC;
                            selectedAngleDelta = 0;
                            selectedFilterIntraMode = filterIntraMode;
                            selectedTransformSize = Av1TransformSize.Size4x4;
                        }
                    }
                }
            }

            if (this.effort >= 5 &&
                blockSize == Av1BlockSize.Block8x8 &&
                this.picture.Parent.FrameHeader.AllowScreenContentTools &&
                this.SelectLumaPalette(
                    writer,
                    macroBlock,
                    sourcePlane,
                    reconstructionPlane,
                    blockOrigin,
                    tileIndex,
                    transformSetType,
                    blockContext,
                    largestTransformRate,
                    transformSizeContext,
                    candidateReconstruction,
                    candidateCoefficients,
                    retainedCoefficients,
                    retainedStates,
                    ref bestTransformCost,
                    ref paletteInfo,
                    ref selectedTransformSize))
            {
                bestMode = Av1PredictionMode.DC;
                selectedAngleDelta = 0;
                selectedFilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            }

            // Efforts six and seven save work by testing transform size only for the global non-palette
            // winner. Effort eight and above already tested both sizes inside every candidate.
            if (this.effort >= 6 &&
                transformSize == Av1TransformSize.Size8x8 &&
                !searchEveryTransformSize &&
                this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select &&
                paletteInfo.PaletteSizes[0] == 0)
            {
                long splitCost = this.GetSplitLumaCandidateCost(
                    writer,
                    macroBlock,
                    sourcePlane,
                    reconstructionPlane,
                    blockOrigin,
                    tileIndex,
                    bestMode,
                    selectedAngleDelta,
                    selectedFilterIntraMode,
                    0,
                    ReadOnlySpan<ushort>.Empty,
                    0,
                    paletteDisabledCost,
                    transformSizeContext,
                    bestTransformCost,
                    candidateReconstruction,
                    candidateCoefficients,
                    workspace.CandidateTransformBlocks);

                if (splitCost < bestTransformCost)
                {
                    CopySplitCandidate(
                        candidateReconstruction,
                        candidateCoefficients,
                        workspace.CandidateTransformBlocks,
                        reconstructionPlane,
                        blockOrigin,
                        retainedCoefficients,
                        retainedStates);

                    bestTransformCost = splitCost;
                    selectedTransformSize = Av1TransformSize.Size4x4;
                }
            }

            selectedCost = bestTransformCost;
            return bestMode;
        }

        private Av1PredictionMode SelectTiledLumaMode(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Av1TransformSize transformSize,
            Span<int> retainedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedStates,
            out int selectedAngleDelta,
            out Av1FilterIntraMode selectedFilterIntraMode,
            out Av1TransformSize selectedTransformSize,
            out long selectedCost)
        {
            Av1EncoderModeDecisionWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>();

            int blockWidth = blockSize.GetWidth();
            int blockHeight = blockSize.GetHeight();
            int blockSampleCount = blockWidth * blockHeight;
            int transformBlockCount = blockSampleCount / transformSize.GetSize2d();
            Span<TSample> candidateReconstruction =
                workspace.GetCandidateReconstruction(0)[..blockSampleCount];

            Span<int> candidateCoefficients =
                workspace.GetCandidateCoefficients(0)[..blockSampleCount];

            Span<Av1EncoderTransformBlockState> candidateStates =
                workspace.CandidateTransformBlocks[..transformBlockCount];

            int contextWidth = blockSize.Get4x4WideCount();
            int contextHeight = blockSize.Get4x4HighCount();
            Span<byte> contexts = workspace.TransformContexts;
            Span<byte> topContexts = contexts[..contextWidth];
            Span<byte> leftContexts = contexts.Slice(contextWidth, contextHeight);
            Av1NeighborArrayUnit<byte> coefficientNeighbors =
                this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex];

            int topIndex = coefficientNeighbors.GetTopIndex(blockOrigin);
            int leftIndex = coefficientNeighbors.GetLeftIndex(blockOrigin);
            int transformSizeContext = Av1TileWriter.GetTransformSizeContext(
                this.picture.TransformFunctionContexts[tileIndex],
                macroBlock,
                blockOrigin,
                blockSize);

            int transformSizeRate = this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select
                ? writer.GetTransformSizeCost(blockSize, transformSize, transformSizeContext)
                : 0;

            int paletteDisabledCost = Av1TileWriter.IsPaletteAllowed(
                this.picture.Parent.FrameHeader.AllowScreenContentTools,
                blockSize)
                    ? writer.GetPaletteYModeCost(
                        false,
                        Av1TileWriter.GetPaletteBlockSizeContext(blockSize),
                        Av1TileWriter.GetPaletteYModeContext(
                            this.picture.PaletteContexts[tileIndex],
                            macroBlock,
                            blockOrigin))
                    : 0;

            int baseModeCount = LumaModeSearchOrder.Length;
            int deltaCount = AngleDeltaSearchOrder.Length;
            int directionalModeCount =
                (int)Av1PredictionMode.Directional67Degrees - (int)Av1PredictionMode.Vertical + 1;

            int candidateCount = this.effort switch
            {
                0 => 1,
                1 => baseModeCount,
                _ => baseModeCount + (directionalModeCount * deltaCount)
            };

            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Buffer2DRegion<TSample> reconstructionPlane = this.reconstruction.GetPlane(Av1Plane.Y);
            long bestCost = long.MaxValue;
            Av1PredictionMode bestMode = Av1PredictionMode.DC;
            selectedAngleDelta = 0;
            selectedFilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            selectedTransformSize = transformSize;

            // Each candidate starts from the live block-edge contexts. Transform updates remain local until
            // that candidate wins, so later modes never inherit state from an earlier trial.
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Av1PredictionMode mode;
                int angleDelta;
                if (candidateIndex < baseModeCount)
                {
                    mode = LumaModeSearchOrder[candidateIndex];
                    angleDelta = 0;
                }
                else
                {
                    int adjustedIndex = candidateIndex - baseModeCount;
                    mode = (Av1PredictionMode)(
                        (int)Av1PredictionMode.Vertical + (adjustedIndex / deltaCount));

                    angleDelta = AngleDeltaSearchOrder[adjustedIndex % deltaCount];
                }

                coefficientNeighbors.Top.Slice(topIndex, contextWidth).CopyTo(topContexts);
                coefficientNeighbors.Left.Slice(leftIndex, contextHeight).CopyTo(leftContexts);
                long distortion = this.GetTiledPlaneCost(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockOrigin,
                    blockSize,
                    blockSize,
                    transformSize,
                    Av1BlockSize.Block64x64,
                    0,
                    0,
                    mode,
                    mode,
                    angleDelta,
                    Av1Plane.Y,
                    sourcePlane,
                    reconstructionPlane,
                    candidateReconstruction,
                    candidateCoefficients,
                    candidateStates,
                    topContexts,
                    leftContexts,
                    out int coefficientRate);

                int rate = Av1TileWriter.GetLumaModeCost(writer, macroBlock, blockSize, mode, angleDelta);
                rate += transformSizeRate + coefficientRate;
                if (mode == Av1PredictionMode.DC)
                {
                    rate += paletteDisabledCost;
                    if (Av1TileWriter.IsFilterIntraAllowedBlockSize(
                        this.picture.Sequence.SequenceHeader.EnableFilterIntra,
                        blockSize))
                    {
                        rate += writer.GetFilterIntraModeCost(
                            Av1FilterIntraMode.AllFilterIntraModes,
                            blockSize);
                    }
                }

                long candidateCost = Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion);
                if (candidateCost < bestCost)
                {
                    CopyTiledCandidate(
                        candidateReconstruction,
                        candidateCoefficients,
                        candidateStates,
                        reconstructionPlane,
                        blockOrigin,
                        blockWidth,
                        blockHeight,
                        transformSize,
                        retainedCoefficients,
                        retainedStates);

                    bestCost = candidateCost;
                    bestMode = mode;
                    selectedAngleDelta = angleDelta;
                }
            }

            selectedCost = bestCost;
            return bestMode;
        }

        private long GetSplitLumaCandidateCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Buffer2DRegion<TSample> sourcePlane,
            Buffer2DRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            ushort tileIndex,
            Av1PredictionMode mode,
            int angleDelta,
            Av1FilterIntraMode filterIntraMode,
            int paletteSize,
            scoped ReadOnlySpan<ushort> paletteColors,
            int paletteHeaderRate,
            int paletteDisabledCost,
            int transformSizeContext,
            long costLimit,
            Span<TSample> candidateReconstruction,
            Span<int> candidateCoefficients,
            Span<Av1EncoderTransformBlockState> candidateTransformBlocks)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const Av1TransformSize TransformSize = Av1TransformSize.Size4x4;
            const int BlockWidth = 8;
            const int TransformWidth = 4;
            const int TransformSampleCount = TransformWidth * TransformWidth;
            Av1EncoderModeDecisionWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>();

            // One transient 64-sample plane holds the 4x4 prediction followed by two reconstruction buffers.
            // The disjoint views stay live together and need no additional owner or allocator rent.
            Span<TSample> transformSamples = workspace.GetCandidateReconstruction(1);
            Span<TSample> prediction = transformSamples[..TransformSampleCount];
            Span<TSample> candidateTransformReconstruction = transformSamples.Slice(
                TransformSampleCount,
                TransformSampleCount);

            Span<TSample> bestTransformReconstruction = transformSamples.Slice(
                TransformSampleCount * 2,
                TransformSampleCount);

            Span<int> transformCoefficientStorage = workspace.GetCandidateCoefficients(1);
            Span<int> candidateTransformCoefficients = transformCoefficientStorage[..TransformSampleCount];
            Span<int> bestTransformCoefficients = transformCoefficientStorage.Slice(
                TransformSampleCount,
                TransformSampleCount);

            Span<short> residual = workspace.Residual[..TransformSampleCount];
            Span<byte> contexts = workspace.TransformContexts;
            Span<byte> topContexts = contexts[..2];
            Span<byte> leftContexts = contexts[2..4];
            Av1NeighborArrayUnit<byte> coefficientNeighbors =
                this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex];

            int topIndex = coefficientNeighbors.GetTopIndex(blockOrigin);
            int leftIndex = coefficientNeighbors.GetLeftIndex(blockOrigin);
            coefficientNeighbors.Top.Slice(topIndex, 2).CopyTo(topContexts);
            coefficientNeighbors.Left.Slice(leftIndex, 2).CopyTo(leftContexts);
            bool useReducedTransformSet = this.picture.Parent.FrameHeader.UseReducedTransformSet;
            Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(
                TransformSize,
                useReducedTransformSet);

            // Prediction-mode and transform-size symbols belong to the 8x8 coding block, while each
            // 4x4 transform contributes its own coefficient rate below.
            int rate = writer.GetTransformSizeCost(BlockSize, TransformSize, transformSizeContext);
            if (paletteSize > 0)
            {
                rate += paletteHeaderRate;
            }
            else
            {
                rate += Av1TileWriter.GetLumaModeCost(writer, macroBlock, BlockSize, mode, angleDelta);
                if (mode == Av1PredictionMode.DC)
                {
                    rate += paletteDisabledCost;
                    if (this.picture.Sequence.SequenceHeader.EnableFilterIntra)
                    {
                        rate += writer.GetFilterIntraModeCost(
                            filterIntraMode,
                            BlockSize);
                    }
                }
            }

            Buffer2DRegion<byte> colorIndexMap = default;
            if (paletteSize > 0)
            {
                colorIndexMap = this.superblock.Workspace
                    .GetPaletteMaps()
                    .GetMap(Av1PlaneType.Y, BlockWidth, BlockWidth);
            }

            long distortion = 0;

            // Raster order is observable here: each retained 4x4 reconstruction supplies reference
            // samples and coefficient context to transforms that follow it in the same coding block.
            for (int transformRow = 0; transformRow < 2; transformRow++)
            {
                for (int transformColumn = 0; transformColumn < 2; transformColumn++)
                {
                    int transformIndex = (transformRow * 2) + transformColumn;
                    int reconstructionOffset =
                        (transformRow * TransformWidth * BlockWidth) + (transformColumn * TransformWidth);

                    Point transformOrigin = blockOrigin + new Size(
                        transformColumn * TransformWidth,
                        transformRow * TransformWidth);

                    if (paletteSize > 0)
                    {
                        // Palette prediction is block-local. A view over the retained map avoids copying indices or
                        // preparing reconstructed neighbor edges that this prediction mode cannot consume.
                        TOperator.PreparePalette(
                            sourcePlane,
                            transformOrigin,
                            paletteColors,
                            colorIndexMap.GetSubRegion(
                                new Rectangle(
                                    transformColumn * TransformWidth,
                                    transformRow * TransformWidth,
                                    TransformWidth,
                                    TransformWidth)),
                            prediction,
                            residual,
                            TransformSize);
                    }
                    else
                    {
                        Span<TSample> aboveStorage = workspace.GetReferenceSamples(0);
                        Span<TSample> leftStorage = workspace.GetReferenceSamples(1);
                        this.PrepareTransformReferenceSamples(
                            reconstructionPlane,
                            blockOrigin,
                            blockOrigin,
                            BlockSize,
                            macroBlock,
                            transformRow,
                            transformColumn,
                            BlockWidth,
                            TransformSize,
                            0,
                            0,
                            candidateReconstruction,
                            aboveStorage,
                            leftStorage,
                            out bool hasLeft,
                            out bool hasAbove);

                        if (filterIntraMode == Av1FilterIntraMode.AllFilterIntraModes)
                        {
                            TOperator.PrepareIntra(
                                this.blockWorkspace,
                                sourcePlane,
                                transformOrigin,
                                prediction,
                                aboveStorage.Slice(1, TransformWidth * 2),
                                leftStorage.Slice(1, TransformWidth * 2),
                                hasLeft,
                                hasAbove,
                                mode,
                                angleDelta,
                                residual,
                                TransformSize,
                                this.bitDepth);
                        }
                        else
                        {
                            // Filter-intra prediction is recursive within each transform unit, so rebuild it from
                            // the reconstructed edges established by the preceding 4x4 candidate.
                            TOperator.PrepareFilterIntra(
                                this.blockWorkspace,
                                sourcePlane,
                                transformOrigin,
                                prediction,
                                aboveStorage.Slice(1, TransformWidth * 2),
                                leftStorage.Slice(1, TransformWidth * 2),
                                residual,
                                filterIntraMode,
                                TransformSize,
                                this.bitDepth);
                        }
                    }

                    Av1TransformBlockContext blockContext = Av1TileWriter.GetTransformBlockContexts(
                        Av1ComponentType.Luminance,
                        topContexts.Slice(transformColumn, 1),
                        leftContexts.Slice(transformRow, 1),
                        BlockSize,
                        TransformSize);

                    long bestTransformCost = long.MaxValue;
                    Av1TransformType bestTransformType = Av1TransformType.DctDct;
                    int bestTransformRate = 0;
                    long bestTransformDistortion = 0;
                    Av1EncoderTransformBlockState bestTransformState = default;
                    Span<int> retainedTransformCoefficients = candidateCoefficients.Slice(
                        transformIndex * TransformSampleCount,
                        TransformSampleCount);

                    // Each transform writes into the compact buffer that does not hold the current best.
                    // Swapping spans on improvement keeps the winner without copying it inside the search loop.
                    for (Av1TransformType transformType = Av1TransformType.DctDct;
                        transformType < Av1TransformType.AllTransformTypes;
                        transformType++)
                    {
                        if (!transformType.IsExtendedSetUsed(transformSetType))
                        {
                            continue;
                        }

                        Av1EncoderTransformBlockState candidateState = default;
                        long candidateDistortion = TOperator.EncodePredictionCandidate(
                            this.blockWorkspace,
                            sourcePlane,
                            transformOrigin,
                            prediction,
                            residual,
                            candidateTransformReconstruction,
                            TransformWidth,
                            candidateTransformCoefficients,
                            TransformSize,
                            transformType,
                            Av1Plane.Y,
                            this.quantization.QIndex[0],
                            this.quantization.DeltaQDc[(int)Av1Plane.Y],
                            this.quantization.DeltaQAc[(int)Av1Plane.Y],
                            this.bitDepth,
                            ref candidateState);

                        int candidateRate = writer.GetCoefficientCost(
                            TransformSize,
                            transformType,
                            mode,
                            candidateTransformCoefficients,
                            Av1ComponentType.Luminance,
                            blockContext,
                            candidateState.EndOfBlock,
                            useReducedTransformSet,
                            filterIntraMode,
                            usesInterTransformSet: false);

                        long candidateCost = Av1RateDistortion.GetCost(
                            this.rateMultiplier,
                            candidateRate,
                            candidateDistortion);

                        if (candidateCost < bestTransformCost)
                        {
                            Span<TSample> previousBestReconstruction = bestTransformReconstruction;
                            bestTransformReconstruction = candidateTransformReconstruction;
                            candidateTransformReconstruction = previousBestReconstruction;

                            Span<int> previousBestCoefficients = bestTransformCoefficients;
                            bestTransformCoefficients = candidateTransformCoefficients;
                            candidateTransformCoefficients = previousBestCoefficients;

                            bestTransformCost = candidateCost;
                            bestTransformType = transformType;
                            bestTransformRate = candidateRate;
                            bestTransformDistortion = candidateDistortion;
                            bestTransformState = candidateState;
                        }
                    }

                    // The next 4x4 prediction consumes this reconstruction from the block mosaic. Publish the
                    // final winner once, after transform search, along with its entropy-context coefficients.
                    bestTransformCoefficients.CopyTo(retainedTransformCoefficients);
                    for (int row = 0; row < TransformWidth; row++)
                    {
                        bestTransformReconstruction.Slice(row * TransformWidth, TransformWidth)
                            .CopyTo(
                                candidateReconstruction.Slice(
                                    reconstructionOffset + (row * BlockWidth),
                                    TransformWidth));
                    }

                    rate += bestTransformRate;
                    distortion += bestTransformDistortion;
                    candidateTransformBlocks[transformIndex] = bestTransformState;
                    byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                        retainedTransformCoefficients,
                        TransformSize,
                        bestTransformType,
                        bestTransformState.EndOfBlock);

                    topContexts[transformColumn] = coefficientContext;
                    leftContexts[transformRow] = coefficientContext;

                    // Every remaining transform can only add nonnegative rate and distortion.
                    if (Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion) >= costLimit)
                    {
                        return long.MaxValue;
                    }
                }
            }

            return Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion);
        }

        private void PrepareTransformReferenceSamples(
            Buffer2DRegion<TSample> reconstructionPlane,
            Point lumaBlockOrigin,
            Point planeBlockOrigin,
            Av1BlockSize blockSize,
            Av1MacroBlockD macroBlock,
            int transformRow,
            int transformColumn,
            int planeBlockWidth,
            Av1TransformSize transformSize,
            int subsamplingX,
            int subsamplingY,
            ReadOnlySpan<TSample> candidateReconstruction,
            Span<TSample> aboveStorage,
            Span<TSample> leftStorage,
            out bool hasLeft,
            out bool hasAbove)
        {
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int rowOffset = transformRow * transformHeight;
            int columnOffset = transformColumn * transformWidth;
            int modeInfoRow = lumaBlockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = lumaBlockOrigin.X >> Av1Constants.ModeInfoSizeLog2;

            // Internal top and left edges come from the candidate mosaic built in raster order. Edges outside
            // the candidate continue to read committed reconstruction, keeping unsuccessful trials isolated.
            hasAbove = transformRow > 0 || macroBlock.IsUpAvailable;
            hasLeft = transformColumn > 0 || macroBlock.IsLeftAvailable;
            int transformRow4x4 = rowOffset >> Av1Constants.ModeInfoSizeLog2;
            int transformColumn4x4 = columnOffset >> Av1Constants.ModeInfoSizeLog2;
            bool rightAvailable =
                modeInfoColumn +
                    ((transformColumn4x4 + transformSize.Get4x4WideCount()) << subsamplingX) <
                macroBlock.Tile.ModeInfoColumnEnd;

            bool bottomAvailable =
                modeInfoRow +
                    ((transformRow4x4 + transformSize.Get4x4HighCount()) << subsamplingY) <
                macroBlock.Tile.ModeInfoRowEnd;

            bool hasTopRight = Av1IntraReferenceAvailability.HasTopRight(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                blockSize,
                modeInfoRow,
                modeInfoColumn,
                hasAbove,
                rightAvailable,
                Av1PartitionType.None,
                transformSize,
                transformRow4x4,
                transformColumn4x4,
                subsamplingX,
                subsamplingY);

            bool hasBottomLeft = Av1IntraReferenceAvailability.HasBottomLeft(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                blockSize,
                modeInfoRow,
                modeInfoColumn,
                bottomAvailable,
                hasLeft,
                Av1PartitionType.None,
                transformSize,
                transformRow4x4,
                transformColumn4x4,
                subsamplingX,
                subsamplingY);

            Span<TSample> above = aboveStorage.Slice(1, transformWidth * 2);
            Span<TSample> left = leftStorage.Slice(1, transformHeight * 2);
            if (hasAbove)
            {
                if (transformRow > 0)
                {
                    candidateReconstruction
                        .Slice(((rowOffset - 1) * planeBlockWidth) + columnOffset, transformWidth)
                        .CopyTo(above);
                }
                else
                {
                    reconstructionPlane.DangerousGetRowSpan(planeBlockOrigin.Y - 1)
                        .Slice(planeBlockOrigin.X + columnOffset, transformWidth)
                        .CopyTo(above);
                }
            }

            if (hasLeft)
            {
                if (transformColumn > 0)
                {
                    for (int row = 0; row < transformHeight; row++)
                    {
                        left[row] = candidateReconstruction[
                            ((rowOffset + row) * planeBlockWidth) + columnOffset - 1];
                    }
                }
                else
                {
                    for (int row = 0; row < transformHeight; row++)
                    {
                        left[row] = reconstructionPlane
                            .DangerousGetRowSpan(planeBlockOrigin.Y + rowOffset + row)[planeBlockOrigin.X - 1];
                    }
                }
            }

            int midpoint = 128 << (this.bitDepth.GetBitCount() - 8);
            if (!hasAbove)
            {
                above[..transformWidth].Fill(hasLeft ? left[0] : TOperator.CreateSample(midpoint - 1));
            }

            if (!hasLeft)
            {
                left[..transformHeight].Fill(hasAbove ? above[0] : TOperator.CreateSample(midpoint + 1));
            }

            if (hasTopRight)
            {
                if (transformRow > 0)
                {
                    candidateReconstruction
                        .Slice(
                            ((rowOffset - 1) * planeBlockWidth) + columnOffset + transformWidth,
                            transformWidth)
                        .CopyTo(above[transformWidth..]);
                }
                else
                {
                    reconstructionPlane.DangerousGetRowSpan(planeBlockOrigin.Y - 1)
                        .Slice(planeBlockOrigin.X + columnOffset + transformWidth, transformWidth)
                        .CopyTo(above[transformWidth..]);
                }
            }
            else
            {
                above[transformWidth..].Fill(above[transformWidth - 1]);
            }

            if (hasBottomLeft)
            {
                if (transformColumn > 0)
                {
                    for (int row = transformHeight; row < transformHeight * 2; row++)
                    {
                        left[row] = candidateReconstruction[
                            ((rowOffset + row) * planeBlockWidth) + columnOffset - 1];
                    }
                }
                else
                {
                    for (int row = transformHeight; row < transformHeight * 2; row++)
                    {
                        left[row] = reconstructionPlane
                            .DangerousGetRowSpan(planeBlockOrigin.Y + rowOffset + row)[planeBlockOrigin.X - 1];
                    }
                }
            }
            else
            {
                left[transformHeight..].Fill(left[transformHeight - 1]);
            }

            // Only an interior transform corner belongs to decision scratch. Boundary corners continue
            // to read the already reconstructed neighboring block so candidate trials remain isolated.
            TSample corner = hasAbove && hasLeft
                ? transformRow > 0 && transformColumn > 0
                    ? candidateReconstruction[((rowOffset - 1) * planeBlockWidth) + columnOffset - 1]
                    : reconstructionPlane.DangerousGetRowSpan(planeBlockOrigin.Y + rowOffset - 1)[
                        planeBlockOrigin.X + columnOffset - 1]
                : hasAbove
                    ? above[0]
                    : hasLeft
                        ? left[0]
                        : TOperator.CreateSample(midpoint);

            aboveStorage[0] = corner;
            leftStorage[0] = corner;
        }

        private long GetLumaCandidateCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Buffer2DRegion<TSample> sourcePlane,
            Point blockOrigin,
            ReadOnlySpan<TSample> prediction,
            ReadOnlySpan<short> residual,
            Av1PredictionMode mode,
            int angleDelta,
            Av1BlockSize blockSize,
            Av1TransformSize transformSize,
            Av1TransformType transformType,
            Av1TransformBlockContext blockContext,
            int paletteDisabledCost,
            int transformSizeRate,
            Span<TSample> candidateReconstruction,
            Span<int> candidateCoefficients,
            ref Av1EncoderTransformBlockState candidateState)
        {
            // Prediction and subtraction were prepared by the owning mode loop. This stage performs only
            // transform, quantization, reconstruction, and distortion for the requested transform type.
            long distortion = TOperator.EncodePredictionCandidate(
                this.blockWorkspace,
                sourcePlane,
                blockOrigin,
                prediction,
                residual,
                candidateReconstruction,
                transformSize.GetWidth(),
                candidateCoefficients,
                transformSize,
                transformType,
                Av1Plane.Y,
                this.quantization.QIndex[0],
                this.quantization.DeltaQDc[(int)Av1Plane.Y],
                this.quantization.DeltaQAc[(int)Av1Plane.Y],
                this.bitDepth,
                ref candidateState);

            // Charge every block-level choice that distinguishes this spatial candidate before adding
            // coefficient syntax derived from the live neighboring-transform context.
            int rate = Av1TileWriter.GetLumaModeCost(writer, macroBlock, blockSize, mode, angleDelta);
            rate += transformSizeRate;
            if (mode == Av1PredictionMode.DC)
            {
                rate += paletteDisabledCost;
            }

            if (mode == Av1PredictionMode.DC &&
                Av1TileWriter.IsFilterIntraAllowedBlockSize(
                    this.picture.Sequence.SequenceHeader.EnableFilterIntra,
                    blockSize))
            {
                rate += writer.GetFilterIntraModeCost(Av1FilterIntraMode.AllFilterIntraModes, blockSize);
            }

            rate += writer.GetCoefficientCost(
                transformSize,
                transformType,
                mode,
                candidateCoefficients,
                Av1ComponentType.Luminance,
                blockContext,
                candidateState.EndOfBlock,
                this.picture.Parent.FrameHeader.UseReducedTransformSet,
                Av1FilterIntraMode.AllFilterIntraModes,
                usesInterTransformSet: false);

            return Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion);
        }

        private long GetFilterIntraCandidateCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Buffer2DRegion<TSample> sourcePlane,
            Point blockOrigin,
            ReadOnlySpan<TSample> prediction,
            ReadOnlySpan<short> residual,
            Av1FilterIntraMode filterIntraMode,
            Av1BlockSize blockSize,
            Av1TransformSize transformSize,
            Av1TransformType transformType,
            Av1TransformBlockContext blockContext,
            int paletteDisabledCost,
            int transformSizeRate,
            Span<TSample> candidateReconstruction,
            Span<int> candidateCoefficients,
            ref Av1EncoderTransformBlockState candidateState)
        {
            long distortion = TOperator.EncodePredictionCandidate(
                this.blockWorkspace,
                sourcePlane,
                blockOrigin,
                prediction,
                residual,
                candidateReconstruction,
                transformSize.GetWidth(),
                candidateCoefficients,
                transformSize,
                transformType,
                Av1Plane.Y,
                this.quantization.QIndex[0],
                this.quantization.DeltaQDc[(int)Av1Plane.Y],
                this.quantization.DeltaQAc[(int)Av1Plane.Y],
                this.bitDepth,
                ref candidateState);

            int rate = Av1TileWriter.GetLumaModeCost(
                writer,
                macroBlock,
                blockSize,
                Av1PredictionMode.DC,
                0);

            rate += transformSizeRate;
            rate += paletteDisabledCost;
            rate += writer.GetFilterIntraModeCost(filterIntraMode, blockSize);
            rate += writer.GetCoefficientCost(
                transformSize,
                transformType,
                Av1PredictionMode.DC,
                candidateCoefficients,
                Av1ComponentType.Luminance,
                blockContext,
                candidateState.EndOfBlock,
                this.picture.Parent.FrameHeader.UseReducedTransformSet,
                filterIntraMode,
                usesInterTransformSet: false);

            return Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion);
        }

        private static void CopyCandidate(
            ReadOnlySpan<TSample> candidateReconstruction,
            ReadOnlySpan<int> candidateCoefficients,
            Buffer2DRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            Span<int> retainedCoefficients,
            Av1TransformSize transformSize,
            Av1EncoderTransformBlockState candidateState,
            ref Av1EncoderTransformBlockState retainedState)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            candidateCoefficients[..transformSize.GetSize2d()].CopyTo(retainedCoefficients);
            for (int row = 0; row < height; row++)
            {
                candidateReconstruction.Slice(row * width, width)
                    .CopyTo(reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y + row).Slice(blockOrigin.X, width));
            }

            retainedState = candidateState;
        }

        private static void CopySplitCandidate(
            ReadOnlySpan<TSample> candidateReconstruction,
            ReadOnlySpan<int> candidateCoefficients,
            ReadOnlySpan<Av1EncoderTransformBlockState> candidateTransformBlocks,
            Buffer2DRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            Span<int> retainedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedTransformBlocks)
        {
            const int BlockWidth = 8;
            const int SampleCount = BlockWidth * BlockWidth;
            candidateCoefficients[..SampleCount].CopyTo(retainedCoefficients);
            candidateTransformBlocks[..Av1EncoderModeDecisionWorkspace<TSample>.CandidateTransformBlockCount]
                .CopyTo(retainedTransformBlocks);

            for (int row = 0; row < BlockWidth; row++)
            {
                candidateReconstruction.Slice(row * BlockWidth, BlockWidth)
                    .CopyTo(
                        reconstructionPlane
                            .DangerousGetRowSpan(blockOrigin.Y + row)
                            .Slice(blockOrigin.X, BlockWidth));
            }
        }
    }
}
