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
        public ModeDecision(
            Av1EncoderFrame<TSample> source,
            Av1EncoderFrame<TSample> reconstruction,
            Av1PictureControlSet picture,
            Av1Superblock superblock,
            Av1EncoderCoefficientBuffer coefficientBuffer,
            Av1EncoderBlockWorkspace blockWorkspace)
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
            ref Av1EncoderBlockStruct block)
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

            ref Av1EncoderTransformBlockState lumaState = ref lumaTransformBlocks[lumaTransformIndex];
            modeInfo.Block.Mode = this.SelectLumaMode(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                lumaCoefficients[this.codedAreaLuma..],
                ref lumaState,
                out int lumaAngleDelta,
                out Av1FilterIntraMode filterIntraMode);

            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = (sbyte)lumaAngleDelta;
            block.FilterIntraMode = filterIntraMode;

            this.codedAreaLuma += LumaTransformSize.GetSize2d();
            bool lumaTransformEmpty = lumaState.EndOfBlock == 0;
            if (this.source.IsMonochrome)
            {
                modeInfo.Block.Skip = lumaTransformEmpty &&
                    Av1TileWriter.ShouldSkipCoefficients(
                        writer,
                        Av1TileWriter.GetSkipContext(macroBlock),
                        this.GetEmptyTransformRate(
                            writer,
                            this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                            Av1ComponentType.Luminance,
                            blockOrigin,
                            BlockSize,
                            LumaTransformSize,
                            lumaState.TransformType,
                            modeInfo.Block.Mode,
                            block.FilterIntraMode));

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
                out int chromaAngleDelta,
                out byte chromaFromLumaIndex,
                out sbyte chromaFromLumaSigns);

            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = (sbyte)chromaAngleDelta;
            block.PredictionUnit.ChromaFromLumaIndex = chromaFromLumaIndex;
            block.PredictionUnit.ChromaFromLumaSigns = chromaFromLumaSigns;

            bool allTransformsEmpty = lumaTransformEmpty && blueState.EndOfBlock == 0 && redState.EndOfBlock == 0;
            if (allTransformsEmpty)
            {
                Av1BlockSize chromaBlockSize = BlockSize.GetSubsampled(
                    colorConfig.SubSamplingX,
                    colorConfig.SubSamplingY);

                int emptyTransformRate = this.GetEmptyTransformRate(
                    writer,
                    this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                    Av1ComponentType.Luminance,
                    blockOrigin,
                    BlockSize,
                    LumaTransformSize,
                    lumaState.TransformType,
                    modeInfo.Block.Mode,
                    block.FilterIntraMode);

                emptyTransformRate += this.GetEmptyTransformRate(
                    writer,
                    this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                    Av1ComponentType.Chroma,
                    chromaOrigin,
                    chromaBlockSize,
                    chromaTransformSize,
                    blueState.TransformType,
                    modeInfo.Block.Mode,
                    Av1FilterIntraMode.AllFilterIntraModes);

                emptyTransformRate += this.GetEmptyTransformRate(
                    writer,
                    this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex],
                    Av1ComponentType.Chroma,
                    chromaOrigin,
                    chromaBlockSize,
                    chromaTransformSize,
                    redState.TransformType,
                    modeInfo.Block.Mode,
                    Av1FilterIntraMode.AllFilterIntraModes);

                modeInfo.Block.Skip = Av1TileWriter.ShouldSkipCoefficients(
                    writer,
                    Av1TileWriter.GetSkipContext(macroBlock),
                    emptyTransformRate);
            }

            this.codedAreaChroma += chromaTransformSize.GetSize2d();
        }

        private int GetEmptyTransformRate(
            Av1SymbolEncoder writer,
            Av1NeighborArrayUnit<byte> coefficientNeighbors,
            Av1ComponentType componentType,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1TransformSize transformSize,
            Av1TransformType transformType,
            Av1PredictionMode lumaMode,
            Av1FilterIntraMode filterIntraMode)
        {
            Av1TransformBlockContext blockContext = Av1TileWriter.GetTransformBlockContexts(
                componentType,
                coefficientNeighbors,
                blockOrigin,
                blockSize,
                transformSize);

            return writer.GetCoefficientCost(
                transformSize,
                transformType,
                lumaMode,
                ReadOnlySpan<int>.Empty,
                componentType,
                blockContext,
                0,
                this.picture.Parent.FrameHeader.UseReducedTransformSet,
                filterIntraMode);
        }

        private Av1PredictionMode SelectLumaMode(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Span<int> retainedCoefficients,
            ref Av1EncoderTransformBlockState retainedState,
            out int selectedAngleDelta,
            out Av1FilterIntraMode selectedFilterIntraMode)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const Av1TransformSize TransformSize = Av1TransformSize.Size8x8;
            const int SampleCount = 8 * 8;
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

            Span<TSample> aboveStorage = stackalloc TSample[17];
            Span<TSample> above = aboveStorage[1..];
            Span<TSample> leftStorage = stackalloc TSample[17];
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

            Span<TSample> candidateReconstruction = stackalloc TSample[SampleCount];
            Span<int> candidateCoefficients = stackalloc int[SampleCount];
            long bestCost = long.MaxValue;
            Av1PredictionMode bestMode = Av1PredictionMode.DC;
            selectedAngleDelta = 0;
            selectedFilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            int baseModeCount = LumaModeSearchOrder.Length;
            int deltaCount = AngleDeltaSearchOrder.Length;
            int directionalModeCount = (int)Av1PredictionMode.Directional67Degrees - (int)Av1PredictionMode.Vertical + 1;
            int candidateCount = baseModeCount + (directionalModeCount * deltaCount);
            bool useReducedTransformSet = this.picture.Parent.FrameHeader.UseReducedTransformSet;
            Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(
                TransformSize,
                useReducedTransformSet);

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

                Av1TransformType defaultTransformType = Av1SymbolContextHelper.GetDefaultIntraTransformType(
                    mode,
                    TransformSize,
                    useReducedTransformSet);

                Av1EncoderTransformBlockState candidateState = default;
                long candidateCost = this.GetLumaCandidateCost(
                    writer,
                    macroBlock,
                    sourcePlane,
                    blockOrigin,
                    above,
                    left,
                    hasLeft,
                    hasAbove,
                    mode,
                    angleDelta,
                    defaultTransformType,
                    blockContext,
                    candidateReconstruction,
                    candidateCoefficients,
                    ref candidateState);

                if (candidateCost < bestCost)
                {
                    CopyCandidate(
                        candidateReconstruction,
                        candidateCoefficients,
                        reconstructionPlane,
                        blockOrigin,
                        retainedCoefficients,
                        TransformSize,
                        candidateState,
                        ref retainedState);

                    bestCost = candidateCost;
                    bestMode = mode;
                    selectedAngleDelta = angleDelta;
                }
            }

            // Mode selection uses the mode-derived default transform, then the winning mode alone pays for
            // an exhaustive transform refinement. This preserves reference tie order without a 61-by-7 search.
            long bestTransformCost = long.MaxValue;
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
                    above,
                    left,
                    hasLeft,
                    hasAbove,
                    bestMode,
                    selectedAngleDelta,
                    transformType,
                    blockContext,
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
                        ref retainedState);

                    bestTransformCost = candidateCost;
                }
            }

            if (this.picture.Sequence.SequenceHeader.EnableFilterIntra)
            {
                Span<TSample> filterPrediction = stackalloc TSample[SampleCount];
                Span<short> filterResidual = stackalloc short[SampleCount];

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
                        filterPrediction,
                        above,
                        left,
                        filterResidual,
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
                            filterPrediction,
                            filterResidual,
                            filterIntraMode,
                            transformType,
                            blockContext,
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
                                ref retainedState);

                            bestTransformCost = candidateCost;
                            bestMode = Av1PredictionMode.DC;
                            selectedAngleDelta = 0;
                            selectedFilterIntraMode = filterIntraMode;
                        }
                    }
                }
            }

            return bestMode;
        }

        private long GetLumaCandidateCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Buffer2DRegion<TSample> sourcePlane,
            Point blockOrigin,
            ReadOnlySpan<TSample> above,
            ReadOnlySpan<TSample> left,
            bool hasLeft,
            bool hasAbove,
            Av1PredictionMode mode,
            int angleDelta,
            Av1TransformType transformType,
            Av1TransformBlockContext blockContext,
            Span<TSample> candidateReconstruction,
            Span<int> candidateCoefficients,
            ref Av1EncoderTransformBlockState candidateState)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const Av1TransformSize TransformSize = Av1TransformSize.Size8x8;
            long distortion = TOperator.EncodeCandidate(
                this.blockWorkspace,
                sourcePlane,
                blockOrigin,
                candidateReconstruction,
                above,
                left,
                hasLeft,
                hasAbove,
                mode,
                angleDelta,
                candidateCoefficients,
                TransformSize,
                transformType,
                Av1Plane.Y,
                this.quantization.QIndex[0],
                this.quantization.DeltaQDc[(int)Av1Plane.Y],
                this.quantization.DeltaQAc[(int)Av1Plane.Y],
                this.bitDepth,
                ref candidateState);

            int rate = Av1TileWriter.GetLumaModeCost(writer, macroBlock, BlockSize, mode, angleDelta);
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
                Av1FilterIntraMode.AllFilterIntraModes);

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
                filterIntraMode);

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
    }
}
