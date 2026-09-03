// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const Av1TransformSize LumaTransformSize = Av1TransformSize.Size8x8;
            int qIndex = this.quantization.QIndex[0];
            modeInfo.Block = new Av1EncoderBlockModeInfo
            {
                BlockSize = BlockSize,
                PartitionType = Av1PartitionType.None,
                SegmentId = 0,
                TransformSize = LumaTransformSize,
                Mode = Av1PredictionMode.DC,
                UvMode = Av1ChromaPredictionMode.DC
            };

            modeInfo.CdefStrength = 0;
            block.HasChroma = !this.source.IsMonochrome;
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

            // One 8x8 coding block retains either one 8x8 transform or four 4x4 transforms. The block-level
            // skip decision is legal only when every transform selected by mode decision has an empty EOB.
            int lumaTransformBlockCount = LumaTransformSize.GetSize2d() / lumaTransformSize.GetSize2d();
            bool lumaTransformEmpty = true;
            for (int transformIndex = 0; transformIndex < lumaTransformBlockCount; transformIndex++)
            {
                lumaTransformEmpty &= retainedLumaStates[transformIndex].EndOfBlock == 0;
            }

            if (this.source.IsMonochrome)
            {
                int emptyTransformRate = lumaTransformEmpty
                    ? this.GetEmptyTransformRate(
                        writer,
                        this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                        Av1ComponentType.Luminance,
                        blockOrigin,
                        BlockSize,
                        lumaTransformSize,
                        modeInfo.Block.Mode,
                        block.FilterIntraMode)
                    : 0;

                modeInfo.Block.Skip = lumaTransformEmpty &&
                    Av1TileWriter.ShouldSkipCoefficients(
                        writer,
                        Av1TileWriter.GetSkipContext(macroBlock),
                        emptyTransformRate);

                if (this.picture.Parent.FrameHeader.AllowIntraBlockCopy)
                {
                    this.SelectIntraBlockCopy(
                        writer,
                        macroBlock,
                        blockOrigin,
                        tileIndex,
                        lumaCost,
                        emptyTransformRate,
                        ref modeInfo,
                        ref block,
                        ref paletteInfo);
                }

                this.codedAreaLuma += LumaTransformSize.GetSize2d();
                return;
            }

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            Point chromaOrigin = new(blockOrigin.X >> subsamplingX, blockOrigin.Y >> subsamplingY);
            Av1TransformSize chromaTransformSize = BlockSize.GetMaxUvTransformSize(
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

            ref Av1EncoderTransformBlockState blueState = ref blueTransformBlocks[chromaTransformIndex];
            ref Av1EncoderTransformBlockState redState = ref redTransformBlocks[chromaTransformIndex];
            modeInfo.Block.UvMode = this.SelectChromaMode(
                writer,
                macroBlock,
                modeInfo,
                blockOrigin,
                chromaOrigin,
                tileIndex,
                modeInfo.Block.Mode,
                chromaTransformSize,
                blueCoefficients[this.codedAreaChroma..],
                redCoefficients[this.codedAreaChroma..],
                ref blueState,
                ref redState,
                ref paletteInfo,
                out int chromaAngleDelta,
                out byte chromaFromLumaIndex,
                out sbyte chromaFromLumaSigns,
                out long chromaCost);

            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = (sbyte)chromaAngleDelta;
            block.PredictionUnit.ChromaFromLumaIndex = chromaFromLumaIndex;
            block.PredictionUnit.ChromaFromLumaSigns = chromaFromLumaSigns;

            // Skip suppresses coefficient syntax for the entire coding block, not one plane independently.
            // Preserve normal coefficient coding when any selected luma or chroma transform is nonempty.
            bool allTransformsEmpty = lumaTransformEmpty && blueState.EndOfBlock == 0 && redState.EndOfBlock == 0;
            int regularEmptyTransformRate = 0;
            if (allTransformsEmpty)
            {
                Av1BlockSize chromaBlockSize = BlockSize.GetSubsampled(
                    colorConfig.SubSamplingX,
                    colorConfig.SubSamplingY);

                regularEmptyTransformRate = this.GetEmptyTransformRate(
                    writer,
                    this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                    Av1ComponentType.Luminance,
                    blockOrigin,
                    BlockSize,
                    lumaTransformSize,
                    modeInfo.Block.Mode,
                    block.FilterIntraMode);

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

                modeInfo.Block.Skip = Av1TileWriter.ShouldSkipCoefficients(
                    writer,
                    Av1TileWriter.GetSkipContext(macroBlock),
                    regularEmptyTransformRate);
            }

            if (this.picture.Parent.FrameHeader.AllowIntraBlockCopy)
            {
                this.SelectIntraBlockCopy(
                    writer,
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    lumaCost + chromaCost,
                    regularEmptyTransformRate,
                    ref modeInfo,
                    ref block,
                    ref paletteInfo);
            }

            this.codedAreaLuma += LumaTransformSize.GetSize2d();
            this.codedAreaChroma += chromaTransformSize.GetSize2d();
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
            ushort tileIndex,
            Span<int> retainedCoefficients,
            Span<Av1EncoderTransformBlockState> retainedStates,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out Av1FilterIntraMode selectedFilterIntraMode,
            out Av1TransformSize selectedTransformSize,
            out long selectedCost)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const Av1TransformSize TransformSize = Av1TransformSize.Size8x8;
            Av1EncoderModeDecisionWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>();

            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Buffer2DRegion<TSample> reconstructionPlane = this.reconstruction.GetPlane(Av1Plane.Y);
            bool hasLeft = macroBlock.IsLeftAvailable;
            bool hasAbove = macroBlock.IsUpAvailable;
            int modeInfoRow = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            bool rightAvailable = modeInfoColumn + TransformSize.Get4x4WideCount() < macroBlock.Tile.ModeInfoColumnEnd;
            bool bottomAvailable = modeInfoRow + TransformSize.Get4x4HighCount() < macroBlock.Tile.ModeInfoRowEnd;
            bool hasTopRight = Av1IntraReferenceAvailability.HasTopRight(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                BlockSize,
                modeInfoRow,
                modeInfoColumn,
                hasAbove,
                rightAvailable,
                Av1PartitionType.None,
                TransformSize,
                0,
                0,
                0,
                0);

            bool hasBottomLeft = Av1IntraReferenceAvailability.HasBottomLeft(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                BlockSize,
                modeInfoRow,
                modeInfoColumn,
                bottomAvailable,
                hasLeft,
                Av1PartitionType.None,
                TransformSize,
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
                reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1).Slice(blockOrigin.X, 8).CopyTo(above[..8]);
            }

            if (hasLeft)
            {
                for (int row = 0; row < 8; row++)
                {
                    left[row] = reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y + row)[blockOrigin.X - 1];
                }
            }

            int midpoint = 128 << (this.bitDepth.GetBitCount() - 8);

            // A missing edge repeats the closest perpendicular sample. Only a block with neither edge
            // available uses the asymmetric midpoint offsets that distinguish top from left.
            if (!hasAbove)
            {
                above[..8].Fill(hasLeft ? left[0] : TOperator.CreateSample(midpoint - 1));
            }

            if (!hasLeft)
            {
                left[..8].Fill(hasAbove ? above[0] : TOperator.CreateSample(midpoint + 1));
            }

            if (hasTopRight)
            {
                reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1).Slice(blockOrigin.X + 8, 8).CopyTo(above[8..]);
            }
            else
            {
                above[8..].Fill(above[7]);
            }

            if (hasBottomLeft)
            {
                for (int row = 8; row < 16; row++)
                {
                    left[row] = reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y + row)[blockOrigin.X - 1];
                }
            }
            else
            {
                left[8..].Fill(left[7]);
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
                BlockSize,
                TransformSize);

            int transformSizeContext = Av1TileWriter.GetTransformSizeContext(
                this.picture.TransformFunctionContexts[tileIndex],
                macroBlock,
                blockOrigin,
                BlockSize);

            int largestTransformRate = this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select
                ? writer.GetTransformSizeCost(BlockSize, TransformSize, transformSizeContext)
                : 0;

            int paletteDisabledCost = 0;
            if (this.picture.Parent.FrameHeader.AllowScreenContentTools)
            {
                Av1NeighborArrayUnit<Av1EncoderPaletteInfo> paletteContexts = this.picture.PaletteContexts[tileIndex];
                int blockSizeContext = Av1TileWriter.GetPaletteBlockSizeContext(BlockSize);
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
            selectedTransformSize = TransformSize;
            int baseModeCount = LumaModeSearchOrder.Length;
            int deltaCount = AngleDeltaSearchOrder.Length;
            int directionalModeCount = (int)Av1PredictionMode.Directional67Degrees - (int)Av1PredictionMode.Vertical + 1;

            // Effort zero evaluates DC only, effort one adds every zero-angle mode, and higher levels add all directional adjustments.
            int candidateCount = this.effort switch
            {
                0 => 1,
                1 => baseModeCount,
                _ => baseModeCount + (directionalModeCount * deltaCount)
            };

            bool useReducedTransformSet = this.picture.Parent.FrameHeader.UseReducedTransformSet;
            Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(
                TransformSize,
                useReducedTransformSet);

            // Transform type and transform size are separate search axes. Splitting their effort thresholds
            // gives callers a useful intermediate tier without changing the fast default path.
            bool searchEveryTransformType = this.effort >= 7;
            bool searchEveryTransformSize = this.effort >= 8;

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
                    TransformSize,
                    this.bitDepth);

                // Transform types are visited in AV1 enumeration order. A strict cost comparison below keeps
                // the first legal type on ties, while lower efforts visit only the mode-derived default.
                Av1TransformType firstTransformType = searchEveryTransformType
                    ? Av1TransformType.DctDct
                    : Av1SymbolContextHelper.GetDefaultIntraTransformType(
                        mode,
                        TransformSize,
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
                            TransformSize,
                            candidateState,
                            ref retainedStates[0]);

                        bestCost = candidateCost;
                        bestMode = mode;
                        selectedAngleDelta = angleDelta;
                        selectedTransformSize = TransformSize;
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
            if (this.effort >= 3 && !searchEveryTransformType)
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
                    TransformSize,
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
                            TransformSize,
                            candidateState,
                            ref retainedStates[0]);

                        bestTransformCost = candidateCost;
                    }
                }
            }

            if (this.effort >= 4 && this.picture.Sequence.SequenceHeader.EnableFilterIntra)
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
                        TransformSize,
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
                        long candidateCost = this.GetFilterIntraCandidateCost(
                            writer,
                            macroBlock,
                            sourcePlane,
                            blockOrigin,
                            prediction,
                            residual,
                            filterIntraMode,
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
                                TransformSize,
                                candidateState,
                                ref retainedStates[0]);

                            bestTransformCost = candidateCost;
                            bestMode = Av1PredictionMode.DC;
                            selectedAngleDelta = 0;
                            selectedFilterIntraMode = filterIntraMode;
                            selectedTransformSize = TransformSize;
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

            Span<TSample> transformSamples = workspace.GetCandidateReconstruction(1);
            Span<TSample> prediction = transformSamples[..TransformSampleCount];
            Span<TSample> transformReconstruction = transformSamples.Slice(
                TransformSampleCount,
                TransformSampleCount);

            Span<int> transformCoefficients = workspace.GetCandidateCoefficients(1)[..TransformSampleCount];
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
                        this.PrepareSplitLumaReferenceSamples(
                            reconstructionPlane,
                            blockOrigin,
                            macroBlock,
                            transformRow,
                            transformColumn,
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
                            transformReconstruction,
                            TransformWidth,
                            transformCoefficients,
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
                            transformCoefficients,
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
                            // Preserve the improving 4x4 trial in the block mosaic. Later transform predictions
                            // consume that reconstruction, and copying the compact result avoids another transform.
                            transformCoefficients.CopyTo(retainedTransformCoefficients);
                            for (int row = 0; row < TransformWidth; row++)
                            {
                                transformReconstruction.Slice(row * TransformWidth, TransformWidth)
                                    .CopyTo(
                                        candidateReconstruction.Slice(
                                            reconstructionOffset + (row * BlockWidth),
                                            TransformWidth));
                            }

                            bestTransformCost = candidateCost;
                            bestTransformType = transformType;
                            bestTransformRate = candidateRate;
                            bestTransformDistortion = candidateDistortion;
                            bestTransformState = candidateState;
                        }
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

        private void PrepareSplitLumaReferenceSamples(
            Buffer2DRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            Av1MacroBlockD macroBlock,
            int transformRow,
            int transformColumn,
            ReadOnlySpan<TSample> candidateReconstruction,
            Span<TSample> aboveStorage,
            Span<TSample> leftStorage,
            out bool hasLeft,
            out bool hasAbove)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const Av1TransformSize TransformSize = Av1TransformSize.Size4x4;
            const int BlockWidth = 8;
            const int TransformWidth = 4;
            int rowOffset = transformRow * TransformWidth;
            int columnOffset = transformColumn * TransformWidth;
            int modeInfoRow = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;

            // Internal top and left edges come from the candidate mosaic built in raster order. Edges outside
            // the 8x8 candidate continue to read committed reconstruction, keeping unsuccessful trials isolated.
            hasAbove = transformRow > 0 || macroBlock.IsUpAvailable;
            hasLeft = transformColumn > 0 || macroBlock.IsLeftAvailable;
            bool rightAvailable =
                modeInfoColumn + transformColumn + TransformSize.Get4x4WideCount() <
                macroBlock.Tile.ModeInfoColumnEnd;

            bool bottomAvailable =
                modeInfoRow + transformRow + TransformSize.Get4x4HighCount() <
                macroBlock.Tile.ModeInfoRowEnd;

            bool hasTopRight = Av1IntraReferenceAvailability.HasTopRight(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                BlockSize,
                modeInfoRow,
                modeInfoColumn,
                hasAbove,
                rightAvailable,
                Av1PartitionType.None,
                TransformSize,
                transformRow,
                transformColumn,
                0,
                0);

            bool hasBottomLeft = Av1IntraReferenceAvailability.HasBottomLeft(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                BlockSize,
                modeInfoRow,
                modeInfoColumn,
                bottomAvailable,
                hasLeft,
                Av1PartitionType.None,
                TransformSize,
                transformRow,
                transformColumn,
                0,
                0);

            Span<TSample> above = aboveStorage.Slice(1, TransformWidth * 2);
            Span<TSample> left = leftStorage.Slice(1, TransformWidth * 2);
            if (hasAbove)
            {
                if (transformRow > 0)
                {
                    candidateReconstruction
                        .Slice(((rowOffset - 1) * BlockWidth) + columnOffset, TransformWidth)
                        .CopyTo(above);
                }
                else
                {
                    reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1)
                        .Slice(blockOrigin.X + columnOffset, TransformWidth)
                        .CopyTo(above);
                }
            }

            if (hasLeft)
            {
                if (transformColumn > 0)
                {
                    for (int row = 0; row < TransformWidth; row++)
                    {
                        left[row] = candidateReconstruction[((rowOffset + row) * BlockWidth) + columnOffset - 1];
                    }
                }
                else
                {
                    for (int row = 0; row < TransformWidth; row++)
                    {
                        left[row] = reconstructionPlane
                            .DangerousGetRowSpan(blockOrigin.Y + rowOffset + row)[blockOrigin.X - 1];
                    }
                }
            }

            int midpoint = 128 << (this.bitDepth.GetBitCount() - 8);
            if (!hasAbove)
            {
                above[..TransformWidth].Fill(hasLeft ? left[0] : TOperator.CreateSample(midpoint - 1));
            }

            if (!hasLeft)
            {
                left[..TransformWidth].Fill(hasAbove ? above[0] : TOperator.CreateSample(midpoint + 1));
            }

            if (hasTopRight)
            {
                if (transformRow > 0)
                {
                    candidateReconstruction
                        .Slice(
                            ((rowOffset - 1) * BlockWidth) + columnOffset + TransformWidth,
                            TransformWidth)
                        .CopyTo(above[TransformWidth..]);
                }
                else
                {
                    reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1)
                        .Slice(blockOrigin.X + columnOffset + TransformWidth, TransformWidth)
                        .CopyTo(above[TransformWidth..]);
                }
            }
            else
            {
                above[TransformWidth..].Fill(above[TransformWidth - 1]);
            }

            if (hasBottomLeft)
            {
                for (int row = TransformWidth; row < TransformWidth * 2; row++)
                {
                    left[row] = reconstructionPlane
                        .DangerousGetRowSpan(blockOrigin.Y + rowOffset + row)[blockOrigin.X - 1];
                }
            }
            else
            {
                left[TransformWidth..].Fill(left[TransformWidth - 1]);
            }

            // Only an interior transform corner belongs to decision scratch. Boundary corners continue
            // to read the already reconstructed neighboring block so candidate trials remain isolated.
            TSample corner = hasAbove && hasLeft
                ? transformRow > 0 && transformColumn > 0
                    ? candidateReconstruction[((rowOffset - 1) * BlockWidth) + columnOffset - 1]
                    : reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y + rowOffset - 1)[
                        blockOrigin.X + columnOffset - 1]
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
            Av1TransformType transformType,
            Av1TransformBlockContext blockContext,
            int paletteDisabledCost,
            int transformSizeRate,
            Span<TSample> candidateReconstruction,
            Span<int> candidateCoefficients,
            ref Av1EncoderTransformBlockState candidateState)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const Av1TransformSize TransformSize = Av1TransformSize.Size8x8;

            // Prediction and subtraction were prepared by the owning mode loop. This stage performs only
            // transform, quantization, reconstruction, and distortion for the requested transform type.
            long distortion = TOperator.EncodePredictionCandidate(
                this.blockWorkspace,
                sourcePlane,
                blockOrigin,
                prediction,
                residual,
                candidateReconstruction,
                TransformSize.GetWidth(),
                candidateCoefficients,
                TransformSize,
                transformType,
                Av1Plane.Y,
                this.quantization.QIndex[0],
                this.quantization.DeltaQDc[(int)Av1Plane.Y],
                this.quantization.DeltaQAc[(int)Av1Plane.Y],
                this.bitDepth,
                ref candidateState);

            // Charge every block-level choice that distinguishes this spatial candidate before adding
            // coefficient syntax derived from the live neighboring-transform context.
            int rate = Av1TileWriter.GetLumaModeCost(writer, macroBlock, BlockSize, mode, angleDelta);
            rate += transformSizeRate;
            if (mode == Av1PredictionMode.DC)
            {
                rate += paletteDisabledCost;
            }

            if (mode == Av1PredictionMode.DC && this.picture.Sequence.SequenceHeader.EnableFilterIntra)
            {
                rate += writer.GetFilterIntraModeCost(Av1FilterIntraMode.AllFilterIntraModes, BlockSize);
            }

            rate += writer.GetCoefficientCost(
                TransformSize,
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
            Av1TransformType transformType,
            Av1TransformBlockContext blockContext,
            int paletteDisabledCost,
            int transformSizeRate,
            Span<TSample> candidateReconstruction,
            Span<int> candidateCoefficients,
            ref Av1EncoderTransformBlockState candidateState)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const Av1TransformSize TransformSize = Av1TransformSize.Size8x8;
            long distortion = TOperator.EncodePredictionCandidate(
                this.blockWorkspace,
                sourcePlane,
                blockOrigin,
                prediction,
                residual,
                candidateReconstruction,
                TransformSize.GetWidth(),
                candidateCoefficients,
                TransformSize,
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
                BlockSize,
                Av1PredictionMode.DC,
                0);

            rate += transformSizeRate;
            rate += paletteDisabledCost;
            rate += writer.GetFilterIntraModeCost(filterIntraMode, BlockSize);
            rate += writer.GetCoefficientCost(
                TransformSize,
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
