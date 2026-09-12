// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
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
    /// Gets the nonzero directional adjustments in the order required for neighboring-cost pruning.
    /// </summary>
    private static ReadOnlySpan<sbyte> PrunedAngleDeltaSearchOrder => [-2, 2, -3, -1, 1, 3];

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
        Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
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
        private readonly Av1EncoderFrame<TSample>.PlanarView reference;
        private readonly Av1EncoderFrame<TSample>.PlanarView goldenReference;
        private readonly bool hasDistinctGoldenReference;
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
        private int replayNodeIndex;
        private Av1PartitionType replayPartition;
        private Point replayPartitionOrigin;
        private Av1BlockSize replayParentSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModeDecision{TSample, TOperator}"/> struct.
        /// </summary>
        /// <param name="source">The coded source frame.</param>
        /// <param name="reference">The reconstructed inter reference, or the current reconstruction for an intra frame.</param>
        /// <param name="goldenReference">The retained long-term reference, or the current reconstruction for an intra frame.</param>
        /// <param name="hasDistinctGoldenReference">Whether GOLDEN differs from LAST and is available for compound prediction.</param>
        /// <param name="reconstruction">The reconstructed frame updated by winning candidates.</param>
        /// <param name="picture">The frame coding and mode-information state.</param>
        /// <param name="superblock">The current superblock.</param>
        /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
        /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
        /// <param name="effort">The mode-search effort in the inclusive range zero through ten.</param>
        public ModeDecision(
            Av1EncoderFrame<TSample> source,
            Av1EncoderFrame<TSample> reference,
            Av1EncoderFrame<TSample> goldenReference,
            bool hasDistinctGoldenReference,
            Av1EncoderFrame<TSample> reconstruction,
            Av1PictureControlSet picture,
            Av1Superblock superblock,
            Av1EncoderCoefficientBuffer coefficientBuffer,
            Av1EncoderBlockWorkspace blockWorkspace,
            int effort)
        {
            this.source = source.CodedView;
            this.reference = reference.CodedView;
            this.goldenReference = goldenReference.CodedView;
            this.hasDistinctGoldenReference = hasDistinctGoldenReference;
            this.reconstruction = reconstruction.CodedView;
            this.picture = picture;
            this.superblock = superblock;
            this.coefficientBuffer = coefficientBuffer;
            this.blockWorkspace = blockWorkspace;
            this.quantization = picture.Parent.FrameHeader.QuantizationParameters;
            this.bitDepth = picture.Sequence.SequenceHeader.ColorConfig.BitDepth;
            this.rateMultiplier = picture.Parent.FrameHeader.IsIntra
                ? Av1RateDistortion.GetKeyFrameRateMultiplier(this.quantization.QIndex[0], this.bitDepth)
                : Av1RateDistortion.GetInterFrameRateMultiplier(this.quantization.QIndex[0], this.bitDepth);

            if (picture.Sequence.SequenceHeader.IsStillPicture)
            {
                // Measure 4x4 source variation once for the entire superblock. Mixed flat and detailed
                // regions need a lower rate weight, shared by every partition and mode decision below it.
                int superblockSize = 1 << picture.Sequence.SequenceHeader.SuperblockSizeLog2;
                int originX = (superblock.Index % coefficientBuffer.SuperblockColumnCount) * superblockSize;
                int originY = (superblock.Index / coefficientBuffer.SuperblockColumnCount) * superblockSize;
                int right = Math.Min(originX + superblockSize, picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2);
                int bottom = Math.Min(originY + superblockSize, picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);
                Buffer2DRegion<TSample> luma = this.source.GetPlane(Av1Plane.Y);
                InlineArray4<TSample> zero = default;
                int sampleShift = (int)this.bitDepth * 2;
                int squareShift = sampleShift * 2;
                int minimumVariance = int.MaxValue;
                int maximumVariance = 0;
                for (int y = originY; y < bottom; y += 4)
                {
                    for (int x = originX; x < right; x += 4)
                    {
                        TOperator.GetMoments(
                            Av1TransformBlockEncoder.GetPlaneSpan(luma, new Point(x, y)),
                            luma.Stride,
                            zero,
                            0,
                            4,
                            4,
                            out int sum,
                            out long squares);

                        // Normalize the two moments independently to the 8-bit domain before subtracting
                        // the squared mean. Edge blocks include samples padded to the mode-info boundary.
                        sum = (sum + ((1 << sampleShift) >> 1)) >> sampleShift;
                        squares = (squares + ((1L << squareShift) >> 1)) >> squareShift;
                        int variance = (int)Math.Max(0, squares - (((long)sum * sum) >> 4));
                        minimumVariance = Math.Min(minimumVariance, variance);
                        maximumVariance = Math.Max(maximumVariance, variance);
                    }
                }

                double minimumLogVariance = double.LogP1(minimumVariance / 16D);
                double maximumLogVariance = double.LogP1(maximumVariance / 16D);
                int modifier = 128;
                if (minimumLogVariance < 2 && maximumLogVariance > 4)
                {
                    double range = maximumLogVariance - minimumLogVariance;
                    modifier -= range > 8 ? 48 : (int)(range * 6);
                }

                this.rateMultiplier = Math.Max(1, (this.rateMultiplier * modifier) >> 7);
            }

            this.effort = effort;
            this.codedAreaLuma = 0;
            this.codedAreaChroma = 0;
            this.SelectedBlockStatistics = default;
            this.replayNodeIndex = -1;
            this.replayPartition = Av1PartitionType.Invalid;
            this.replayPartitionOrigin = default;
            this.replayParentSize = Av1BlockSize.Invalid;
            if (effort >= 9)
            {
                int side = 1 << picture.Sequence.SequenceHeader.SuperblockSizeLog2;
                int x = (superblock.Index % coefficientBuffer.SuperblockColumnCount) * side;
                int y = (superblock.Index / coefficientBuffer.SuperblockColumnCount) * side;
                int width = Math.Min(side, (picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) - x);
                int height = Math.Min(side, (picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2) - y);
                blockWorkspace.PartitionTree.Reset(
                    picture.Sequence.SequenceHeader,
                    width,
                    height,
                    picture.Parent.FrameHeader.AllowScreenContentTools);
            }
        }

        /// <inheritdoc/>
        public static bool UsesRetainedDecisions => false;

        /// <summary>
        /// Gets the statistics of the most recently encoded block.
        /// </summary>
        public Av1RateDistortionStatistics SelectedBlockStatistics { get; private set; }

        /// <inheritdoc/>
        public Av1PartitionType SelectPartition(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            Av1PartitionType preparedPartition)
        {
            this.replayNodeIndex = -1;
            // Live decisions change the number of nodes visited before this position. The original flat
            // skeleton's index no longer identifies this block, so derive its default from current geometry.
            // Otherwise an earlier unsplit 16x16 can make a later 32x32 consume an old 8x8 NONE entry.
            preparedPartition = blockSize == Av1BlockSize.Block8x8 ? Av1PartitionType.None : Av1PartitionType.Split;

            int nodeIndex = 0;
            int nodeWidth = this.picture.Sequence.SequenceHeader.SuperblockSize.GetWidth();
            int localX = blockOrigin.X & (nodeWidth - 1);
            int localY = blockOrigin.Y & (nodeWidth - 1);
            while (nodeWidth > blockSize.GetWidth())
            {
                nodeWidth >>= 1;
                int childIndex = (localX >= nodeWidth ? 1 : 0) | (localY >= nodeWidth ? 2 : 0);
                nodeIndex = (nodeIndex * 4) + childIndex + 1;
                localX &= nodeWidth - 1;
                localY &= nodeWidth - 1;
            }

            if (this.picture.Parent.SpeedSettings.UseVarianceBasedPartition)
            {
                if (this.superblock.Workspace.PartitionSearchTypes[0] == (byte)Av1PartitionType.Invalid)
                {
                    this.PrepareVariancePartitions(macroBlock, blockOrigin);
                }

                Av1PartitionType variancePartition = (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[nodeIndex];
                this.PreparePartitionGeometry(blockOrigin, blockSize, variancePartition);
                return variancePartition;
            }

            bool searchPartition = blockSize is Av1BlockSize.Block8x8 or Av1BlockSize.Block16x16 ||
                (this.effort == 10 &&
                    blockSize is Av1BlockSize.Block32x32 or Av1BlockSize.Block64x64 or Av1BlockSize.Block128x128);

            // Inter prediction currently retains one transform per plane. A 128x128 parent requires four
            // 64x64 transform regions, so keep its prepared split until tiled inter transforms are available.
            if (!this.picture.Parent.FrameHeader.IsIntra && blockSize == Av1BlockSize.Block128x128)
            {
                return preparedPartition;
            }

            if (!searchPartition)
            {
                return preparedPartition;
            }

            if (this.effort < 9)
            {
                return preparedPartition;
            }

            Av1PartitionType selectedPartition = (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[nodeIndex];
            if (selectedPartition == Av1PartitionType.Invalid)
            {
                selectedPartition = this.SelectBestPartition(
                    writer,
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    blockSize,
                    nodeIndex,
                    Av1RateDistortionStatistics.Invalid,
                    out _);
            }

            // Search children retain their choices by quadtree position, independently of the final writer's
            // preorder index. Later traversal visits those choices without repeating recursive partition search.
            this.PreparePartitionGeometry(blockOrigin, blockSize, selectedPartition);
            this.replayNodeIndex = nodeIndex;
            this.replayPartition = selectedPartition;
            this.replayPartitionOrigin = blockOrigin;
            this.replayParentSize = blockSize;
            return selectedPartition;
        }

        private Av1PartitionType SelectBestPartition(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            int nodeIndex,
            Av1RateDistortionStatistics costLimit,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            int savedLumaArea = this.codedAreaLuma;
            int savedChromaArea = this.codedAreaChroma;
            this.SavePartitionTrialContexts(blockOrigin, tileIndex, blockSize);
            Av1RateDistortionStatistics bestStatistics = costLimit;
            selectedStatistics = Av1RateDistortionStatistics.Invalid;
            Av1PartitionType selectedPartition = Av1PartitionType.None;
            ReadOnlySpan<Av1PartitionType> searchOrder = PartitionSearchOrder;
            int candidateCount = blockSize == Av1BlockSize.Block8x8 ? 4 : searchOrder.Length;
            bool noneInvalid = false;
            long noneCost = long.MaxValue;
            bool pruneHorizontalRectangle = false;
            bool pruneVerticalRectangle = false;
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Av1PartitionType partitionType = searchOrder[candidateIndex];
                if ((partitionType == Av1PartitionType.Horizontal && pruneHorizontalRectangle) ||
                    (partitionType == Av1PartitionType.Vertical && pruneVerticalRectangle))
                {
                    continue;
                }

                if (!this.IsPartitionCandidateAllowed(blockOrigin, blockSize, partitionType))
                {
                    continue;
                }

                if (partitionType is >= Av1PartitionType.HorizontalA and <= Av1PartitionType.VerticalB)
                {
                    bool squareFirst = partitionType is Av1PartitionType.HorizontalA or Av1PartitionType.VerticalA;
                    int sourceNode = squareFirst ? (nodeIndex * 4) + 1 : nodeIndex;
                    Av1PartitionType sourcePartition = squareFirst
                        ? Av1PartitionType.None
                        : partitionType == Av1PartitionType.HorizontalB ? Av1PartitionType.Horizontal : Av1PartitionType.Vertical;

                    bool firstAvailable = !squareFirst ||
                        (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[sourceNode] == Av1PartitionType.None;

                    if (firstAvailable)
                    {
                        Av1EncoderPartitionTree.ModeContext previous =
                            this.blockWorkspace.PartitionTree.GetContext(sourceNode, sourcePartition, 0);

                        if (previous.Snapshot.Ready)
                        {
                            // These leading leaves have the same geometry and already reconstructed neighbors.
                            // Palette and CfL choices are excluded when establishing reuse, since their inputs
                            // depend on the surrounding partition's reconstruction and palette contexts.
                            this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, 0).CopyFrom(previous, partitionType);
                            int secondNode = (nodeIndex * 4) + 2;
                            if (partitionType == Av1PartitionType.HorizontalA &&
                                (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[secondNode] == Av1PartitionType.None)
                            {
                                Av1EncoderPartitionTree.ModeContext second =
                                    this.blockWorkspace.PartitionTree.GetContext(secondNode, Av1PartitionType.None, 0);

                                if (second.Snapshot.Ready)
                                {
                                    this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, 1).CopyFrom(second, partitionType);
                                }
                            }
                        }
                    }
                }

                Av1RateDistortionStatistics candidateStatistics = this.EvaluatePartitionCandidate(
                    writer,
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    blockSize,
                    partitionType,
                    nodeIndex,
                    bestStatistics,
                    searchChildren: true,
                    publishFinalContexts: false);

                if (partitionType == Av1PartitionType.None)
                {
                    noneInvalid = candidateStatistics.Cost == long.MaxValue;
                    noneCost = candidateStatistics.Cost;
                    if (!noneInvalid && this.picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level6)
                    {
                        Av1PredictionMode noneMode = this.blockWorkspace.PartitionTree
                            .GetContext(nodeIndex, Av1PartitionType.None, 0)
                            .Snapshot.ModeInfo.Block.Mode;

                        if (noneMode is Av1PredictionMode.DC or Av1PredictionMode.Smooth)
                        {
                            bool largerLeft = macroBlock.IsLeftAvailable &&
                                GetBlockArea(macroBlock.GetRelativeModeInfo(-1).Block.BlockSize) > GetBlockArea(blockSize);

                            bool largerAbove = macroBlock.IsUpAvailable &&
                                GetBlockArea(macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block.BlockSize) > GetBlockArea(blockSize);

                            pruneHorizontalRectangle = largerLeft || largerAbove;
                            pruneVerticalRectangle = pruneHorizontalRectangle;
                        }
                        else
                        {
                            pruneHorizontalRectangle = noneMode is
                                Av1PredictionMode.Directional67Degrees or
                                Av1PredictionMode.Vertical or
                                Av1PredictionMode.Directional113Degrees;

                            pruneVerticalRectangle = noneMode is
                                Av1PredictionMode.Directional157Degrees or
                                Av1PredictionMode.Horizontal or
                                Av1PredictionMode.Directional203Degrees;
                        }
                    }
                }

                if (partitionType == Av1PartitionType.Split &&
                    this.picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level3 &&
                    noneCost != long.MaxValue &&
                    noneCost < candidateStatistics.Cost)
                {
                    pruneHorizontalRectangle = true;
                    pruneVerticalRectangle = true;
                }

                bool terminateAfterSplit = partitionType == Av1PartitionType.Split &&
                    ShouldTerminatePartitionSearchAfterNoneAndSplit(
                        this.picture.Parent.EncodingSpeed,
                        blockSize,
                        this.picture.Sequence.SequenceHeader.SuperblockSize,
                        noneInvalid,
                        candidateStatistics.Cost == long.MaxValue);

                if (candidateStatistics.Cost < bestStatistics.Cost)
                {
                    bestStatistics = candidateStatistics;
                    selectedStatistics = candidateStatistics;
                    selectedPartition = partitionType;
                }

                this.ResetPartitionTrial(
                    blockOrigin,
                    tileIndex,
                    blockSize,
                    savedLumaArea,
                    savedChromaArea);

                if (terminateAfterSplit)
                {
                    break;
                }
            }

            this.superblock.Workspace.PartitionSearchTypes[nodeIndex] = selectedStatistics.Cost == long.MaxValue
                ? (byte)Av1PartitionType.Invalid
                : (byte)selectedPartition;

            return selectedPartition;
        }

        private static int GetBlockArea(Av1BlockSize blockSize)
            => blockSize.GetWidth() * blockSize.GetHeight();

        internal static bool ShouldTerminatePartitionSearchAfterNoneAndSplit(
            HeifEncodingSpeed speed,
            Av1BlockSize blockSize,
            Av1BlockSize superblockSize,
            bool noneInvalid,
            bool splitInvalid)
            => speed >= HeifEncodingSpeed.Level4 &&
                blockSize != superblockSize &&
                noneInvalid &&
                splitInvalid;

        private Av1RateDistortionStatistics EvaluateSelectedPartitionTree(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            int nodeIndex,
            Av1RateDistortionStatistics costLimit,
            bool searchChildren,
            bool publishContexts)
        {
            Av1PartitionType selectedPartition = (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[nodeIndex];
            if (searchChildren)
            {
                selectedPartition = this.SelectBestPartition(
                    writer,
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    blockSize,
                    nodeIndex,
                    costLimit,
                    out Av1RateDistortionStatistics selectedStatistics);

                if (selectedStatistics.Cost == long.MaxValue)
                {
                    return selectedStatistics;
                }
            }

            Av1RateDistortionStatistics statistics = this.EvaluatePartitionCandidate(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
                blockSize,
                selectedPartition,
                nodeIndex,
                Av1RateDistortionStatistics.Invalid,
                searchChildren: false,
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

            return statistics;
        }

        private Av1RateDistortionStatistics EvaluatePartitionCandidate(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType,
            int nodeIndex,
            Av1RateDistortionStatistics costLimit,
            bool searchChildren,
            bool publishFinalContexts)
        {
            int rate = Av1TileWriter.GetPartitionCost(
                this.picture,
                writer,
                blockSize,
                partitionType,
                blockOrigin,
                this.picture.PartitionContexts[tileIndex]);

            Av1RateDistortionStatistics statistics = new(this.rateMultiplier, rate, 0);
            int leafCount = GetPartitionLeafCount(partitionType);

            // Each child consumes part of the parent's bound. A losing prefix cannot be recovered by
            // later nonnegative rates or distortion, so it must stop before another child changes contexts.
            for (int leafIndex = 0; leafIndex < leafCount; leafIndex++)
            {
                if (statistics.Cost >= costLimit.Cost)
                {
                    return Av1RateDistortionStatistics.Invalid;
                }

                GetPartitionLeafGeometry(
                    blockOrigin,
                    blockSize,
                    partitionType,
                    leafIndex,
                    out Point leafOrigin,
                    out Av1BlockSize leafSize);

                if (!this.IsBlockOriginInsideFrame(leafOrigin))
                {
                    continue;
                }

                Av1RateDistortionStatistics remainingCost = costLimit.Subtract(this.rateMultiplier, in statistics);
                bool publishContexts = leafIndex < leafCount - 1 || publishFinalContexts;
                Av1RateDistortionStatistics childStatistics = partitionType == Av1PartitionType.Split && blockSize > Av1BlockSize.Block8x8
                    ? this.EvaluateSelectedPartitionTree(
                        writer,
                        macroBlock,
                        leafOrigin,
                        tileIndex,
                        leafSize,
                        (nodeIndex * 4) + leafIndex + 1,
                        remainingCost,
                        searchChildren,
                        publishContexts)
                    : searchChildren &&
                        !this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex).Snapshot.Ready
                        ? this.EvaluatePartitionLeaf(
                            writer,
                            macroBlock,
                            leafOrigin,
                            tileIndex,
                            leafSize,
                            partitionType == Av1PartitionType.Split ? Av1PartitionType.None : partitionType,
                            this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex),
                            publishContexts)
                        : this.ReconstructPartitionLeaf(
                            writer,
                            macroBlock,
                            leafOrigin,
                            tileIndex,
                            this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex),
                            publishContexts);

                if (childStatistics.Cost == long.MaxValue)
                {
                    return Av1RateDistortionStatistics.Invalid;
                }

                statistics.Add(this.rateMultiplier, in childStatistics);
                bool reusableSplit = partitionType == Av1PartitionType.Split && blockSize > Av1BlockSize.Block8x8 && leafIndex < 2;
                bool reusableRectangle = partitionType is Av1PartitionType.Horizontal or Av1PartitionType.Vertical &&
                    leafIndex == 0 && statistics.Cost < costLimit.Cost;

                int reusableNode = reusableSplit ? (nodeIndex * 4) + leafIndex + 1 : nodeIndex;
                if (searchChildren &&
                    (reusableRectangle || (reusableSplit &&
                        (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[reusableNode] == Av1PartitionType.None)))
                {
                    Av1EncoderPartitionTree.ModeContext reusable = this.blockWorkspace.PartitionTree.GetContext(
                        reusableNode, reusableSplit ? Av1PartitionType.None : partitionType, 0);

                    reusable.Snapshot.Ready = reusable.Snapshot.Palette.PaletteSizes[0] == 0 &&
                        reusable.Snapshot.Palette.PaletteSizes[1] == 0 &&
                        reusable.Snapshot.ModeInfo.Block.UvMode != Av1ChromaPredictionMode.ChromaFromLuma;
                }
            }

            return statistics.Cost < costLimit.Cost ? statistics : Av1RateDistortionStatistics.Invalid;
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
                    // Mixed vertical partitions reconstruct square leaves in a different order.
                    // Retain the parent decision so prediction uses the same edge availability as the decoder.
                    // Split children own another partition node; a terminal 4x4 child implicitly owns NONE.
                    this.SetBlockGeometry(
                        leafOrigin,
                        leafSize,
                        partitionType == Av1PartitionType.Split ? Av1PartitionType.None : partitionType);
                }
            }
        }

        private bool IsPartitionCandidateAllowed(
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType)
        {
            int halfWidth = blockSize.GetWidth() >> 1;
            int halfHeight = blockSize.GetHeight() >> 1;
            bool hasRows = blockOrigin.Y + halfHeight <
                (this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

            bool hasColumns = blockOrigin.X + halfWidth <
                (this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2);

            // At frame edges the partition alphabet depends on whether each midpoint is visible.
            // A remaining half-block is split implicitly; permitted leaves may extend into padding.
            if ((partitionType == Av1PartitionType.None && (!hasRows || !hasColumns)) ||
                (partitionType == Av1PartitionType.Horizontal && !hasColumns) ||
                (partitionType == Av1PartitionType.Vertical && !hasRows) ||
                (partitionType >= Av1PartitionType.HorizontalA && (!hasRows || !hasColumns)))
            {
                return false;
            }

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
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
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
            if (this.replayNodeIndex >= 0)
            {
                int halfSize = this.replayParentSize.GetWidth() >> 1;
                int localX = blockOrigin.X - this.replayPartitionOrigin.X;
                int localY = blockOrigin.Y - this.replayPartitionOrigin.Y;
                int leafIndex = this.replayPartition switch
                {
                    Av1PartitionType.Horizontal => localY / halfSize,
                    Av1PartitionType.Vertical => localX / halfSize,
                    Av1PartitionType.Split => (localX / halfSize) + (2 * (localY / halfSize)),
                    Av1PartitionType.HorizontalA => localY < halfSize ? localX / halfSize : 2,
                    Av1PartitionType.HorizontalB => localY < halfSize ? 0 : 1 + (localX / halfSize),
                    Av1PartitionType.VerticalA => localX < halfSize ? localY / halfSize : 2,
                    Av1PartitionType.VerticalB => localX < halfSize ? 0 : 1 + (localY / halfSize),
                    Av1PartitionType.Horizontal4 => localY / (halfSize >> 1),
                    Av1PartitionType.Vertical4 => localX / (halfSize >> 1),
                    _ => 0
                };

                // The writer reaches each terminal leaf in partition order. Restore its selected syntax
                // and rebuild the residual against the preceding leaves, without running mode search again.
                Av1EncoderPartitionTree.ModeContext context =
                    this.blockWorkspace.PartitionTree.GetContext(this.replayNodeIndex, this.replayPartition, leafIndex);

                modeInfo = context.Snapshot.ModeInfo;
                block = context.Snapshot.Block;
                paletteInfo = context.Snapshot.Palette;
                if (modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra)
                {
                    InlineArray18<Av1EncoderTransformBlockState> states = default;
                    Av1TransformSize replayTransformSize = modeInfo.Block.TransformSize;
                    int lumaTransformCount =
                        (modeInfo.Block.BlockSize.GetWidth() / replayTransformSize.GetWidth()) *
                        (modeInfo.Block.BlockSize.GetHeight() / replayTransformSize.GetHeight());
                    int lumaStateStride = replayTransformSize.GetSize2d() /
                        Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                    ReadOnlySpan<Av1EncoderTransformBlockState> replayLumaStates = context.GetTransformStates(Av1Plane.Y);
                    for (int transformIndex = 0; transformIndex < lumaTransformCount; transformIndex++)
                    {
                        states[transformIndex] = replayLumaStates[transformIndex * lumaStateStride];
                    }

                    if (block.HasChroma)
                    {
                        states[16] = context.GetTransformStates(Av1Plane.U)[0];
                        states[17] = context.GetTransformStates(Av1Plane.V)[0];
                    }

                    this.ReconstructSelectedInterBlock(
                        writer,
                        macroBlock,
                        tileIndex,
                        blockOrigin,
                        modeInfo,
                        block,
                        context.Snapshot.Displacement,
                        context.Snapshot.SecondaryDisplacement,
                        states);

                    Point replayModeInfoPosition = new(
                        blockOrigin.X >> Av1Constants.ModeInfoSizeLog2,
                        blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);

                    this.picture.SetDisplacementVector(replayModeInfoPosition, context.Snapshot.Displacement);
                    if (modeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                    {
                        this.picture.SetSecondaryDisplacementVector(replayModeInfoPosition, context.Snapshot.SecondaryDisplacement);
                    }

                    Size replayLumaExtent = GetCodedTransformExtent(
                        macroBlock,
                        modeInfo.Block.BlockSize,
                        modeInfo.Block.TransformSize,
                        0,
                        0);

                    this.codedAreaLuma += replayLumaExtent.Width * replayLumaExtent.Height;
                    if (block.HasChroma)
                    {
                        int subX = this.source.ChromaSubsamplingX;
                        int subY = this.source.ChromaSubsamplingY;
                        Av1BlockSize chromaBlockSize = modeInfo.Block.BlockSize.GetSubsampled(subX != 0, subY != 0);
                        Av1TransformSize chromaTransformSize = modeInfo.Block.BlockSize.GetMaxUvTransformSize(subX != 0, subY != 0);
                        Size chromaExtent = GetCodedTransformExtent(
                            macroBlock,
                            chromaBlockSize,
                            chromaTransformSize,
                            subX,
                            subY);

                        this.codedAreaChroma += chromaExtent.Width * chromaExtent.Height;
                    }
                }
                else
                {
                    this.ReconstructSelectedIntraBlock(writer, macroBlock, blockOrigin, tileIndex, context);
                }
                this.SelectedBlockStatistics = context.Snapshot.Statistics;
                return;
            }

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
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            block.HasChroma = !this.source.IsMonochrome &&
                Av1TileReader.HasChroma(this.picture.Sequence.SequenceHeader, modeInfoPosition, blockSize);

            block.QuantizationIndex = qIndex;
            block.SegmentId = 0;

            if (this.picture.Sequence.SequenceHeader.IsStillPicture && this.picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level8)
            {
                int sourceVariance = this.GetSourceVariance(blockOrigin, blockSize);
                if (this.picture.Parent.EncodingSpeed == HeifEncodingSpeed.Level9 || blockSize >= Av1BlockSize.Block16x16 || sourceVariance < 101)
                {
                    this.EncodeEstimatedIntraBlock(
                        writer, macroBlock, blockOrigin, blockSize, tileIndex, sourceVariance, ref modeInfo, ref block, ref paletteInfo);

                    return;
                }
            }

            bool isInterFrame = !this.picture.Parent.FrameHeader.IsIntra;
            Av1RateDistortionStatistics interStatistics = Av1RateDistortionStatistics.Invalid;
            Av1MacroBlockModeInfo interModeInfo = default;
            Av1EncoderBlockStruct interBlock = default;
            InlineArray18<Av1EncoderTransformBlockState> interStates = default;
            Av1MotionVector interVector = default;
            Av1MotionVector interSecondaryVector = default;
            if (isInterFrame)
            {
                Av1MacroBlockModeInfo initialModeInfo = modeInfo;
                Av1EncoderBlockStruct initialBlock = block;
                interStatistics = this.SelectInterBlock(
                    writer,
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    ref modeInfo,
                    ref block,
                    out interVector,
                    out interSecondaryVector,
                    out interStates);

                interModeInfo = modeInfo;
                interBlock = block;

                // Only the winning syntax and transform choices survive across mode families. Intra trials
                // reuse prediction and coefficient scratch; the selected inter block is reconstructed afterward.
                modeInfo = initialModeInfo;
                block = initialBlock;
                paletteInfo = default;
            }

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
                out Av1RateDistortionStatistics lumaStatistics);

            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = (sbyte)lumaAngleDelta;
            block.FilterIntraMode = filterIntraMode;
            modeInfo.Block.TransformSize = lumaTransformSize;

            int chromaArea = 0;
            if (block.HasChroma)
            {
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
                    out Av1RateDistortionStatistics chromaStatistics);

                block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = (sbyte)chromaAngleDelta;
                block.PredictionUnit.ChromaFromLumaIndex = chromaFromLumaIndex;
                block.PredictionUnit.ChromaFromLumaSigns = chromaFromLumaSigns;
                Size chromaExtent = GetCodedTransformExtent(
                    macroBlock, chromaBlockSize, chromaTransformSize, subsamplingX, subsamplingY);

                chromaArea = chromaExtent.Width * chromaExtent.Height;
                lumaStatistics.Add(this.rateMultiplier, in chromaStatistics);
            }

            bool allowIntraBlockCopy = blockSize == Av1BlockSize.Block8x8 &&
                this.picture.Parent.FrameHeader.AllowIntraBlockCopy;

            Av1RateDistortionStatistics regularStatistics = this.GetRegularBlockCost(
                writer,
                macroBlock,
                lumaStatistics,
                allowIntraBlockCopy);

            if (isInterFrame &&
                this.picture.Parent.FrameHeader.SkipModeParameters.SkipModeFlag &&
                Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8)
            {
                regularStatistics = new Av1RateDistortionStatistics(
                    this.rateMultiplier,
                    regularStatistics.Rate + writer.GetSkipModeCost(false, Av1TileWriter.GetSkipModeContext(macroBlock)),
                    regularStatistics.Distortion);
            }

            if (isInterFrame && interStatistics.Cost <= regularStatistics.Cost)
            {
                // Inter candidates precede intra candidates, so an equal cost retains the inter winner.
                modeInfo = interModeInfo;
                block = interBlock;
                paletteInfo = default;
                this.ReconstructSelectedInterBlock(
                    writer,
                    macroBlock,
                    tileIndex,
                    blockOrigin,
                    modeInfo,
                    block,
                    interVector,
                    interSecondaryVector,
                    interStates);
                this.picture.SetDisplacementVector(modeInfoPosition, interVector);
                if (modeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                {
                    // The encoder's spatial motion stack must retain both vectors of the selected pair; otherwise
                    // later compound blocks would derive a different nearest pair than the decoder.
                    this.picture.SetSecondaryDisplacementVector(modeInfoPosition, interSecondaryVector);
                }

                this.SelectedBlockStatistics = interStatistics;
            }
            else
            {
                this.SelectedBlockStatistics = allowIntraBlockCopy
                    ? this.SelectIntraBlockCopy(
                        writer,
                        macroBlock,
                        blockOrigin,
                        tileIndex,
                        regularStatistics,
                        ref modeInfo,
                        ref block,
                        ref paletteInfo)
                    : regularStatistics;
            }

            Size lumaExtent = GetCodedTransformExtent(macroBlock, blockSize, modeInfo.Block.TransformSize, 0, 0);
            this.codedAreaLuma += lumaExtent.Width * lumaExtent.Height;
            this.codedAreaChroma += chromaArea;
        }

        private Av1RateDistortionStatistics EvaluatePartitionLeaf(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType,
            Av1EncoderPartitionTree.ModeContext context,
            bool publishContexts)
        {
            // Trial leaves must use the same reconstruction order as final leaves of this partition.
            this.SetBlockGeometry(blockOrigin, blockSize, partitionType);
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
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

            context.Snapshot = new Av1EncoderPartitionTree.ModeSnapshot
            {
                ModeInfo = modeInfo,
                Block = block,
                Palette = paletteInfo,
                Statistics = this.SelectedBlockStatistics,
                Displacement = modeInfo.Block.UseIntraBlockCopy ? this.picture.GetDisplacementVector(modeInfoPosition) : default,
                SecondaryDisplacement = default,
                Ready = false
            };

            if (modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra)
            {
                context.Snapshot.Displacement = this.picture.GetDisplacementVector(modeInfoPosition);
                context.Snapshot.SecondaryDisplacement = modeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra
                    ? this.picture.GetSecondaryDisplacementVector(modeInfoPosition)
                    : default;
            }

            int planeCount = block.HasChroma ? 3 : 1;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                Av1BlockSize planeSize = plane == Av1Plane.Y
                    ? blockSize
                    : blockSize.GetSubsampled(this.source.ChromaSubsamplingX != 0, this.source.ChromaSubsamplingY != 0);

                int count = plane == Av1Plane.Y ? this.codedAreaLuma - lumaArea : this.codedAreaChroma - chromaArea;
                int area = plane == Av1Plane.Y ? lumaArea : chromaArea;
                this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, plane)
                    .Slice(
                        area / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount,
                        count / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount)
                    .CopyTo(context.GetTransformStates(plane));

                if (plane != Av1Plane.V && paletteInfo.PaletteSizes[plane == Av1Plane.Y ? 0 : 1] > 0)
                {
                    Av1PlaneType planeType = plane == Av1Plane.Y ? Av1PlaneType.Y : Av1PlaneType.Uv;
                    int width = planeSize.GetWidth();
                    int height = planeSize.GetHeight();
                    Buffer2DRegion<byte> map = this.superblock.Workspace.GetPaletteMaps().GetMap(planeType, width, height);
                    Span<byte> retained = context.GetPaletteIndices(planeType);
                    for (int row = 0; row < height; row++)
                    {
                        map.DangerousGetRowSpan(row).CopyTo(retained.Slice(row * width, width));
                    }
                }
            }

            if (publishContexts)
            {
                this.PublishPartitionLeafContexts(
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    lumaArea,
                    chromaArea,
                    modeInfo,
                    block,
                    paletteInfo);
            }

            return this.SelectedBlockStatistics;
        }

        private Av1RateDistortionStatistics ReconstructPartitionLeaf(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1EncoderPartitionTree.ModeContext context,
            bool publishContexts)
        {
            Av1EncoderPartitionTree.ModeSnapshot snapshot = context.Snapshot;
            Av1BlockSize blockSize = snapshot.ModeInfo.Block.BlockSize;
            this.SetBlockGeometry(blockOrigin, blockSize, snapshot.ModeInfo.Block.PartitionType);
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            this.picture.GetMacroBlockModeInfo(modeInfoPosition) = snapshot.ModeInfo;
            Av1TileWriter.SetModeInfoRowAndColumn(
                this.picture,
                macroBlock,
                macroBlock.Tile,
                modeInfoPosition,
                blockSize,
                this.picture.Parent.Common.ModeInfoStride,
                this.picture.Parent.Common.ModeInfoRowCount,
                this.picture.Parent.Common.ModeInfoColumnCount);

            int lumaArea = this.codedAreaLuma;
            int chromaArea = this.codedAreaChroma;
            this.ReconstructSelectedIntraBlock(writer, macroBlock, blockOrigin, tileIndex, context);
            if (publishContexts)
            {
                this.PublishPartitionLeafContexts(
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    lumaArea,
                    chromaArea,
                    snapshot.ModeInfo,
                    snapshot.Block,
                    snapshot.Palette);
            }

            this.SelectedBlockStatistics = snapshot.Statistics;
            return snapshot.Statistics;
        }

        private void ReconstructSelectedIntraBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1EncoderPartitionTree.ModeContext context)
        {
            Av1EncoderPartitionTree.ModeSnapshot snapshot = context.Snapshot;
            Av1BlockSize blockSize = snapshot.ModeInfo.Block.BlockSize;
            bool blockCopy = snapshot.ModeInfo.Block.UseIntraBlockCopy;
            bool usesChromaFromLuma = snapshot.Block.HasChroma && snapshot.ModeInfo.Block.UvMode == Av1ChromaPredictionMode.ChromaFromLuma;
            Av1EncoderModeDecisionWorkspace<TSample> workspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>();
            Span<short> lumaQ3 = workspace.ChromaFromLumaSamples;
            int planeCount = snapshot.Block.HasChroma ? 3 : 1;
            int chromaArea = 0;

            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                Point planeOrigin = planeIndex == 0 ? blockOrigin : Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
                Av1BlockSize planeBlockSize = planeIndex == 0 ? blockSize : blockSize.GetSubsampled(subX != 0, subY != 0);
                int width = planeBlockSize.GetWidth();
                int height = planeBlockSize.GetHeight();
                Av1TransformSize transformSize = planeIndex == 0
                    ? snapshot.ModeInfo.Block.TransformSize
                    : this.picture.Parent.FrameHeader.CodedLossless
                        ? Av1TransformSize.Size4x4
                        : blockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

                Size codedExtent = GetCodedTransformExtent(macroBlock, planeBlockSize, transformSize, subX, subY);
                int transformWidth = transformSize.GetWidth();
                int transformHeight = transformSize.GetHeight();
                int sampleCount = transformSize.GetSize2d();
                int coefficientOffset = planeIndex == 0 ? this.codedAreaLuma : this.codedAreaChroma;
                ReadOnlySpan<Av1EncoderTransformBlockState> states = context.GetTransformStates(plane);
                Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(plane);
                Buffer2DRegion<TSample> destinationPlane = this.reconstruction.GetPlane(plane);
                ReadOnlySpan<TSample> reconstructedBlock = Av1TransformBlockEncoder.GetPlaneSpan(destinationPlane, planeOrigin);

                // Prediction stays outside transient CfL storage. The zero-mean luma surface survives
                // both chroma planes while the residual and inverse-transform workspaces are reused.
                Span<TSample> prediction = workspace.GetCandidateReconstruction(0)[..sampleCount];
                Span<short> residual = workspace.Residual[..sampleCount];
                Span<TSample> aboveStorage = workspace.GetReferenceSamples(0);
                Span<TSample> leftStorage = workspace.GetReferenceSamples(1);
                Av1PlaneType planeType = planeIndex == 0 ? Av1PlaneType.Y : Av1PlaneType.Uv;
                int contextWidth = planeBlockSize.Get4x4WideCount();
                int contextHeight = planeBlockSize.Get4x4HighCount();
                Span<byte> topContexts = workspace.TransformContexts[..contextWidth];
                Span<byte> leftContexts = workspace.TransformContexts.Slice(contextWidth, contextHeight);
                Av1NeighborArrayUnit<byte> neighbors = plane switch
                {
                    Av1Plane.Y => this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                    Av1Plane.U => this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                    _ => this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex]
                };

                neighbors.Top.Slice(neighbors.GetTopIndex(planeOrigin), contextWidth).CopyTo(topContexts);
                neighbors.Left.Slice(neighbors.GetLeftIndex(planeOrigin), contextHeight).CopyTo(leftContexts);
                Av1ComponentType component = planeIndex == 0 ? Av1ComponentType.Luminance : Av1ComponentType.Chroma;
                int paletteSize = snapshot.Palette.PaletteSizes[(int)planeType];
                Buffer2DRegion<byte> paletteMap = default;
                if (paletteSize > 0)
                {
                    paletteMap = this.superblock.Workspace.GetPaletteMaps().GetMap(planeType, width, height);
                    if (plane != Av1Plane.V)
                    {
                        ReadOnlySpan<byte> retainedMap = context.GetPaletteIndices(planeType);
                        for (int row = 0; row < height; row++)
                        {
                            retainedMap.Slice(row * width, width).CopyTo(paletteMap.DangerousGetRowSpan(row));
                        }
                    }
                }

                if (plane == Av1Plane.U && usesChromaFromLuma)
                {
                    TOperator.PrepareChromaFromLuma(
                        this.reconstruction.GetPlane(Av1Plane.Y),
                        new Point(planeOrigin.X << subX, planeOrigin.Y << subY),
                        lumaQ3,
                        transformSize,
                        subX != 0,
                        subY != 0);
                }

                Av1BlockSize maximumUnit = planeIndex == 0
                    ? Av1BlockSize.Block64x64
                    : Av1BlockSize.Block64x64.GetSubsampled(subX != 0, subY != 0);

                int unitWidth = Math.Min(maximumUnit.GetWidth(), codedExtent.Width);
                int unitHeight = Math.Min(maximumUnit.GetHeight(), codedExtent.Height);
                int transformIndex = 0;

                // Large coding blocks visit bounded 64x64 luma regions before advancing to the next region.
                // Within each region, raster order supplies the reconstructed edges of later transforms.
                for (int unitY = 0; unitY < codedExtent.Height; unitY += unitHeight)
                {
                    for (int unitX = 0; unitX < codedExtent.Width; unitX += unitWidth)
                    {
                        for (int y = unitY; y < Math.Min(unitY + unitHeight, codedExtent.Height); y += transformHeight)
                        {
                            for (int x = unitX; x < Math.Min(unitX + unitWidth, codedExtent.Width); x += transformWidth, transformIndex++)
                            {
                                Point transformOrigin = planeOrigin + new Size(x, y);
                                if (blockCopy)
                                {
                                    Point referenceOrigin = new(
                                        blockOrigin.X + (snapshot.Displacement.Column >> 3),
                                        blockOrigin.Y + (snapshot.Displacement.Row >> 3));

                                    TOperator.PrepareIntraBlockCopyPrediction(
                                        sourcePlane,
                                        transformOrigin,
                                        destinationPlane,
                                        new Point((referenceOrigin.X >> subX) + x, (referenceOrigin.Y >> subY) + y),
                                        subX != 0 && (referenceOrigin.X & 1) != 0,
                                        subY != 0 && (referenceOrigin.Y & 1) != 0,
                                        prediction,
                                        residual,
                                        transformSize);
                                }
                                else if (paletteSize > 0)
                                {
                                    TOperator.PreparePalette(
                                        sourcePlane,
                                        transformOrigin,
                                        snapshot.Palette.GetColors(plane),
                                        paletteMap.GetSubRegion(x, y, transformWidth, transformHeight),
                                        prediction,
                                        residual,
                                        transformSize);
                                }
                                else
                                {
                                    this.PrepareTransformReferenceSamples(
                                        destinationPlane,
                                        blockOrigin,
                                        planeOrigin,
                                        blockSize,
                                        macroBlock,
                                        y / transformHeight,
                                        x / transformWidth,
                                        destinationPlane.Stride,
                                        transformSize,
                                        subX,
                                        subY,
                                        reconstructedBlock,
                                        aboveStorage,
                                        leftStorage,
                                        out bool hasLeft,
                                        out bool hasAbove);

                                    ReadOnlySpan<TSample> above = aboveStorage.Slice(1, transformWidth + transformHeight);
                                    ReadOnlySpan<TSample> left = leftStorage.Slice(1, transformWidth + transformHeight);
                                    if (planeIndex > 0 && usesChromaFromLuma)
                                    {
                                        TOperator.PrepareChromaFromLumaDc(
                                            prediction, above, left, hasLeft, hasAbove, transformSize, this.bitDepth);

                                        int jointSign = snapshot.Block.PredictionUnit.ChromaFromLumaSigns;
                                        int indices = snapshot.Block.PredictionUnit.ChromaFromLumaIndex;
                                        int sign = plane == Av1Plane.U ? Av1ChromaFromLumaMath.SignU(jointSign) : Av1ChromaFromLumaMath.SignV(jointSign);
                                        int magnitude = plane == Av1Plane.U ? Av1ChromaFromLumaMath.IndexU(indices) : Av1ChromaFromLumaMath.IndexV(indices);
                                        int alpha = sign == Av1ChromaFromLumaMath.SignZero
                                            ? 0
                                            : (magnitude + 1) * (sign == Av1ChromaFromLumaMath.SignNegative ? -1 : 1);

                                        TOperator.ApplyChromaFromLuma(lumaQ3, prediction, alpha, transformSize, this.bitDepth);
                                        TOperator.SubtractPrediction(sourcePlane, transformOrigin, prediction, residual, transformSize);
                                    }
                                    else if (planeIndex == 0 && snapshot.Block.FilterIntraMode != Av1FilterIntraMode.AllFilterIntraModes)
                                    {
                                        TOperator.PrepareFilterIntra(
                                            this.blockWorkspace,
                                            sourcePlane,
                                            transformOrigin,
                                            prediction,
                                            above,
                                            left,
                                            residual,
                                            snapshot.Block.FilterIntraMode,
                                            transformSize,
                                            this.bitDepth);
                                    }
                                    else
                                    {
                                        Av1PredictionMode mode = planeIndex == 0
                                            ? snapshot.ModeInfo.Block.Mode
                                            : snapshot.ModeInfo.Block.UvMode.ToLumaMode();

                                        TOperator.PrepareIntra(
                                            this.blockWorkspace,
                                            sourcePlane,
                                            transformOrigin,
                                            prediction,
                                            transformSize.GetWidth(),
                                            above,
                                            left,
                                            hasLeft,
                                            hasAbove,
                                            mode,
                                            snapshot.Block.PredictionUnit.AngleDelta[(int)planeType],
                                            this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                                            this.UseSmoothIntraEdges(macroBlock, blockOrigin, blockSize, plane),
                                            residual,
                                            transformSize,
                                            this.bitDepth);
                                    }
                                }

                                int stateIndex = transformIndex * sampleCount / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                                Span<byte> transformTop = topContexts.Slice(x / 4, transformWidth / 4);
                                Span<byte> transformLeft = leftContexts.Slice(y / 4, transformHeight / 4);
                                Av1TransformBlockContext blockContext = Av1TileWriter.GetTransformBlockContexts(
                                    component,
                                    transformTop,
                                    transformLeft,
                                    planeBlockSize,
                                    transformSize);

                                this.ReconstructSelectedTransform(
                                    writer,
                                    blockContext,
                                    blockCopy,
                                    transformOrigin,
                                    plane,
                                    transformSize,
                                    prediction,
                                    residual,
                                    states[stateIndex],
                                    snapshot.ModeInfo.Block.Skip,
                                    coefficientOffset + (transformIndex * sampleCount));

                                int outputOffset = coefficientOffset + (transformIndex * sampleCount);
                                Av1EncoderTransformBlockState outputState = this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, plane)[
                                    outputOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];

                                byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                                    this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, plane).Slice(outputOffset, sampleCount),
                                    transformSize,
                                    outputState.TransformType,
                                    outputState.EndOfBlock);

                                transformTop.Fill(coefficientContext);
                                transformLeft.Fill(coefficientContext);
                            }
                        }
                    }
                }

                if (planeIndex != 0)
                {
                    chromaArea = codedExtent.Width * codedExtent.Height;
                }
            }

            if (blockCopy)
            {
                this.picture.SetDisplacementVector(
                    new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2),
                    snapshot.Displacement);
            }

            Size lumaExtent = GetCodedTransformExtent(macroBlock, blockSize, snapshot.ModeInfo.Block.TransformSize, 0, 0);
            this.codedAreaLuma += lumaExtent.Width * lumaExtent.Height;
            this.codedAreaChroma += chromaArea;
        }

        private void SetBlockGeometry(
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType)
        {
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            ref Av1MacroBlockModeInfo modeInfo = ref this.picture.GetMacroBlockModeInfo(modeInfoPosition);
            modeInfo.Block = new Av1EncoderBlockModeInfo
            {
                BlockSize = blockSize,
                PartitionType = partitionType
            };

            this.picture.MapModeInfoBlock(modeInfoPosition, blockSize);
        }

        private void PublishPartitionLeafContexts(
            Av1MacroBlockD macroBlock,
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
                GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0),
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

            // Lossless residuals retain one state per 4x4 transform, including chroma. Publish those exact
            // edges during partition trials so a later sibling sees the contexts that final writing will use.
            Av1TransformSize chromaTransformSize = this.picture.Parent.FrameHeader.CodedLossless
                ? Av1TransformSize.Size4x4
                : blockSize.GetMaxUvTransformSize(colorConfig.SubSamplingX, colorConfig.SubSamplingY);

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
                GetCodedTransformExtent(macroBlock, chromaBlockSize, chromaTransformSize, subsamplingX, subsamplingY),
                chromaTransformSize,
                maximumChromaUnitBlockSize,
                blueCoefficients[chromaArea..],
                blueStates[chromaStateIndex..]);

            PublishCoefficientContexts(
                this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex],
                chromaOrigin,
                GetCodedTransformExtent(macroBlock, chromaBlockSize, chromaTransformSize, subsamplingX, subsamplingY),
                chromaTransformSize,
                maximumChromaUnitBlockSize,
                redCoefficients[chromaArea..],
                redStates[chromaStateIndex..]);
        }

        private static void PublishCoefficientContexts(
            Av1NeighborArrayUnit<byte> neighbors,
            Point blockOrigin,
            Size codedExtent,
            Av1TransformSize transformSize,
            Av1BlockSize maximumUnitBlockSize,
            ReadOnlySpan<int> coefficients,
            ReadOnlySpan<Av1EncoderTransformBlockState> states)
        {
            const Av1NeighborArrayUnit<byte>.UnitMask EdgeMask =
                Av1NeighborArrayUnit<byte>.UnitMask.Top |
                Av1NeighborArrayUnit<byte>.UnitMask.Left;

            int blockWidth = codedExtent.Width;
            int blockHeight = codedExtent.Height;
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

        private Av1RateDistortionStatistics GetRegularBlockCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Av1RateDistortionStatistics modeStatistics,
            bool allowIntraBlockCopy)
        {
            int rateAdjustment = writer.GetSkipCost(false, Av1TileWriter.GetSkipContext(macroBlock));
            if (!this.picture.Parent.FrameHeader.IsIntra)
            {
                int intraInterContext = Av1TileWriter.GetIntraInterContext(macroBlock);
                rateAdjustment += writer.GetIsInterCost(false, intraInterContext);
            }

            if (allowIntraBlockCopy)
            {
                rateAdjustment += writer.GetUseIntraBlockCopyCost(false);
            }

            return new(this.rateMultiplier, modeStatistics.Rate + rateAdjustment, modeStatistics.Distortion);
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
            out Av1RateDistortionStatistics selectedStatistics)
        {
            Av1PredictionMode mode = this.SelectLumaPrediction(
                writer,
                macroBlock,
                blockOrigin,
                blockSize,
                tileIndex,
                retainedCoefficients,
                retainedStates,
                ref paletteInfo,
                out selectedAngleDelta,
                out selectedFilterIntraMode,
                out selectedTransformSize,
                out selectedStatistics);

            if (Av1TileWriter.IsPaletteAllowed(this.picture.Parent.FrameHeader.AllowScreenContentTools, blockSize) &&
                this.SelectLumaPalette(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    retainedCoefficients,
                    retainedStates,
                    64,
                    ref selectedStatistics,
                    ref paletteInfo,
                    ref selectedTransformSize))
            {
                mode = Av1PredictionMode.DC;
                selectedAngleDelta = 0;
                selectedFilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            }

            return mode;
        }

        private Av1PredictionMode SelectLumaPrediction(
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
            out Av1RateDistortionStatistics selectedStatistics)
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
                    out selectedStatistics);
            }

            bool hasLeft = macroBlock.IsLeftAvailable;
            bool hasAbove = macroBlock.IsUpAvailable;
            int modeInfoRow = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            bool rightAvailable = modeInfoColumn + transformSize.Get4x4WideCount() < macroBlock.Tile.ModeInfoColumnEnd;
            bool bottomAvailable = modeInfoRow + transformSize.Get4x4HighCount() < macroBlock.Tile.ModeInfoRowEnd;
            Av1PartitionType partitionType = macroBlock.GetRelativeModeInfo(0).Block.PartitionType;
            bool hasTopRight = Av1IntraReferenceAvailability.HasTopRight(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                blockSize,
                modeInfoRow,
                modeInfoColumn,
                hasAbove,
                rightAvailable,
                partitionType,
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
                partitionType,
                transformSize,
                0,
                0,
                0,
                0);

            Span<TSample> aboveStorage = workspace.GetReferenceSamples(0);
            Span<TSample> leftStorage = workspace.GetReferenceSamples(1);
            PrepareReferenceSamples(
                reconstructionPlane,
                blockOrigin,
                blockWidth,
                blockHeight,
                hasLeft,
                hasAbove,
                hasTopRight,
                hasBottomLeft,
                this.bitDepth,
                aboveStorage,
                leftStorage);

            ReadOnlySpan<TSample> above = aboveStorage.Slice(1, blockWidth + blockHeight);
            ReadOnlySpan<TSample> left = leftStorage.Slice(1, blockWidth + blockHeight);

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
            Av1RateDistortionStatistics bestStatistics = Av1RateDistortionStatistics.Invalid;
            Av1PredictionMode bestMode = Av1PredictionMode.DC;
            selectedAngleDelta = 0;
            selectedFilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            selectedTransformSize = transformSize;
            int baseModeCount = LumaModeSearchOrder.Length;
            bool pruneOddAngleDeltas = this.picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level6;
            ReadOnlySpan<sbyte> angleDeltaSearchOrder = pruneOddAngleDeltas
                ? PrunedAngleDeltaSearchOrder
                : AngleDeltaSearchOrder;

            int deltaCount = angleDeltaSearchOrder.Length;
            int directionalModeCount = (int)Av1PredictionMode.Directional67Degrees - (int)Av1PredictionMode.Vertical + 1;
            Span<long> directionalCosts = stackalloc long[directionalModeCount * 7];
            directionalCosts.Fill(long.MaxValue);
            Span<long> topModelCosts = stackalloc long[4];
            topModelCosts.Fill(long.MaxValue);
            long bestModelCost = long.MaxValue;
            int topModelCount = this.picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level6 ? 2 :
                this.picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level1 ? 3 : 4;
            int visibleWidth = blockWidth + (Math.Min(0, macroBlock.ToRightEdge) >> 3);
            int visibleHeight = blockHeight + (Math.Min(0, macroBlock.ToBottomEdge) >> 3);
            byte directionalModeSkipMask = this.GetDirectionalModeSkipMask(
                sourcePlane,
                blockOrigin,
                visibleHeight,
                visibleWidth);

            int candidateCount = blockSize >= Av1BlockSize.Block8x8
                ? baseModeCount + (directionalModeCount * deltaCount)
                : baseModeCount;

            bool useReducedTransformSet = this.picture.Parent.FrameHeader.UseReducedTransformSet;
            Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(
                transformSize,
                useReducedTransformSet);

            // Libaom speed four and above evaluates each intra mode with its mode-derived default transform,
            // then performs the broader transform search only for the selected mode. Avoiding the Cartesian
            // product of every prediction and transform is essential to the reference controller's complexity.
            bool deferTransformTypeSearch = !codedLossless &&
                this.picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level4;
            bool searchEveryTransformType = !codedLossless && !deferTransformTypeSearch;
            bool searchEveryTransformSize = !codedLossless &&
                this.picture.Parent.EncodingSpeed < HeifEncodingSpeed.Level8 &&
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
                    angleDelta = angleDeltaSearchOrder[adjustedIndex % deltaCount];
                    if (pruneOddAngleDeltas && ShouldPruneOddAngleDelta(mode, angleDelta, directionalCosts, bestStatistics.Cost))
                    {
                        continue;
                    }
                }

                if (mode is >= Av1PredictionMode.Vertical and <= Av1PredictionMode.Directional67Degrees &&
                    (directionalModeSkipMask & (1 << ((int)mode - (int)Av1PredictionMode.Vertical))) != 0)
                {
                    continue;
                }

                // Prediction and subtraction do not depend on transform type. Preparing them once keeps
                // exhaustive transform search from repeating the same pixel traversal for every candidate.
                TOperator.PrepareIntra(
                    this.blockWorkspace,
                    sourcePlane,
                    blockOrigin,
                    prediction,
                    transformSize.GetWidth(),
                    above,
                    left,
                    hasLeft,
                    hasAbove,
                    mode,
                    angleDelta,
                    this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                    this.UseSmoothIntraEdges(macroBlock, blockOrigin, blockSize, Av1Plane.Y),
                    residual,
                    transformSize,
                    this.bitDepth);

                int modelSize = Math.Min(32, transformSize.GetWidth());
                long modelCost = Av1ForwardTransformer.GetHadamardCost(
                    residual,
                    transformSize.GetWidth(),
                    modelSize,
                    this.bitDepth != Av1BitDepth.EightBit,
                    this.blockWorkspace.TransformCoefficients,
                    this.blockWorkspace.TransformWorkspace);

                if (ShouldPruneIntraModel(
                    modelCost,
                    mode,
                    macroBlock,
                    this.quantization.QIndex[0],
                    topModelCosts,
                    topModelCount,
                    ref bestModelCost))
                {
                    continue;
                }

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

                Av1RateDistortionStatistics modeStatistics = Av1RateDistortionStatistics.Invalid;
                for (Av1TransformType transformType = firstTransformType;
                    transformType < transformTypeLimit;
                    transformType++)
                {
                    if (!transformType.IsExtendedSetUsed(transformSetType))
                    {
                        continue;
                    }

                    Av1EncoderTransformBlockState candidateState = default;
                    Av1RateDistortionStatistics candidateStatistics = this.GetLumaCandidateCost(
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

                    if (candidateStatistics.Cost < modeStatistics.Cost)
                    {
                        modeStatistics = candidateStatistics;
                    }

                    if (candidateStatistics.Cost < bestStatistics.Cost)
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

                        bestStatistics = candidateStatistics;
                        bestMode = mode;
                        selectedAngleDelta = angleDelta;
                        selectedTransformSize = transformSize;
                    }
                }

                if (mode is >= Av1PredictionMode.Vertical and <= Av1PredictionMode.Directional67Degrees)
                {
                    int directionalIndex = (int)mode - (int)Av1PredictionMode.Vertical;
                    directionalCosts[(directionalIndex * 7) + angleDelta + 3] = modeStatistics.Cost;
                }

                // At exhaustive effort, transform size belongs to this mode's RD result. Evaluate it
                // before advancing so an 8x8-only preliminary result cannot discard a better split mode.
                if (searchEveryTransformSize &&
                    this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select)
                {
                    Av1RateDistortionStatistics splitStatistics = this.GetUniformLumaCandidateCost(
                        writer,
                        macroBlock,
                        sourcePlane,
                        reconstructionPlane,
                        blockOrigin,
                        Av1BlockSize.Block8x8,
                        Av1TransformSize.Size4x4,
                        tileIndex,
                        mode,
                        angleDelta,
                        Av1FilterIntraMode.AllFilterIntraModes,
                        0,
                        ReadOnlySpan<ushort>.Empty,
                        0,
                        paletteDisabledCost,
                        transformSizeContext,
                        bestStatistics.Cost,
                        candidateReconstruction,
                        candidateCoefficients,
                        workspace.CandidateTransformBlocks);

                    if (splitStatistics.Cost < bestStatistics.Cost)
                    {
                        CopyTiledCandidate(
                            candidateReconstruction,
                            candidateCoefficients,
                            workspace.CandidateTransformBlocks,
                            reconstructionPlane,
                            blockOrigin,
                            8,
                            GetCodedTransformExtent(macroBlock, blockSize, Av1TransformSize.Size4x4, 0, 0),
                            Av1TransformSize.Size4x4,
                            retainedCoefficients,
                            retainedStates);

                        bestStatistics = splitStatistics;
                        bestMode = mode;
                        selectedAngleDelta = angleDelta;
                        selectedTransformSize = Av1TransformSize.Size4x4;
                    }
                }
            }

            Av1RateDistortionStatistics bestTransformStatistics = bestStatistics;

            // Libaom's all-intra speed-six policy sets prune_filter_intra_level to two, which
            // disables this overlapping predictor family after the ordinary luma search.
            if (this.picture.Parent.EncodingSpeed < HeifEncodingSpeed.Level6 &&
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
                    if (this.picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level2 &&
                        !IsFilterIntraModeDerivedFromBestMode(filterIntraMode, bestMode))
                    {
                        continue;
                    }

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

                    long filterModelCost = Av1ForwardTransformer.GetHadamardCost(
                        residual,
                        transformSize.GetWidth(),
                        Math.Min(32, transformSize.GetWidth()),
                        this.bitDepth != Av1BitDepth.EightBit,
                        this.blockWorkspace.TransformCoefficients,
                        this.blockWorkspace.TransformWorkspace);

                    if (bestModelCost != long.MaxValue &&
                        filterModelCost > bestModelCost + (bestModelCost >> 2))
                    {
                        continue;
                    }

                    bestModelCost = Math.Min(bestModelCost, filterModelCost);

                    Av1TransformType filterTransformTypeLimit = codedLossless || deferTransformTypeSearch
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
                        Av1RateDistortionStatistics candidateStatistics = this.GetFilterIntraCandidateCost(
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

                        if (candidateStatistics.Cost < bestTransformStatistics.Cost)
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

                            bestTransformStatistics = candidateStatistics;
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
                        Av1RateDistortionStatistics splitStatistics = this.GetUniformLumaCandidateCost(
                            writer,
                            macroBlock,
                            sourcePlane,
                            reconstructionPlane,
                            blockOrigin,
                            Av1BlockSize.Block8x8,
                            Av1TransformSize.Size4x4,
                            tileIndex,
                            Av1PredictionMode.DC,
                            0,
                            filterIntraMode,
                            0,
                            ReadOnlySpan<ushort>.Empty,
                            0,
                            paletteDisabledCost,
                            transformSizeContext,
                            bestTransformStatistics.Cost,
                            candidateReconstruction,
                            candidateCoefficients,
                            workspace.CandidateTransformBlocks);

                        if (splitStatistics.Cost < bestTransformStatistics.Cost)
                        {
                            CopyTiledCandidate(
                                candidateReconstruction,
                                candidateCoefficients,
                                workspace.CandidateTransformBlocks,
                                reconstructionPlane,
                                blockOrigin,
                                8,
                                GetCodedTransformExtent(macroBlock, blockSize, Av1TransformSize.Size4x4, 0, 0),
                                Av1TransformSize.Size4x4,
                                retainedCoefficients,
                                retainedStates);

                            bestTransformStatistics = splitStatistics;
                            bestMode = Av1PredictionMode.DC;
                            selectedAngleDelta = 0;
                            selectedFilterIntraMode = filterIntraMode;
                            selectedTransformSize = Av1TransformSize.Size4x4;
                        }
                    }
                }
            }

            // The preliminary winner may be an ordinary or filter-intra mode. Rebuild only that predictor,
            // then search its legal transforms so filter modes receive the same winner treatment without
            // multiplying transform work across every discarded mode.
            if (!codedLossless && !searchEveryTransformType)
            {
                if (selectedFilterIntraMode == Av1FilterIntraMode.AllFilterIntraModes)
                {
                    TOperator.PrepareIntra(
                        this.blockWorkspace,
                        sourcePlane,
                        blockOrigin,
                        prediction,
                        transformSize.GetWidth(),
                        above,
                        left,
                        hasLeft,
                        hasAbove,
                        bestMode,
                        selectedAngleDelta,
                        this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                        this.UseSmoothIntraEdges(macroBlock, blockOrigin, blockSize, Av1Plane.Y),
                        residual,
                        transformSize,
                        this.bitDepth);
                }
                else
                {
                    TOperator.PrepareFilterIntra(
                        this.blockWorkspace,
                        sourcePlane,
                        blockOrigin,
                        prediction,
                        above,
                        left,
                        residual,
                        selectedFilterIntraMode,
                        transformSize,
                        this.bitDepth);
                }

                for (Av1TransformType transformType = Av1TransformType.DctDct;
                    transformType < Av1TransformType.AllTransformTypes;
                    transformType++)
                {
                    if (!transformType.IsExtendedSetUsed(transformSetType))
                    {
                        continue;
                    }

                    Av1EncoderTransformBlockState candidateState = default;
                    Av1RateDistortionStatistics candidateStatistics = selectedFilterIntraMode == Av1FilterIntraMode.AllFilterIntraModes
                        ? this.GetLumaCandidateCost(
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
                            ref candidateState)
                        : this.GetFilterIntraCandidateCost(
                            writer,
                            macroBlock,
                            sourcePlane,
                            blockOrigin,
                            prediction,
                            residual,
                            selectedFilterIntraMode,
                            blockSize,
                            transformSize,
                            transformType,
                            blockContext,
                            paletteDisabledCost,
                            largestTransformRate,
                            candidateReconstruction,
                            candidateCoefficients,
                            ref candidateState);

                    if (candidateStatistics.Cost < bestTransformStatistics.Cost)
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

                        bestTransformStatistics = candidateStatistics;
                        selectedTransformSize = transformSize;
                    }
                }
            }

            if (transformSize == Av1TransformSize.Size8x8 &&
                !searchEveryTransformSize &&
                this.picture.Parent.EncodingSpeed < HeifEncodingSpeed.Level8 &&
                this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select &&
                paletteInfo.PaletteSizes[0] == 0)
            {
                Av1RateDistortionStatistics splitStatistics = this.GetUniformLumaCandidateCost(
                    writer,
                    macroBlock,
                    sourcePlane,
                    reconstructionPlane,
                    blockOrigin,
                    Av1BlockSize.Block8x8,
                    Av1TransformSize.Size4x4,
                    tileIndex,
                    bestMode,
                    selectedAngleDelta,
                    selectedFilterIntraMode,
                    0,
                    ReadOnlySpan<ushort>.Empty,
                    0,
                    paletteDisabledCost,
                    transformSizeContext,
                    bestTransformStatistics.Cost,
                    candidateReconstruction,
                    candidateCoefficients,
                    workspace.CandidateTransformBlocks);

                if (splitStatistics.Cost < bestTransformStatistics.Cost)
                {
                    CopyTiledCandidate(
                        candidateReconstruction,
                        candidateCoefficients,
                        workspace.CandidateTransformBlocks,
                        reconstructionPlane,
                        blockOrigin,
                        8,
                        GetCodedTransformExtent(macroBlock, blockSize, Av1TransformSize.Size4x4, 0, 0),
                        Av1TransformSize.Size4x4,
                        retainedCoefficients,
                        retainedStates);

                    bestTransformStatistics = splitStatistics;
                    selectedTransformSize = Av1TransformSize.Size4x4;
                }
            }

            selectedStatistics = bestTransformStatistics;
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
            out Av1RateDistortionStatistics selectedStatistics)
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

            int transformSizeRate = !this.picture.Parent.FrameHeader.CodedLossless &&
                this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select
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
            bool pruneOddAngleDeltas = this.picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level6;
            ReadOnlySpan<sbyte> angleDeltaSearchOrder = pruneOddAngleDeltas
                ? PrunedAngleDeltaSearchOrder
                : AngleDeltaSearchOrder;

            int deltaCount = angleDeltaSearchOrder.Length;
            int directionalModeCount =
                (int)Av1PredictionMode.Directional67Degrees - (int)Av1PredictionMode.Vertical + 1;
            Span<long> directionalCosts = stackalloc long[directionalModeCount * 7];
            directionalCosts.Fill(long.MaxValue);

            // A lossless 4x8 or 8x4 block has multiple 4x4 transforms but carries no angle-delta symbol.
            // Its predictor must therefore use the unadjusted direction, just as the decoder does.
            int candidateCount = blockSize >= Av1BlockSize.Block8x8
                ? baseModeCount + (directionalModeCount * deltaCount)
                : baseModeCount;

            Buffer2DRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Buffer2DRegion<TSample> reconstructionPlane = this.reconstruction.GetPlane(Av1Plane.Y);
            int visibleWidth = blockWidth + (Math.Min(0, macroBlock.ToRightEdge) >> 3);
            int visibleHeight = blockHeight + (Math.Min(0, macroBlock.ToBottomEdge) >> 3);
            byte directionalModeSkipMask = this.GetDirectionalModeSkipMask(
                sourcePlane,
                blockOrigin,
                visibleHeight,
                visibleWidth);

            Av1RateDistortionStatistics bestStatistics = Av1RateDistortionStatistics.Invalid;
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

                    angleDelta = angleDeltaSearchOrder[adjustedIndex % deltaCount];
                    if (pruneOddAngleDeltas && ShouldPruneOddAngleDelta(mode, angleDelta, directionalCosts, bestStatistics.Cost))
                    {
                        continue;
                    }
                }

                if (mode is >= Av1PredictionMode.Vertical and <= Av1PredictionMode.Directional67Degrees &&
                    (directionalModeSkipMask & (1 << ((int)mode - (int)Av1PredictionMode.Vertical))) != 0)
                {
                    continue;
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
                    [],
                    default,
                    candidateReconstruction,
                    candidateCoefficients,
                    candidateStates,
                    topContexts,
                    leftContexts,
                    out int coefficientRate);

                int rate = Av1TileWriter.GetLumaModeCost(
                    writer,
                    macroBlock,
                    blockSize,
                    mode,
                    angleDelta,
                    this.picture.Parent.FrameHeader.IsIntra);

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

                Av1RateDistortionStatistics candidateStatistics = new(this.rateMultiplier, rate, distortion);
                if (mode is >= Av1PredictionMode.Vertical and <= Av1PredictionMode.Directional67Degrees)
                {
                    int directionalIndex = (int)mode - (int)Av1PredictionMode.Vertical;
                    directionalCosts[(directionalIndex * 7) + angleDelta + 3] = candidateStatistics.Cost;
                }

                if (candidateStatistics.Cost < bestStatistics.Cost)
                {
                    CopyTiledCandidate(
                        candidateReconstruction,
                        candidateCoefficients,
                        candidateStates,
                        reconstructionPlane,
                        blockOrigin,
                        blockWidth,
                        GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0),
                        transformSize,
                        retainedCoefficients,
                        retainedStates);

                    bestStatistics = candidateStatistics;
                    bestMode = mode;
                    selectedAngleDelta = angleDelta;
                }
            }

            selectedStatistics = bestStatistics;
            return bestMode;
        }

        private Av1RateDistortionStatistics GetUniformLumaCandidateCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Buffer2DRegion<TSample> sourcePlane,
            Buffer2DRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1TransformSize transformSize,
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
            int blockWidth = blockSize.GetWidth();
            int blockHeight = blockSize.GetHeight();
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int transformSampleCount = transformSize.GetSize2d();
            int transformWidth4x4 = transformSize.Get4x4WideCount();
            int transformHeight4x4 = transformSize.Get4x4HighCount();
            int contextWidth = blockSize.Get4x4WideCount();
            int contextHeight = blockSize.Get4x4HighCount();
            Av1EncoderModeDecisionWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>();

            // Reuse the second candidate plane for one prediction and two transform reconstructions.
            // Their disjoint spans remain live while the first candidate plane accumulates the block mosaic.
            Span<TSample> transformSamples = workspace.GetCandidateReconstruction(1);
            Span<TSample> prediction = transformSamples[..transformSampleCount];
            Span<TSample> candidateTransformReconstruction = transformSamples.Slice(
                transformSampleCount,
                transformSampleCount);

            Span<TSample> bestTransformReconstruction = transformSamples.Slice(
                transformSampleCount * 2,
                transformSampleCount);

            Span<int> transformCoefficientStorage = workspace.GetCandidateCoefficients(1);
            Span<int> candidateTransformCoefficients = transformCoefficientStorage[..transformSampleCount];
            Span<int> bestTransformCoefficients = transformCoefficientStorage.Slice(
                transformSampleCount,
                transformSampleCount);

            Span<short> residual = (paletteSize > 0 ? workspace.Palette.GetResidual(0) : workspace.Residual)[..transformSampleCount];
            Span<byte> contexts = workspace.TransformContexts;
            Span<byte> topContexts = contexts[..contextWidth];
            Span<byte> leftContexts = contexts.Slice(contextWidth, contextHeight);
            Av1NeighborArrayUnit<byte> coefficientNeighbors =
                this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex];

            int topIndex = coefficientNeighbors.GetTopIndex(blockOrigin);
            int leftIndex = coefficientNeighbors.GetLeftIndex(blockOrigin);
            coefficientNeighbors.Top.Slice(topIndex, contextWidth).CopyTo(topContexts);
            coefficientNeighbors.Left.Slice(leftIndex, contextHeight).CopyTo(leftContexts);
            bool useReducedTransformSet = this.picture.Parent.FrameHeader.UseReducedTransformSet;
            Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(
                transformSize,
                useReducedTransformSet);

            // Prediction and transform-size syntax belongs to the coding block. Each residual transform
            // contributes its own coefficient cost; lossless and fixed-size modes do not signal a size choice.
            bool codedLossless = this.picture.Parent.FrameHeader.CodedLossless;
            int rate = !codedLossless && this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select
                ? writer.GetTransformSizeCost(blockSize, transformSize, transformSizeContext)
                : 0;
            if (paletteSize > 0)
            {
                rate += paletteHeaderRate;
            }
            else
            {
                rate += Av1TileWriter.GetLumaModeCost(
                    writer,
                    macroBlock,
                    blockSize,
                    mode,
                    angleDelta,
                    this.picture.Parent.FrameHeader.IsIntra);

                if (mode == Av1PredictionMode.DC)
                {
                    rate += paletteDisabledCost;
                    if (this.picture.Sequence.SequenceHeader.EnableFilterIntra)
                    {
                        rate += writer.GetFilterIntraModeCost(
                            filterIntraMode,
                            blockSize);
                    }
                }
            }

            Buffer2DRegion<byte> colorIndexMap = default;
            if (paletteSize > 0)
            {
                colorIndexMap = this.superblock.Workspace
                    .GetPaletteMaps()
                    .GetMap(Av1PlaneType.Y, blockWidth, blockHeight);
            }

            long distortion = 0;

            // Earlier transforms provide reconstructed edges and coefficient contexts to later transforms.
            // Palette blocks fit within one bounded 64x64 luma region, so their transform order is raster order.
            Size codedExtent = GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0);
            int transformColumnCount = codedExtent.Width / transformWidth;
            int transformRowCount = codedExtent.Height / transformHeight;
            for (int transformRow = 0; transformRow < transformRowCount; transformRow++)
            {
                for (int transformColumn = 0; transformColumn < transformColumnCount; transformColumn++)
                {
                    int transformIndex = (transformRow * transformColumnCount) + transformColumn;
                    int reconstructionOffset =
                        (transformRow * transformHeight * blockWidth) + (transformColumn * transformWidth);

                    Point transformOrigin = blockOrigin + new Size(
                        transformColumn * transformWidth,
                        transformRow * transformHeight);

                    if (paletteSize > 0)
                    {
                        // Palette prediction is block-local. A view over the retained map avoids copying indices or
                        // preparing reconstructed neighbor edges that this prediction mode cannot consume.
                        TOperator.PreparePalette(
                            sourcePlane,
                            transformOrigin,
                            paletteColors,
                            colorIndexMap.GetSubRegion(
                                transformColumn * transformWidth,
                                transformRow * transformHeight,
                                transformWidth,
                                transformHeight),
                            prediction,
                            residual,
                            transformSize);
                    }
                    else
                    {
                        // The parent mode search retains reference slots 0 and 1. Use the other pair here
                        // because these transform edges also include earlier reconstructions in this candidate.
                        Span<TSample> aboveStorage = workspace.GetReferenceSamples(2);
                        Span<TSample> leftStorage = workspace.GetReferenceSamples(3);
                        this.PrepareTransformReferenceSamples(
                            reconstructionPlane,
                            blockOrigin,
                            blockOrigin,
                            blockSize,
                            macroBlock,
                            transformRow,
                            transformColumn,
                            blockWidth,
                            transformSize,
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
                                transformSize.GetWidth(),
                                aboveStorage.Slice(1, transformWidth + transformHeight),
                                leftStorage.Slice(1, transformWidth + transformHeight),
                                hasLeft,
                                hasAbove,
                                mode,
                                angleDelta,
                                this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                                this.UseSmoothIntraEdges(macroBlock, blockOrigin, blockSize, Av1Plane.Y),
                                residual,
                                transformSize,
                                this.bitDepth);
                        }
                        else
                        {
                            // Filter-intra prediction is recursive within each transform unit, so rebuild it from
                            // the reconstructed edges established by preceding transforms.
                            TOperator.PrepareFilterIntra(
                                this.blockWorkspace,
                                sourcePlane,
                                transformOrigin,
                                prediction,
                                aboveStorage.Slice(1, transformWidth + transformHeight),
                                leftStorage.Slice(1, transformWidth + transformHeight),
                                residual,
                                filterIntraMode,
                                transformSize,
                                this.bitDepth);
                        }
                    }

                    Av1TransformBlockContext blockContext = Av1TileWriter.GetTransformBlockContexts(
                        Av1ComponentType.Luminance,
                        topContexts.Slice(transformColumn * transformWidth4x4, transformWidth4x4),
                        leftContexts.Slice(transformRow * transformHeight4x4, transformHeight4x4),
                        blockSize,
                        transformSize);

                    long bestTransformCost = long.MaxValue;
                    Av1TransformType bestTransformType = Av1TransformType.DctDct;
                    int bestTransformRate = 0;
                    long bestTransformDistortion = 0;
                    Av1EncoderTransformBlockState bestTransformState = default;
                    Span<int> retainedTransformCoefficients = candidateCoefficients.Slice(
                        transformIndex * transformSampleCount,
                        transformSampleCount);

                    // Each transform writes into the compact buffer that does not hold the current best.
                    // Swapping spans on improvement keeps the winner without copying it inside the search loop.
                    Av1TransformType transformTypeLimit = codedLossless
                        ? Av1TransformType.AdstDct
                        : Av1TransformType.AllTransformTypes;

                    for (Av1TransformType transformType = Av1TransformType.DctDct;
                        transformType < transformTypeLimit;
                        transformType++)
                    {
                        if (!transformType.IsExtendedSetUsed(transformSetType))
                        {
                            continue;
                        }

                        Av1EncoderTransformBlockState candidateState = default;
                        long candidateDistortion = TOperator.EncodePredictionCandidate(
                            this.blockWorkspace,
                            writer,
                            blockContext,
                            this.rateMultiplier,
                            false,
                            this.picture.Sequence.SequenceHeader.IsStillPicture,
                            sourcePlane,
                            transformOrigin,
                            prediction,
                            residual,
                            candidateTransformReconstruction,
                            transformWidth,
                            candidateTransformCoefficients,
                            transformSize,
                            transformType,
                            Av1Plane.Y,
                            this.quantization.QIndex[0],
                            this.quantization.DeltaQDc[(int)Av1Plane.Y],
                            this.quantization.DeltaQAc[(int)Av1Plane.Y],
                            this.bitDepth,
                            ref candidateState);

                        int candidateRate = writer.GetCoefficientCost(
                            transformSize,
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

                    // Publish the winner once after transform search. Later transforms consume its pixels
                    // from the block mosaic and its coefficient context from the local edge arrays.
                    bestTransformCoefficients.CopyTo(retainedTransformCoefficients);
                    for (int row = 0; row < transformHeight; row++)
                    {
                        bestTransformReconstruction.Slice(row * transformWidth, transformWidth)
                            .CopyTo(
                                candidateReconstruction.Slice(
                                    reconstructionOffset + (row * blockWidth),
                                    transformWidth));
                    }

                    rate += bestTransformRate;
                    distortion += bestTransformDistortion;
                    candidateTransformBlocks[transformIndex] = bestTransformState;
                    byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                        retainedTransformCoefficients,
                        transformSize,
                        bestTransformType,
                        bestTransformState.EndOfBlock);

                    topContexts.Slice(transformColumn * transformWidth4x4, transformWidth4x4).Fill(coefficientContext);
                    leftContexts.Slice(transformRow * transformHeight4x4, transformHeight4x4).Fill(coefficientContext);

                    // Every remaining transform can only add nonnegative rate and distortion.
                    if (Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion) >= costLimit)
                    {
                        return Av1RateDistortionStatistics.Invalid;
                    }
                }
            }

            return new(this.rateMultiplier, rate, distortion);
        }

        private void PrepareTransformReferenceSamples(
            Buffer2DRegion<TSample> reconstructionPlane,
            Point lumaBlockOrigin,
            Point planeBlockOrigin,
            Av1BlockSize blockSize,
            Av1MacroBlockD macroBlock,
            int transformRow,
            int transformColumn,
            int candidateStride,
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

            // Internal edges read the supplied surface at its own stride. Trials supply their isolated mosaic;
            // selected-mode reconstruction supplies the frame containing earlier transforms.
            hasAbove = transformRow > 0 || macroBlock.IsUpAvailable;
            hasLeft = transformColumn > 0 || macroBlock.IsLeftAvailable;
            if (transformColumn == 0 && subsamplingX != 0 && blockSize.Get4x4WideCount() < 2)
            {
                hasLeft = modeInfoColumn - 1 > macroBlock.Tile.ModeInfoColumnStart;
            }

            if (transformRow == 0 && subsamplingY != 0 && blockSize.Get4x4HighCount() < 2)
            {
                hasAbove = modeInfoRow - 1 > macroBlock.Tile.ModeInfoRowStart;
            }

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

            Av1PartitionType partitionType = macroBlock.GetRelativeModeInfo(0).Block.PartitionType;
            bool hasTopRight = Av1IntraReferenceAvailability.HasTopRight(
                this.picture.Sequence.SequenceHeader.SuperblockSize,
                blockSize,
                modeInfoRow,
                modeInfoColumn,
                hasAbove,
                rightAvailable,
                partitionType,
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
                partitionType,
                transformSize,
                transformRow4x4,
                transformColumn4x4,
                subsamplingX,
                subsamplingY);

            // Reference availability ends at the coded frame edge, even when a transform reaches into
            // padded storage. Extend the final available sample instead of reading padding as a neighbor.
            Av1BlockSize planeBlockSize = blockSize.GetSubsampled(subsamplingX != 0, subsamplingY != 0);
            int remainingWidth = planeBlockSize.GetWidth() +
                (macroBlock.ToRightEdge >> (3 + subsamplingX)) - columnOffset;

            int remainingHeight = planeBlockSize.GetHeight() +
                (macroBlock.ToBottomEdge >> (3 + subsamplingY)) - rowOffset;

            Span<TSample> above = aboveStorage.Slice(1, transformWidth + transformHeight);
            Span<TSample> left = leftStorage.Slice(1, transformWidth + transformHeight);
            int topCount = hasAbove ? Math.Min(transformWidth, remainingWidth) : 0;
            int leftCount = hasLeft ? Math.Min(transformHeight, remainingHeight) : 0;
            if (hasAbove)
            {
                if (transformRow > 0)
                {
                    candidateReconstruction
                        .Slice(((rowOffset - 1) * candidateStride) + columnOffset, topCount)
                        .CopyTo(above);
                }
                else
                {
                    reconstructionPlane.DangerousGetRowSpan(planeBlockOrigin.Y - 1)
                        .Slice(planeBlockOrigin.X + columnOffset, topCount)
                        .CopyTo(above);
                }

                int topRightCount = hasTopRight
                    ? Math.Min(Math.Min(transformWidth, transformHeight), remainingWidth - transformWidth)
                    : 0;

                if (topRightCount > 0)
                {
                    if (transformRow > 0)
                    {
                        candidateReconstruction
                            .Slice(((rowOffset - 1) * candidateStride) + columnOffset + transformWidth, topRightCount)
                            .CopyTo(above[transformWidth..]);
                    }
                    else
                    {
                        reconstructionPlane.DangerousGetRowSpan(planeBlockOrigin.Y - 1)
                            .Slice(planeBlockOrigin.X + columnOffset + transformWidth, topRightCount)
                            .CopyTo(above[transformWidth..]);
                    }

                    topCount += topRightCount;
                }

                above[topCount..].Fill(above[topCount - 1]);
            }

            if (hasLeft)
            {
                int bottomLeftCount = hasBottomLeft
                    ? Math.Min(Math.Min(transformHeight, transformWidth), remainingHeight - transformHeight)
                    : 0;

                if (bottomLeftCount > 0)
                {
                    leftCount += bottomLeftCount;
                }

                if (transformColumn > 0)
                {
                    for (int row = 0; row < leftCount; row++)
                    {
                        left[row] = candidateReconstruction[
                            ((rowOffset + row) * candidateStride) + columnOffset - 1];
                    }
                }
                else
                {
                    for (int row = 0; row < leftCount; row++)
                    {
                        left[row] = reconstructionPlane
                            .DangerousGetRowSpan(planeBlockOrigin.Y + rowOffset + row)[planeBlockOrigin.X - 1];
                    }
                }

                left[leftCount..].Fill(left[leftCount - 1]);
            }

            int midpoint = 128 << (this.bitDepth.GetBitCount() - 8);
            if (!hasAbove)
            {
                above.Fill(hasLeft ? left[0] : TOperator.CreateSample(midpoint - 1));
            }

            if (!hasLeft)
            {
                left.Fill(hasAbove ? above[0] : TOperator.CreateSample(midpoint + 1));
            }

            // Only an interior transform corner belongs to decision scratch. Boundary corners continue
            // to read the already reconstructed neighboring block so candidate trials remain isolated.
            TSample corner = hasAbove && hasLeft
                ? transformRow > 0 && transformColumn > 0
                    ? candidateReconstruction[((rowOffset - 1) * candidateStride) + columnOffset - 1]
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

        private Av1RateDistortionStatistics GetLumaCandidateCost(
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
                writer,
                blockContext,
                this.rateMultiplier,
                false,
                this.picture.Sequence.SequenceHeader.IsStillPicture,
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
            int rate = Av1TileWriter.GetLumaModeCost(
                writer,
                macroBlock,
                blockSize,
                mode,
                angleDelta,
                this.picture.Parent.FrameHeader.IsIntra);

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

            return new(this.rateMultiplier, rate, distortion);
        }

        private Av1RateDistortionStatistics GetFilterIntraCandidateCost(
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
                writer,
                blockContext,
                this.rateMultiplier,
                false,
                this.picture.Sequence.SequenceHeader.IsStillPicture,
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
                0,
                this.picture.Parent.FrameHeader.IsIntra);

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

            return new(this.rateMultiplier, rate, distortion);
        }

        private static Size GetCodedTransformExtent(
            Av1MacroBlockD macroBlock,
            Av1BlockSize planeBlockSize,
            Av1TransformSize transformSize,
            int subsamplingX,
            int subsamplingY)
        {
            int width = planeBlockSize.GetWidth();
            int height = planeBlockSize.GetHeight();
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();

            // A transform that intersects the coded frame is encoded in full. Only transforms wholly
            // in the padded border are omitted; the coefficient stream packs the remaining transforms.
            width += Math.Min(0, macroBlock.ToRightEdge >> (3 + subsamplingX));
            height += Math.Min(0, macroBlock.ToBottomEdge >> (3 + subsamplingY));
            width &= ~((1 << Av1Constants.ModeInfoSizeLog2) - 1);
            height &= ~((1 << Av1Constants.ModeInfoSizeLog2) - 1);
            return new Size(
                (width + transformWidth - 1) & -transformWidth,
                (height + transformHeight - 1) & -transformHeight);
        }

        /// <summary>
        /// Determines whether an odd directional angle can be rejected from its neighboring even-angle costs.
        /// </summary>
        private static bool ShouldPruneOddAngleDelta(
            Av1PredictionMode mode,
            int angleDelta,
            ReadOnlySpan<long> directionalCosts,
            long bestCost)
        {
            if ((Math.Abs(angleDelta) & 1) == 0 || bestCost == long.MaxValue)
            {
                return false;
            }

            int directionalIndex = (int)mode - (int)Av1PredictionMode.Vertical;
            int costIndex = (directionalIndex * 7) + angleDelta + 3;
            long threshold = bestCost + (bestCost >> 3);
            long lowerCost = angleDelta == -3 ? long.MaxValue : directionalCosts[costIndex - 1];
            long upperCost = angleDelta == 3 ? long.MaxValue : directionalCosts[costIndex + 1];
            return lowerCost > threshold && upperCost > threshold;
        }

        private static bool ShouldPruneIntraModel(
            long modelCost,
            Av1PredictionMode mode,
            Av1MacroBlockD macroBlock,
            int qIndex,
            Span<long> topModelCosts,
            int topModelCount,
            ref long bestModelCost)
        {
            for (int index = 0; index < topModelCount; index++)
            {
                if (modelCost >= topModelCosts[index])
                {
                    continue;
                }

                for (int destination = topModelCount - 1; destination > index; destination--)
                {
                    topModelCosts[destination] = topModelCosts[destination - 1];
                }

                topModelCosts[index] = modelCost;
                break;
            }

            int pruningIndex = topModelCount - 1;
            if (topModelCount == 2)
            {
                bool leftDiffers = macroBlock.IsLeftAvailable &&
                    macroBlock.GetRelativeModeInfo(-1).Block.Mode != mode;
                bool aboveDiffers = macroBlock.IsUpAvailable &&
                    macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block.Mode != mode;
                if ((qIndex <= 127 && (leftDiffers || aboveDiffers)) ||
                    (qIndex > 127 && leftDiffers && aboveDiffers))
                {
                    pruningIndex = 0;
                }
            }

            if (topModelCosts[pruningIndex] != long.MaxValue && modelCost > topModelCosts[pruningIndex])
            {
                return true;
            }

            if (bestModelCost != long.MaxValue && modelCost > bestModelCost + (bestModelCost >> 1))
            {
                return true;
            }

            bestModelCost = Math.Min(bestModelCost, modelCost);
            return false;
        }

        private static bool IsFilterIntraModeDerivedFromBestMode(
            Av1FilterIntraMode filterIntraMode,
            Av1PredictionMode bestMode)
            => filterIntraMode == Av1FilterIntraMode.DC ||
                (bestMode switch
                {
                    Av1PredictionMode.Vertical => Av1FilterIntraMode.Vertical,
                    Av1PredictionMode.Horizontal => Av1FilterIntraMode.Horizontal,
                    Av1PredictionMode.Directional157Degrees => Av1FilterIntraMode.Directional157,
                    Av1PredictionMode.Paeth => Av1FilterIntraMode.Paeth,
                    _ => Av1FilterIntraMode.DC
                }) == filterIntraMode;

        /// <summary>
        /// Builds the directional-mode skip mask selected by libaom's all-intra speed policy.
        /// </summary>
        /// <param name="sourcePlane">The source luma plane.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="visibleHeight">The number of source rows inside the coded image.</param>
        /// <param name="visibleWidth">The number of source columns inside the coded image.</param>
        /// <returns>The bit mask for the eight directional modes, or zero when HOG pruning is disabled.</returns>
        private byte GetDirectionalModeSkipMask(
            Buffer2DRegion<TSample> sourcePlane,
            Point blockOrigin,
            int visibleHeight,
            int visibleWidth)
        {
            int speed = (int)this.picture.Parent.EncodingSpeed;
            if (visibleWidth < 3 || visibleHeight < 3)
            {
                return 0;
            }

            // These all-intra thresholds are indexed by libaom's HOG pruning levels. Levels one and two
            // deliberately share -1.2; speed six raises the threshold to 0.4 and removes more directions.
            float threshold = speed >= 6 ? 0.4F : speed >= 3 ? -0.6F : -1.2F;
            return GetDirectionalModeSkipMask(
                sourcePlane,
                blockOrigin,
                visibleHeight,
                visibleWidth,
                threshold);
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
            Span<TSample> destination = Av1TransformBlockEncoder.GetPlaneSpan(reconstructionPlane, blockOrigin);
            for (int row = 0; row < height; row++)
            {
                candidateReconstruction.Slice(row * width, width)
                    .CopyTo(destination.Slice(row * reconstructionPlane.Stride, width));
            }

            retainedState = candidateState;
        }

        private void ReconstructSelectedTransform(
            Av1SymbolEncoder writer,
            Av1TransformBlockContext context,
            bool isInter,
            Point planeOrigin,
            Av1Plane plane,
            Av1TransformSize transformSize,
            ReadOnlySpan<TSample> prediction,
            ReadOnlySpan<short> residual,
            Av1EncoderTransformBlockState selectedState,
            bool skipTransform,
            int coefficientOffset)
        {
            Buffer2DRegion<TSample> destinationPlane = this.reconstruction.GetPlane(plane);
            Span<TSample> destination = Av1TransformBlockEncoder.GetPlaneSpan(destinationPlane, planeOrigin);
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            for (int row = 0; row < height; row++)
            {
                prediction.Slice(row * width, width).CopyTo(destination.Slice(row * destinationPlane.Stride, width));
            }

            Span<int> coefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, plane)
                .Slice(coefficientOffset, transformSize.GetSize2d());

            ref Av1EncoderTransformBlockState state = ref this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, plane)[
                coefficientOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];

            state = default;
            state.EntropyContext = selectedState.EntropyContext;
            if (skipTransform || selectedState.EndOfBlock == 0)
            {
                // Empty selected transforms must retain prediction. Re-quantizing an inferred transform
                // could otherwise introduce residual coefficients that were absent from the winning mode.
                coefficients.Clear();
                return;
            }

            // Mode and transform decisions are already fixed. Generate their coefficients and add the
            // inverse transform directly to the frame, without another distortion scan or candidate copy.
            int planeIndex = (int)plane;
            Av1TransformBlockEncoder.EncodeLossyCandidate(
                this.blockWorkspace,
                writer,
                context,
                residual,
                transformSize.GetWidth(),
                coefficients,
                transformSize,
                selectedState.TransformType,
                this.quantization.QIndex[0],
                this.quantization.DeltaQDc[planeIndex],
                this.quantization.DeltaQAc[planeIndex],
                this.bitDepth,
                plane == Av1Plane.Y ? Av1ComponentType.Luminance : Av1ComponentType.Chroma,
                this.rateMultiplier,
                isInter,
                this.picture.Sequence.SequenceHeader.IsStillPicture,
                true,
                ref state);

            if (state.EndOfBlock > 0)
            {
                TOperator.AddSelectedResidual(
                    this.blockWorkspace,
                    destination,
                    destinationPlane.Stride,
                    transformSize,
                    plane,
                    this.bitDepth,
                    this.quantization.QIndex[0] == 0,
                    state);
            }
        }
    }
}
