// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;
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
    /// The position of the split candidate in <see cref="PartitionSearchOrder"/>.
    /// </summary>
    private const int SplitSearchOrderIndex = 1;

    /// <summary>
    /// Gets the zero-angle luma mode evaluation order.
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
    /// Gets the complete nonzero directional adjustment order.
    /// </summary>
    private static ReadOnlySpan<sbyte> AngleDeltaSearchOrder => [-3, -2, -1, 1, 2, 3];

    /// <summary>
    /// Gets the nonzero directional adjustments in the order required for neighboring-cost pruning.
    /// </summary>
    private static ReadOnlySpan<sbyte> PrunedAngleDeltaSearchOrder => [-2, 2, -3, -1, 1, 3];

    /// <summary>
    /// Gets the partition candidate evaluation order.
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
    /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
    /// <param name="superblock">The reusable partition and final-block decisions.</param>
    /// <param name="superblockOrigin">The absolute luma-sample origin of the superblock.</param>
    public static void Prepare(Av1PictureControlSet picture, Span<Av1MacroBlockModeInfo> modeInfoAllocation, Av1Superblock superblock, Point superblockOrigin)
    {
        superblock.Workspace.Reset();
        int partitionIndex = 0;
        PreparePartitionTree(picture, modeInfoAllocation, superblock, superblockOrigin, picture.Sequence.SequenceHeader.SuperblockSize, ref partitionIndex);
    }

    /// <summary>
    /// Splits a block of the superblock down to 8x8 blocks in coding order, and resets the mode information of each
    /// 8x8 block inside the frame.
    /// </summary>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
    /// <param name="superblock">The reusable partition and final-block decisions.</param>
    /// <param name="blockOrigin">The absolute luma-sample origin of the block.</param>
    /// <param name="blockSize">The block size.</param>
    /// <param name="partitionIndex">The next partition entry of the superblock, advanced for each block.</param>
    private static void PreparePartitionTree(
        Av1PictureControlSet picture,
        Span<Av1MacroBlockModeInfo> modeInfoAllocation,
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
            ref Av1MacroBlockModeInfo modeInfo = ref picture.GetMacroBlockModeInfo(modeInfoAllocation, modeInfoPosition);
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
        PreparePartitionTree(picture, modeInfoAllocation, superblock, blockOrigin, subSize, ref partitionIndex);
        PreparePartitionTree(picture, modeInfoAllocation, superblock, blockOrigin + new Size(halfBlockSize, 0), subSize, ref partitionIndex);
        PreparePartitionTree(picture, modeInfoAllocation, superblock, blockOrigin + new Size(0, halfBlockSize), subSize, ref partitionIndex);
        PreparePartitionTree(picture, modeInfoAllocation, superblock, blockOrigin + new Size(halfBlockSize, halfBlockSize), subSize, ref partitionIndex);
    }

    /// <summary>
    /// Produces one final block at a time against the tile state immediately preceding its syntax.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The type-specific block encoding operations.</typeparam>
    internal partial struct ModeDecision<TSample, TOperator> : Av1TileWriter.IBlockEncodingHandler<TSample>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        /// <summary>
        /// The number of combined reference types: the single references and every compound pair.
        /// </summary>
        private const int ModeContextReferenceFrameCount = Av1Constants.ReferenceFrameCount + (4 * 3) + 9;

        /// <summary>
        /// The leaf bound that stands for a bound that is already negative at the multiplier of the caller. With this bound, the leaf
        /// search returns before it sets up the block.
        /// </summary>
        private const long NegativeLeafBound = long.MinValue;

        private readonly Av1EncoderFrame<TSample>.PlanarView source;
        private readonly ReadOnlyMemory<Av1EncoderFrame<TSample>> references;

        /// <summary>
        /// The frames that the motion search reads, indexed by reference type. Each entry is the reference, or its copy resized to the
        /// size of the coded frame when the reference has another size.
        /// </summary>
        private readonly ReadOnlyMemory<Av1EncoderFrame<TSample>> searchReferences;

        /// <summary>
        /// One bit for each available reference type whose frame has another size than the current frame. The prediction from such a
        /// reference scales, and the motion search reads it through its resized copy.
        /// </summary>
        private readonly int scaledReferenceMask;
        private readonly Av1EncoderFrame<TSample>.PlanarView reference;
        private readonly Av1ReferenceFrameType simpleMotionReference;
        private readonly Av1EncoderFrame<TSample>.PlanarView goldenReference;
        private readonly bool hasDistinctGoldenReference;
        private readonly Av1EncoderFrame<TSample>.PlanarView reconstruction;
        private readonly Av1PictureControlSet picture;
        private readonly Av1Superblock superblock;
        private readonly Av1EncoderCoefficientBuffer coefficientBuffer;
        private readonly Av1EncoderBlockWorkspace blockWorkspace;
        private readonly ObuQuantizationParameters quantization;
        private readonly Av1BitDepth bitDepth;
        private readonly int baseRateMultiplier;
        private readonly int rateMultiplierModifier;

        // The quantizer of the superblock: the frame quantizer moved by any delta quantizer.
        private readonly int superblockQIndex;

        // The quantizer of the current block or partition node: the superblock quantizer moved by the segment of the last block setup.
        private int blockQIndex;

        // The segment of the block being searched.
        private int blockSegmentId;

        // The kind of encode that a block of the estimated inter search represents. It decides the cyclic refresh update of the block.
        private EstimatedLeafEncode estimatedLeafEncode;

        // The cost of the unsplit block that a split trial child of a merge trial compares against.
        private long mergeNoneCost;

        // The split cost of the children before the current split trial child.
        private Av1RateDistortionStatistics mergeSplitStatistics;

        // The position of the current split trial child, from 0 to 3.
        private int mergeChildIndex;

        // The multiplier that the merge trial prices its costs with.
        private int mergeRateMultiplier;

        /// <summary>
        /// The prediction error of the best new vector of each single reference in the block being searched, or
        /// <see cref="int.MaxValue"/> before one is found.
        /// </summary>
        private InlineArray8<uint> bestSingleReferenceSses;

        /// <summary>
        /// The luma prediction error that the last model or fractional search of each first reference left in the block being searched,
        /// or <see cref="int.MaxValue"/> before one. A forced integer vector skips the fractional search and reads the value that an
        /// earlier candidate left.
        /// </summary>
        private InlineArray8<uint> predictionSses;

        /// <summary>
        /// The best estimate of the mode loop when the transform search of the kept candidates found no mode below the block budget, or
        /// invalid otherwise. The skip mode comparison still reads it.
        /// </summary>
        private Av1RateDistortionStatistics leftoverInterEstimate;

        /// <summary>
        /// The luma cost that an intra mode of an inter frame must beat. This is the luma cost of the inter winner. After the transform
        /// search of the kept candidates, it is the luma cost of the cheapest candidate that the search completed, even one above the
        /// block budget.
        /// </summary>
        private long interLumaThreshold;
        private int rateMultiplier;
        private int codedAreaLuma;
        private int codedAreaChroma;
        private int replayNodeIndex;
        private Av1PartitionType replayPartition;
        private Point replayPartitionOrigin;
        private Av1BlockSize replayParentSize;
        private InlineArray3<LumaCandidate> lumaCandidates;
        private int lumaCandidateCount;
        private InlineArray16<long> interTransformNoSplitCosts;
        private bool estimateInterCandidates;
        private bool lumaSearchFailed;

        // The cost of the last completed inter trial before the image tune bias.
        private long unbiasedInterTrialCost;

        // The sharpness 3 distortion offset of the luma samples that the intra luma search of an inter frame leaves for its chroma
        // search. The chroma search adds it to its own cost.
        private long intraSmoothingOffset;
        private int interCandidateCount;
        private int compoundSearchRecordCount;
        private int interpolationSearchRecordCount;
        private long bestInterEstimate;
        private long bestInterPredictionCost;
        private long bestInterLumaPredictionCost;
        private Av1GlobalMotionParameters warpedModel;
        private bool useWarpedPrediction;

        /// <summary>
        /// Set while the estimated real-time search predicts each reference of another size from its copy resized to the frame size,
        /// without scaling. The selected block then predicts from the original reference.
        /// </summary>
        private bool predictsFromSearchReferences;

        // Set while the candidate predicts with OBMC, with the block and its neighbor availability.
        private bool useObmcPrediction;
        private Point obmcBlockOrigin;

        /// <summary>
        /// The reference of the current OBMC motion search, whose fractional candidates scale when the reference has
        /// another size than the frame.
        /// </summary>
        private Av1ReferenceFrameType obmcSearchReference;
        private Av1BlockSize obmcBlockSize;
        private bool obmcAboveAvailable;
        private bool obmcLeftAvailable;
        private InlineArray10<Av1MotionModeWinner> motionModeWinners;
        private int motionModeWinnerCount;
        private int motionModeWinnerLimit;
        private bool evaluatingMotionModeWinners;
        private InlineArray8<uint> interModeSkipMasks;

        // The skip flag that the last completed transform search of the inter mode search leaves. The final encode of an inter winner
        // adds it to the flags of the winner.
        private bool transformSearchSkip;

        // Whether a motion mode trial of the current mode search reached its transform search. Each such trial clears the leftover skip
        // flag before it searches.
        private bool transformSearchReset;

        // The reference types that a rectangular block does not search, one bit per reference type.
        private int skipReferenceFrameMask;

        // The predicted-vector SAD of each reference, and the best of the references that precede and follow the frame.
        private InlineArray8<int> predictionVectorSads;
        private int bestPastPredictionVectorSad;
        private int bestFuturePredictionVectorSad;
        private bool searchingRetainedCandidates;
        private long interSourceVarianceCost;
        private int interSourceVariance;
        private bool mustFindValidPartition;

        // The selected reference-coded block kept no coefficient. The frame grid marks it skipped, but the partition context keeps the
        // searched flag.
        private bool encodedWithoutCoefficients;

        // Set while a leaf is reconstructed for the partition search. The mode search keeps the types that it searched. Only an encode
        // of the decision writes DCT_DCT for a luma block that quantized to nothing.
        private bool keepSearchedZeroBlockTypes;
        private long blockCostLimit;
        private Av1BlockSize maximumPartitionSize;

        private InlineArray3<Av1AsymmetricModeCacheEntry> asymmetricModeCache;

        private Av1AsymmetricModeCacheEntry activeModeCache;
        private bool intraPartitionFeaturesValid;
        private float intraPartitionLogQuantizer;
        private readonly Av1SourceSadLevel sourceSadLevel;
        private readonly bool sourceLightingChange;
        private readonly bool sourceLowSumDifference;
        private readonly bool filterTemporalSource;
        private Av1MotionVector partitionMotion;
        private Av1MotionVector superblockMotion;
        private Av1ReferenceFrameType partitionReference;
        private bool usePartitionMotion;

        /// <summary>
        /// The inter-prediction buffer that holds the luma prediction the estimated search built last, or -1.
        /// </summary>
        private int lastLumaPredictionBuffer = -1;

        /// <summary>
        /// The color sensitivity of the block searched last, after its block-level check. It starts from the superblock sensitivity.
        /// </summary>
        private InlineArray2<byte> blockColorSensitivity;
        private int estimatedReferencePruning;
        private InlineArray2<byte> superblockColorSensitivity;

        // Whether each 64x64 unit of the superblock still leaves CDEF off, in raster order. The superblock sets the flags, and each
        // block search can clear them.
        private InlineArray4<bool> cdefSkipUnits;
        private InlineArray2<byte> goldenColorSensitivity;
        private InlineArray2<byte> alternateColorSensitivity;

        /// <summary>
        /// Whether the temporal dependency model keeps each reference type, INTRA to ALTREF, from the selective reference pruning in the
        /// current superblock.
        /// </summary>
        private InlineArray8<bool> tplKeepReferenceFrames;

        /// <summary>
        /// The number of 16x16 model blocks of the superblock inside the frame whose costs and vectors the block workspace holds, or zero
        /// without them.
        /// </summary>
        private int tplSuperblockBlockCount;

        /// <summary>
        /// The number of model blocks per superblock row of the gathered costs and vectors.
        /// </summary>
        private int tplSuperblockStride;

        /// <summary>
        /// Whether the inter mode search of the current block skips modes by the reference costs of the model.
        /// </summary>
        private bool tplInterModePruning;

        /// <summary>
        /// The model prediction error of each reference LAST to ALTREF, summed over the current block.
        /// </summary>
        private InlineArray7<long> tplReferenceInterCosts;

        /// <summary>
        /// The smallest nonzero entry of <see cref="tplReferenceInterCosts"/> among the references that the selective pruning keeps.
        /// </summary>
        private long tplBestInterCost;

        /// <summary>
        /// Whether the mode decision workspace holds the luma gradients of the current superblock.
        /// </summary>
        private bool lumaGradientsCached;

        /// <summary>
        /// Whether the mode decision workspace holds the blue-difference gradients of the current superblock.
        /// </summary>
        private bool chromaGradientsCached;

        /// <summary>
        /// Whether <see cref="sourceVarianceValue"/> holds the source variance of the block at
        /// <see cref="sourceVarianceOrigin"/> with size <see cref="sourceVarianceSize"/>.
        /// </summary>
        private bool sourceVarianceValid;

        /// <summary>
        /// The luma origin of the block whose source variance is kept.
        /// </summary>
        private Point sourceVarianceOrigin;

        /// <summary>
        /// The size of the block whose source variance is kept.
        /// </summary>
        private Av1BlockSize sourceVarianceSize;

        /// <summary>
        /// The per-sample source variance of the last measured block. The source does not change during the encode, so
        /// every later request for the same block reads it here instead of measuring the block again.
        /// </summary>
        private int sourceVarianceValue;

        /// <summary>
        /// The luma origin of the current superblock, which locates a block in the gradient cache.
        /// </summary>
        private Point gradientSuperblockOrigin;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModeDecision{TSample, TOperator}"/> struct.
        /// </summary>
        /// <param name="source">The coded source frame.</param>
        /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
        /// <param name="searchReferences">
        /// The frames the motion search reads, indexed by prediction reference identifier: each reference, or its
        /// copy resized to the size of the coded frame.
        /// </param>
        /// <param name="reconstruction">The reconstructed frame updated by winning candidates.</param>
        /// <param name="picture">The frame coding and mode-information state.</param>
        /// <param name="superblock">The current superblock.</param>
        /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
        /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
        public ModeDecision(
            Av1EncoderFrame<TSample> source,
            ReadOnlyMemory<Av1EncoderFrame<TSample>> references,
            ReadOnlyMemory<Av1EncoderFrame<TSample>> searchReferences,
            Av1EncoderFrame<TSample> reconstruction,
            Av1PictureControlSet picture,
            Av1Superblock superblock,
            Av1EncoderCoefficientBuffer coefficientBuffer,
            Av1EncoderBlockWorkspace blockWorkspace)
        {
            this.source = source.CodedView;
            this.references = references;
            this.searchReferences = searchReferences;
            this.scaledReferenceMask = GetScaledReferenceMask(references.Span, picture.Parent);

            // The simple motion searches read ALTREF in a frame coded from its source, and LAST otherwise. These searches, the variance
            // partition and the estimated real-time search read a resized reference through its copy at the frame size.
            this.simpleMotionReference = picture.Parent.IsSourceAlternateReference ? Av1ReferenceFrameType.Alternate : Av1ReferenceFrameType.Last;
            this.reference = picture.Parent.FrameHeader.IsIntra ? reconstruction.CodedView : searchReferences.Span[(int)this.simpleMotionReference].CodedView;
            this.goldenReference = picture.Parent.FrameHeader.IsIntra ? reconstruction.CodedView : searchReferences.Span[(int)Av1ReferenceFrameType.Golden].CodedView;
            this.hasDistinctGoldenReference = (picture.Parent.AvailableReferenceMask & (1 << (int)Av1ReferenceFrameType.Golden)) != 0;
            this.reconstruction = reconstruction.CodedView;
            this.picture = picture;
            this.superblock = superblock;
            this.coefficientBuffer = coefficientBuffer;
            this.blockWorkspace = blockWorkspace;
            blockWorkspace.SpeedSettings = picture.Parent.SpeedSettings;
            blockWorkspace.EncoderOptions = picture.Parent.EncoderOptions;
            blockWorkspace.SetQuantizationMatrixLevels(picture.Parent.FrameHeader.QuantizationParameters, picture.Parent.FrameHeader.LosslessArray[0]);

            // A partition never exceeds its superblock, so the speed cap is limited to the superblock size before anything reads it.
            this.maximumPartitionSize = (Av1BlockSize)Math.Min(
                (int)picture.Parent.SpeedSettings.MaximumPartitionSize,
                (int)picture.Sequence.SequenceHeader.SuperblockSize);

            this.blockCostLimit = long.MaxValue;

            // Each superblock starts without a block variance. The value -1 reads as the largest unsigned value until a block search
            // measures one.
            this.interSourceVariance = -1;
            blockWorkspace.SourceLogVariances.Fill(-1D);
            blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Default;
            this.quantization = picture.Parent.FrameHeader.QuantizationParameters;
            this.bitDepth = picture.Sequence.SequenceHeader.ColorConfig.BitDepth;

            // A delta quantizer mode picks the superblock quantizer, rounded to the delta resolution against the previous coded
            // superblock of the tile. The full search also measures its rate multiplier at that quantizer. The estimated search keeps the
            // frame multiplier.
            Av1PictureParentControlSet parent = picture.Parent;
            int superblockSampleSize = 1 << picture.Sequence.SequenceHeader.SuperblockSizeLog2;
            int superblockModeInfoSize = superblockSampleSize >> Av1Constants.ModeInfoSizeLog2;
            Point superblockModeInfo = new(
                (superblock.Index % coefficientBuffer.SuperblockColumnCount) * superblockModeInfoSize,
                (superblock.Index / coefficientBuffer.SuperblockColumnCount) * superblockModeInfoSize);

            this.superblockQIndex = this.quantization.BaseQIndex;
            int rateQIndex = this.superblockQIndex;
            ObuDeltaParameters deltaQ = parent.FrameHeader.DeltaQParameters;
            if (deltaQ.IsPresent)
            {
                int superblockSize = 1 << picture.Sequence.SequenceHeader.SuperblockSizeLog2;
                Point superblockOrigin = new(
                    (superblock.Index % coefficientBuffer.SuperblockColumnCount) * superblockSize,
                    (superblock.Index / coefficientBuffer.SuperblockColumnCount) * superblockSize);

                int baseQIndex = this.quantization.BaseQIndex;
                int wantedQIndex = baseQIndex;
                if (parent.EncoderOptions.DeltaQMode == Av1DeltaQMode.VarianceBoost)
                {
                    wantedQIndex = this.GetVarianceBoostQIndex(superblockOrigin, baseQIndex);
                }
                else if (parent.EncoderOptions.DeltaQMode == Av1DeltaQMode.Objective && parent.TplFrame is { } tplFrame)
                {
                    // The quantizer follows the importance of the superblock. The regularized importance that it leaves scales the coding
                    // block rate multipliers.
                    double regularizedImportance = blockWorkspace.RegularizedImportance;
                    wantedQIndex = Av1TplDecisions.GetQForDeltaQObjective(
                        tplFrame,
                        superblockModeInfoSize,
                        superblockModeInfo.Y,
                        superblockModeInfo.X,
                        baseQIndex,
                        this.bitDepth,
                        parent.TplImportance,
                        ref regularizedImportance,
                        out _,
                        false);

                    blockWorkspace.RegularizedImportance = regularizedImportance;
                }

                // Only the full search updates the anchor after each coded superblock. The estimated search keeps the frame quantizer as
                // the anchor for the whole tile.
                bool estimated = picture.Parent.SpeedSettings.UseEstimatedModeDecision;

                int anchorQIndex = estimated ? baseQIndex : picture.Parent.PreviousQIndex.Span[superblock.TileIndex];
                this.superblockQIndex = Av1VarianceBoost.AdjustToResolution(deltaQ.Resolution, anchorQIndex, wantedQIndex);
                picture.Parent.SuperblockDeltaQIndex = this.superblockQIndex - baseQIndex;
                picture.Parent.DeltaQUsed |= this.superblockQIndex != baseQIndex;
                if (!estimated)
                {
                    rateQIndex = this.superblockQIndex;
                }

                // The quantizer setup derives the motion vector error per bit from the multiplier at the superblock quantizer, without the
                // coding block scaling.
                blockWorkspace.ErrorPerBitRateMultiplier = parent.GetRateMultiplier(
                    this.superblockQIndex + this.quantization.DeltaQDc[0], this.bitDepth);
            }

            this.blockQIndex = this.superblockQIndex;

            // The base multiplier is the multiplier at the frame quantizer for the estimated search, and at the superblock quantizer for the
            // full search. Both take the layer depth and the golden boost from the first-pass statistics.
            this.baseRateMultiplier = parent.GetRateMultiplier(rateQIndex + this.quantization.DeltaQDc[0], this.bitDepth);

            // The superblock starts from the references that the temporal dependency model keeps against the selective reference pruning.
            // Without statistics or with adaptive quantization, it keeps none.
            this.tplKeepReferenceFrames[..].Clear();

            // Every superblock starts without model blocks. Thus a frame or superblock without statistics never reads the vectors that an
            // earlier superblock gathered.
            this.tplSuperblockBlockCount = 0;
            if (parent.TplFrame is { } superblockTplFrame)
            {
                if (parent.EncoderOptions.AdaptiveQuantizationMode == Av1AdaptiveQuantizationMode.None)
                {
                    Av1TplDecisions.GetKeptReferenceFrames(
                        parent.TplStatisticsReady,
                        superblockTplFrame,
                        parent.FrameUpdateType,
                        parent.IsTplEligible,
                        superblockModeInfoSize,
                        superblockModeInfo.Y,
                        superblockModeInfo.X,
                        this.tplKeepReferenceFrames[..]);
                }

                // The recursive partition search gathers the model costs and vectors of the 16x16 blocks of the superblock. The
                // variance-based partition search does not.
                if (!UsesGivenPartition(picture))
                {
                    this.tplSuperblockBlockCount = Av1TplDecisions.GetSuperblockStatistics(
                        parent.TplStatisticsReady,
                        superblockTplFrame,
                        parent.FrameHeader.FrameType == ObuFrameType.KeyFrame,
                        parent.FrameUpdateType,
                        superblockModeInfoSize,
                        superblockModeInfo.Y,
                        superblockModeInfo.X,
                        blockWorkspace.TplSuperblockInterCosts,
                        blockWorkspace.TplSuperblockIntraCosts,
                        blockWorkspace.TplSuperblockVectors,
                        out this.tplSuperblockStride);
                }
            }

            // The recursive partition search measures the superblock at its root. The variance-based partition search of the fastest
            // speeds does not run that measure, so its rate weight stays at 128.
            this.rateMultiplierModifier = 128;
            if (picture.Parent.EncoderOptions.IsAllIntra && !UsesGivenPartition(picture))
            {
                // The 4x4 source variation is measured once for the entire superblock. Mixed flat and detailed regions get a lower rate
                // weight, which every partition and mode decision below it shares.
                int superblockSize = 1 << picture.Sequence.SequenceHeader.SuperblockSizeLog2;
                int originX = (superblock.Index % coefficientBuffer.SuperblockColumnCount) * superblockSize;
                int originY = (superblock.Index / coefficientBuffer.SuperblockColumnCount) * superblockSize;
                int right = Math.Min(originX + superblockSize, picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2);
                int bottom = Math.Min(originY + superblockSize, picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);
                (double minimumLogVariance, double maximumLogVariance) = this.GetLogSubBlockVariance(
                    blockWorkspace.Storage,
                    source.CodedView.GetPlane(Av1Plane.Y).Samples,
                    source.CodedView.GetPlane(Av1Plane.U).Samples,
                    source.CodedView.GetPlane(Av1Plane.V).Samples,
                    new Rectangle(originX, originY, right - originX, bottom - originY));

                int modifier = 128;
                if (minimumLogVariance < 2 && maximumLogVariance > 4)
                {
                    double range = maximumLogVariance - minimumLogVariance;
                    modifier -= range > 8 ? 48 : (int)(range * 6);
                }

                this.rateMultiplierModifier = modifier;
            }

            int rootSize = 1 << picture.Sequence.SequenceHeader.SuperblockSizeLog2;
            this.rateMultiplier = this.GetBlockRateMultiplier(
                new Point(
                    (superblock.Index % coefficientBuffer.SuperblockColumnCount) * rootSize,
                    (superblock.Index / coefficientBuffer.SuperblockColumnCount) * rootSize),
                picture.Sequence.SequenceHeader.SuperblockSize);

            this.sourceSadLevel = Av1SourceSadLevel.Medium;
            if (picture.Parent.SpeedSettings.UseEstimatedInterModeDecision && !picture.Parent.FrameHeader.IsIntra)
            {
                // The activity compares successive source pictures, so quantization noise in reconstructed references cannot make a
                // stationary source look like motion. Border samples complete the superblock at the right and bottom edges, so no separate
                // clipped-block rule is necessary.
                int side = picture.Sequence.SequenceHeader.SuperblockSize.GetWidth();
                Point origin = new(
                    (superblock.Index % coefficientBuffer.SuperblockColumnCount) * side,
                    (superblock.Index / coefficientBuffer.SuperblockColumnCount) * side);

                int columns = (picture.Parent.FrameHeader.FrameSize.FrameWidth + 63) >> 6;
                int rows = (picture.Parent.FrameHeader.FrameSize.FrameHeight + 63) >> 6;
                int column = origin.X >> 6;
                int row = origin.Y >> 6;
                ulong cachedSad = ulong.MaxValue;

                // Scene detection keeps no block errors for a frame whose encoder size is not the image size. The last superblock row and
                // column read no cached error either.
                if (column < columns - 1 && row < rows - 1 && !picture.Parent.SourceBlockSad.IsEmpty)
                {
                    ReadOnlySpan<ulong> errors = picture.Parent.SourceBlockSad.Span;
                    int index = (row * columns) + column;
                    cachedSad = errors[index];
                    if (side == 128)
                    {
                        cachedSad += errors[index + 1] + errors[index + columns] + errors[index + columns + 1];
                    }
                }

                ulong averageSad = side == 128 ? (cachedSad == ulong.MaxValue ? cachedSad : (cachedSad + 2) >> 2) : cachedSad;
                bool measureMoments = true;
                if (picture.Parent.FrameSourceSad == 0 || cachedSad == 0)
                {
                    this.sourceSadLevel = Av1SourceSadLevel.Zero;
                    measureMoments = false;
                }
                else if (picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level9 &&
                    Math.Min(picture.Parent.FrameHeader.FrameSize.FrameWidth, picture.Parent.FrameHeader.FrameSize.FrameHeight) < 360 &&
                    averageSad > 15000 && averageSad < 40000)
                {
                    // A middle-range cached SAD already establishes medium activity. Boundary blocks
                    // retain the full moment measurement because their cache shortcut is not used.
                    measureMoments = false;
                }

                if (measureMoments && this.bitDepth.GetBitCount() == 8)
                {
                    Av1PlaneRegion<TSample> current = this.source.GetPlane(Av1Plane.Y);
                    Av1PlaneRegion<byte> previous = picture.Parent.PreviousSource.GetPlane(Av1Plane.Y);
                    Av1ResidualBuilder.GetMoments(
                        MemoryMarshal.Cast<TSample, byte>(Av1TransformBlockEncoder.GetPlaneSpan(current, origin)),
                        current.Stride,
                        Av1TransformBlockEncoder.GetPlaneSpan(previous, origin),
                        previous.Stride,
                        side,
                        side,
                        out int sum,
                        out long squaredError);

                    this.sourceSadLevel = squaredError == 0 ? Av1SourceSadLevel.Zero
                        : squaredError < 10000 ? Av1SourceSadLevel.VeryLow
                        : squaredError < 100000 ? Av1SourceSadLevel.Low
                        : squaredError > 1000000 ? Av1SourceSadLevel.High
                        : Av1SourceSadLevel.Medium;

                    // Removing the squared mean separates a uniform brightness change from local motion.
                    long meanSquaredError = ((long)sum * sum) / (side * side);
                    long variance = squaredError - meanSquaredError;
                    this.sourceLightingChange = variance < (squaredError >> 1) && meanSquaredError > 10000;
                    this.sourceLowSumDifference = squaredError != 0 && meanSquaredError < 5000;
                    if (squaredError != 0 && !picture.Parent.HighSourceSad &&
                        picture.Parent.FrameSourceSad <= 20000 && !picture.Parent.FrameHeader.CodedLossless &&
                        !picture.Sequence.SequenceHeader.EnableSuperResolution)
                    {
                        int step = Av1QuantizationLookup.GetAcQuant(this.quantization.QIndex[0], 0, this.bitDepth);
                        int averageStep = Av1QuantizationLookup.GetAcQuant(picture.Parent.AverageInterQuantizer, 0, this.bitDepth);
                        int threshold = step * (Math.Min(
                            picture.Parent.FrameHeader.FrameSize.FrameWidth,
                            picture.Parent.FrameHeader.FrameSize.FrameHeight) < 360 ? 250 : Math.Clamp(averageStep, 250, 1000));

                        this.filterTemporalSource = variance <= threshold && meanSquaredError <= 15;
                    }
                }
            }

            this.codedAreaLuma = 0;
            this.codedAreaChroma = 0;
            this.SelectedBlockStatistics = default;
            this.cdefSkipUnits[..].Fill(true);
            this.replayNodeIndex = -1;
            this.replayPartition = Av1PartitionType.Invalid;
            this.replayPartitionOrigin = default;
            this.replayParentSize = Av1BlockSize.Invalid;
            bool searchesLeaves = SearchesVariancePartitionLeaves(picture);
            if (!picture.Parent.SpeedSettings.UseVarianceBasedPartition || searchesLeaves ||
                (!picture.Parent.FrameHeader.IsIntra && picture.Parent.SpeedSettings.GetEstimatedPartitionMergeLevel() != 0))
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
                    picture.Parent.FrameHeader.AllowScreenContentTools,
                    picture.Parent.SpeedSettings.UseVarianceBasedPartition && !searchesLeaves);
            }
        }

        /// <inheritdoc/>
        public static bool UsesRetainedDecisions => false;

        /// <summary>
        /// Gets the statistics of the most recently encoded block.
        /// </summary>
        public Av1RateDistortionStatistics SelectedBlockStatistics { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the frame decides its blocks with the estimated inter-frame search, which encodes each winner
        /// in place.
        /// </summary>
        private readonly bool UsesEstimatedInterSearch =>
            !this.picture.Parent.FrameHeader.IsIntra && this.picture.Parent.SpeedSettings.UseEstimatedInterModeDecision;

        /// <summary>
        /// Gets a value indicating whether the full-pixel searches cap their first mesh interval. This applies to the alternate reference
        /// frames of screen content at good quality speeds up to 2.
        /// </summary>
        private readonly bool UsesFineSearchInterval =>
            this.picture.Parent.IsScreenContent &&
            this.picture.Parent.FrameUpdateType == Av1FrameUpdateType.Alternate &&
            !this.picture.Parent.SpeedSettings.IsRealtime &&
            this.picture.Parent.EncodingSpeed <= HeifEncodingSpeed.Level2;

        /// <summary>
        /// Gets a value indicating whether the segment of the block being searched codes losslessly. The flag comes from the
        /// segment quantizer without the delta quantizer of the superblock, so it can differ from a zero block quantizer index.
        /// </summary>
        private readonly bool BlockLossless => this.picture.Parent.FrameHeader.LosslessArray[this.blockSegmentId];

        /// <inheritdoc/>
        public Av1PartitionType SelectPartition(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<Av1EncoderBlockStruct> blockEncodings,
            Span<Av1EncoderPaletteInfo> blockPalettes,
            Span<byte> paletteTokens,
            Span<int> cdefPreset,
            Span<int> previousQIndex,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            Span<TSample> sourceLuma,
            Span<TSample> sourceBlue,
            Span<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType preparedPartition)
        {
            this.replayNodeIndex = -1;
            this.mustFindValidPartition = false;

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

            if (UsesGivenPartition(this.picture))
            {
                bool searchesLeaves = SearchesVariancePartitionLeaves(this.picture);
                if (this.superblock.Workspace.PartitionSearchTypes[0] == (byte)Av1PartitionType.Invalid)
                {
                    // The variance analysis reads the edge availability of the superblock itself, not the one that the previously coded
                    // block left.
                    Av1TileWriter.SetModeInfoRowAndColumn(
                        macroBlock,
                        macroBlock.Tile,
                        new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2),
                        blockSize,
                        this.picture.Parent.Common.ModeInfoStride,
                        this.picture.Parent.Common.ModeInfoRowCount,
                        this.picture.Parent.Common.ModeInfoColumnCount);

                    if (this.picture.Parent.FixedPartitionSize != Av1BlockSize.Invalid)
                    {
                        this.PrepareFixedPartitions(macroBlock.Tile, blockOrigin);
                    }
                    else
                    {
                        this.PrepareVariancePartitions(
                            transformCoefficients,
                            in interWorkspace,
                            modeWorkspace.GetCandidateReconstruction(0),
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors,
                            encoderSegmentMap,
                            previousSegmentMap,
                            workspaceStorage,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            macroBlock,
                            blockOrigin);
                    }

                    if (searchesLeaves)
                    {
                        // Every leaf of the superblock is searched and reconstructed before the writer encodes any of them.
                        this.SearchVariancePartition(
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            searchDequantizedCoefficients,
                            transformWorkspace,
                            transformTypeProbabilities,
                            blockResidual,
                            searchCoefficients,
                            searchReconstructions,
                            estimationRowCoefficients,
                            in interWorkspace,
                            motionSearchPrediction,
                            in motionVectorCosts,
                            transformPrediction,
                            interIntraAbove,
                            interIntraLeft,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            in partitionEdges,
                            in transformEdges,
                            in paletteEdges,
                            in lumaCoefficientEdges,
                            in blueCoefficientEdges,
                            in redCoefficientEdges,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors,
                            referenceContexts,
                            encoderSegmentMap,
                            searchSegmentMap,
                            previousSegmentMap,
                            superblockCoefficients,
                            workspaceStorage,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            blockOrigin,
                            blockSize,
                            0,
                            true);
                    }
                }

                Av1PartitionType variancePartition = (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[nodeIndex];
                if (!this.picture.Parent.FrameHeader.IsIntra && variancePartition == Av1PartitionType.Split &&
                    blockSize <= Av1BlockSize.Block64x64)
                {
                    variancePartition = this.RefineEstimatedLeafPartition(
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in partitionEdges,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        referenceContexts,
                        encoderSegmentMap,
                        searchSegmentMap,
                        previousSegmentMap,
                        superblockCoefficients,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        nodeIndex);
                }

                this.PreparePartitionGeometry(modeInfoGrid, modeInfoAllocation, blockOrigin, blockSize, variancePartition);
                if (searchesLeaves)
                {
                    // The writer reaches each leaf in partition order and encodes the decision the search kept.
                    this.replayNodeIndex = nodeIndex;
                    this.replayPartition = variancePartition;
                    this.replayPartitionOrigin = blockOrigin;
                    this.replayParentSize = blockSize;
                }
                else if (variancePartition == Av1PartitionType.None && !this.picture.Parent.FrameHeader.IsIntra &&
                    this.picture.Parent.SpeedSettings.GetEstimatedPartitionMergeLevel() != 0 &&
                    this.blockWorkspace.PartitionTree.GetContext(nodeIndex, Av1PartitionType.None, 0).Snapshot.Ready)
                {
                    this.replayNodeIndex = nodeIndex;
                    this.replayPartition = variancePartition;
                    this.replayPartitionOrigin = blockOrigin;
                    this.replayParentSize = blockSize;
                }

                return variancePartition;
            }

            Av1PartitionType selectedPartition = (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[nodeIndex];
            if (selectedPartition == Av1PartitionType.Invalid)
            {
                selectedPartition = this.SelectBestPartition(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in partitionEdges,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    encoderSegmentMap,
                    searchSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    nodeIndex,
                    Av1RateDistortionStatistics.Invalid,
                    out _,
                    out _,
                    out _);
            }

            // Search children retain their choices by quadtree position, independently of the final writer's
            // preorder index. Later traversal visits those choices without repeating recursive partition search.
            this.PreparePartitionGeometry(modeInfoGrid, modeInfoAllocation, blockOrigin, blockSize, selectedPartition);
            this.replayNodeIndex = nodeIndex;
            this.replayPartition = selectedPartition;
            this.replayPartitionOrigin = blockOrigin;
            this.replayParentSize = blockSize;
            return selectedPartition;
        }

        /// <summary>
        /// Compares an unsplit block with the four leaves selected by variance partitioning.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="nodeIndex">The partition tree node of the block.</param>
        /// <returns>The selected partition type.</returns>
        private Av1PartitionType RefineEstimatedLeafPartition(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int nodeIndex)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            int mergeLevel = parent.SpeedSettings.GetEstimatedPartitionMergeLevel();

            if (mergeLevel == 0)
            {
                return Av1PartitionType.Split;
            }

            int firstChild = (nodeIndex * 4) + 1;
            int half = blockSize.GetWidth() >> 1;
            for (int child = 0; child < 4; child++)
            {
                Point childOrigin = blockOrigin + new Size((child & 1) * half, (child >> 1) * half);
                if (!this.IsBlockOriginInsideFrame(childOrigin) || (blockSize != Av1BlockSize.Block16x16 &&
                    (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[firstChild + child] != Av1PartitionType.None))
                {
                    return Av1PartitionType.Split;
                }
            }

            // The merge trial compares the two costs with the multiplier of the preceding frame. The block searches set their own
            // multiplier and then restore that value.
            int mergeMultiplier = this.blockWorkspace.PreviousFrameRateMultiplier;
            int savedLumaArea = this.codedAreaLuma;
            int savedChromaArea = this.codedAreaChroma;
            this.SavePartitionTrialContexts(
                workspaceStorage,
                in partitionEdges,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                blockOrigin,
                blockSize);

            int noneRate = Av1TileWriter.GetPartitionCost(
                this.picture, writer, tables.ModeCosts, blockSize, Av1PartitionType.None, blockOrigin, in partitionEdges);

            int splitRate = Av1TileWriter.GetPartitionCost(
                this.picture, writer, tables.ModeCosts, blockSize, Av1PartitionType.Split, blockOrigin, in partitionEdges);

            Av1EncoderPartitionTree.ModeContext noneContext = this.blockWorkspace.PartitionTree.GetContext(nodeIndex, Av1PartitionType.None, 0);
            this.estimatedLeafEncode = EstimatedLeafEncode.Search;
            Av1RateDistortionStatistics none = this.EvaluatePartitionLeaf(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                blockResidual,
                searchCoefficients,
                searchReconstructions,
                estimationRowCoefficients,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                transformPrediction,
                interIntraAbove,
                interIntraLeft,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                encoderSegmentMap,
                searchSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                blockSize,
                Av1PartitionType.None,
                noneContext,
                long.MaxValue,
                false,
                true);

            this.estimatedLeafEncode = EstimatedLeafEncode.Output;

            // This is the skip decision of the search, not the skip decision of the encoded block.
            bool noneSkip = none.AllTransformsEmpty;
            Av1RateDistortionStatistics noneSyntax = new(mergeMultiplier, noneRate, 0);
            none.Add(mergeMultiplier, noneSyntax);
            this.ResetPartitionTrial(
                workspaceStorage,
                in partitionEdges,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                blockOrigin,
                blockSize,
                savedLumaArea,
                savedChromaArea);

            // The split is a candidate when the merge level is less than 2, when the merged block keeps a residual, or when it codes NEWMV.
            // Then the split flag test decides.
            Av1PredictionMode noneMode = noneContext.Snapshot.ModeInfo.Block.Mode;
            bool evaluateSplit = false;
            if (mergeLevel < 2 || !noneSkip || noneMode == Av1PredictionMode.NewMotionVector)
            {
                bool noneBoosted = this.picture.Parent.CyclicRefresh is not null &&
                    this.picture.Parent.FrameHeader.SegmentationParameters.Enabled &&
                    Av1CyclicRefresh.IsBoosted(noneContext.Snapshot.ModeInfo.Block.SegmentId);

                evaluateSplit = this.CalculateDoSplit(
                    sourceLuma, sourceBlue, sourceRed, in interWorkspace, mergeLevel, noneSkip, noneBoosted, noneMode, blockSize, blockOrigin);
            }

            Av1RateDistortionStatistics split = Av1RateDistortionStatistics.Invalid;
            if (evaluateSplit)
            {
                split = new(mergeMultiplier, splitRate, 0);
                Av1BlockSize childSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
                for (int child = 0; child < 4; child++)
                {
                    Point childOrigin = blockOrigin + new Size((child & 1) * half, (child >> 1) * half);
                    if (!this.IsBlockOriginInsideFrame(childOrigin))
                    {
                        continue;
                    }

                    Av1EncoderPartitionTree.ModeContext childContext =
                        this.blockWorkspace.PartitionTree.GetContext(firstChild + child, Av1PartitionType.None, 0);

                    this.estimatedLeafEncode = EstimatedLeafEncode.MergeSplitTrial;
                    this.mergeNoneCost = none.Cost;
                    this.mergeSplitStatistics = split;
                    this.mergeChildIndex = child;
                    this.mergeRateMultiplier = mergeMultiplier;
                    Av1RateDistortionStatistics childStatistics = this.EvaluatePartitionLeaf(
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        referenceContexts,
                        encoderSegmentMap,
                        searchSegmentMap,
                        previousSegmentMap,
                        superblockCoefficients,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        childOrigin,
                        childSize,
                        Av1PartitionType.None,
                        childContext,
                        long.MaxValue,
                        child < 3,
                        true);

                    this.estimatedLeafEncode = EstimatedLeafEncode.Output;

                    // A leaf that found no mode kept nothing, so nothing can read its decision back.
                    childContext.Snapshot.Ready = childStatistics.Cost != long.MaxValue;
                    this.superblock.Workspace.PartitionSearchTypes[firstChild + child] = (byte)Av1PartitionType.None;
                    split.Add(mergeMultiplier, childStatistics);
                    if (none.Cost < split.Cost)
                    {
                        break;
                    }
                }

                this.ResetPartitionTrial(
                    workspaceStorage,
                    in partitionEdges,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    blockOrigin,
                    blockSize,
                    savedLumaArea,
                    savedChromaArea);
            }

            Av1PartitionType selected = none.Cost < split.Cost ? Av1PartitionType.None : Av1PartitionType.Split;
            noneContext.Snapshot.Ready = selected == Av1PartitionType.None;
            this.superblock.Workspace.PartitionSearchTypes[nodeIndex] = (byte)selected;
            return selected;
        }

        /// <summary>
        /// Decides whether a merge trial compares the four sub-blocks.
        /// </summary>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="interWorkspace">The inter prediction buffers that hold the last luma prediction.</param>
        /// <param name="mergeLevel">The partition merge level of the speed settings.</param>
        /// <param name="noneSkip">Whether the merged block's search skipped its residual.</param>
        /// <param name="noneBoosted">Whether the merged block is in a boosted cyclic refresh segment.</param>
        /// <param name="noneMode">The merged block's selected mode.</param>
        /// <param name="blockSize">The merged block size.</param>
        /// <param name="blockOrigin">The merged block origin.</param>
        /// <returns><see langword="true"/> when the split is compared.</returns>
        private bool CalculateDoSplit(
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            int mergeLevel,
            bool noneSkip,
            bool noneBoosted,
            Av1PredictionMode noneMode,
            Av1BlockSize blockSize,
            Point blockOrigin)
        {
            bool largerQuantizer = this.picture.Parent.FrameHeader.QuantizationParameters.BaseQIndex > 100;
            bool doSplit = mergeLevel != 3 || blockSize <= Av1BlockSize.Block32x32 || (largerQuantizer && blockSize <= Av1BlockSize.Block64x64);
            if (mergeLevel < 2 || noneBoosted || !noneSkip)
            {
                return doSplit;
            }

            // A skip from the Hadamard estimate is reliable, so the split is not compared. The large-block model's
            // skip is less reliable, and the split is still compared above quantizer index 100.
            if (!this.UsesLargeBlockModel(blockSize) || !largerQuantizer)
            {
                return false;
            }

            if (noneMode == Av1PredictionMode.NewMotionVector && blockSize == Av1BlockSize.Block32x32 && doSplit &&
                this.lastLumaPredictionBuffer >= 0)
            {
                // The four 16x16 residuals of a 32x32 NEWMV block are measured against the prediction the search
                // built last, which is what the frame buffer holds after the search. A split is not compared when
                // their per-sample errors are within 1.5 of each other.
                ReadOnlySpan<TSample> prediction = this.GetLastLumaPrediction(in interWorkspace);
                Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
                ReadOnlySpan<TSample> sourceSamples = sourceLuma;
                double minimumError = double.MaxValue;
                double maximumError = 0;
                int quadrants = 0;
                for (int i = 0; i < 4; i++)
                {
                    Point quadrant = blockOrigin + new Size((i & 1) * 16, (i >> 1) * 16);
                    if (!this.IsBlockOriginInsideFrame(quadrant))
                    {
                        break;
                    }

                    TOperator.GetMoments(
                        sourceSamples[sourcePlane.GetOffset(quadrant.X, quadrant.Y)..],
                        sourcePlane.Stride,
                        prediction[(((i >> 1) * 16 * 32) + ((i & 1) * 16))..],
                        32,
                        16,
                        16,
                        out int sum,
                        out long squaredError);

                    uint variance = (uint)(squaredError - (((long)sum * sum) >> 8));
                    double error = Math.Sqrt((double)variance / 16 / 16);
                    minimumError = Math.Min(minimumError, error);
                    maximumError = Math.Max(maximumError, error);
                    quadrants++;
                }

                if (quadrants == 4 && maximumError - minimumError <= 1.5)
                {
                    doSplit = false;
                }
            }

            return doSplit;
        }

        /// <summary>
        /// Runs <see cref="SelectBestPartitionCore"/> at the rate multiplier of the block.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="nodeIndex">The partition tree node of the block.</param>
        /// <param name="costLimit">The cost above which the search stops.</param>
        /// <param name="selectedStatistics">The rate and distortion of the selected partition.</param>
        /// <param name="noneCost">The cost of the unsplit block.</param>
        /// <param name="rectangleWins">Bit zero when the horizontal halves win, and bit one when the vertical halves win.</param>
        /// <returns>The selected partition type.</returns>
        private Av1PartitionType SelectBestPartition(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int nodeIndex,
            Av1RateDistortionStatistics costLimit,
            out Av1RateDistortionStatistics selectedStatistics,
            out long noneCost,
            out byte rectangleWins)
        {
            // A negative bound skips the block setup.
            int savedRateMultiplier = this.rateMultiplier;
            if (costLimit.Cost >= 0)
            {
                this.SetBlockSegment(encoderSegmentMap, previousSegmentMap, blockOrigin, blockSize);
            }

            this.rateMultiplier = costLimit.Cost < 0
                ? this.GetBlockRateMultiplier(blockOrigin, blockSize)
                : this.SetupBlockRateMultiplier(blockOrigin, blockSize);

            // The bound arrives at the multiplier of the parent, so its cost is measured again at the multiplier of this block.
            costLimit.UpdateCost(this.rateMultiplier);

            // A 16x16 node of a frame that refreshes its variance segments measures the segment that its smaller blocks take.
            if (costLimit.Cost >= 0 && blockSize == Av1BlockSize.Block16x16 && this.picture.Parent.VarianceSegmentRefresh)
            {
                this.blockWorkspace.MacroblockEnergy = (int)this.GetLogBlockVariance(
                    workspaceStorage, sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize);
            }

            Av1PartitionType result = this.SelectBestPartitionCore(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                blockResidual,
                searchCoefficients,
                searchReconstructions,
                estimationRowCoefficients,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                transformPrediction,
                interIntraAbove,
                interIntraLeft,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in partitionEdges,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                encoderSegmentMap,
                searchSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                blockSize,
                nodeIndex,
                costLimit,
                out selectedStatistics,
                out noneCost,
                out rectangleWins);

            this.rateMultiplier = savedRateMultiplier;
            return result;
        }

        /// <summary>
        /// Gets the remaining cost bound of a partition leaf at the rate multiplier of the leaf.
        /// </summary>
        /// <param name="remainingCost">The remaining bound at the multiplier of the node.</param>
        /// <param name="leafOrigin">The leaf origin in luma samples.</param>
        /// <param name="leafSize">The leaf size.</param>
        /// <param name="searchesOwnPartition">
        /// Whether the leaf is a 4x4 child of a split, which runs a partition search of its own.
        /// </param>
        /// <returns>The remaining cost bound, or <see cref="NegativeLeafBound"/> when the bound is already negative.</returns>
        private readonly long GetLeafCostLimit(
            Av1RateDistortionStatistics remainingCost,
            Point leafOrigin,
            Av1BlockSize leafSize,
            bool searchesOwnPartition)
        {
            // The leaf search tests the bound at the multiplier of the caller before it sets up the block.
            if (remainingCost.Cost < 0)
            {
                return NegativeLeafBound;
            }

            // The leaf search measures the remaining bound at its own multiplier.
            remainingCost.UpdateCost(this.GetBlockRateMultiplier(leafOrigin, leafSize));

            // A 4x4 child measures the bound at its own multiplier when its partition search opens. Its unsplit search tests the bound
            // again before the block setup.
            if (searchesOwnPartition && remainingCost.Cost < 0)
            {
                return NegativeLeafBound;
            }

            return remainingCost.Cost;
        }

        /// <summary>
        /// Gets the rate multiplier of a block. The start value is the superblock multiplier. When the frame derives coding block
        /// multipliers from ready temporal dependency statistics, the value scales by the block importance over the regularized importance
        /// of the superblock. For the SSIM and image tunes, it then scales by the SSIM factors of the block. Last, the all-intra superblock
        /// modifier applies.
        /// </summary>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="segmentId">The segment whose quantizer prices the block, or -1 for the superblock quantizer.</param>
        /// <returns>The rate multiplier, at least one.</returns>
        private readonly int GetBlockRateMultiplier(Point blockOrigin, Av1BlockSize blockSize, int segmentId = -1)
        {
            int multiplier = this.GetTunedRateMultiplier(blockOrigin, blockSize, segmentId);

            // The product widens to 64 bits before the shift, so a large multiplier cannot overflow.
            multiplier = (int)(((long)multiplier * this.rateMultiplierModifier) >> 7);
            return Math.Max(multiplier, 1);
        }

        /// <summary>
        /// Sets the segment and quantizer of a block or partition node when the frame uses segmentation. A frame that refreshes its
        /// variance segments searches every block in segment 0. Any other frame takes the smallest segment that the segment map holds
        /// under the block.
        /// </summary>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        private void SetBlockSegment(ReadOnlySpan<byte> encoderSegmentMap, ReadOnlySpan<byte> previousSegmentMap, Point blockOrigin, Av1BlockSize blockSize)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            ObuSegmentationParameters segmentation = parent.FrameHeader.SegmentationParameters;
            this.blockSegmentId = 0;
            if (!segmentation.Enabled)
            {
                return;
            }

            // A frame that updates its segment map reads the encoder map. Any other frame reads the map of the primary reference frame.
            ReadOnlySpan<byte> map = segmentation.SegmentationUpdateMap == 1 ? encoderSegmentMap : previousSegmentMap;

            if (!parent.VarianceSegmentRefresh && !map.IsEmpty)
            {
                this.blockSegmentId = Av1SymbolContextHelper.GetSegmentId(
                    parent.Common,
                    map,
                    blockSize,
                    new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2));
            }

            this.blockQIndex = Av1QuantizationLookup.GetQIndex(segmentation, this.blockSegmentId, this.superblockQIndex);
            this.blockWorkspace.SetQuantizationMatrixLevels(parent.FrameHeader.QuantizationParameters, parent.FrameHeader.LosslessArray[this.blockSegmentId]);
        }

        /// <summary>
        /// Adds the estimated rates of the segment of a coded block, coded spatially and coded against the map of the primary reference.
        /// The spatial predictor reads the map that the frame buffer still holds from its previous frame. The prediction flag context is 0,
        /// because the encoder never sets the flag.
        /// </summary>
        /// <param name="tables">The rate tables that price the syntax.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="macroBlock">The block's neighbor availability.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="segmentId">The block's segment.</param>
        private readonly void AddSegmentPredictionCosts(
            in Av1CoefficientTables tables,
            ReadOnlySpan<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int segmentId)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            ObuSegmentationParameters segmentation = parent.FrameHeader.SegmentationParameters;
            if (!segmentation.Enabled)
            {
                return;
            }

            int prediction = Av1TileWriter.GetSpatialSegmentationPrediction(parent.Common, searchSegmentMap, macroBlock, blockOrigin, out int context);
            int codedId = Av1SymbolContextHelper.NegativeInterleave(segmentId, prediction, segmentation.LastActiveSegmentId + 1);
            int spatialCost = tables.ModeCosts.GetSegmentId(context, codedId);
            parent.SpatialSegmentCost += spatialCost;

            int previousSegmentId = previousSegmentMap.IsEmpty
                ? 0
                : Av1SymbolContextHelper.GetSegmentId(
                    parent.Common,
                    previousSegmentMap,
                    blockSize,
                    new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2));

            bool predicted = previousSegmentId == segmentId;
            parent.TemporalSegmentCost += tables.ModeCosts.GetSegmentIdPredicted(0, predicted ? 1 : 0);
            if (!predicted)
            {
                parent.TemporalSegmentCost += spatialCost;
            }
        }

        /// <summary>
        /// Sets the quantizer of a selected block from its coded segment before the block is coded.
        /// </summary>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="segmentId">The segment the search gave the selected block.</param>
        private void SetCodedBlockSegment(
            ReadOnlySpan<byte> encoderSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int segmentId)
        {
            ObuSegmentationParameters segmentation = this.picture.Parent.FrameHeader.SegmentationParameters;
            if (this.picture.Parent.EncoderOptions.AdaptiveQuantizationMode != Av1AdaptiveQuantizationMode.None)
            {
                this.blockSegmentId = this.GetCodedBlockSegment(encoderSegmentMap, previousSegmentMap, blockOrigin, blockSize, segmentId);
                this.blockQIndex = Av1QuantizationLookup.GetQIndex(segmentation, this.blockSegmentId, this.superblockQIndex);
            }
        }

        /// <summary>
        /// Returns the segment that codes a selected block. Complexity adaptive quantization takes it from the segment map, which the
        /// search of the block and its neighbors wrote after the setup of the block. Any other mode keeps the searched segment.
        /// </summary>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="segmentId">The segment the search gave the selected block.</param>
        /// <returns>The coded segment.</returns>
        private readonly int GetCodedBlockSegment(
            ReadOnlySpan<byte> encoderSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int segmentId)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            ObuSegmentationParameters segmentation = parent.FrameHeader.SegmentationParameters;
            if (!segmentation.Enabled || parent.EncoderOptions.AdaptiveQuantizationMode != Av1AdaptiveQuantizationMode.Complexity)
            {
                return segmentId;
            }

            // A frame that updates its segment map reads the encoder map. Any other frame reads the map of the primary reference frame.
            ReadOnlySpan<byte> map = segmentation.SegmentationUpdateMap == 1 ? encoderSegmentMap : previousSegmentMap;

            return map.IsEmpty
                ? 0
                : Av1SymbolContextHelper.GetSegmentId(
                    parent.Common,
                    map,
                    blockSize,
                    new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2));
        }

        /// <summary>
        /// Returns the variance segment of a block: the mean log variance of its 4x4 luma blocks inside the mode-information grid, at
        /// most 7. A block of 16x16 or smaller takes the value that its 16x16 partition node measured.
        /// </summary>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <returns>The segment identifier.</returns>
        private int GetVarianceSegmentId(
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize)
            => blockSize <= Av1BlockSize.Block16x16
                ? this.blockWorkspace.MacroblockEnergy
                : (int)this.GetLogBlockVariance(workspaceStorage, sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize);

        /// <summary>
        /// Returns the mean log variance of the 4x4 luma blocks of a block inside the mode-information grid, at most 7.
        /// </summary>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <returns>The mean log variance.</returns>
        private double GetLogBlockVariance(
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize)
        {
            Av1EncoderCommon common = this.picture.Parent.Common;
            int width = Math.Min(blockSize.GetWidth(), (common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) - blockOrigin.X);
            int height = Math.Min(blockSize.GetHeight(), (common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2) - blockOrigin.Y);
            double sum = 0;
            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    sum += this.GetSourceLogVariance(workspaceStorage, sourceLuma, sourceBlue, sourceRed, new Point(blockOrigin.X + x, blockOrigin.Y + y));
                }
            }

            // The divisor counts whole 4x4 blocks. The integer expression evaluates left to right, so each axis truncates on its own.
            sum /= width / 4 * height / 4;
            return Math.Min(sum, 7);
        }

        /// <summary>
        /// Gets the rate multiplier of a block as <see cref="GetBlockRateMultiplier"/> does. With the SSIM and image tunes, it also records
        /// the multiplier from which the motion vector error per bit derives. That multiplier comes before the all-intra modifier.
        /// </summary>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="segmentId">The segment whose quantizer prices the block, or -1 for the superblock quantizer.</param>
        /// <returns>The rate multiplier, at least one.</returns>
        private readonly int SetupBlockRateMultiplier(Point blockOrigin, Av1BlockSize blockSize, int segmentId = -1)
        {
            if (this.picture.Parent.SsimRateMultiplierFactors is not null)
            {
                this.blockWorkspace.ErrorPerBitRateMultiplier = this.GetTunedRateMultiplier(blockOrigin, blockSize, segmentId);
            }

            return this.GetBlockRateMultiplier(blockOrigin, blockSize, segmentId);
        }

        /// <summary>
        /// Gets the rate multiplier of a block before the all-intra superblock modifier: the superblock multiplier, scaled by the coding
        /// block importance and by the SSIM factors.
        /// </summary>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="segmentId">The segment whose quantizer prices the block, or -1 for the superblock quantizer.</param>
        /// <returns>The rate multiplier, at least zero.</returns>
        private readonly int GetTunedRateMultiplier(Point blockOrigin, Av1BlockSize blockSize, int segmentId = -1)
        {
            // A segment prices the block at the segment quantizer of the frame quantizer, unless the coding block multiplier replaces it.
            // Adaptive quantization then keeps the superblock multiplier. Cyclic refresh prices only a boosted block at the multiplier of
            // the first boosted segment.
            int multiplier = this.baseRateMultiplier;
            Av1PictureParentControlSet parent = this.picture.Parent;
            if (segmentId >= 0 && parent.CyclicRefresh is { } cyclicRefresh)
            {
                if (parent.FrameHeader.SegmentationParameters.Enabled && Av1CyclicRefresh.IsBoosted(segmentId))
                {
                    multiplier = cyclicRefresh.RateMultiplier;
                }
            }
            else if (segmentId >= 0 && !parent.CodingBlockDeltaRateMultiplier)
            {
                multiplier = parent.GetRateMultiplier(
                    Av1QuantizationLookup.GetQIndex(parent.FrameHeader.SegmentationParameters, segmentId, this.quantization.BaseQIndex),
                    this.bitDepth);
            }

            if (parent.CodingBlockDeltaRateMultiplier && parent.TplFrame is { } tplFrame &&
                parent.EncoderOptions.AdaptiveQuantizationMode == Av1AdaptiveQuantizationMode.None)
            {
                multiplier = Av1TplDecisions.GetCodingBlockRateMultiplier(
                    parent.TplStatisticsReady,
                    tplFrame,
                    blockSize,
                    blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2,
                    blockOrigin.X >> Av1Constants.ModeInfoSizeLog2,
                    multiplier,
                    this.blockWorkspace.RegularizedImportance);
            }

            if (parent.SsimRateMultiplierFactors is not null)
            {
                multiplier = this.ScaleSsimRateMultiplier(multiplier, blockOrigin, blockSize);
            }

            return multiplier;
        }

        /// <summary>
        /// Searches the partition types of one block, from the unsplit block through the split, the rectangles and the extended shapes,
        /// and keeps the cheapest.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="nodeIndex">The partition tree node of the block.</param>
        /// <param name="costLimit">The cost above which the search stops.</param>
        /// <param name="selectedStatistics">The rate and distortion of the selected partition.</param>
        /// <param name="noneCost">The cost of the unsplit block.</param>
        /// <param name="rectangleWins">Bit zero when the horizontal halves win, and bit one when the vertical halves win.</param>
        /// <returns>The selected partition type.</returns>
        private Av1PartitionType SelectBestPartitionCore(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int nodeIndex,
            Av1RateDistortionStatistics costLimit,
            out Av1RateDistortionStatistics selectedStatistics,
            out long noneCost,
            out byte rectangleWins)
        {
            // The superblock retry keeps the rectangle decisions of the first search. The retry starts after the pruning that runs before
            // the search, and the reset of the partition limits does not change the rectangle flags.
            bool retrying = false;
            bool retainedRectangularSplit = true;
            bool retainedPruneHorizontal = false;
            bool retainedPruneVertical = false;

        SearchPartitions:
            if (blockSize == Av1BlockSize.Block64x64 && this.picture.Parent.FrameHeader.IsIntra)
            {
                this.intraPartitionFeaturesValid = false;
            }

            rectangleWins = 3;
            InlineArray4<byte> childRectangleWinStorage = default;
            Span<byte> childRectangleWins = childRectangleWinStorage;
            childRectangleWins[..].Fill(3);
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            Av1TileWriter.SetModeInfoRowAndColumn(
                macroBlock,
                macroBlock.Tile,
                modeInfoPosition,
                blockSize,
                this.picture.Parent.Common.ModeInfoStride,
                this.picture.Parent.Common.ModeInfoRowCount,
                this.picture.Parent.Common.ModeInfoColumnCount);

            if (nodeIndex == 0 && !this.mustFindValidPartition && !this.picture.Parent.FrameHeader.IsIntra)
            {
                Av1ReferenceMotionVectors starts = default;
                starts.Build(
                    this.picture,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    macroBlock,
                    modeInfoPosition,
                    blockSize,
                    Av1PartitionType.None,
                    this.picture.Sequence.SequenceHeader,
                    this.picture.Parent.FrameHeader,
                    this.simpleMotionReference,
                    Av1ReferenceFrameType.None);

                Av1MotionVector nearest = starts.Nearest;

                // Round the initial spatial predictor to whole samples, with half samples away from zero.
                Av1MotionVector fullStart = new(
                    ((nearest.Row + 3 + (nearest.Row >= 0 ? 1 : 0)) >> 3) * 8,
                    ((nearest.Column + 3 + (nearest.Column >= 0 ? 1 : 0)) >> 3) * 8);

                Span<Av1SimpleMotionData> nodes = this.blockWorkspace.GetSimpleMotionData(workspaceStorage);
                nodes.Clear();
                foreach (ref Av1SimpleMotionData node in nodes)
                {
                    node.Starts[(int)Av1ReferenceFrameType.Last] = fullStart;
                }

                Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;

                // An intra picture has no motion to predict a partition size from, so it keeps the speed cap.
                if (settings.MaximumPartitionPredictionMode != Av1EncoderSpeedSettings.MaximumPartitionPrediction.Disabled &&
                    !this.picture.Parent.FrameHeader.IsIntra &&
                    blockSize == Av1BlockSize.Block128x128 && !this.picture.Parent.FrameHeader.AllowScreenContentTools &&
                    this.picture.Parent.FrameUpdateType is not (Av1FrameUpdateType.Overlay or Av1FrameUpdateType.IntermediateOverlay) &&
                    blockOrigin.X + 128 <= (this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) &&
                    blockOrigin.Y + 128 <= (this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2))
                {
                    this.maximumPartitionSize = (Av1BlockSize)Math.Clamp(
                        (int)this.PredictMaximumPartition(
                            workspaceStorage,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            motionSearchPrediction,
                            interWorkspace.FilterRows,
                            in motionVectorCosts,
                            modeWorkspace.GetCandidateReconstruction(0),
                            blockOrigin),
                        (int)settings.MinimumPartitionSize,
                        Math.Min((int)settings.MaximumPartitionSize, (int)this.picture.Sequence.SequenceHeader.SuperblockSize));
                }
            }

            int savedLumaArea = this.codedAreaLuma;
            int savedChromaArea = this.codedAreaChroma;
            this.SavePartitionTrialContexts(
                workspaceStorage,
                in partitionEdges,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                blockOrigin,
                blockSize);

            Av1RateDistortionStatistics bestStatistics = costLimit;
            selectedStatistics = Av1RateDistortionStatistics.Invalid;
            Av1PartitionType selectedPartition = Av1PartitionType.None;
            ReadOnlySpan<Av1PartitionType> searchOrder = PartitionSearchOrder;
            int candidateCount = blockSize == Av1BlockSize.Block8x8 ? 4 : searchOrder.Length;
            bool noneInvalid = true;
            bool splitInvalid = true;
            noneCost = 0;
            long nonePartitionCost = long.MaxValue;
            InlineArray4<long> splitNoneCostStorage = default;
            InlineArray2<long> horizontalCostStorage = default;
            InlineArray2<long> verticalCostStorage = default;
            InlineArray4<Av1AsymmetricModeCacheEntry> splitModeCacheStorage = default;
            InlineArray2<Av1AsymmetricModeCacheEntry> horizontalModeCacheStorage = default;
            InlineArray2<Av1AsymmetricModeCacheEntry> verticalModeCacheStorage = default;
            Span<long> splitNoneCosts = splitNoneCostStorage;
            Span<long> horizontalCosts = horizontalCostStorage;
            Span<long> verticalCosts = verticalCostStorage;
            Span<Av1AsymmetricModeCacheEntry> splitModeCache = splitModeCacheStorage;
            Span<Av1AsymmetricModeCacheEntry> horizontalModeCache = horizontalModeCacheStorage;
            Span<Av1AsymmetricModeCacheEntry> verticalModeCache = verticalModeCacheStorage;
            int asymmetricMask = 15;
            int fourStripMask = 3;
            int parentSourceVariance = -1;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            Av1EncoderSpeedSettings partitionSettings = this.picture.Parent.SpeedSettings;

            // Asymmetric candidates exist only above 8x8, so no smaller block keeps a cached decision.
            bool cacheAsymmetricModes = blockSize > Av1BlockSize.Block8x8 &&
                partitionSettings.ReuseBestPredictionForAsymmetricPartitions(frameHeader.IsIntra);

            Av1BlockSize extendedThreshold = partitionSettings.GetExtendedPartitionThreshold(
                frameHeader.AllowScreenContentTools, frameHeader.IsIntra, this.picture.Parent.FrameUpdateType);

            bool restrictExtendedToWinner = !this.mustFindValidPartition && partitionSettings.RestrictExtendedPartitionsToWinner(
                frameHeader.AllowScreenContentTools, frameHeader.IsIntra);

            // A losing split suppresses non-square searches, except a simple rectangle that
            // crosses an active frame edge. Directional mode pruning remains a separate decision.
            int halfWidth = blockSize.GetWidth() >> 1;
            int halfHeight = blockSize.GetHeight() >> 1;
            bool activeHorizontalEdge = blockOrigin.Y == 0 ||
                (this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2) < blockOrigin.Y + halfHeight;

            bool activeVerticalEdge = blockOrigin.X == 0 ||
                (this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) < blockOrigin.X + halfWidth;

            // Asymmetric partitions exist only where both block midpoints are inside the frame.
            bool midpointsInsideFrame =
                blockOrigin.Y + halfHeight < (this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2) &&
                blockOrigin.X + halfWidth < (this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2);

            int sub8PruningLevel = partitionSettings.GetSub8PartitionPruningLevel(frameHeader.AllowScreenContentTools);
            bool pruneSmallSplits = !this.mustFindValidPartition && blockSize == Av1BlockSize.Block8x8 &&
                (sub8PruningLevel == 2 ||
                 (sub8PruningLevel == 1 && macroBlock.IsLeftAvailable && macroBlock.IsUpAvailable &&
                  (macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -1).Block.BlockSize > Av1BlockSize.Block8x8 ||
                   macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -macroBlock.ModeInfoStride).Block.BlockSize > Av1BlockSize.Block8x8)));

            bool allowRectangularSplit = true;
            bool pruneExtendedPartitions = false;
            bool allowExtendedStage = true;
            bool pruneHorizontalRectangle = false;
            bool pruneVerticalRectangle = false;
            (int motionSplitLevel, int motionAggressiveness, bool motionTerminateNone) = partitionSettings.GetSimpleMotionPartitionSettings(
                frameHeader.AllowScreenContentTools, this.picture.Parent.FrameUpdateType);

            bool allowMotionNone = true;
            bool allowMotionSplit = !pruneSmallSplits && this.IsPartitionCandidateAllowed(blockOrigin, blockSize, Av1PartitionType.Split);
            bool squarePartitionsOnly = false;
            int intraPruningLevel = partitionSettings.GetIntraPartitionPruningLevel();
            if (!this.mustFindValidPartition && frameHeader.IsIntra && intraPruningLevel != 0 && blockSize <= Av1BlockSize.Block64x64 &&
                blockOrigin.X + blockSize.GetWidth() <= (this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) &&
                blockOrigin.Y + blockSize.GetHeight() <= (this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2))
            {
                squarePartitionsOnly = this.PruneIntraPartitions(
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    blockOrigin,
                    blockSize,
                    intraPruningLevel,
                    ref allowMotionNone,
                    ref allowMotionSplit,
                    ref allowRectangularSplit);
            }

            if (!this.mustFindValidPartition && !frameHeader.IsIntra && motionAggressiveness >= 0 &&
                frameHeader.FrameSize.SuperResolutionDenominator == Av1Constants.ScaleNumerator)
            {
                this.PrunePartitionsBySimpleMotion(
                    motionSearchPrediction,
                    interWorkspace.FilterRows,
                    in motionVectorCosts,
                    modeInfoGrid,
                    modeInfoAllocation,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    nodeIndex,
                    motionSplitLevel,
                    motionAggressiveness,
                    ref allowMotionNone,
                    ref allowMotionSplit,
                    ref allowRectangularSplit,
                    ref pruneHorizontalRectangle,
                    ref pruneVerticalRectangle);

                squarePartitionsOnly |= !allowMotionNone;
            }

            if (retrying)
            {
                allowRectangularSplit = retainedRectangularSplit;
                pruneHorizontalRectangle = retainedPruneHorizontal;
                pruneVerticalRectangle = retainedPruneVertical;
            }

            // A block above the largest partition can only split, whatever the models above decided. This rule applies after the pruning
            // that runs before the search.
            if (blockSize > this.maximumPartitionSize)
            {
                allowMotionNone = false;
                allowMotionSplit = true;
                allowRectangularSplit = false;
            }

            if (this.picture.Parent.EncoderOptions.IsAllIntra &&
                (blockSize >= Av1BlockSize.Block16x16 || (!this.mustFindValidPartition && partitionSettings.Speed >= HeifEncodingSpeed.Level6)))
            {
                int right = Math.Min(
                    blockOrigin.X + blockSize.GetWidth(),
                    this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2);

                int bottom = Math.Min(
                    blockOrigin.Y + blockSize.GetHeight(),
                    this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

                (double minimum, double maximum) = this.GetLogSubBlockVariance(
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    new Rectangle(blockOrigin.X, blockOrigin.Y, right - blockOrigin.X, bottom - blockOrigin.Y));

                // A split separates sharp detail from an almost-flat quarter before ringing spreads across the larger block. This test can
                // enable the square split again after the learned model turned it off.
                if (blockSize >= Av1BlockSize.Block16x16 && minimum < 0.272 && maximum - minimum > 3D)
                {
                    allowMotionNone = false;
                    allowMotionSplit = true;
                }
                else if (!this.mustFindValidPartition && partitionSettings.Speed >= HeifEncodingSpeed.Level6 && maximum - minimum < 3D)
                {
                    allowRectangularSplit = false;
                }
            }

            // The simple motion data exists only for inter frames.
            Span<Av1SimpleMotionData> simpleMotionData = frameHeader.IsIntra ? default : this.blockWorkspace.GetSimpleMotionData(workspaceStorage);
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Av1PartitionType partitionType = searchOrder[candidateIndex];

                // Neither the unsplit candidate nor the split candidate produced a cost, so nothing remains that this block can code. A
                // candidate that the search skipped counts as one that produced no cost. This test runs after the split search.
                if (candidateIndex > SplitSearchOrderIndex && noneInvalid && splitInvalid &&
                    !this.mustFindValidPartition &&
                    partitionSettings.TerminatePartitionSearchAfterInvalidNoneAndSplit &&
                    blockSize != this.picture.Sequence.SequenceHeader.SuperblockSize)
                {
                    break;
                }

                // A split stage that is not searched still runs the pruning that follows it, with no split cost.
                if (partitionType == Av1PartitionType.Split &&
                    (!allowMotionSplit || pruneSmallSplits || !this.IsPartitionCandidateAllowed(blockOrigin, blockSize, partitionType)))
                {
                    InlineArray4<long> unsearchedCostStorage = default;
                    Span<long> unsearchedCosts = unsearchedCostStorage;
                    bool noneAndSplitInvalid = !this.mustFindValidPartition && ShouldTerminatePartitionSearchAfterNoneAndSplit(
                        partitionSettings.TerminatePartitionSearchAfterInvalidNoneAndSplit,
                        blockSize,
                        this.picture.Sequence.SequenceHeader.SuperblockSize,
                        noneInvalid,
                        true);

                    if (this.PrunePartitionsAfterSplit(
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        in modeWorkspace,
                        motionSearchPrediction,
                        interWorkspace.FilterRows,
                        in motionVectorCosts,
                        blockOrigin,
                        blockSize,
                        nodeIndex,
                        noneInvalid,
                        bestStatistics.Cost,
                        nonePartitionCost,
                        noneCost,
                        long.MaxValue,
                        unsearchedCosts,
                        noneAndSplitInvalid,
                        ref allowRectangularSplit,
                        ref pruneExtendedPartitions,
                        ref pruneHorizontalRectangle,
                        ref pruneVerticalRectangle,
                        ref parentSourceVariance))
                    {
                        break;
                    }

                    continue;
                }

                if ((!allowMotionNone && partitionType == Av1PartitionType.None) ||
                    (squarePartitionsOnly && partitionType is not (Av1PartitionType.None or Av1PartitionType.Split)))
                {
                    continue;
                }

                if (pruneSmallSplits && partitionType != Av1PartitionType.None)
                {
                    continue;
                }

                if (!allowRectangularSplit &&
                    ((partitionType == Av1PartitionType.Horizontal && !activeHorizontalEdge) ||
                     (partitionType == Av1PartitionType.Vertical && !activeVerticalEdge) ||
                     (partitionType >= Av1PartitionType.HorizontalA && bestStatistics.Cost != long.MaxValue)))
                {
                    continue;
                }

                if (partitionType is Av1PartitionType.HorizontalA or Av1PartitionType.Horizontal4)
                {
                    // Resolve the stage from its incoming winner once. A winning asymmetric candidate
                    // must not retroactively exclude the remaining candidates in the same family.
                    bool incompatibleWinner = restrictExtendedToWinner &&
                        (partitionType == Av1PartitionType.HorizontalA
                            ? selectedPartition is not (Av1PartitionType.Horizontal or Av1PartitionType.Vertical)
                            : selectedPartition == Av1PartitionType.None);

                    bool widthAllowed = partitionType != Av1PartitionType.Horizontal4 ||
                        blockSize.GetWidth() >= (partitionSettings.MinimumPartitionSize.GetWidth() << partitionSettings.FourStripPartitionMinimumScale);

                    allowExtendedStage = bestStatistics.Cost == long.MaxValue ||
                        (!pruneExtendedPartitions && blockSize > extendedThreshold && !incompatibleWinner && widthAllowed);

                    if (partitionType == Av1PartitionType.Horizontal4 && allowExtendedStage &&
                        blockSize is >= Av1BlockSize.Block16x16 and <= Av1BlockSize.Block64x64)
                    {
                        fourStripMask = 3;
                        if (partitionSettings.ExtendedPartitionPruningLevel == 2)
                        {
                            bool horizontalWinner = selectedPartition is Av1PartitionType.Horizontal or
                                Av1PartitionType.HorizontalA or Av1PartitionType.HorizontalB or Av1PartitionType.Split or Av1PartitionType.None;

                            bool verticalWinner = selectedPartition is Av1PartitionType.Vertical or
                                Av1PartitionType.VerticalA or Av1PartitionType.VerticalB or Av1PartitionType.Split or Av1PartitionType.None;

                            fourStripMask = (horizontalWinner ? 1 : 0) | (verticalWinner ? 2 : 0);
                        }

                        if (this.IsPartitionCandidateAllowed(blockOrigin, blockSize, Av1PartitionType.Horizontal) &&
                            this.IsPartitionCandidateAllowed(blockOrigin, blockSize, Av1PartitionType.Vertical))
                        {
                            if (parentSourceVariance < 0)
                            {
                                parentSourceVariance = this.GetSourceVariance(
                                    sourceLuma, sourceBlue, sourceRed, modeWorkspace.GetCandidateReconstruction(0), blockOrigin, blockSize);
                            }

                            fourStripMask = this.ClassifyFourStripPartitions(
                                sourceLuma,
                                sourceBlue,
                                sourceRed,
                                modeWorkspace.GetCandidateReconstruction(0),
                                blockOrigin,
                                blockSize,
                                selectedPartition,
                                bestStatistics.Cost,
                                parentSourceVariance,
                                horizontalCosts,
                                verticalCosts,
                                splitNoneCosts,
                                fourStripMask);
                        }

                        if (partitionSettings.ChildPartitionPruningLevel != 0)
                        {
                            int requiredWins = Math.Min((3 * (255 - this.blockQIndex) / 255) + 1, 3);
                            int horizontalWins = 0;
                            int verticalWins = 0;
                            for (int child = 0; child < 4; child++)
                            {
                                horizontalWins += childRectangleWins[child] & 1;
                                verticalWins += (childRectangleWins[child] >> 1) & 1;
                            }

                            if (horizontalWins < requiredWins)
                            {
                                fourStripMask &= 2;
                            }

                            if (verticalWins < requiredWins)
                            {
                                fourStripMask &= 1;
                            }
                        }

                        bool boosted = this.picture.Parent.FrameUpdateType is Av1FrameUpdateType.Key or
                            Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;

                        // The strip model runs only when the frame keeps the simple motion reference.
                        if (fourStripMask == 3 && !frameHeader.IsIntra && bestStatistics.Cost != long.MaxValue &&
                            (this.picture.Parent.AvailableReferenceMask & (1 << (int)this.simpleMotionReference)) != 0 &&
                            partitionSettings.Speed >= HeifEncodingSpeed.Level1 && partitionSettings.Speed <= HeifEncodingSpeed.Level6 &&
                            (!boosted || partitionSettings.Speed >= HeifEncodingSpeed.Level3) &&
                            blockOrigin.X + blockSize.GetWidth() <=
                                (this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) &&
                            blockOrigin.Y + blockSize.GetHeight() <=
                                (this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2))
                        {
                            Av1MotionVector start = simpleMotionData[nodeIndex].Starts[(int)Av1ReferenceFrameType.Last];
                            InlineArray2<long> directionCostStorage = default;
                            Span<long> directionCosts = directionCostStorage;
                            for (int direction = 0; direction < 2; direction++)
                            {
                                Av1PartitionType stripPartition = direction == 0 ? Av1PartitionType.Horizontal4 : Av1PartitionType.Vertical4;
                                long squaredError = 0;
                                for (int strip = 0; strip < 4; strip++)
                                {
                                    GetPartitionLeafGeometry(blockOrigin, blockSize, stripPartition, strip, out Point origin, out Av1BlockSize size);
                                    this.SearchSimpleMotion(
                                        workspaceStorage,
                                        sourceLuma,
                                        sourceBlue,
                                        sourceRed,
                                        reconstructionLuma,
                                        reconstructionBlue,
                                        reconstructionRed,
                                        motionSearchPrediction,
                                        interWorkspace.FilterRows,
                                        in motionVectorCosts,
                                        origin,
                                        size,
                                        start,
                                        true,
                                        out int error,
                                        out _);

                                    squaredError += error;
                                }

                                int rate = Av1TileWriter.GetPartitionCost(
                                    this.picture, writer, tables.ModeCosts, blockSize, stripPartition, blockOrigin, in partitionEdges);

                                directionCosts[direction] = new Av1RateDistortionStatistics(this.rateMultiplier, rate, squaredError).Cost;
                            }

                            // Compare complete strips with the same starting vector and partition-symbol cost.
                            // Equal costs retain both directions.
                            if (directionCosts[0] > directionCosts[1])
                            {
                                fourStripMask &= 2;
                            }
                            else if (directionCosts[1] > directionCosts[0])
                            {
                                fourStripMask &= 1;
                            }
                        }
                    }
                }

                if ((partitionType == Av1PartitionType.Horizontal4 && (fourStripMask & 1) == 0) ||
                    (partitionType == Av1PartitionType.Vertical4 && (fourStripMask & 2) == 0))
                {
                    continue;
                }

                if (partitionType >= Av1PartitionType.HorizontalA && !allowExtendedStage)
                {
                    continue;
                }

                // The asymmetric stage measures the variance of the complete block. The source border
                // covers a block only while its midpoints are inside the frame, which is also the only
                // geometry where an asymmetric candidate is legal.
                if (partitionType == Av1PartitionType.HorizontalA && midpointsInsideFrame)
                {
                    if (parentSourceVariance < 0)
                    {
                        parentSourceVariance = this.GetSourceVariance(
                            sourceLuma, sourceBlue, sourceRed, modeWorkspace.GetCandidateReconstruction(0), blockOrigin, blockSize);
                    }

                    // Compare all four asymmetric candidates against the same incoming winner and costs.
                    // Invalid or unvisited component searches contribute zero to this lower-cost estimate.
                    int pruningLevel = partitionSettings.ExtendedPartitionPruningLevel;
                    bool flatNone = pruningLevel == 1 && selectedPartition == Av1PartitionType.None &&
                        parentSourceVariance < 32;

                    bool allowHorizontal = selectedPartition is Av1PartitionType.Horizontal or Av1PartitionType.Split || flatNone;
                    bool allowVertical = selectedPartition is Av1PartitionType.Vertical or Av1PartitionType.Split || flatNone;
                    int weight = pruningLevel == 1 ? 14 : 15;
                    for (int child = 0; child < 4; child++)
                    {
                        if (splitNoneCosts[child] == long.MaxValue)
                        {
                            splitNoneCosts[child] = 0;
                        }
                    }

                    for (int child = 0; child < 2; child++)
                    {
                        if (horizontalCosts[child] == long.MaxValue)
                        {
                            horizontalCosts[child] = 0;
                        }

                        if (verticalCosts[child] == long.MaxValue)
                        {
                            verticalCosts[child] = 0;
                        }
                    }

                    asymmetricMask = 0;
                    if (allowHorizontal)
                    {
                        if (((horizontalCosts[1] + splitNoneCosts[0] + splitNoneCosts[1]) / 16 * weight) < bestStatistics.Cost)
                        {
                            asymmetricMask |= 1;
                        }

                        if (((horizontalCosts[0] + splitNoneCosts[2] + splitNoneCosts[3]) / 16 * weight) < bestStatistics.Cost)
                        {
                            asymmetricMask |= 2;
                        }
                    }

                    if (allowVertical)
                    {
                        if (((verticalCosts[1] + splitNoneCosts[0] + splitNoneCosts[2]) / 16 * weight) < bestStatistics.Cost)
                        {
                            asymmetricMask |= 4;
                        }

                        if (((verticalCosts[0] + splitNoneCosts[1] + splitNoneCosts[3]) / 16 * weight) < bestStatistics.Cost)
                        {
                            asymmetricMask |= 8;
                        }
                    }

                    if (this.IsPartitionCandidateAllowed(blockOrigin, blockSize, Av1PartitionType.Horizontal) &&
                        this.IsPartitionCandidateAllowed(blockOrigin, blockSize, Av1PartitionType.Vertical))
                    {
                        // The classifier selects its own mask from all sixteen classes, replacing the
                        // preliminary cost mask rather than intersecting away classes it permits.
                        asymmetricMask = this.ClassifyAsymmetricPartitions(
                            blockSize, selectedPartition, bestStatistics.Cost, horizontalCosts, verticalCosts, splitNoneCosts, asymmetricMask);
                    }

                    if (partitionSettings.ChildPartitionPruningLevel >= 2)
                    {
                        int requiredWins = Math.Min(3 * (2 * (255 - this.blockQIndex) / 255), 3);
                        for (int shape = 0; shape < 4; shape++)
                        {
                            int firstChild = shape < 2 ? shape * 2 : shape - 2;
                            int secondChild = firstChild + (shape < 2 ? 1 : 2);
                            int directionBit = shape < 2 ? 1 : 2;

                            // The superblock root has no rectangle win record. At the root, a win means that the current best partition is
                            // that rectangle.
                            int wins = nodeIndex == 0
                                ? (selectedPartition == (shape < 2 ? Av1PartitionType.Horizontal : Av1PartitionType.Vertical) ? 1 : 0)
                                : (rectangleWins & directionBit) != 0 ? 1 : 0;

                            Av1PartitionType firstPartition =
                                (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[(nodeIndex * 4) + firstChild + 1];

                            Av1PartitionType secondPartition =
                                (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[(nodeIndex * 4) + secondChild + 1];

                            // Unvisited children retain their initial unsplit choice for this decision.
                            wins += firstPartition is Av1PartitionType.None or Av1PartitionType.Invalid ? 1 : 0;
                            wins += secondPartition is Av1PartitionType.None or Av1PartitionType.Invalid ? 1 : 0;
                            if (wins < requiredWins)
                            {
                                asymmetricMask &= ~(1 << shape);
                            }
                        }
                    }
                }

                if (partitionType is >= Av1PartitionType.HorizontalA and <= Av1PartitionType.VerticalB &&
                    (asymmetricMask & (1 << ((int)partitionType - (int)Av1PartitionType.HorizontalA))) == 0)
                {
                    continue;
                }

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
                            // These leading leaves have the same geometry and already reconstructed neighbors. The reuse excludes the palette
                            // and CfL choices, because their inputs depend on the reconstruction and palette contexts of the partition around
                            // them.
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

                Span<long> childCosts = partitionType switch
                {
                    Av1PartitionType.Split => splitNoneCosts,
                    Av1PartitionType.Horizontal => horizontalCosts,
                    Av1PartitionType.Vertical => verticalCosts,
                    _ => []
                };

                // An asymmetric sub-block keeps the luma mode of the candidate that already covered the same samples.
                this.asymmetricModeCache = default;
                if (cacheAsymmetricModes && partitionType is >= Av1PartitionType.HorizontalA and <= Av1PartitionType.VerticalB)
                {
                    SetAsymmetricModeCache(
                        ref this.asymmetricModeCache, partitionType, splitModeCache, horizontalModeCache, verticalModeCache);
                }

                // A block above the maximum partition size keeps what its split children leave. The contexts are not restored after the
                // split, so the dry run of the last child leaves its contexts, as the dry run of every earlier child does.
                bool keepsSplitContexts = blockSize > this.maximumPartitionSize &&
                    blockSize != this.picture.Sequence.SequenceHeader.SuperblockSize;

                Av1RateDistortionStatistics candidateStatistics = this.EvaluatePartitionCandidate(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in partitionEdges,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    encoderSegmentMap,
                    searchSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    partitionType,
                    nodeIndex,
                    bestStatistics,
                    searchChildren: true,
                    publishFinalContexts: keepsSplitContexts && partitionType == Av1PartitionType.Split,
                    childCosts,
                    partitionType == Av1PartitionType.Split ? childRectangleWins : [],
                    out int stoppedAtLeaf,
                    out long accumulatedCost);

                this.asymmetricModeCache = default;
                if (cacheAsymmetricModes)
                {
                    this.CaptureAsymmetricModeCacheSources(
                        nodeIndex,
                        partitionType,
                        stoppedAtLeaf,
                        childCosts,
                        ref splitModeCacheStorage,
                        ref horizontalModeCacheStorage,
                        ref verticalModeCacheStorage);
                }

                if (candidateStatistics.Cost >= bestStatistics.Cost)
                {
                    if (partitionType == Av1PartitionType.Horizontal)
                    {
                        rectangleWins &= 2;
                    }
                    else if (partitionType == Av1PartitionType.Vertical)
                    {
                        rectangleWins &= 1;
                    }
                }

                bool terminateAfterNone = false;
                if (partitionType == Av1PartitionType.None)
                {
                    noneInvalid = stoppedAtLeaf == 0 || accumulatedCost == long.MaxValue;

                    // A failed unsplit search leaves no cost.
                    nonePartitionCost = noneInvalid ? long.MaxValue : accumulatedCost;

                    // The unsplit cost is priced again from the rate and distortion of the block at the multiplier of the node. This drops
                    // the cost bias of the image tune.
                    Av1RateDistortionStatistics noneStatistics =
                        this.blockWorkspace.PartitionTree.GetContext(nodeIndex, Av1PartitionType.None, 0).Snapshot.Statistics;

                    noneStatistics.UpdateCost(this.rateMultiplier);
                    noneCost = noneInvalid ? long.MaxValue : noneStatistics.Cost;

                    if (!noneInvalid && !this.picture.Parent.FrameHeader.IsIntra &&
                        this.picture.Parent.SpeedSettings.GetRectangularPartitionReferencePruning(this.picture.Parent.FrameUpdateType) != 0)
                    {
                        this.UpdatePickedReferenceFrames(
                            blockOrigin,
                            blockSize,
                            this.blockWorkspace.PartitionTree.GetContext(nodeIndex, Av1PartitionType.None, 0).Snapshot.ModeInfo.Block);
                    }

                    // The unsplit search leaves the block variance that it measured, whether or not it found a mode. A search that returned
                    // on a negative bound leaves the variance of the block searched before it. Before the first search of the superblock, it
                    // leaves no variance.
                    parentSourceVariance = this.interSourceVariance;

                    if (candidateStatistics.Cost < bestStatistics.Cost && !this.picture.Parent.FrameHeader.IsIntra &&
                        !this.BlockLossless && (allowMotionSplit || allowRectangularSplit) &&
                        IsSkippable(this.blockWorkspace.PartitionTree.GetContext(nodeIndex, Av1PartitionType.None, 0).Snapshot))
                    {
                        // The distortion threshold scales by the block area relative to a maximum superblock. The rate threshold uses the
                        // logarithmic sample count. Both tests must pass before smaller partitions stop.
                        int sampleCountLog2 = BitOperations.Log2((uint)GetBlockArea(blockSize));
                        Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
                        long distortionThreshold = settings.PartitionBreakoutDistortionThreshold >>
                            ((2 * Av1Constants.MaxSuperBlockSizeLog2) - sampleCountLog2);

                        int rateThreshold = settings.PartitionBreakoutRateThreshold * sampleCountLog2;

                        // The breakout clears the square and rectangular searches, but it does not end the search. A rectangle on an active
                        // image edge and the pruning after the split stage still run.
                        if (this.ShouldStopPartitionSearch(blockSize, candidateStatistics) ||
                            (candidateStatistics.Distortion < distortionThreshold && candidateStatistics.Rate < rateThreshold))
                        {
                            allowMotionSplit = false;
                            allowRectangularSplit = false;
                        }
                    }

                    if (!noneInvalid && this.picture.Parent.SpeedSettings.PruneRectangularPartitionsUsingIntraMode)
                    {
                        Av1PredictionMode noneMode = this.blockWorkspace.PartitionTree
                            .GetContext(nodeIndex, Av1PartitionType.None, 0)
                            .Snapshot.ModeInfo.Block.Mode;

                        if (noneMode is Av1PredictionMode.DC or Av1PredictionMode.Smooth)
                        {
                            bool largerLeft = macroBlock.IsLeftAvailable &&
                                GetBlockArea(macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -1).Block.BlockSize) > GetBlockArea(blockSize);

                            bool largerAbove = macroBlock.IsUpAvailable &&
                                GetBlockArea(macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -macroBlock.ModeInfoStride).Block.BlockSize) >
                                GetBlockArea(blockSize);

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

                // The model also runs in the retry that must find a partition.
                if (partitionType == Av1PartitionType.None && candidateStatistics.Cost < bestStatistics.Cost &&
                    !terminateAfterNone &&
                    motionTerminateNone && frameHeader.ShowFrame && !frameHeader.IsIntra && blockSize >= Av1BlockSize.Block16x16 &&
                    blockOrigin.X + halfWidth < (this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) &&
                    blockOrigin.Y + halfHeight < (this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2) &&
                    candidateStatistics.Cost is >= 0 and < long.MaxValue && candidateStatistics.Rate is >= 0 and < int.MaxValue &&
                    (allowMotionSplit || allowRectangularSplit))
                {
                    terminateAfterNone = this.ShouldTerminateAfterMotionNone(
                        motionSearchPrediction,
                        interWorkspace.FilterRows,
                        in motionVectorCosts,
                        modeInfoGrid,
                        modeInfoAllocation,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        nodeIndex,
                        candidateStatistics);
                }

                if (partitionType == Av1PartitionType.Split &&
                    candidateStatistics.Cost >= bestStatistics.Cost &&
                    (this.picture.Parent.SpeedSettings.RectangularPartitionPruningLevel == 2 || stoppedAtLeaf <= 2) &&
                    noneCost > 0 &&
                    noneCost < accumulatedCost)
                {
                    allowRectangularSplit = false;
                }

                if (partitionType == Av1PartitionType.Split)
                {
                    splitInvalid = accumulatedCost == long.MaxValue;
                }

                bool terminateAfterSplit = !this.mustFindValidPartition && partitionType == Av1PartitionType.Split &&
                    ShouldTerminatePartitionSearchAfterNoneAndSplit(
                        this.picture.Parent.SpeedSettings.TerminatePartitionSearchAfterInvalidNoneAndSplit,
                        blockSize,
                        this.picture.Sequence.SequenceHeader.SuperblockSize,
                        noneInvalid,
                        accumulatedCost == long.MaxValue);

                if (candidateStatistics.Cost < bestStatistics.Cost)
                {
                    bestStatistics = candidateStatistics;
                    selectedStatistics = candidateStatistics;
                    selectedPartition = partitionType;
                }

                if (partitionType == Av1PartitionType.Split)
                {
                    terminateAfterSplit = this.PrunePartitionsAfterSplit(
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        in modeWorkspace,
                        motionSearchPrediction,
                        interWorkspace.FilterRows,
                        in motionVectorCosts,
                        blockOrigin,
                        blockSize,
                        nodeIndex,
                        noneInvalid,
                        bestStatistics.Cost,
                        nonePartitionCost,
                        noneCost,
                        accumulatedCost,
                        splitNoneCosts,
                        terminateAfterSplit,
                        ref allowRectangularSplit,
                        ref pruneExtendedPartitions,
                        ref pruneHorizontalRectangle,
                        ref pruneVerticalRectangle,
                        ref parentSourceVariance);
                }

                // A block above the maximum partition size never reconstructs its winning subtree again, so the split children keep the
                // contexts and coefficients that they produced.
                if (partitionType != Av1PartitionType.Split || !keepsSplitContexts)
                {
                    this.ResetPartitionTrial(
                        workspaceStorage,
                        in partitionEdges,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        blockOrigin,
                        blockSize,
                        savedLumaArea,
                        savedChromaArea);
                }

                if (terminateAfterNone || terminateAfterSplit)
                {
                    break;
                }
            }

            if (blockSize == this.picture.Sequence.SequenceHeader.SuperblockSize && selectedStatistics.Cost == long.MaxValue)
            {
                // A superblock must produce a legal partition. Retry with optional shape restrictions
                // removed, while retaining frame-edge syntax and the configured minimum/maximum sizes.
                this.mustFindValidPartition = true;
                retrying = true;
                retainedRectangularSplit = allowRectangularSplit;
                retainedPruneHorizontal = pruneHorizontalRectangle;
                retainedPruneVertical = pruneVerticalRectangle;
                goto SearchPartitions;
            }

            this.superblock.Workspace.PartitionSearchTypes[nodeIndex] = selectedStatistics.Cost == long.MaxValue
                ? (byte)Av1PartitionType.Invalid
                : (byte)selectedPartition;

            if (!this.picture.Parent.FrameHeader.IsIntra)
            {
                this.blockWorkspace.GetSimpleMotionData(workspaceStorage)[nodeIndex].Partition = selectedPartition;
            }

            return selectedPartition;
        }

        /// <summary>
        /// Measures the minimum and maximum log variance of visible 4x4 source blocks.
        /// </summary>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="bounds">The visible luma area to measure, in samples.</param>
        /// <returns>The smallest and the largest log variance of the 4x4 source blocks in the area.</returns>
        private (double Minimum, double Maximum) GetLogSubBlockVariance(
            Span<int> workspaceStorage, ReadOnlySpan<TSample> sourceLuma, ReadOnlySpan<TSample> sourceBlue, ReadOnlySpan<TSample> sourceRed, Rectangle bounds)
        {
            double minimum = double.MaxValue;
            double maximum = 0;
            for (int y = bounds.Top; y < bounds.Bottom; y += 4)
            {
                for (int x = bounds.Left; x < bounds.Right; x += 4)
                {
                    double variance = this.GetSourceLogVariance(workspaceStorage, sourceLuma, sourceBlue, sourceRed, new Point(x, y));
                    minimum = Math.Min(minimum, variance);
                    maximum = Math.Max(maximum, variance);
                }
            }

            return (minimum, maximum);
        }

        /// <summary>
        /// Gets the log variance of one 4x4 source block. The superblock keeps each value after the first measure, and a
        /// negative entry marks a value that is not measured yet.
        /// </summary>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="origin">The luma origin of the 4x4 block, in samples.</param>
        /// <returns>The log of one plus the variance of the 4x4 source block.</returns>
        private double GetSourceLogVariance(
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Point origin)
        {
            int side = 1 << this.picture.Sequence.SequenceHeader.SuperblockSizeLog2;
            int index = (((origin.Y & (side - 1)) >> 2) * (side >> 2)) + ((origin.X & (side - 1)) >> 2);
            ref double variance = ref this.blockWorkspace.GetSourceLogVariances(workspaceStorage)[index];
            if (variance < 0)
            {
                variance = this.GetLogVariance(this.source.GetPlane(Av1Plane.Y), sourceLuma, origin);
            }

            return variance;
        }

        /// <summary>
        /// Computes log(1 + variance) of one 4x4 cell of a frame plane.
        /// </summary>
        /// <param name="plane">The frame plane.</param>
        /// <param name="planeSamples">The samples of the complete frame plane, read once by the caller.</param>
        /// <param name="origin">The origin of the 4x4 cell, in plane samples.</param>
        /// <returns>The log of one plus the variance of the cell.</returns>
        private double GetLogVariance(Av1PlaneRegion<TSample> plane, ReadOnlySpan<TSample> planeSamples, Point origin)
        {
            InlineArray4<TSample> zero = default;
            TOperator.GetMoments(
                Av1TransformBlockEncoder.GetPlaneSpan(planeSamples, plane, origin),
                plane.Stride,
                zero,
                0,
                4,
                4,
                out int sum,
                out long squares);

            // Normalize both moments before subtracting the squared mean. Plane storage includes
            // the final 4x4 cell up to the mode-information boundary.
            int sampleShift = this.bitDepth.GetBitCount() - 8;
            int squareShift = sampleShift * 2;
            sum = (sum + ((1 << sampleShift) >> 1)) >> sampleShift;
            squares = (squares + ((1L << squareShift) >> 1)) >> squareShift;
            long variance = Math.Max(0, squares - (((long)sum * sum) >> 4));
            return double.LogP1(variance / 16D);
        }

        /// <summary>
        /// Scales a predictor's cost by how far its reconstruction drifts from the source in log variance,
        /// reading the reconstruction from the candidate the last transform grid produced.
        /// </summary>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="origin">The luma block origin, in samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="reconstruction">The reconstruction of the candidate, starting at the block origin.</param>
        /// <param name="reconstructionStride">The distance between rows of the reconstruction.</param>
        /// <returns>The cost factor, from one to three.</returns>
        private double GetIntraVarianceFactor(
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Point origin,
            Av1BlockSize blockSize,
            ReadOnlySpan<TSample> reconstruction,
            int reconstructionStride)
        {
            double threshold = 1D - (0.25D * (int)this.picture.Parent.EncodingSpeed);
            if (threshold <= 0)
            {
                return 1D;
            }

            int right = Math.Min(origin.X + blockSize.GetWidth(), this.picture.Parent.Common.ModeInfoColumnCount << 2);
            int bottom = Math.Min(origin.Y + blockSize.GetHeight(), this.picture.Parent.Common.ModeInfoRowCount << 2);
            double sourceVariance = 0;
            double reconstructionVariance = 0;
            for (int y = origin.Y; y < bottom; y += 4)
            {
                for (int x = origin.X; x < right; x += 4)
                {
                    sourceVariance += this.GetSourceLogVariance(workspaceStorage, sourceLuma, sourceBlue, sourceRed, new Point(x, y));
                    reconstructionVariance += GetLogVariance(
                        reconstruction[(((y - origin.Y) * reconstructionStride) + (x - origin.X))..],
                        reconstructionStride,
                        this.bitDepth);
                }
            }

            int cells = (right - origin.X) * (bottom - origin.Y) / 16;
            sourceVariance = (sourceVariance / cells) + 0.000001D;
            reconstructionVariance = (reconstructionVariance / cells) + 0.000001D;

            // A loss of detail in a flat reconstruction gets a larger penalty than added variation. The small offset keeps the ratio of a
            // flat source block finite. The cap limits the influence of such blocks.
            double difference = sourceVariance - reconstructionVariance;
            double factor = 1D;
            if (difference > 0.5D && reconstructionVariance < threshold)
            {
                factor += 2D * difference / sourceVariance;
            }
            else if (difference < -0.5D && sourceVariance < threshold)
            {
                factor -= difference / (2D * sourceVariance);
            }

            return Math.Min(3D, factor);
        }

        /// <summary>
        /// Computes log(1 + variance) of one 4x4 cell held in a flat candidate buffer.
        /// </summary>
        private static double GetLogVariance(ReadOnlySpan<TSample> cell, int stride, Av1BitDepth bitDepth)
        {
            InlineArray4<TSample> zero = default;
            TOperator.GetMoments(cell, stride, zero, 0, 4, 4, out int sum, out long squares);
            int sampleShift = bitDepth.GetBitCount() - 8;
            int squareShift = sampleShift * 2;
            sum = (sum + ((1 << sampleShift) >> 1)) >> sampleShift;
            squares = (squares + ((1L << squareShift) >> 1)) >> squareShift;
            long variance = Math.Max(0, squares - (((long)sum * sum) >> 4));
            return double.LogP1(variance / 16D);
        }

        /// <summary>
        /// Selects the samples of one component plane from the samples of the three planes.
        /// </summary>
        /// <param name="plane">The component plane.</param>
        /// <param name="luma">The luma samples.</param>
        /// <param name="blue">The blue-difference samples.</param>
        /// <param name="red">The red-difference samples.</param>
        /// <returns>The samples of the plane.</returns>
        private static ReadOnlySpan<TSample> SelectPlane(Av1Plane plane, ReadOnlySpan<TSample> luma, ReadOnlySpan<TSample> blue, ReadOnlySpan<TSample> red)
            => plane == Av1Plane.Y ? luma : plane == Av1Plane.U ? blue : red;

        /// <summary>
        /// Selects the writable samples of one component plane from the samples of the three planes.
        /// </summary>
        /// <param name="plane">The component plane.</param>
        /// <param name="luma">The luma samples.</param>
        /// <param name="blue">The blue-difference samples.</param>
        /// <param name="red">The red-difference samples.</param>
        /// <returns>The samples of the plane.</returns>
        private static Span<TSample> SelectPlane(Av1Plane plane, Span<TSample> luma, Span<TSample> blue, Span<TSample> red)
            => plane == Av1Plane.Y ? luma : plane == Av1Plane.U ? blue : red;

        /// <summary>
        /// Returns the number of luma samples in a block size.
        /// </summary>
        /// <param name="blockSize">The block size.</param>
        /// <returns>The width times the height.</returns>
        private static int GetBlockArea(Av1BlockSize blockSize)
            => blockSize.GetWidth() * blockSize.GetHeight();

        /// <summary>
        /// Prunes the partitions that follow the split stage, whether or not that stage searched the split. A skippable unsplit block
        /// without a new vector drops the rectangles. The after-split model can end the search, and the rectangle model can drop either
        /// direction.
        /// </summary>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="modeWorkspace">The mode decision buffers, whose first candidate row holds the variance midpoint.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the simple motion searches.</param>
        /// <param name="filterRows">The intermediate rows of the prediction filters.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="nodeIndex">The partition tree node of the block.</param>
        /// <param name="noneInvalid">Whether the unsplit candidate produced no cost.</param>
        /// <param name="bestCost">The best cost of the block so far.</param>
        /// <param name="nonePartitionCost">The unsplit cost with its partition syntax.</param>
        /// <param name="noneCost">The unsplit cost of the block decisions.</param>
        /// <param name="splitCost">The split cost, or the maximum when the split was not searched.</param>
        /// <param name="splitCosts">The costs of the split children.</param>
        /// <param name="terminated">Whether the search already ends after the split stage.</param>
        /// <param name="allowRectangularSplit">Whether rectangles remain searchable.</param>
        /// <param name="pruneExtendedPartitions">Whether the extended partitions are pruned.</param>
        /// <param name="pruneHorizontalRectangle">Whether the horizontal rectangle is pruned.</param>
        /// <param name="pruneVerticalRectangle">Whether the vertical rectangle is pruned.</param>
        /// <param name="parentSourceVariance">The cached source variance of the block, or a negative value.</param>
        /// <returns><see langword="true"/> when the partition search ends here.</returns>
        private bool PrunePartitionsAfterSplit(
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<TSample> motionSearchPrediction,
            Span<short> filterRows,
            in Av1MotionVectorCosts motionVectorCosts,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int nodeIndex,
            bool noneInvalid,
            long bestCost,
            long nonePartitionCost,
            long noneCost,
            long splitCost,
            ReadOnlySpan<long> splitCosts,
            bool terminated,
            ref bool allowRectangularSplit,
            ref bool pruneExtendedPartitions,
            ref bool pruneHorizontalRectangle,
            ref bool pruneVerticalRectangle,
            ref int parentSourceVariance)
        {
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            if (!noneInvalid && blockSize >= Av1BlockSize.Block16x16 && settings.SkippablePartitionPruningLevel != 0)
            {
                ref Av1EncoderPartitionTree.ModeSnapshot noneSnapshot = ref this.blockWorkspace.PartitionTree
                    .GetContext(nodeIndex, Av1PartitionType.None, 0).Snapshot;

                Av1MacroBlockModeInfo noneModeInfo = noneSnapshot.ModeInfo;

                // A zero-residual square makes further shape refinement optional. The stronger setting also excludes ordinary rectangles,
                // but only for inherited motion at lower quantizers. Both read the skippable result of the search, not the skip flag that
                // the cost comparison chooses.
                bool noneSkippable = IsSkippable(noneSnapshot);
                pruneExtendedPartitions = !this.mustFindValidPartition && settings.SkippablePartitionPruningLevel >= 1 && noneSkippable;
                if (!this.mustFindValidPartition && settings.SkippablePartitionPruningLevel >= 2 && noneSkippable &&
                    this.blockQIndex <= 200 &&
                    noneModeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra &&
                    noneModeInfo.Block.Mode is not (Av1PredictionMode.NewMotionVector or Av1PredictionMode.NewNewMotionVector or
                        Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NewNearestMotionVector or
                        Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector))
                {
                    allowRectangularSplit = false;
                }
            }

            bool rectangleAllowed = this.IsPartitionCandidateAllowed(blockOrigin, blockSize, Av1PartitionType.Horizontal) ||
                this.IsPartitionCandidateAllowed(blockOrigin, blockSize, Av1PartitionType.Vertical);

            // The model also runs in the retry that must find a partition. Its gate does not read that retry flag.
            if (!terminated && !frameHeader.IsIntra && settings.AfterSplitTerminationLevel != 0 &&
                allowRectangularSplit && rectangleAllowed)
            {
                terminated = this.ShouldTerminateAfterSplit(
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    motionSearchPrediction,
                    filterRows,
                    in motionVectorCosts,
                    blockOrigin,
                    blockSize,
                    nodeIndex,
                    bestCost,
                    nonePartitionCost,
                    splitCost,
                    splitCosts);
            }

            if (!terminated && !frameHeader.IsIntra && settings.EnableRectanglePartitionModel &&
                !pruneHorizontalRectangle && !pruneVerticalRectangle && rectangleAllowed)
            {
                if (parentSourceVariance < 0)
                {
                    parentSourceVariance = this.GetSourceVariance(
                        sourceLuma, sourceBlue, sourceRed, modeWorkspace.GetCandidateReconstruction(0), blockOrigin, blockSize);
                }

                this.PruneRectangularPartitions(
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    modeWorkspace.GetCandidateReconstruction(0),
                    blockOrigin,
                    blockSize,
                    bestCost,
                    noneCost,
                    parentSourceVariance,
                    splitCosts,
                    out pruneHorizontalRectangle,
                    out pruneVerticalRectangle);
            }

            return terminated;
        }

        /// <summary>
        /// Gets whether a selected mode is skippable for the partition search, from the empty-transform flag of its rate statistics. An
        /// inter mode is skippable when its transform search left every block empty. A regular intra mode of an inter frame never is,
        /// because its statistics always code the non-skip flag. A palette winner is skippable when both its luma and its chroma quantized
        /// to nothing. This is not the coded skip flag.
        /// </summary>
        /// <param name="snapshot">The mode snapshot of the selected mode.</param>
        /// <returns><see langword="true"/> when the mode is skippable.</returns>
        private static bool IsSkippable(Av1EncoderPartitionTree.ModeSnapshot snapshot)
            => snapshot.Statistics.AllTransformsEmpty;

        /// <summary>
        /// Returns whether the partition search of a block ends because neither the unsplit candidate nor the split candidate produced
        /// a cost. The superblock itself never ends this way.
        /// </summary>
        /// <param name="enabled">Whether the speed settings enable this termination.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="superblockSize">The superblock size.</param>
        /// <param name="noneInvalid">Whether the unsplit candidate produced no cost.</param>
        /// <param name="splitInvalid">Whether the split candidate produced no cost.</param>
        /// <returns><see langword="true"/> when the partition search ends.</returns>
        internal static bool ShouldTerminatePartitionSearchAfterNoneAndSplit(
            bool enabled,
            Av1BlockSize blockSize,
            Av1BlockSize superblockSize,
            bool noneInvalid,
            bool splitInvalid)
            => enabled &&
                blockSize != superblockSize &&
                noneInvalid &&
                splitInvalid;

        /// <summary>
        /// Gets a value indicating whether the leaves of a given partition keep the full mode search, not the estimated one. This is true
        /// for a fixed partition, and for a variance partition of an intra frame while the estimated mode decision is off.
        /// </summary>
        /// <param name="picture">The picture.</param>
        /// <returns><see langword="true"/> when the leaves keep the full mode search.</returns>
        private static bool SearchesVariancePartitionLeaves(Av1PictureControlSet picture)
            => picture.Parent.FixedPartitionSize != Av1BlockSize.Invalid ||
                (picture.Parent.SpeedSettings.UseVarianceBasedPartition &&
                picture.Parent.FrameHeader.IsIntra &&
                !picture.Parent.SpeedSettings.UseEstimatedModeDecision);

        /// <summary>
        /// Gets a value indicating whether the superblock searches only the modes of a given partition. This is true for the
        /// variance-based partition and for a fixed partition.
        /// </summary>
        /// <param name="picture">The picture.</param>
        /// <returns><see langword="true"/> when the partition is given.</returns>
        private static bool UsesGivenPartition(Av1PictureControlSet picture)
            => picture.Parent.SpeedSettings.UseVarianceBasedPartition || picture.Parent.FixedPartitionSize != Av1BlockSize.Invalid;

        /// <summary>
        /// Runs <see cref="SearchVariancePartitionCore"/> at the rate multiplier of the block.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="nodeIndex">The partition tree node of the block.</param>
        /// <param name="reconstruct">Whether the finished subtree is encoded again.</param>
        /// <returns><see langword="true"/> when the partition produced a valid result.</returns>
        private bool SearchVariancePartition(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int nodeIndex,
            bool reconstruct)
        {
            // A block outside the frame skips the block setup.
            int savedRateMultiplier = this.rateMultiplier;
            this.rateMultiplier = this.IsBlockOriginInsideFrame(blockOrigin)
                ? this.SetupBlockRateMultiplier(blockOrigin, blockSize)
                : this.GetBlockRateMultiplier(blockOrigin, blockSize);

            bool result = this.SearchVariancePartitionCore(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                blockResidual,
                searchCoefficients,
                searchReconstructions,
                estimationRowCoefficients,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                transformPrediction,
                interIntraAbove,
                interIntraLeft,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in partitionEdges,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                encoderSegmentMap,
                searchSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                blockSize,
                nodeIndex,
                reconstruct);

            this.rateMultiplier = savedRateMultiplier;
            return result;
        }

        /// <summary>
        /// Searches the modes of the partition that the variance analysis chose. A finished subtree that a later block predicts from is
        /// encoded again from the decisions that it kept. Then the entropy contexts return to their state before the subtree, so the
        /// writer encodes the superblock again from the start.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="nodeIndex">The partition tree node of the block.</param>
        /// <param name="reconstruct">Whether the finished subtree is encoded again.</param>
        /// <returns><see langword="true"/> when the partition produced a valid result.</returns>
        private bool SearchVariancePartitionCore(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int nodeIndex,
            bool reconstruct)
        {
            if (!this.IsBlockOriginInsideFrame(blockOrigin))
            {
                return true;
            }

            Av1PartitionType partition = (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[nodeIndex];
            int savedLumaArea = this.codedAreaLuma;
            int savedChromaArea = this.codedAreaChroma;
            this.SavePartitionTrialContexts(
                workspaceStorage,
                in partitionEdges,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                blockOrigin,
                blockSize);

            bool valid = true;
            switch (partition)
            {
                case Av1PartitionType.None:
                    valid = this.EvaluatePartitionLeaf(
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        referenceContexts,
                        encoderSegmentMap,
                        searchSegmentMap,
                        previousSegmentMap,
                        superblockCoefficients,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        Av1PartitionType.None,
                        this.blockWorkspace.PartitionTree.GetContext(nodeIndex, Av1PartitionType.None, 0),
                        long.MaxValue,
                        false,
                        false).Cost != long.MaxValue;

                    break;

                case Av1PartitionType.Horizontal:
                case Av1PartitionType.Vertical:
                    Av1EncoderPartitionTree.ModeContext first = this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partition, 0);
                    Av1BlockSize subSize = partition.GetBlockSubSize(blockSize);
                    valid = this.EvaluatePartitionLeaf(
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        referenceContexts,
                        encoderSegmentMap,
                        searchSegmentMap,
                        previousSegmentMap,
                        superblockCoefficients,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        subSize,
                        partition,
                        first,
                        long.MaxValue,
                        false,
                        false).Cost != long.MaxValue;

                    GetPartitionLeafGeometry(blockOrigin, blockSize, partition, 1, out Point secondOrigin, out _);
                    if (valid && this.IsBlockOriginInsideFrame(secondOrigin))
                    {
                        // The first half is encoded as a dry run before the second is searched, so the second predicts from it.
                        int firstLumaArea = this.codedAreaLuma;
                        int firstChromaArea = this.codedAreaChroma;
                        if (first.Snapshot.ModeInfo.Block.UseIntraBlockCopy)
                        {
                            this.ReconstructPartitionLeaf(
                                writer,
                                in tables,
                                in modeWorkspace,
                                transformCoefficients,
                                dequantizedCoefficients,
                                transformWorkspace,
                                in interWorkspace,
                                transformPrediction,
                                interIntraAbove,
                                interIntraLeft,
                                firstIntermediate,
                                secondIntermediate,
                                compoundMask,
                                in transformEdges,
                                in paletteEdges,
                                in lumaCoefficientEdges,
                                in blueCoefficientEdges,
                                in redCoefficientEdges,
                                modeInfoGrid,
                                modeInfoAllocation,
                                displacementVectors,
                                referenceContexts,
                                encoderSegmentMap,
                                previousSegmentMap,
                                superblockCoefficients,
                                sourceLuma,
                                sourceBlue,
                                sourceRed,
                                reconstructionLuma,
                                reconstructionBlue,
                                reconstructionRed,
                                macroBlock,
                                blockOrigin,
                                first,
                                true,
                                false,
                                false);
                        }
                        else
                        {
                            this.ReconstructSelectedIntraBlock(
                                writer,
                                in tables,
                                in modeWorkspace,
                                transformCoefficients,
                                dequantizedCoefficients,
                                transformWorkspace,
                                in lumaCoefficientEdges,
                                in blueCoefficientEdges,
                                in redCoefficientEdges,
                                modeInfoGrid,
                                modeInfoAllocation,
                                encoderSegmentMap,
                                previousSegmentMap,
                                superblockCoefficients,
                                sourceLuma,
                                sourceBlue,
                                sourceRed,
                                reconstructionLuma,
                                reconstructionBlue,
                                reconstructionRed,
                                macroBlock,
                                blockOrigin,
                                first);

                            this.PublishPartitionLeafContexts(
                                in transformEdges,
                                in paletteEdges,
                                in lumaCoefficientEdges,
                                in blueCoefficientEdges,
                                in redCoefficientEdges,
                                superblockCoefficients,
                                macroBlock,
                                blockOrigin,
                                firstLumaArea,
                                firstChromaArea,
                                first.Snapshot.ModeInfo,
                                first.Snapshot.Block,
                                first.Snapshot.Palette,
                                true);
                        }

                        this.codedAreaLuma = firstLumaArea;
                        this.codedAreaChroma = firstChromaArea;
                        valid = this.EvaluatePartitionLeaf(
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            searchDequantizedCoefficients,
                            transformWorkspace,
                            transformTypeProbabilities,
                            blockResidual,
                            searchCoefficients,
                            searchReconstructions,
                            estimationRowCoefficients,
                            in interWorkspace,
                            motionSearchPrediction,
                            in motionVectorCosts,
                            transformPrediction,
                            interIntraAbove,
                            interIntraLeft,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            in transformEdges,
                            in paletteEdges,
                            in lumaCoefficientEdges,
                            in blueCoefficientEdges,
                            in redCoefficientEdges,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors,
                            referenceContexts,
                            encoderSegmentMap,
                            searchSegmentMap,
                            previousSegmentMap,
                            superblockCoefficients,
                            workspaceStorage,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            secondOrigin,
                            subSize,
                            partition,
                            this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partition, 1),
                            long.MaxValue,
                            false,
                            false).Cost != long.MaxValue;
                    }

                    break;

                case Av1PartitionType.Split:
                    Av1BlockSize childSize = partition.GetBlockSubSize(blockSize);
                    int half = childSize.GetWidth();
                    for (int child = 0; child < 4 && valid; child++)
                    {
                        Point childOrigin = blockOrigin + new Size((child & 1) * half, (child >> 1) * half);
                        valid = this.SearchVariancePartition(
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            searchDequantizedCoefficients,
                            transformWorkspace,
                            transformTypeProbabilities,
                            blockResidual,
                            searchCoefficients,
                            searchReconstructions,
                            estimationRowCoefficients,
                            in interWorkspace,
                            motionSearchPrediction,
                            in motionVectorCosts,
                            transformPrediction,
                            interIntraAbove,
                            interIntraLeft,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            in partitionEdges,
                            in transformEdges,
                            in paletteEdges,
                            in lumaCoefficientEdges,
                            in blueCoefficientEdges,
                            in redCoefficientEdges,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors,
                            referenceContexts,
                            encoderSegmentMap,
                            searchSegmentMap,
                            previousSegmentMap,
                            superblockCoefficients,
                            workspaceStorage,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            childOrigin,
                            childSize,
                            (nodeIndex * 4) + child + 1,
                            child != 3);
                    }

                    break;
            }

            this.ResetPartitionTrial(
                workspaceStorage,
                in partitionEdges,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                blockOrigin,
                blockSize,
                savedLumaArea,
                savedChromaArea);

            if (reconstruct && blockSize != this.picture.Sequence.SequenceHeader.SuperblockSize)
            {
                // A dry run encodes the finished subtree, so a later block predicts from its reconstruction.
                _ = this.EvaluatePartitionCandidate(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in partitionEdges,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    encoderSegmentMap,
                    searchSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    partition,
                    nodeIndex,
                    Av1RateDistortionStatistics.Invalid,
                    searchChildren: false,
                    true,
                    [],
                    [],
                    out _,
                    out _);

                Av1TileWriter.UpdatePartitionContexts(in partitionEdges, blockOrigin, partition.GetBlockSubSize(blockSize), blockSize, partition);
            }

            return valid;
        }

        /// <summary>
        /// Searches the partition of one block when its children are searched, then evaluates the selected partition
        /// tree again so that the winning samples stay where later blocks predict from them.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="nodeIndex">The partition tree node of the block.</param>
        /// <param name="costLimit">The cost above which the search stops.</param>
        /// <param name="searchChildren">Whether the partition of the block is searched first.</param>
        /// <param name="publishContexts">Whether the partition contexts of the selected tree are written.</param>
        /// <param name="noneCost">The cost of the unsplit block.</param>
        /// <param name="rectangleWins">Bit zero when the horizontal halves win, and bit one when the vertical halves win.</param>
        /// <returns>The rate and distortion of the selected partition tree.</returns>
        private Av1RateDistortionStatistics EvaluateSelectedPartitionTree(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int nodeIndex,
            Av1RateDistortionStatistics costLimit,
            bool searchChildren,
            bool publishContexts,
            out long noneCost,
            out byte rectangleWins)
        {
            noneCost = 0;
            rectangleWins = 3;
            Av1PartitionType selectedPartition = (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[nodeIndex];
            if (searchChildren)
            {
                selectedPartition = this.SelectBestPartition(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    searchCoefficients,
                    searchReconstructions,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in partitionEdges,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    encoderSegmentMap,
                    searchSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    nodeIndex,
                    costLimit,
                    out Av1RateDistortionStatistics selectedStatistics,
                    out noneCost,
                    out rectangleWins);

                if (selectedStatistics.Cost == long.MaxValue)
                {
                    return selectedStatistics;
                }

                if (!this.ShouldReconstructSelectedTree(blockSize, nodeIndex))
                {
                    return selectedStatistics;
                }
            }

            Av1RateDistortionStatistics statistics = this.EvaluatePartitionCandidate(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                blockResidual,
                searchCoefficients,
                searchReconstructions,
                estimationRowCoefficients,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                transformPrediction,
                interIntraAbove,
                interIntraLeft,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in partitionEdges,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                encoderSegmentMap,
                searchSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                blockSize,
                selectedPartition,
                nodeIndex,
                Av1RateDistortionStatistics.Invalid,
                searchChildren: false,
                publishContexts,
                [],
                [],
                out _,
                out _);

            if (publishContexts)
            {
                Av1TileWriter.UpdatePartitionContexts(
                    in partitionEdges,
                    blockOrigin,
                    selectedPartition.GetBlockSubSize(blockSize),
                    blockSize,
                    selectedPartition);
            }

            return statistics;
        }

        /// <summary>
        /// Decides whether a completed partition search reconstructs its winning subtree again.
        /// </summary>
        /// <remarks>
        /// This reconstruction only leaves the winning samples where a later trial of the surrounding block predicts from them. A block
        /// above the maximum partition size always splits, so its search result is already final. The fourth child is the last one
        /// searched, so no sibling predicts from it, and its parent reconstructs the whole subtree afterwards.
        /// </remarks>
        /// <param name="blockSize">The block size of the node.</param>
        /// <param name="nodeIndex">The index of the node in the partition tree, where the children of node n start at 4n + 1.</param>
        /// <returns><see langword="true"/> when the winning subtree is reconstructed again.</returns>
        private bool ShouldReconstructSelectedTree(Av1BlockSize blockSize, int nodeIndex)
        {
            Av1BlockSize superblockSize = this.picture.Sequence.SequenceHeader.SuperblockSize;
            if (blockSize == superblockSize)
            {
                return true;
            }

            if (blockSize > this.maximumPartitionSize)
            {
                return false;
            }

            if (((nodeIndex - 1) & 3) != 3)
            {
                return true;
            }

            // The parent of a largest partition is not searched as a block, so its fourth child
            // remains the last writer of these samples.
            Av1BlockSize halfSuperblockSize = Av1PartitionType.Split.GetBlockSubSize(superblockSize);
            return blockSize == this.maximumPartitionSize && halfSuperblockSize != this.maximumPartitionSize;
        }

        /// <summary>
        /// Evaluates one partition type of a block leaf by leaf and adds the partition syntax. The evaluation stops at
        /// the first leaf whose accumulated cost passes the limit.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="partitionType">The partition type to evaluate.</param>
        /// <param name="nodeIndex">The partition tree node of the block.</param>
        /// <param name="costLimit">The cost above which the evaluation stops.</param>
        /// <param name="searchChildren">Whether each square leaf searches its own partition.</param>
        /// <param name="publishFinalContexts">Whether the last leaf writes its partition contexts.</param>
        /// <param name="childCosts">The cost of each leaf, or empty.</param>
        /// <param name="childRectangleWins">The rectangle wins of each split leaf, or empty.</param>
        /// <param name="stoppedAtLeaf">The leaf at which the evaluation stopped, or the leaf count when it finished.</param>
        /// <param name="accumulatedCost">The cost of the evaluated leaves, or the maximum when a leaf failed.</param>
        /// <returns>The rate and distortion of the partition.</returns>
        private Av1RateDistortionStatistics EvaluatePartitionCandidate(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType,
            int nodeIndex,
            Av1RateDistortionStatistics costLimit,
            bool searchChildren,
            bool publishFinalContexts,
            Span<long> childCosts,
            Span<byte> childRectangleWins,
            out int stoppedAtLeaf,
            out long accumulatedCost)
        {
            int rate = Av1TileWriter.GetPartitionCost(this.picture, writer, tables.ModeCosts, blockSize, partitionType, blockOrigin, in partitionEdges);

            Av1RateDistortionStatistics statistics = new(this.rateMultiplier, rate, 0);
            int leafCount = GetPartitionLeafCount(partitionType);

            // The sub-blocks of the asymmetric and four-way partitions are costed at their own rate multipliers. At the end, the sum
            // returns to the multiplier of the node.
            bool asymmetric = partitionType is >= Av1PartitionType.HorizontalA and <= Av1PartitionType.Vertical4;
            int nodeRateMultiplier = this.rateMultiplier;
            stoppedAtLeaf = 0;
            accumulatedCost = statistics.Cost;

            // Each child uses part of the bound of the parent. Later rates and distortions are not negative, so a losing prefix cannot
            // recover. The loop must stop before another child changes the contexts.
            for (int leafIndex = 0; leafIndex < leafCount; leafIndex++)
            {
                stoppedAtLeaf = leafIndex;

                // Only a square split compares the partition symbol alone against the bound. Every other shape measures its first
                // sub-block first and leaves those samples.
                if (!asymmetric && (leafIndex > 0 || partitionType == Av1PartitionType.Split) &&
                    statistics.Cost >= costLimit.Cost)
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

                Av1RateDistortionStatistics leafLimit = costLimit;
                if (asymmetric)
                {
                    // Each asymmetric sub-block sets up its own multiplier and measures the bound at it.
                    this.rateMultiplier = this.SetupBlockRateMultiplier(leafOrigin, leafSize);
                    leafLimit.UpdateCost(this.rateMultiplier);
                }

                Av1RateDistortionStatistics remainingCost = leafLimit.Subtract(this.rateMultiplier, statistics);
                bool publishContexts = leafIndex < leafCount - 1 || publishFinalContexts;
                this.activeModeCache = searchChildren && partitionType is >= Av1PartitionType.HorizontalA and <= Av1PartitionType.VerticalB
                    ? this.asymmetricModeCache[leafIndex]
                    : default;

                long childNoneCost = 0;
                byte childWins = 3;

                // When an encode follows the search of a leaf, the leaf leaves its coefficient contexts to that encode. The mode search
                // updates no entropy context, so the encode reads the contexts of the neighbors, not the contexts of the leaf.
                bool siblingEncodeFollows = searchChildren &&
                    leafIndex + 1 < leafCount && (partitionType != Av1PartitionType.Split || blockSize <= Av1BlockSize.Block8x8);

                bool encodeFollows = siblingEncodeFollows && this.picture.Parent.FrameHeader.IsIntra;

                // A replay of a finished subtree encodes every leaf at the multiplier of that leaf.
                if (!searchChildren && !(partitionType == Av1PartitionType.Split && blockSize > Av1BlockSize.Block8x8))
                {
                    this.rateMultiplier = this.SetupBlockRateMultiplier(leafOrigin, leafSize);
                }

                Av1RateDistortionStatistics childStatistics = partitionType == Av1PartitionType.Split && blockSize > Av1BlockSize.Block8x8
                    ? this.EvaluateSelectedPartitionTree(
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        estimationRowCoefficients,
                        in interWorkspace,
                        motionSearchPrediction,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in partitionEdges,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        referenceContexts,
                        encoderSegmentMap,
                        searchSegmentMap,
                        previousSegmentMap,
                        superblockCoefficients,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        leafOrigin,
                        leafSize,
                        (nodeIndex * 4) + leafIndex + 1,
                        remainingCost,
                        searchChildren,
                        publishContexts,
                        out childNoneCost,
                        out childWins)
                    : searchChildren &&
                        !this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex).Snapshot.Ready
                        ? this.EvaluatePartitionLeaf(
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            searchDequantizedCoefficients,
                            transformWorkspace,
                            transformTypeProbabilities,
                            blockResidual,
                            searchCoefficients,
                            searchReconstructions,
                            estimationRowCoefficients,
                            in interWorkspace,
                            motionSearchPrediction,
                            in motionVectorCosts,
                            transformPrediction,
                            interIntraAbove,
                            interIntraLeft,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            in transformEdges,
                            in paletteEdges,
                            in lumaCoefficientEdges,
                            in blueCoefficientEdges,
                            in redCoefficientEdges,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors,
                            referenceContexts,
                            encoderSegmentMap,
                            searchSegmentMap,
                            previousSegmentMap,
                            superblockCoefficients,
                            workspaceStorage,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            leafOrigin,
                            leafSize,
                            partitionType == Av1PartitionType.Split ? Av1PartitionType.None : partitionType,
                            this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex),
                            this.GetLeafCostLimit(remainingCost, leafOrigin, leafSize, partitionType == Av1PartitionType.Split),
                            publishContexts,
                            !encodeFollows,
                            siblingEncodeFollows)
                        : this.ReconstructPartitionLeaf(
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            transformWorkspace,
                            in interWorkspace,
                            transformPrediction,
                            interIntraAbove,
                            interIntraLeft,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            in transformEdges,
                            in paletteEdges,
                            in lumaCoefficientEdges,
                            in blueCoefficientEdges,
                            in redCoefficientEdges,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors,
                            referenceContexts,
                            encoderSegmentMap,
                            previousSegmentMap,
                            superblockCoefficients,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            leafOrigin,
                            this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex),
                            publishContexts,
                            searchChildren,
                            encodeFollows);

                this.activeModeCache = default;
                if (!searchChildren)
                {
                    this.rateMultiplier = nodeRateMultiplier;
                }

                if (!childRectangleWins.IsEmpty)
                {
                    childRectangleWins[leafIndex] = childWins;
                }

                // A 4x4 split child is an unsplit search of its own. When its result stays within its budget, it records the reference
                // that it picked for the rectangles of its parent.
                if (searchChildren && partitionType == Av1PartitionType.Split && blockSize <= Av1BlockSize.Block8x8 &&
                    childStatistics.Cost < remainingCost.Cost && !this.picture.Parent.FrameHeader.IsIntra &&
                    this.picture.Parent.SpeedSettings.GetRectangularPartitionReferencePruning(this.picture.Parent.FrameUpdateType) != 0)
                {
                    this.UpdatePickedReferenceFrames(
                        leafOrigin,
                        leafSize,
                        this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex).Snapshot.ModeInfo.Block);
                }

                if (!childCosts.IsEmpty)
                {
                    // The split classification uses the unsplit cost of each child, whatever its winner is. Rectangular leaves have no
                    // child partition search. They keep their complete mode cost, measured again at the multiplier of the node.
                    Av1RateDistortionStatistics nodeLeafCost = childStatistics;
                    if (partitionType != Av1PartitionType.Split)
                    {
                        nodeLeafCost.UpdateCost(nodeRateMultiplier);
                    }

                    childCosts[leafIndex] = partitionType == Av1PartitionType.Split && blockSize > Av1BlockSize.Block8x8
                        ? childNoneCost
                        : nodeLeafCost.Cost;
                }

                // Every split child is searched as a partition of its own. It reports a result only when that result stays below the
                // budget that it received. Otherwise the split is invalid. A 4x4 child is evaluated here directly, so the same test applies
                // to it.
                if (childStatistics.Cost == long.MaxValue ||
                    (partitionType == Av1PartitionType.Split && blockSize <= Av1BlockSize.Block8x8 &&
                    childStatistics.Cost >= remainingCost.Cost))
                {
                    this.rateMultiplier = nodeRateMultiplier;
                    accumulatedCost = long.MaxValue;
                    return Av1RateDistortionStatistics.Invalid;
                }

                statistics.Add(this.rateMultiplier, childStatistics);
                accumulatedCost = statistics.Cost;
                if (asymmetric && statistics.Cost >= leafLimit.Cost)
                {
                    this.rateMultiplier = nodeRateMultiplier;
                    return Av1RateDistortionStatistics.Invalid;
                }

                // A sibling predicts from the reconstruction that this leaf leaves, so the leaf is encoded across every plane before the
                // loop moves on. The mode search alone leaves the last chroma candidate in the plane, not the winner. A split child encodes
                // itself when its own partition search ends. A leaf that already spent the bound encodes nothing, because the search stops
                // and does not measure the sibling. A rectangle whose second half falls outside the frame encodes nothing either. A
                // rectangle tests its cost before the encode. An asymmetric sub-block encodes every sub-block but the last.
                // A split child normally reconstructs its own winning subtree when its partition search ends. A 4x4 child has no
                // partition search of its own, so the parent leaves those samples instead, for every child but the last.
                bool reconstructSplitLeaf = partitionType == Av1PartitionType.Split &&
                    blockSize <= Av1BlockSize.Block8x8 && leafIndex + 1 < leafCount;

                bool reconstructSibling = partitionType != Av1PartitionType.Split && leafIndex + 1 < leafCount &&
                    (asymmetric || statistics.Cost < costLimit.Cost) &&
                    this.IsPartitionSiblingInsideFrame(blockOrigin, blockSize, partitionType, leafIndex);

                // An inter leaf keeps the samples of its own search, but the encode that it represents still writes DCT_DCT for each luma
                // block that quantized to nothing. An intra leaf, in an inter frame and in an intra frame, is quantized again by the intra
                // block encode. A leaf whose search was reused also needs that encode for its samples.
                bool siblingIsIntra = (reconstructSplitLeaf || reconstructSibling) &&
                    (this.picture.Parent.FrameHeader.IsIntra ||
                     this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex).Snapshot.ModeInfo.Block.ReferenceFrame <= Av1ReferenceFrameType.Intra);

                if (searchChildren && !siblingIsIntra && (reconstructSplitLeaf || reconstructSibling))
                {
                    Av1EncoderPartitionTree.ModeContext sibling =
                        this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex);

                    if (!this.picture.Parent.FrameHeader.LosslessArray[sibling.Snapshot.ModeInfo.Block.SegmentId] &&
                        !sibling.Snapshot.ModeInfo.Block.Skip)
                    {
                        Span<Av1EncoderTransformBlockState> siblingStates = sibling.GetTransformStates(Av1Plane.Y);
                        RetainEncodedZeroBlockTypes(siblingStates, siblingStates);
                    }
                }

                if (searchChildren && siblingIsIntra)
                {
                    Av1EncoderPartitionTree.ModeContext sibling =
                        this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex);

                    int siblingLumaArea = this.codedAreaLuma;
                    int siblingChromaArea = this.codedAreaChroma;
                    if (sibling.Snapshot.ModeInfo.Block.UseIntraBlockCopy)
                    {
                        // A copied block is encoded from its displacement, not from an intra predictor, as an inter block is.
                        this.ReconstructPartitionLeaf(
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            transformWorkspace,
                            in interWorkspace,
                            transformPrediction,
                            interIntraAbove,
                            interIntraLeft,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            in transformEdges,
                            in paletteEdges,
                            in lumaCoefficientEdges,
                            in blueCoefficientEdges,
                            in redCoefficientEdges,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors,
                            referenceContexts,
                            encoderSegmentMap,
                            previousSegmentMap,
                            superblockCoefficients,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            leafOrigin,
                            sibling,
                            true,
                            false,
                            false);
                    }
                    else
                    {
                        this.ReconstructSelectedIntraBlock(
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            transformWorkspace,
                            in lumaCoefficientEdges,
                            in blueCoefficientEdges,
                            in redCoefficientEdges,
                            modeInfoGrid,
                            modeInfoAllocation,
                            encoderSegmentMap,
                            previousSegmentMap,
                            superblockCoefficients,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            leafOrigin,
                            sibling);

                        // The encode leaves the entropy contexts of what it coded for the next leaf, from the coefficients that it just wrote.
                        this.PublishPartitionLeafContexts(
                            in transformEdges,
                            in paletteEdges,
                            in lumaCoefficientEdges,
                            in blueCoefficientEdges,
                            in redCoefficientEdges,
                            superblockCoefficients,
                            macroBlock,
                            leafOrigin,
                            siblingLumaArea,
                            siblingChromaArea,
                            sibling.Snapshot.ModeInfo,
                            sibling.Snapshot.Block,
                            sibling.Snapshot.Palette,
                            true);
                    }

                    this.codedAreaLuma = siblingLumaArea;
                    this.codedAreaChroma = siblingChromaArea;
                }

                // The sub-block of an asymmetric partition encodes its sibling state before its multiplier returns to the node.
                this.rateMultiplier = nodeRateMultiplier;

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

            stoppedAtLeaf = leafCount;
            if (asymmetric)
            {
                statistics.UpdateCost(this.rateMultiplier);
                accumulatedCost = statistics.Cost;
            }

            return statistics.Cost < costLimit.Cost ? statistics : Av1RateDistortionStatistics.Invalid;
        }

        /// <summary>
        /// Reports whether the sub-partition that follows this one starts inside the frame. A rectangle whose second half falls outside
        /// the frame codes only its first half, so nothing predicts from the reconstruction of that half. Every other shape measures its
        /// sibling in all cases.
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="partitionType">The partition type.</param>
        /// <param name="leafIndex">The index of the current sub-partition.</param>
        /// <returns><see langword="true"/> when the next sub-partition starts inside the frame.</returns>
        private bool IsPartitionSiblingInsideFrame(
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType,
            int leafIndex)
        {
            if (partitionType is not (Av1PartitionType.Horizontal or Av1PartitionType.Vertical))
            {
                return true;
            }

            GetPartitionLeafGeometry(blockOrigin, blockSize, partitionType, leafIndex + 1, out Point siblingOrigin, out _);
            return this.IsBlockOriginInsideFrame(siblingOrigin);
        }

        /// <summary>
        /// Returns the coefficient positions and the tile context edges to their state before a partition trial.
        /// </summary>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size of the partition node.</param>
        /// <param name="savedLumaArea">The luma coefficient position before the trial.</param>
        /// <param name="savedChromaArea">The chroma coefficient position before the trial.</param>
        private void ResetPartitionTrial(
            Span<int> workspaceStorage,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int savedLumaArea,
            int savedChromaArea)
        {
            this.codedAreaLuma = savedLumaArea;
            this.codedAreaChroma = savedChromaArea;
            this.RestorePartitionTrialContexts(
                workspaceStorage,
                in partitionEdges,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                blockOrigin,
                blockSize);
        }

        /// <summary>
        /// Maps the mode information of every leaf of a partition that starts inside the frame.
        /// </summary>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="blockOrigin">The luma origin of the partitioned block.</param>
        /// <param name="blockSize">The size of the partitioned block.</param>
        /// <param name="partitionType">The partition of the block.</param>
        private void PreparePartitionGeometry(
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
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
                    // Mixed vertical partitions reconstruct square leaves in another order. The leaf keeps the parent decision, so the
                    // prediction uses the same edge availability as the decoder. Split children own another partition node. A terminal 4x4
                    // child implicitly owns NONE.
                    this.SetBlockGeometry(
                        modeInfoGrid,
                        modeInfoAllocation,
                        leafOrigin,
                        leafSize,
                        partitionType == Av1PartitionType.Split ? Av1PartitionType.None : partitionType);
                }
            }
        }

        /// <summary>
        /// Returns whether the partition search can try a partition type for a block. The test applies the speed limits, the frame edges,
        /// the AV1 partition alphabet and the chroma subsampling.
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="partitionType">The partition type.</param>
        /// <returns><see langword="true"/> when the partition type is a candidate.</returns>
        private bool IsPartitionCandidateAllowed(
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType)
        {
            Av1EncoderSpeedSettings speedSettings = this.picture.Parent.SpeedSettings;
            if (blockSize > this.maximumPartitionSize && partitionType != Av1PartitionType.Split)
            {
                return false;
            }

            int halfWidth = blockSize.GetWidth() >> 1;
            int halfHeight = blockSize.GetHeight() >> 1;
            bool hasRows = blockOrigin.Y + halfHeight <
                (this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

            bool hasColumns = blockOrigin.X + halfWidth <
                (this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2);

            // At the configured minimum, every rectangle closes, whether or not the frame reaches the midpoint of this block. One candidate
            // remains: the unsplit block when the frame reaches both midpoints, and the square split when it does not. A block below 8x8
            // has no square split and keeps the unsplit candidate in both cases. The retry that follows a search with no valid partition
            // sets its own limits, so it keeps the alphabet that it had.
            if (!this.mustFindValidPartition && blockSize <= speedSettings.MinimumPartitionSize)
            {
                if (partitionType is not (Av1PartitionType.None or Av1PartitionType.Split))
                {
                    return false;
                }

                bool squareSplitAllowed = blockSize >= Av1BlockSize.Block8x8 && !(hasRows && hasColumns);
                if (partitionType == Av1PartitionType.Split ? !squareSplitAllowed : squareSplitAllowed)
                {
                    return false;
                }
            }

            if (!this.mustFindValidPartition && partitionType is not (Av1PartitionType.None or Av1PartitionType.Split))
            {
                ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
                (Av1BlockSize minimum, Av1BlockSize maximum) = speedSettings.GetRectangularPartitionRange(
                    frameHeader.AllowScreenContentTools,
                    frameHeader.IsIntra,
                    this.picture.Parent.FrameUpdateType,
                    this.blockQIndex);

                if (blockSize < minimum || blockSize > maximum)
                {
                    return false;
                }
            }

            // Above the square-only limit, rectangular leaves are necessary only where the frame ends before the midpoint. Extended
            // rectangles need both halves, so they are excluded.
            if (!this.mustFindValidPartition && blockSize > speedSettings.SquareOnlyPartitionThreshold &&
                ((partitionType == Av1PartitionType.Horizontal && hasRows) ||
                 (partitionType == Av1PartitionType.Vertical && hasColumns) ||
                 partitionType >= Av1PartitionType.HorizontalA))
            {
                return false;
            }

            // At frame edges, the partition alphabet depends on whether each midpoint is visible. A remaining half-block is split
            // implicitly. Permitted leaves can extend into the padding.
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

        /// <summary>
        /// Returns whether a block origin lies inside the mode-information grid of the frame.
        /// </summary>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <returns><see langword="true"/> when the origin is inside the frame.</returns>
        private bool IsBlockOriginInsideFrame(Point blockOrigin)
        {
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            return modeInfoPosition.Y < this.picture.Parent.Common.ModeInfoRowCount &&
                modeInfoPosition.X < this.picture.Parent.Common.ModeInfoColumnCount;
        }

        /// <summary>
        /// Selects the source candidate of the cached luma decision of every asymmetric sub-block. Each sub-block takes the square or
        /// rectangular leaf that covers the same samples.
        /// </summary>
        /// <param name="cache">Receives the cached decision of each of the three sub-blocks.</param>
        /// <param name="partitionType">The asymmetric partition type.</param>
        /// <param name="split">The decisions of the four split children.</param>
        /// <param name="horizontal">The decisions of the two horizontal halves.</param>
        /// <param name="vertical">The decisions of the two vertical halves.</param>
        private static void SetAsymmetricModeCache(
            ref InlineArray3<Av1AsymmetricModeCacheEntry> cache,
            Av1PartitionType partitionType,
            ReadOnlySpan<Av1AsymmetricModeCacheEntry> split,
            ReadOnlySpan<Av1AsymmetricModeCacheEntry> horizontal,
            ReadOnlySpan<Av1AsymmetricModeCacheEntry> vertical)
        {
            switch (partitionType)
            {
                case Av1PartitionType.HorizontalA:
                    cache[0] = split[0];
                    cache[1] = split[1];
                    cache[2] = horizontal[1];
                    return;
                case Av1PartitionType.HorizontalB:
                    cache[0] = horizontal[0];
                    cache[1] = split[2];
                    cache[2] = split[3];
                    return;
                case Av1PartitionType.VerticalA:
                    cache[0] = split[0];
                    cache[1] = split[2];
                    cache[2] = vertical[1];
                    return;
                default:
                    cache[0] = vertical[0];
                    cache[1] = split[1];
                    cache[2] = split[3];
                    return;
            }
        }

        /// <summary>
        /// Keeps the luma decision of a completed square or rectangular candidate, for the asymmetric candidates that cover the same
        /// samples.
        /// </summary>
        /// <remarks>
        /// The method keeps the unsplit context of each square child and the mode context of each rectangular half. It offers a decision
        /// only when that search produced a result.
        /// </remarks>
        /// <param name="nodeIndex">The partition tree node of the block.</param>
        /// <param name="partitionType">The completed partition type: split, horizontal or vertical.</param>
        /// <param name="stoppedAtLeaf">The leaf at which the candidate stopped.</param>
        /// <param name="childCosts">The costs of the leaves, or an empty span when the candidate keeps no leaf costs.</param>
        /// <param name="split">Receives the decisions of the four split children.</param>
        /// <param name="horizontal">Receives the decisions of the two horizontal halves.</param>
        /// <param name="vertical">Receives the decisions of the two vertical halves.</param>
        private void CaptureAsymmetricModeCacheSources(
            int nodeIndex,
            Av1PartitionType partitionType,
            int stoppedAtLeaf,
            ReadOnlySpan<long> childCosts,
            ref InlineArray4<Av1AsymmetricModeCacheEntry> split,
            ref InlineArray2<Av1AsymmetricModeCacheEntry> horizontal,
            ref InlineArray2<Av1AsymmetricModeCacheEntry> vertical)
        {
            if (childCosts.IsEmpty)
            {
                return;
            }

            Av1EncoderPartitionTree tree = this.blockWorkspace.PartitionTree;

            // The leaf that ended the search early still keeps the context that its own search filled. Thus a split child whose partition
            // search found nothing within its budget still offers its unsplit mode. Only the rate of the unsplit context is tested.
            int count = Math.Min(childCosts.Length, stoppedAtLeaf + 1);
            for (int leaf = 0; leaf < count; leaf++)
            {
                // A leaf that was never searched reports no cost and offers no mode. A square child that never searched its unsplit shape
                // is the same. Its unsplit context exists, but still holds the invalid statistics that it was created with.
                if (childCosts[leaf] == long.MaxValue || childCosts[leaf] == 0)
                {
                    continue;
                }

                // A candidate outside the coded frame keeps no decision, as a sub-block that was never searched has no context.
                int sourceNode = partitionType == Av1PartitionType.Split ? (nodeIndex * 4) + leaf + 1 : nodeIndex;
                Av1PartitionType sourcePartition = partitionType == Av1PartitionType.Split ? Av1PartitionType.None : partitionType;
                int sourceLeaf = partitionType == Av1PartitionType.Split ? 0 : leaf;
                if (!tree.HasContext(sourceNode, sourcePartition, sourceLeaf))
                {
                    continue;
                }

                Av1EncoderPartitionTree.ModeSnapshot snapshot = tree.GetContext(sourceNode, sourcePartition, sourceLeaf).Snapshot;

                Av1AsymmetricModeCacheEntry entry = new()
                {
                    Active = true,
                    Mode = snapshot.ModeInfo.Block.Mode,
                    FilterIntraMode = snapshot.Block.FilterIntraMode,
                    ReferenceFrame = snapshot.ModeInfo.Block.ReferenceFrame,
                    SecondaryReferenceFrame = snapshot.ModeInfo.Block.SecondaryReferenceFrame
                };

                switch (partitionType)
                {
                    case Av1PartitionType.Split:
                        split[leaf] = entry;
                        break;
                    case Av1PartitionType.Horizontal:
                        horizontal[leaf] = entry;
                        break;
                    default:
                        vertical[leaf] = entry;
                        break;
                }
            }
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
        /// <summary>
        /// Runs <see cref="EncodeSelectedBlock"/> at the rate multiplier of the block.
        /// </summary>
        public void EncodeBlock(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<Av1EncoderBlockStruct> blockEncodings,
            Span<Av1EncoderPaletteInfo> blockPalettes,
            Span<byte> paletteTokens,
            Span<int> cdefPreset,
            Span<int> previousQIndex,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            Span<TSample> sourceLuma,
            Span<TSample> sourceBlue,
            Span<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            int savedRateMultiplier = this.rateMultiplier;

            // A block that the estimated inter search codes now takes its segment from the map. Cyclic refresh prices a boosted block at
            // its own multiplier, which also sets the motion vector error per bit.
            int segmentId = -1;
            if (this.replayNodeIndex < 0 && this.UsesEstimatedInterSearch)
            {
                this.SetBlockSegment(encoderSegmentMap, previousSegmentMap, blockOrigin, modeInfo.Block.BlockSize);
                if (this.picture.Parent.CyclicRefresh is not null)
                {
                    segmentId = this.blockSegmentId;
                }
            }

            this.rateMultiplier = this.SetupBlockRateMultiplier(blockOrigin, modeInfo.Block.BlockSize, segmentId);
            if (segmentId >= 0)
            {
                this.blockWorkspace.ErrorPerBitRateMultiplier = this.rateMultiplier;
            }

            this.EncodeSelectedBlock(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                blockResidual,
                searchCoefficients,
                searchReconstructions,
                estimationRowCoefficients,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                transformPrediction,
                interIntraAbove,
                interIntraLeft,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                encoderSegmentMap,
                searchSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                ref modeInfo,
                ref block,
                ref paletteInfo);

            this.rateMultiplier = savedRateMultiplier;
        }

        /// <summary>
        /// Selects the modes of one block and encodes the winner.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="paletteInfo">The palette of the block.</param>
        private void EncodeSelectedBlock(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            this.encodedWithoutCoefficients = false;
            this.EncodeBlockCore(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                blockResidual,
                searchCoefficients,
                searchReconstructions,
                estimationRowCoefficients,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                transformPrediction,
                interIntraAbove,
                interIntraLeft,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                encoderSegmentMap,
                searchSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                ref modeInfo,
                ref block,
                ref paletteInfo,
                true);

            if (this.encodedWithoutCoefficients)
            {
                modeInfo.Block.Skip = true;
            }

            if (this.picture.Parent.SpeedSettings.SkipCdefSuperblock)
            {
                this.UpdateCdefSkip(modeInfoAllocation, blockOrigin, modeInfo.Block.Mode);
            }
        }

        /// <summary>
        /// Narrows the CDEF skip flag of the 64x64 unit that holds a coded block. The flag goes to the first block of the unit, which
        /// carries the strength index of the unit. A unit keeps CDEF off only when every block allows skipping and no block is intra or
        /// uses a new motion vector. A block allows skipping when the frame is more than 10 frames after the key frame, the source error
        /// is not high, and the block has no color sensitivity.
        /// </summary>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="blockOrigin">The luma origin of the coded block.</param>
        /// <param name="mode">The selected luma prediction mode.</param>
        private void UpdateCdefSkip(Span<Av1MacroBlockModeInfo> modeInfoAllocation, Point blockOrigin, Av1PredictionMode mode)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            bool allowSkipping = parent.FramesSinceKey > 10 && !parent.HighSourceSad &&
                this.blockColorSensitivity[0] == 0 && this.blockColorSensitivity[1] == 0;

            int unit = (((blockOrigin.Y >> 6) & 1) << 1) | ((blockOrigin.X >> 6) & 1);
            ref bool skip = ref this.cdefSkipUnits[unit];
            skip = skip && allowSkipping && !(mode < Av1PredictionMode.IntraModeEnd || mode == Av1PredictionMode.NewMotionVector);
            Point unitOrigin = new((blockOrigin.X & ~63) >> Av1Constants.ModeInfoSizeLog2, (blockOrigin.Y & ~63) >> Av1Constants.ModeInfoSizeLog2);
            this.picture.GetMacroBlockModeInfo(modeInfoAllocation, unitOrigin).CdefStrength = skip ? 1 : 0;
        }

        /// <summary>
        /// Selects the modes of one block without encoding the winner afterwards.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="paletteInfo">The palette of the block.</param>
        private void SearchBlock(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            this.EncodeBlockCore(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                blockResidual,
                searchCoefficients,
                searchReconstructions,
                estimationRowCoefficients,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                transformPrediction,
                interIntraAbove,
                interIntraLeft,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                encoderSegmentMap,
                searchSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                ref modeInfo,
                ref block,
                ref paletteInfo,
                false);
        }

        /// <summary>
        /// Selects the inter, intra and intra block copy modes of one block, and encodes the winner when asked.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="modeInfo">The block decisions.</param>
        /// <param name="block">The block state.</param>
        /// <param name="paletteInfo">The palette of the block.</param>
        /// <param name="encodeSelected">Whether the winner is encoded after the search.</param>
        private void EncodeBlockCore(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo,
            bool encodeSelected)
        {
            this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Candidate;
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

                // Complexity adaptive quantization takes the coded segment from the segment map, before the segment costs are added.
                int codedSegmentId = this.GetCodedBlockSegment(
                    encoderSegmentMap, previousSegmentMap, blockOrigin, modeInfo.Block.BlockSize, modeInfo.Block.SegmentId);

                modeInfo.Block.SegmentId = codedSegmentId;
                block.SegmentId = codedSegmentId;

                // A block that a merge trial of the estimated inter search kept updates its cyclic refresh segment when it is coded for
                // output. It always prices its segment, because the estimated search never sets the skip flag that the state update tests.
                bool countSegments = false;
                bool cyclicRefreshEncode = encodeSelected && this.UsesEstimatedInterSearch &&
                    this.UpdatesCyclicRefreshSegment(context.Snapshot.Statistics, out countSegments);

                if (cyclicRefreshEncode)
                {
                    Av1MotionVector firstVector = modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra
                        ? context.Snapshot.Displacement
                        : default;

                    this.UpdateCyclicRefreshSegment(
                        encoderSegmentMap,
                        searchSegmentMap,
                        blockOrigin,
                        ref modeInfo.Block,
                        ref block,
                        firstVector,
                        context.Snapshot.Statistics,
                        countSegments);

                    // The intra reconstruction reads its segment from the retained decision.
                    context.Snapshot.ModeInfo.Block.SegmentId = modeInfo.Block.SegmentId;
                    context.Snapshot.Block.SegmentId = block.SegmentId;
                }

                bool searchSkip = !cyclicRefreshEncode && modeInfo.Block.Skip;
                if (encodeSelected && !searchSkip)
                {
                    this.AddSegmentPredictionCosts(
                        in tables, searchSegmentMap, previousSegmentMap, macroBlock, blockOrigin, modeInfo.Block.BlockSize, modeInfo.Block.SegmentId);
                }

                if (modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra || modeInfo.Block.UseIntraBlockCopy)
                {
                    InlineArray128<Av1EncoderTransformBlockState> stateStorage = default;
                    Span<Av1EncoderTransformBlockState> states = stateStorage;

                    // A lossless segment codes 4x4 transforms with one retained state per transform.
                    bool replayLossless = this.picture.Parent.FrameHeader.LosslessArray[modeInfo.Block.SegmentId];
                    Av1TransformSize replayRootSize = replayLossless
                        ? Av1TransformSize.Size4x4
                        : modeInfo.Block.BlockSize.GetMaximumTransformSize();

                    Av1TransformSize replayTraversalSize = replayRootSize.GetSubSize().GetSubSize();
                    Size replayExtent = GetCodedTransformExtent(
                        macroBlock, modeInfo.Block.BlockSize, Av1TransformSize.Size4x4, 0, 0);

                    ReadOnlySpan<Av1EncoderTransformBlockState> replayLumaStates = context.GetTransformStates(Av1Plane.Y);
                    int replayArea = 0;
                    int replayStateCount = 0;
                    int replayLeafCount = modeInfo.Block.BlockSize.GetWidth() * modeInfo.Block.BlockSize.GetHeight() / replayTraversalSize.GetSize2d();
                    for (int leaf = 0; leaf < replayLeafCount; leaf++)
                    {
                        Point offset = replayRootSize.GetBlockPartitionOrigin(modeInfo.Block.BlockSize, replayTraversalSize, leaf, 0, 0);
                        Av1TransformSize size = modeInfo.Block.InterTransformSizes[
                            modeInfo.Block.GetInterTransformSizeIndex(offset.Y >> 2, offset.X >> 2)];

                        if (offset.X >= replayExtent.Width || offset.Y >= replayExtent.Height ||
                            (offset.X % size.GetWidth()) != 0 || (offset.Y % size.GetHeight()) != 0)
                        {
                            continue;
                        }

                        if (!replayLossless)
                        {
                            states[replayStateCount++] = replayLumaStates[
                                replayArea / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];
                        }

                        replayArea += size.GetSize2d();
                    }

                    if (block.HasChroma)
                    {
                        int subX = this.source.ChromaSubsamplingX;
                        int subY = this.source.ChromaSubsamplingY;
                        Av1TransformSize chromaTransformSize = replayLossless
                            ? Av1TransformSize.Size4x4
                            : modeInfo.Block.BlockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

                        Av1BlockSize chromaBlockSize = modeInfo.Block.BlockSize.GetSubsampled(subX != 0, subY != 0);
                        Size chromaExtent = GetCodedTransformExtent(macroBlock, chromaBlockSize, chromaTransformSize, subX, subY);
                        int chromaTransformCount = chromaExtent.Width * chromaExtent.Height / chromaTransformSize.GetSize2d();
                        int chromaStateStride = chromaTransformSize.GetSize2d() / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                        ReadOnlySpan<Av1EncoderTransformBlockState> blueStates = context.GetTransformStates(Av1Plane.U);
                        ReadOnlySpan<Av1EncoderTransformBlockState> redStates = context.GetTransformStates(Av1Plane.V);
                        for (int index = 0; !replayLossless && index < chromaTransformCount; index++)
                        {
                            states[64 + index] = blueStates[index * chromaStateStride];
                            states[80 + index] = redStates[index * chromaStateStride];
                        }
                    }

                    this.encodedWithoutCoefficients = !this.ReconstructSelectedInterBlock(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        in interWorkspace,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        encoderSegmentMap,
                        previousSegmentMap,
                        superblockCoefficients,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        modeInfo,
                        block,
                        context.Snapshot.Displacement,
                        context.Snapshot.SecondaryDisplacement,
                        states);

                    Point replayModeInfoPosition = new(
                        blockOrigin.X >> Av1Constants.ModeInfoSizeLog2,
                        blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);

                    this.picture.SetDisplacementVector(displacementVectors, replayModeInfoPosition, context.Snapshot.Displacement);
                    if (modeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                    {
                        this.picture.SetSecondaryDisplacementVector(referenceContexts, replayModeInfoPosition, context.Snapshot.SecondaryDisplacement);
                    }

                    this.codedAreaLuma += replayArea;
                    if (block.HasChroma)
                    {
                        int subX = this.source.ChromaSubsamplingX;
                        int subY = this.source.ChromaSubsamplingY;
                        Av1BlockSize chromaBlockSize = modeInfo.Block.BlockSize.GetSubsampled(subX != 0, subY != 0);
                        Av1TransformSize chromaTransformSize = replayLossless
                            ? Av1TransformSize.Size4x4
                            : modeInfo.Block.BlockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

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
                    this.ReconstructSelectedIntraBlock(
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        encoderSegmentMap,
                        previousSegmentMap,
                        superblockCoefficients,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        context);
                }

                if (encodeSelected && this.UsesEstimatedInterSearch)
                {
                    this.CountNoiseStillBlock(
                        blockOrigin,
                        modeInfo.Block,
                        modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra ? context.Snapshot.Displacement : default);
                }

                if (cyclicRefreshEncode)
                {
                    this.ResetCyclicRefreshSkip(
                        encoderSegmentMap,
                        searchSegmentMap,
                        macroBlock,
                        blockOrigin,
                        ref modeInfo.Block,
                        ref block,
                        modeInfo.Block.Skip || this.encodedWithoutCoefficients,
                        countSegments);
                }

                this.SelectedBlockStatistics = context.Snapshot.Statistics;
                return;
            }

            this.interTransformNoSplitCosts[..].Fill(long.MaxValue);
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            this.interSourceVariance = this.GetSourceVariance(
                sourceLuma, sourceBlue, sourceRed, modeWorkspace.GetCandidateReconstruction(0), blockOrigin, blockSize);

            Av1PartitionType partitionType = modeInfo.Block.PartitionType;
            Av1TransformSize maximumLumaTransformSize = this.BlockLossless
                ? Av1TransformSize.Size4x4
                : blockSize.GetMaximumTransformSize();

            int qIndex = this.blockQIndex;
            modeInfo.Block = new Av1EncoderBlockModeInfo
            {
                BlockSize = blockSize,
                PartitionType = partitionType,
                SegmentId = this.blockSegmentId,
                TransformSize = maximumLumaTransformSize,
                Mode = Av1PredictionMode.DC,
                UvMode = Av1ChromaPredictionMode.DC
            };

            modeInfo.CdefStrength = 0;
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            block.HasChroma = !this.source.IsMonochrome &&
                Av1TileReader.HasChroma(this.picture.Sequence.SequenceHeader, modeInfoPosition, blockSize);

            // The delta quantizer syntax codes the superblock quantizer, not the quantizer of the segment.
            block.QuantizationIndex = this.superblockQIndex;
            block.SegmentId = this.blockSegmentId;

            if (this.picture.Parent.FrameHeader.IsIntra && this.picture.Parent.SpeedSettings.UseEstimatedModeDecision)
            {
                // A nonzero hybrid level sends a block below 16x16 to the full intra search when its source variance
                // reaches the threshold of the level. Levels 1, 2 and 3 use the thresholds 0, 101 and 201.
                int sourceVariance = this.interSourceVariance;
                int hybridLevel = this.picture.Parent.SpeedSettings.HybridIntraSearchLevel;
                bool useFullSearch = blockSize < Av1BlockSize.Block16x16 &&
                    hybridLevel != 0 &&
                    sourceVariance >= hybridLevel switch { 1 => 0, 2 => 101, _ => 201 };

                if (!useFullSearch)
                {
                    this.EncodeEstimatedIntraBlock(
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        estimationRowCoefficients,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        superblockCoefficients,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        sourceVariance,
                        ref modeInfo,
                        ref block,
                        ref paletteInfo);

                    return;
                }
            }

            bool isInterFrame = !this.picture.Parent.FrameHeader.IsIntra;
            if (isInterFrame && this.picture.Parent.SpeedSettings.UseEstimatedInterModeDecision)
            {
                this.EncodeEstimatedInterBlock(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    estimationRowCoefficients,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    encoderSegmentMap,
                    searchSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    ref modeInfo,
                    ref block,
                    ref paletteInfo);

                return;
            }

            Av1RateDistortionStatistics interStatistics = Av1RateDistortionStatistics.Invalid;
            Av1MacroBlockModeInfo interModeInfo = default;
            Av1EncoderBlockStruct interBlock = default;
            InlineArray128<Av1EncoderTransformBlockState> interStates = default;
            Av1MotionVector interVector = default;
            Av1MotionVector interSecondaryVector = default;
            if (isInterFrame)
            {
                Av1MacroBlockModeInfo initialModeInfo = modeInfo;
                Av1EncoderBlockStruct initialBlock = block;
                interStatistics = this.SelectInterBlock(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    searchDequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    blockResidual,
                    in interWorkspace,
                    motionSearchPrediction,
                    in motionVectorCosts,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in transformEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    referenceContexts,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    ref modeInfo,
                    ref block,
                    out interVector,
                    out interSecondaryVector,
                    out interStates);

                interModeInfo = modeInfo;
                interBlock = block;

                // Only the winning syntax and transform choices survive across mode families. The intra trials use the prediction and
                // coefficient storage again. The selected inter block is reconstructed afterwards.
                modeInfo = initialModeInfo;
                block = initialBlock;
                paletteInfo = default;
            }

            Span<Av1EncoderTransformBlockState> lumaTransformBlocks = this.coefficientBuffer.GetTransformBlockSpan(superblockCoefficients, Av1Plane.Y);

            int lumaTransformIndex = this.codedAreaLuma /
                Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            Span<Av1EncoderTransformBlockState> retainedLumaStates = lumaTransformBlocks[lumaTransformIndex..];
            bool skipIntra = false;
            if (isInterFrame)
            {
                Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;

                // A block above the intra size limit skips the intra search when its best single-reference motion is small and its source is
                // not flat. Without a valid inter mode, the best mode is zero.
                bool validInter = interStatistics.Cost != long.MaxValue;
                if (settings.IntraModeMotionRangePruneLevel != 0 && blockSize > settings.MaximumIntraBlockSize &&
                    !(validInter && interModeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra))
                {
                    Av1MotionVector bestVector = validInter ? interVector : default;
                    int threshold = 16 << settings.IntraModeMotionRangePruneLevel;
                    skipIntra = Math.Abs(bestVector.Row) < threshold && Math.Abs(bestVector.Column) < threshold &&
                        this.interSourceVariance > 128;
                }

                // The mean model intra and inter costs of the block stay -1 without the statistics of a whole superblock. They also stay -1
                // at speeds 0 and 1 for frames whose shorter side is more than 480 lines.
                long tplInterCost = -1;
                long tplIntraCost = -1;
                ObuFrameSize frameSize = this.picture.Parent.FrameHeader.FrameSize;
                int minimumFrameDimension = Math.Min(frameSize.FrameWidth, frameSize.FrameHeight);
                bool tplPruning = !(minimumFrameDimension > 480 && this.picture.Parent.EncodingSpeed <= HeifEncodingSpeed.Level1);
                if (tplPruning && settings.IntraInInterPruningLevel != 0)
                {
                    Av1TplModePruning.GetCostFromTplData(
                        this.blockWorkspace.TplSuperblockInterCosts,
                        this.blockWorkspace.TplSuperblockIntraCosts,
                        this.tplSuperblockBlockCount,
                        this.tplSuperblockStride,
                        this.picture.Sequence.SequenceHeader.SuperblockSize.Get4x4WideCount(),
                        blockSize,
                        blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2,
                        blockOrigin.X >> Av1Constants.ModeInfoSizeLog2,
                        out tplInterCost,
                        out tplIntraCost);
                }

                // Without a valid inter mode, the best mode is zero, so it neither skips its transform nor codes a new vector.
                bool bestSkip = validInter && interModeInfo.Block.Skip;
                if (!skipIntra && settings.IntraInInterPruningLevel != 0 && this.interSourceVariance > 1)
                {
                    if (settings.IntraInInterPruningLevel >= 2 && bestSkip)
                    {
                        bool newMotion = interModeInfo.Block.Mode is Av1PredictionMode.NewMotionVector or
                            Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NewNearestMotionVector or
                            Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector or Av1PredictionMode.NewNewMotionVector;

                        // Nearly flat sources keep the intra search. Otherwise a skipped residual from inherited motion is sufficient
                        // evidence, but the weaker policy applies only at Q 200 or less. Without model costs, the strongest policies also accept
                        // searched motion.
                        skipIntra = (!newMotion && (settings.IntraInInterPruningLevel >= 3 || qIndex <= 200)) ||
                            (settings.IntraInInterPruningLevel >= 4 && (tplInterCost < 0 || tplIntraCost < 0));
                    }

                    // With both model costs, a small network decides from them, the transform skip of the best mode, the block shape and the
                    // quantizer.
                    if (!skipIntra)
                    {
                        skipIntra = Av1TplModePruning.SkipIntraByNetwork(
                            bestSkip, blockSize, tplIntraCost, tplInterCost, qIndex, this.bitDepth, minimumFrameDimension);
                    }
                }

                // A nonzero sharpness skips the intra search of a block wider or taller than 16 samples. High bit depth sharpness 3 is an
                // exception, because it searches all sizes.
                bool largeBlock = blockSize.GetWidth() > 16 || blockSize.GetHeight() > 16;
                if (this.blockWorkspace.EncoderOptions.Sharpness != 0 && !this.UsesHighBitDepthSharpness && largeBlock)
                {
                    skipIntra = true;
                }
            }

            int lumaAngleDelta = 0;
            Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            Av1TransformSize lumaTransformSize = maximumLumaTransformSize;
            Av1RateDistortionStatistics lumaStatistics = Av1RateDistortionStatistics.Invalid;
            if (!skipIntra)
            {
                // The intra search clears the leftover skip flag of the inter transform search.
                this.transformSearchSkip = false;
                modeInfo.Block.Mode = this.SelectLumaMode(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    encoderSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    retainedLumaStates,
                    Math.Min(this.blockCostLimit, interStatistics.Cost),
                    ref paletteInfo,
                    out lumaAngleDelta,
                    out filterIntraMode,
                    out lumaTransformSize,
                    out lumaStatistics);
            }

            // The chroma search is useful only after luma beats the inter luma cost. That cost is the luma cost of the winner, or, after
            // the search of the kept candidates, of the cheapest completed candidate, even above the budget. Empty luma residuals use the
            // skip-symbol estimate for this gate. The final intra syntax stays coded.
            if (isInterFrame && lumaStatistics.LumaCost >= this.interLumaThreshold)
            {
                lumaStatistics = Av1RateDistortionStatistics.Invalid;
            }

            block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = (sbyte)lumaAngleDelta;
            block.FilterIntraMode = filterIntraMode;
            modeInfo.Block.TransformSize = lumaTransformSize;

            int chromaArea = 0;
            Av1RateDistortionStatistics chromaStatistics = new(this.rateMultiplier, 0, 0);
            if (block.HasChroma)
            {
                ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
                int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
                int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
                Point chromaOrigin = Av1TileWriter.GetChromaBlockOrigin(
                    blockOrigin,
                    subsamplingX,
                    subsamplingY);

                Av1TransformSize chromaTransformSize = this.BlockLossless
                    ? Av1TransformSize.Size4x4
                    : blockSize.GetMaxUvTransformSize(
                        colorConfig.SubSamplingX,
                        colorConfig.SubSamplingY);

                Av1BlockSize chromaBlockSize = blockSize.GetSubsampled(
                    colorConfig.SubSamplingX,
                    colorConfig.SubSamplingY);

                Size chromaExtent = GetCodedTransformExtent(
                    macroBlock, chromaBlockSize, chromaTransformSize, subsamplingX, subsamplingY);

                chromaArea = chromaExtent.Width * chromaExtent.Height;
                if (lumaStatistics.Cost != long.MaxValue)
                {
                    Span<Av1EncoderTransformBlockState> blueTransformBlocks = this.coefficientBuffer.GetTransformBlockSpan(superblockCoefficients, Av1Plane.U);

                    Span<Av1EncoderTransformBlockState> redTransformBlocks = this.coefficientBuffer.GetTransformBlockSpan(superblockCoefficients, Av1Plane.V);

                    int chromaTransformIndex = this.codedAreaChroma /
                        Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

                    Span<Av1EncoderTransformBlockState> retainedBlueStates = blueTransformBlocks[chromaTransformIndex..];
                    Span<Av1EncoderTransformBlockState> retainedRedStates = redTransformBlocks[chromaTransformIndex..];
                    modeInfo.Block.UvMode = this.SelectChromaMode(
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        blockResidual,
                        searchCoefficients,
                        searchReconstructions,
                        in paletteEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        modeInfo,
                        blockOrigin,
                        chromaOrigin,
                        blockSize,
                        modeInfo.Block.Mode,
                        chromaTransformSize,
                        retainedBlueStates,
                        retainedRedStates,
                        ref paletteInfo,
                        out int chromaAngleDelta,
                        out byte chromaFromLumaIndex,
                        out sbyte chromaFromLumaSigns,
                        out chromaStatistics);

                    block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = (sbyte)chromaAngleDelta;
                    block.PredictionUnit.ChromaFromLumaIndex = chromaFromLumaIndex;
                    block.PredictionUnit.ChromaFromLumaSigns = chromaFromLumaSigns;
                    if (chromaStatistics.Cost == long.MaxValue ||
                        (isInterFrame && Av1RateDistortion.GetCost(
                            this.rateMultiplier, chromaStatistics.ResidualRate, chromaStatistics.Distortion) > interStatistics.Cost))
                    {
                        lumaStatistics = Av1RateDistortionStatistics.Invalid;
                    }
                    else
                    {
                        lumaStatistics.Add(this.rateMultiplier, chromaStatistics);
                    }
                }
            }

            // The estimated mode search never searches a copy, even in the blocks that take the full search, although the header can still
            // allow one. Only a screen-content tune keeps the copy search in real-time usage. This encoder has no such tune, and detected
            // screen content does not select it.
            bool allowIntraBlockCopy = this.picture.Parent.FrameHeader.AllowIntraBlockCopy;
            bool nonRdWithoutCopy = this.picture.Parent.SpeedSettings.UseEstimatedModeDecision;

            bool searchIntraBlockCopy = allowIntraBlockCopy && !nonRdWithoutCopy &&
                this.picture.Parent.MotionSearchSettings.AllowIntraBlockCopy &&
                (!this.picture.Parent.MotionSearchSettings.UseFastIntraBlockCopySearch ||
                 blockSize is Av1BlockSize.Block4x4 or Av1BlockSize.Block8x8 or Av1BlockSize.Block16x16);

            Av1RateDistortionStatistics regularStatistics = lumaStatistics.Cost == long.MaxValue
                ? Av1RateDistortionStatistics.Invalid
                : this.GetRegularBlockCost(in tables, modeInfoGrid, modeInfoAllocation, macroBlock, lumaStatistics);

            // Sharpness 3 charges the merged intra result of an inter frame by the luma samples that its luma search left, before the
            // budget comparison.
            if (regularStatistics.Cost != long.MaxValue && this.ChargesSmoothing)
            {
                regularStatistics.AddSmoothingOffset(this.rateMultiplier, this.intraSmoothingOffset);
            }
            else if (regularStatistics.Cost != long.MaxValue && isInterFrame && this.ChargesHighBitDepthTextureLoss)
            {
                // At a high bit depth, the charge also uses the mode of the luma winner, and its extra cost scales the merged cost.
                bool smoothMode = IsSmoothTextureMode(modeInfo.Block.Mode);
                long currentCost = regularStatistics.Cost;
                this.ChargeTextureLoss(
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    ref regularStatistics,
                    currentCost,
                    this.intraSmoothingOffset,
                    blockOrigin,
                    blockSize,
                    false,
                    false,
                    smoothMode);
            }

            // An inter frame keeps the intra result only when it beats the budget of the block. Thus a block without a mode below the budget
            // has no winner to refine.
            if (isInterFrame && regularStatistics.Cost >= this.blockCostLimit)
            {
                regularStatistics = Av1RateDistortionStatistics.Invalid;
            }

            if (isInterFrame && interStatistics.Cost != long.MaxValue && interStatistics.Cost <= regularStatistics.Cost)
            {
                // Inter candidates precede intra candidates, so an equal cost retains the inter winner.
                modeInfo = interModeInfo;
                block = interBlock;
                paletteInfo = default;
                Av1EncoderSpeedSettings winnerSettings = this.picture.Parent.SpeedSettings;
                bool hasNewMotion = modeInfo.Block.Mode is Av1PredictionMode.NewMotionVector or
                    Av1PredictionMode.NewNewMotionVector or Av1PredictionMode.NearestNewMotionVector or
                    Av1PredictionMode.NewNearestMotionVector or Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector;

                // The pruning level decides whether the winner skips its refinement, from its skip flag, its all-empty result, its motion
                // and the quantizer.
                bool bypassWinner = winnerSettings.GetInterWinnerPruningLevel(this.picture.Parent.FrameUpdateType) switch
                {
                    2 => !hasNewMotion && interStatistics.AllTransformsEmpty,
                    3 => !hasNewMotion && (interStatistics.AllTransformsEmpty || (this.blockQIndex <= 127 && modeInfo.Block.Skip)),
                    4 => !(winnerSettings.CoefficientOptimizationLevel >= 5 && this.blockQIndex <= 70) &&
                        (modeInfo.Block.Skip || interStatistics.AllTransformsEmpty),
                    _ => false
                };

                // The partition search reads whether the mode-evaluation winner kept any coefficient. The winner refinement does not change
                // this value.
                bool skippable = interStatistics.AllTransformsEmpty;
                if (!bypassWinner && (winnerSettings.EnableWinnerCoefficientOptimization ||
                    winnerSettings.DeferTransformSizeSearch || winnerSettings.UseWinnerInterpolation ||
                    winnerSettings.InterTransformTypeProbabilityThreshold != int.MaxValue))
                {
                    this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Winner;
                    this.SetWarpedPrediction(macroBlock, blockOrigin, modeInfo.Block.PartitionType, modeInfo.Block, interVector);
                    this.RefineInterTransformSize(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        in interWorkspace,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        ref modeInfo,
                        block,
                        interVector,
                        interSecondaryVector,
                        ref interStatistics,
                        ref interStates);

                    this.useWarpedPrediction = false;
                    this.useObmcPrediction = false;
                }

                interStatistics.AllTransformsEmpty = skippable;
                this.SelectedBlockStatistics = interStatistics;
            }
            else
            {
                this.SelectedBlockStatistics = searchIntraBlockCopy
                    ? this.SelectIntraBlockCopy(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        searchDequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        in interWorkspace,
                        in motionVectorCosts,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in transformEdges,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        encoderSegmentMap,
                        previousSegmentMap,
                        superblockCoefficients,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        regularStatistics,
                        ref modeInfo,
                        ref block,
                        ref paletteInfo)
                    : regularStatistics;
            }

            this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Default;
            if (isInterFrame && modeInfo.Block.ReferenceFrame == Av1ReferenceFrameType.Intra &&
                this.SelectedBlockStatistics.Cost != long.MaxValue)
            {
                // Winner refinement and palette search follow the family comparison. Retain syntax and
                // transform states in idle inter storage while these trials reuse intra scratch.
                this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Default;
                Av1EncoderPartitionTree.ModeContext winner = Av1EncoderBlockWorkspace.GetIntraWinnerContext(workspaceStorage, block.HasChroma ? 3 : 1);
                winner.Snapshot = new Av1EncoderPartitionTree.ModeSnapshot
                {
                    ModeInfo = modeInfo,
                    Block = block,
                    Palette = paletteInfo,
                    Statistics = this.SelectedBlockStatistics
                };

                Size retainedExtent = GetCodedTransformExtent(macroBlock, blockSize, modeInfo.Block.TransformSize, 0, 0);
                this.RetainModeContext(
                    superblockCoefficients, winner, this.codedAreaLuma, this.codedAreaChroma, retainedExtent.Width * retainedExtent.Height, chromaArea);

                // The winner keeps the transform grid that its own luma search chose. The shared coefficient buffer belongs to whichever
                // candidate wrote it last. A grid taken from there can give this block a transform type that its size does not allow.
                CopyWinnerTransformStates(retainedLumaStates, winner.GetTransformStates(Av1Plane.Y));

                // The palette search reads the mode search result and its chroma from before the winner refinement. The refinement keeps its
                // own best cost, so the palette bound and comparison use the result of the mode search.
                Av1RateDistortionStatistics modeSearchStatistics = this.SelectedBlockStatistics;
                Av1RateDistortionStatistics modeSearchChroma = chromaStatistics;
                Av1EncoderSpeedSettings speedSettings = this.picture.Parent.SpeedSettings;
                if (!this.BlockLossless &&
                    (speedSettings.IntraTransformTypeSearchLevel != 0 ||
                     speedSettings.EnableWinnerCoefficientOptimization || speedSettings.DeferTransformSizeSearch))
                {
                    // The luma refinement writes each improving trial into the luma winner context, which shares storage with this winner.
                    // A copy of the grid of the winner lets a rejected refinement leave the grid as the mode search chose it.
                    InlineArray256<Av1EncoderTransformBlockState> keptLumaStates = default;
                    Span<Av1EncoderTransformBlockState> winnerLuma = winner.GetTransformStates(Av1Plane.Y);
                    Span<Av1EncoderTransformBlockState> keptLuma = keptLumaStates[..winnerLuma.Length];
                    winnerLuma.CopyTo(keptLuma);
                    bool refinementAccepted = false;

                    this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Winner;
                    Av1RateDistortionStatistics refinedLuma = Av1RateDistortionStatistics.Invalid;
                    Av1EncoderPaletteInfo refinedPalette = paletteInfo;
                    int refinedAngle = block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y];
                    Av1FilterIntraMode refinedFilter = block.FilterIntraMode;
                    Av1TransformSize refinedSize = modeInfo.Block.TransformSize;
                    Av1PredictionMode refinedMode = this.RefineLumaMode(
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        in transformEdges,
                        in paletteEdges,
                        in lumaCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        workspaceStorage,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        retainedLumaStates,
                        modeInfo.Block.Mode,
                        ref refinedPalette,
                        ref refinedAngle,
                        ref refinedFilter,
                        ref refinedSize,
                        ref refinedLuma);

                    // The selected UV mode is refined against the newly reconstructed luma. Its mode, angle, palette and CfL alpha stay
                    // fixed. Only the transform coefficients change.
                    if (refinedLuma.Cost != long.MaxValue)
                    {
                        this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Winner;
                        Av1MacroBlockModeInfo refinedModeInfo = modeInfo;
                        refinedModeInfo.Block.Mode = refinedMode;
                        refinedModeInfo.Block.TransformSize = refinedSize;
                        Av1EncoderBlockStruct refinedBlock = block;
                        refinedBlock.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = (sbyte)refinedAngle;
                        refinedBlock.FilterIntraMode = refinedFilter;
                        Av1RateDistortionStatistics refinedChroma = this.RefineSelectedChroma(
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            searchDequantizedCoefficients,
                            transformWorkspace,
                            transformTypeProbabilities,
                            blockResidual,
                            searchCoefficients,
                            searchReconstructions,
                            in blueCoefficientEdges,
                            in redCoefficientEdges,
                            modeInfoGrid,
                            modeInfoAllocation,
                            superblockCoefficients,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            blockOrigin,
                            refinedModeInfo,
                            refinedBlock,
                            refinedPalette,
                            chromaStatistics);

                        refinedLuma.Add(this.rateMultiplier, refinedChroma);
                        Av1RateDistortionStatistics refinedStatistics =
                            this.GetRegularBlockCost(in tables, modeInfoGrid, modeInfoAllocation, macroBlock, refinedLuma);

                        if (this.UsesHighBitDepthSharpness)
                        {
                            this.ChargeRefinedIntraTextureLoss(
                                sourceLuma,
                                sourceBlue,
                                sourceRed,
                                reconstructionLuma,
                                reconstructionBlue,
                                reconstructionRed,
                                ref refinedStatistics,
                                blockOrigin,
                                blockSize,
                                refinedMode);
                        }

                        if (refinedStatistics.Cost < this.SelectedBlockStatistics.Cost)
                        {
                            modeInfo = refinedModeInfo;
                            block = refinedBlock;
                            paletteInfo = refinedPalette;
                            chromaStatistics = refinedChroma;
                            this.SelectedBlockStatistics = refinedStatistics;
                            winner.Snapshot.ModeInfo = modeInfo;
                            winner.Snapshot.Block = block;
                            winner.Snapshot.Palette = paletteInfo;
                            winner.Snapshot.Statistics = refinedStatistics;
                            Size refinedExtent = GetCodedTransformExtent(macroBlock, blockSize, refinedSize, 0, 0);
                            this.RetainModeContext(
                                superblockCoefficients,
                                winner,
                                this.codedAreaLuma,
                                this.codedAreaChroma,
                                refinedExtent.Width * refinedExtent.Height,
                                chromaArea);

                            CopyWinnerTransformStates(retainedLumaStates, winner.GetTransformStates(Av1Plane.Y));
                            refinementAccepted = true;
                        }
                    }

                    if (!refinementAccepted)
                    {
                        keptLuma.CopyTo(winnerLuma);
                        CopyWinnerTransformStates(keptLuma, retainedLumaStates);
                    }

                    this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Default;
                }

                // The search leaves the last transform block of each plane as its prediction. Thus the winner is encoded again, whether or
                // not a refinement ran. The next block predicts from it and stores its luma for chroma-from-luma. A leaf of its own partition
                // search has no such encode, so the winner keeps its searched types here. The partition search writes DCT_DCT for a sibling
                // that quantized to nothing.
                int reconstructedLumaArea = this.codedAreaLuma;
                int reconstructedChromaArea = this.codedAreaChroma;
                this.keepSearchedZeroBlockTypes = true;
                this.ReconstructSelectedIntraBlock(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    encoderSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    winner);

                this.keepSearchedZeroBlockTypes = false;
                this.codedAreaLuma = reconstructedLumaArea;
                this.codedAreaChroma = reconstructedChromaArea;

                Av1RateDistortionStatistics paletteStatistics = modeSearchStatistics;
                Av1EncoderPaletteInfo candidatePalette = paletteInfo;
                Av1TransformSize paletteTransformSize = modeInfo.Block.TransformSize;
                Av1ModeCosts modeCosts = tables.ModeCosts;
                if (Av1TileWriter.IsPaletteAllowed(this.picture.Parent.FrameHeader.AllowScreenContentTools, blockSize) &&
                    this.SelectLumaPalette(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    retainedLumaStates,
                    64,
                    Av1SymbolEncoder.GetInterFrameLumaModeCost(modeCosts, Av1PredictionMode.DC, blockSize),
                    ref paletteStatistics,
                    ref candidatePalette,
                    ref paletteTransformSize))
                {
                    // An intra transform block always signals coefficients in the rate-distortion search. Thus a palette is never skippable,
                    // even when it quantized to nothing, and it pays for its residual syntax and the cleared skip flag.
                    int intraInterContext = Av1TileWriter.GetIntraInterContext(modeInfoGrid, modeInfoAllocation, macroBlock);
                    int skipContext = Av1TileWriter.GetSkipContext(modeInfoGrid, modeInfoAllocation, macroBlock);
                    int rate = Av1SymbolEncoder.GetIsInterCost(tables.ModeCosts, false, intraInterContext) +
                        paletteStatistics.Rate + modeSearchChroma.Rate +
                        Av1SymbolEncoder.GetSkipCost(modeCosts, false, skipContext);

                    Av1RateDistortionStatistics combinedStatistics = new(
                        this.rateMultiplier, rate, paletteStatistics.Distortion + modeSearchChroma.Distortion);

                    if (combinedStatistics.Cost < modeSearchStatistics.Cost)
                    {
                        // The coded skip flag stays clear, so the final encode quantizes every transform block again.
                        modeInfo.Block.Mode = Av1PredictionMode.DC;
                        modeInfo.Block.TransformSize = paletteTransformSize;
                        modeInfo.Block.Skip = false;
                        block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = 0;
                        block.FilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
                        paletteInfo = candidatePalette;
                        this.SelectedBlockStatistics = combinedStatistics;
                        winner.Snapshot.ModeInfo = modeInfo;
                        winner.Snapshot.Block = block;
                        winner.Snapshot.Palette = paletteInfo;
                        winner.Snapshot.Statistics = combinedStatistics;
                        Size paletteExtent = GetCodedTransformExtent(macroBlock, blockSize, paletteTransformSize, 0, 0);
                        this.RetainModeContext(
                            superblockCoefficients, winner, this.codedAreaLuma, this.codedAreaChroma, paletteExtent.Width * paletteExtent.Height, chromaArea);

                        CopyWinnerTransformStates(retainedLumaStates, winner.GetTransformStates(Av1Plane.Y));
                    }

                    // The kept winner is built again after the palette trials. This also builds the CfL chroma again from the reconstructed
                    // luma of a winning palette, and keeps its selected UV mode.
                    int savedLumaArea = this.codedAreaLuma;
                    int savedChromaArea = this.codedAreaChroma;
                    this.keepSearchedZeroBlockTypes = true;
                    this.ReconstructSelectedIntraBlock(
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        encoderSegmentMap,
                        previousSegmentMap,
                        superblockCoefficients,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        winner);

                    this.keepSearchedZeroBlockTypes = false;
                    this.codedAreaLuma = savedLumaArea;
                    this.codedAreaChroma = savedChromaArea;
                }
            }
            else if (encodeSelected && modeInfo.Block.ReferenceFrame <= Av1ReferenceFrameType.Intra &&
                !modeInfo.Block.UseIntraBlockCopy && this.SelectedBlockStatistics.Cost != long.MaxValue)
            {
                // The search leaves the last transform block of each plane as its prediction. Thus the winner is encoded again over every
                // transform block before the syntax is written, and the next block predicts from it.
                Av1EncoderPartitionTree.ModeContext winner = Av1EncoderBlockWorkspace.GetIntraWinnerContext(workspaceStorage, block.HasChroma ? 3 : 1);
                winner.Snapshot = new Av1EncoderPartitionTree.ModeSnapshot
                {
                    ModeInfo = modeInfo,
                    Block = block,
                    Palette = paletteInfo,
                    Statistics = this.SelectedBlockStatistics
                };

                Size selectedExtent = GetCodedTransformExtent(macroBlock, blockSize, modeInfo.Block.TransformSize, 0, 0);
                this.RetainModeContext(
                    superblockCoefficients, winner, this.codedAreaLuma, this.codedAreaChroma, selectedExtent.Width * selectedExtent.Height, chromaArea);

                CopyWinnerTransformStates(retainedLumaStates, winner.GetTransformStates(Av1Plane.Y));
                int savedLumaArea = this.codedAreaLuma;
                int savedChromaArea = this.codedAreaChroma;
                this.ReconstructSelectedIntraBlock(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    encoderSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    winner);

                this.codedAreaLuma = savedLumaArea;
                this.codedAreaChroma = savedChromaArea;
            }

            if (isInterFrame)
            {
                // Skip mode is a one-sided compound when every reference is in the past. Thus a frame that drops those pairs neither
                // searches it nor prices the non-skip-mode symbol. Sharpness 3 never searches it.
                ObuSkipModeParameters skipModeParameters = this.picture.Parent.FrameHeader.SkipModeParameters;
                if (skipModeParameters.SkipModeFlag && Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8 &&
                    this.blockWorkspace.EncoderOptions.Sharpness != 3 &&
                    !this.picture.Parent.PrunesAllCompoundReferences)
                {
                    int skipModeContext = Av1TileWriter.GetSkipModeContext(modeInfoGrid, modeInfoAllocation, macroBlock);
                    Av1RateDistortionStatistics selectedStatistics = this.SelectedBlockStatistics;

                    // A block whose kept candidates all failed still compares skip mode with the best estimate of the mode loop. It keeps no
                    // mode when skip mode loses.
                    bool comparesLeftoverEstimate = selectedStatistics.Cost == long.MaxValue &&
                        this.leftoverInterEstimate.Cost != long.MaxValue;

                    if (comparesLeftoverEstimate)
                    {
                        selectedStatistics = this.leftoverInterEstimate;
                    }

                    Av1RateDistortionStatistics syntaxStatistics = new(
                        this.rateMultiplier, Av1SymbolEncoder.GetSkipModeCost(tables.ModeCosts, false, skipModeContext), 0);

                    // The non-skip-mode symbol is priced only when the reference lists of the pair exist.
                    bool skipModeListsReady = this.HasSkipModeReferenceLists(
                        workspaceStorage, skipModeParameters.FirstReferenceFrame, skipModeParameters.SecondReferenceFrame);

                    byte availableReferences = this.picture.Parent.AvailableReferenceMask;
                    bool searchesSkipMode = (availableReferences & (1 << (int)skipModeParameters.FirstReferenceFrame)) != 0 &&
                        (availableReferences & (1 << (int)skipModeParameters.SecondReferenceFrame)) != 0 &&
                        skipModeListsReady;

                    // A searched skip mode prices the symbol itself, after its vectors pass the vector validity test.
                    if (selectedStatistics.Cost != long.MaxValue && skipModeListsReady && !searchesSkipMode)
                    {
                        selectedStatistics.Add(this.rateMultiplier, syntaxStatistics);
                    }

                    if (searchesSkipMode)
                    {
                        this.SelectSkipModeBlock(
                            in tables,
                            in interWorkspace,
                            firstIntermediate,
                            secondIntermediate,
                            compoundMask,
                            modeInfoGrid,
                            modeInfoAllocation,
                            displacementVectors,
                            referenceContexts,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            blockOrigin,
                            skipModeContext,
                            syntaxStatistics,
                            ref modeInfo,
                            ref block,
                            ref selectedStatistics,
                            ref interVector,
                            ref interSecondaryVector,
                            ref interStates);
                    }

                    // Skip mode replaces a result that nothing else produced. But a skip mode at or above the block budget still leaves the
                    // block without a result.
                    bool noMode = modeInfo.Block.SkipMode
                        ? selectedStatistics.Cost >= this.blockCostLimit
                        : comparesLeftoverEstimate;

                    this.SelectedBlockStatistics = noMode ? Av1RateDistortionStatistics.Invalid : selectedStatistics;
                }

                if (modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra)
                {
                    // The block codes no residual when the last transform search left its skip flag set, or when the mode search winner was
                    // skippable. This also applies after a winner refinement that kept coefficients. The rate-distortion result keeps the
                    // refined cost.
                    if (!modeInfo.Block.Skip && (this.transformSearchSkip || this.SelectedBlockStatistics.AllTransformsEmpty))
                    {
                        Av1TransformSize maximumSize = this.BlockLossless
                            ? Av1TransformSize.Size4x4
                            : blockSize.GetMaximumTransformSize();

                        modeInfo.Block.Skip = true;
                        modeInfo.Block.TransformSize = maximumSize;
                        modeInfo.Block.InterTransformSizes.Fill(maximumSize);
                        interStates = default;
                    }

                    paletteInfo = default;
                    this.keepSearchedZeroBlockTypes = true;
                    this.encodedWithoutCoefficients = !this.ReconstructSelectedInterBlock(
                        writer,
                        in tables,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        in interWorkspace,
                        transformPrediction,
                        interIntraAbove,
                        interIntraLeft,
                        firstIntermediate,
                        secondIntermediate,
                        compoundMask,
                        in lumaCoefficientEdges,
                        in blueCoefficientEdges,
                        in redCoefficientEdges,
                        modeInfoGrid,
                        modeInfoAllocation,
                        displacementVectors,
                        encoderSegmentMap,
                        previousSegmentMap,
                        superblockCoefficients,
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        macroBlock,
                        blockOrigin,
                        modeInfo,
                        block,
                        interVector,
                        interSecondaryVector,
                        interStates);

                    this.keepSearchedZeroBlockTypes = false;
                    this.picture.SetDisplacementVector(displacementVectors, modeInfoPosition, interVector);
                    if (modeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                    {
                        // The spatial motion stack of the encoder must keep both vectors of the selected pair. Otherwise later compound blocks
                        // derive another nearest pair than the decoder does.
                        this.picture.SetSecondaryDisplacementVector(referenceContexts, modeInfoPosition, interSecondaryVector);
                    }
                }
            }

            if (this.SelectedBlockStatistics.Cost == long.MaxValue)
            {
                return;
            }

            // A frame coded from the source of an alternate reference leaves the thresholds as they were.
            if (isInterFrame && this.picture.Parent.SpeedSettings.AdaptiveModeThresholdLevel != 0 &&
                !this.picture.Parent.IsSourceAlternateReference)
            {
                // The update runs only after the residual refinement, the palette and the skip-mode selection are complete. The partition
                // replay returns earlier and must not count the kept winner a second time.
                Av1ModeThresholds.Update(
                    this.blockWorkspace.GetModeThresholdFactors(workspaceStorage),
                    blockSize,
                    this.picture.Sequence.SequenceHeader.SuperblockSizeLog2 == 7 ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64,
                    Av1ModeThresholds.GetIndex(modeInfo.Block.Mode, modeInfo.Block.ReferenceFrame, modeInfo.Block.SecondaryReferenceFrame),
                    this.picture.Parent.FrameHeader.ReferenceMode == ObuReferenceMode.SingleReference,
                    this.picture.Parent.SpeedSettings.AdaptiveModeThresholdLevel);
            }

            Size lumaExtent = GetCodedTransformExtent(macroBlock, blockSize, modeInfo.Block.TransformSize, 0, 0);
            this.codedAreaLuma += modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra || modeInfo.Block.UseIntraBlockCopy
                ? GetInterLumaCodedArea(macroBlock, ref modeInfo.Block)
                : lumaExtent.Width * lumaExtent.Height;

            this.codedAreaChroma += chromaArea;
        }

        /// <summary>
        /// Runs <see cref="EvaluatePartitionLeafCore"/> at the rate multiplier of the block.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="partitionType">The partition type that contains the leaf.</param>
        /// <param name="context">The context that keeps the winner of the leaf.</param>
        /// <param name="costLimit">The cost above which the search stops.</param>
        /// <param name="publishContexts">Whether the winner writes the neighbor contexts.</param>
        /// <param name="publishCoefficientContexts">Whether the winner writes the coefficient contexts.</param>
        /// <param name="intraEncodeFollows">Whether an encode of an intra winner follows, which writes its own coefficient contexts.</param>
        /// <returns>The rate and distortion of the leaf.</returns>
        private Av1RateDistortionStatistics EvaluatePartitionLeaf(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType,
            Av1EncoderPartitionTree.ModeContext context,
            long costLimit,
            bool publishContexts,
            bool publishCoefficientContexts,
            bool intraEncodeFollows = false)
        {
            // A bound that is already negative returns before the block setup, so neither the block variance nor the error per bit changes.
            if (costLimit == NegativeLeafBound)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            // The block takes its segment, and the variance and complexity modes price it at the segment quantizer. A frame that refreshes
            // its variance segments places the block by its source variance, but still searches it with the quantizer of segment 0.
            this.SetBlockSegment(encoderSegmentMap, previousSegmentMap, blockOrigin, blockSize);
            int rateSegmentId = -1;
            Av1AdaptiveQuantizationMode adaptiveQuantization = this.picture.Parent.EncoderOptions.AdaptiveQuantizationMode;
            if (adaptiveQuantization == Av1AdaptiveQuantizationMode.Variance)
            {
                if (this.picture.Parent.VarianceSegmentRefresh)
                {
                    this.blockSegmentId = this.GetVarianceSegmentId(workspaceStorage, sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize);
                }

                rateSegmentId = this.blockSegmentId;
            }
            else if (adaptiveQuantization is Av1AdaptiveQuantizationMode.Complexity or Av1AdaptiveQuantizationMode.CyclicRefresh)
            {
                rateSegmentId = this.blockSegmentId;
            }

            // The search sets the motion vector error per bit from the block multiplier.
            int savedRateMultiplier = this.rateMultiplier;
            this.rateMultiplier = this.GetBlockRateMultiplier(blockOrigin, blockSize, rateSegmentId);
            this.blockWorkspace.ErrorPerBitRateMultiplier = this.rateMultiplier;
            Av1RateDistortionStatistics result = this.EvaluatePartitionLeafCore(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                blockResidual,
                searchCoefficients,
                searchReconstructions,
                estimationRowCoefficients,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                transformPrediction,
                interIntraAbove,
                interIntraLeft,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                encoderSegmentMap,
                searchSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                blockSize,
                partitionType,
                context,
                costLimit,
                publishContexts,
                publishCoefficientContexts,
                intraEncodeFollows);

            this.rateMultiplier = savedRateMultiplier;

            // Complexity adaptive quantization places each searched block of 16x16 or larger by its rate. The block size order puts the 4:1
            // sizes after 128x128, so this test also includes them.
            if (result.Rate != int.MaxValue && blockSize >= Av1BlockSize.Block16x16 &&
                adaptiveQuantization == Av1AdaptiveQuantizationMode.Complexity && this.picture.Parent.ComplexitySegmentRefresh)
            {
                this.SelectComplexitySegment(encoderSegmentMap, workspaceStorage, sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize, result.Rate);
            }

            return result;
        }

        /// <summary>
        /// Writes the complexity segment of a searched block into the encoder segment map.
        /// </summary>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="projectedRate">The block's searched rate.</param>
        private void SelectComplexitySegment(
            Span<byte> encoderSegmentMap,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int projectedRate)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            Av1EncoderCommon common = parent.Common;
            int modeInfoRow = blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = blockOrigin.X >> Av1Constants.ModeInfoSizeLog2;
            int visibleColumns = Math.Min(common.ModeInfoColumnCount - modeInfoColumn, blockSize.Get4x4WideCount());
            int visibleRows = Math.Min(common.ModeInfoRowCount - modeInfoRow, blockSize.Get4x4HighCount());
            byte segment = Av1ComplexityAdaptiveQuantization.SelectSegment(
                projectedRate,
                parent.SuperblockTargetRate,
                visibleColumns * visibleRows,
                this.picture.Sequence.SequenceHeader.SuperblockModeInfoSize,
                this.GetLogBlockVariance(workspaceStorage, sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize),
                this.quantization.BaseQIndex,
                this.bitDepth);

            // The segment fills the visible 4x4 units of the block in the map.
            for (int row = 0; row < visibleRows; row++)
            {
                encoderSegmentMap.Slice(((modeInfoRow + row) * common.ModeInfoColumnCount) + modeInfoColumn, visibleColumns).Fill(segment);
            }
        }

        /// <summary>
        /// Searches the modes of one partition leaf, keeps the winner in its context and writes the neighbor contexts that later leaves
        /// read.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the candidate.</param>
        /// <param name="searchDequantizedCoefficients">The dequantized coefficient buffer of the winner.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="blockResidual">The residual buffer of the motion search.</param>
        /// <param name="searchCoefficients">The quantized coefficient buffer of the winner.</param>
        /// <param name="searchReconstructions">The storage of the candidate and winner reconstructions of the type search.</param>
        /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="partitionType">The partition type that contains the leaf.</param>
        /// <param name="context">The context that keeps the winner of the leaf.</param>
        /// <param name="costLimit">The cost above which the search stops.</param>
        /// <param name="publishContexts">Whether the winner writes the neighbor contexts.</param>
        /// <param name="publishCoefficientContexts">Whether the winner writes the coefficient contexts.</param>
        /// <param name="intraEncodeFollows">Whether an encode of an intra winner follows, which writes its own coefficient contexts.</param>
        /// <returns>The rate and distortion of the leaf.</returns>
        private Av1RateDistortionStatistics EvaluatePartitionLeafCore(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> searchDequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Span<short> blockResidual,
            Span<int> searchCoefficients,
            Span<int> searchReconstructions,
            Span<int> estimationRowCoefficients,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> motionSearchPrediction,
            in Av1MotionVectorCosts motionVectorCosts,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType,
            Av1EncoderPartitionTree.ModeContext context,
            long costLimit,
            bool publishContexts,
            bool publishCoefficientContexts,
            bool intraEncodeFollows)
        {
            this.blockCostLimit = costLimit;

            // Trial leaves must use the same reconstruction order as final leaves of this partition.
            this.SetBlockGeometry(modeInfoGrid, modeInfoAllocation, blockOrigin, blockSize, partitionType);
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            Av1TileWriter.SetModeInfoRowAndColumn(
                macroBlock,
                macroBlock.Tile,
                modeInfoPosition,
                blockSize,
                this.picture.Parent.Common.ModeInfoStride,
                this.picture.Parent.Common.ModeInfoRowCount,
                this.picture.Parent.Common.ModeInfoColumnCount);

            ref Av1MacroBlockModeInfo modeInfo = ref this.picture.GetMacroBlockModeInfo(modeInfoAllocation, modeInfoPosition);
            Av1EncoderBlockStruct block = default;
            Av1EncoderPaletteInfo paletteInfo = default;
            int lumaArea = this.codedAreaLuma;
            int chromaArea = this.codedAreaChroma;
            this.encodedWithoutCoefficients = false;
            this.SearchBlock(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                searchDequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                blockResidual,
                searchCoefficients,
                searchReconstructions,
                estimationRowCoefficients,
                in interWorkspace,
                motionSearchPrediction,
                in motionVectorCosts,
                transformPrediction,
                interIntraAbove,
                interIntraLeft,
                firstIntermediate,
                secondIntermediate,
                compoundMask,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                in blueCoefficientEdges,
                in redCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                displacementVectors,
                referenceContexts,
                encoderSegmentMap,
                searchSegmentMap,
                previousSegmentMap,
                superblockCoefficients,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                ref modeInfo,
                ref block,
                ref paletteInfo);

            if (this.SelectedBlockStatistics.Cost == long.MaxValue)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            context.Snapshot = new Av1EncoderPartitionTree.ModeSnapshot
            {
                ModeInfo = modeInfo,
                Block = block,
                Palette = paletteInfo,
                Statistics = this.SelectedBlockStatistics,
                Displacement = modeInfo.Block.UseIntraBlockCopy
                    ? this.picture.GetDisplacementVector(modeInfoGrid, displacementVectors, modeInfoPosition)
                    : default,
                SecondaryDisplacement = default,
                Ready = false
            };

            if (modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra || modeInfo.Block.UseIntraBlockCopy)
            {
                context.Snapshot.Displacement = this.picture.GetDisplacementVector(modeInfoGrid, displacementVectors, modeInfoPosition);
                context.Snapshot.SecondaryDisplacement = modeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra
                    ? this.picture.GetSecondaryDisplacementVector(modeInfoGrid, referenceContexts, modeInfoPosition)
                    : default;
            }

            this.RetainModeContext(superblockCoefficients, context, lumaArea, chromaArea, this.codedAreaLuma - lumaArea, this.codedAreaChroma - chromaArea);
            if (this.encodedWithoutCoefficients)
            {
                modeInfo.Block.Skip = true;
            }

            // The leaf keeps the transform grid that its own search produced. The shared coefficient buffer belongs to whichever block wrote
            // it last. A grid taken from there can name a transform type that the size of this block does not allow.
            if (modeInfo.Block.ReferenceFrame <= Av1ReferenceFrameType.Intra && !modeInfo.Block.UseIntraBlockCopy &&
                !this.UsesEstimatedInterSearch)
            {
                // Only a luma search that ran for this block left its grid here. A winner that names another size belongs to another block,
                // so this leaf keeps what it kept before.
                Av1EncoderPartitionTree.ModeContext lumaWinner = Av1EncoderBlockWorkspace.GetIntraWinnerContext(workspaceStorage, 1);
                if (lumaWinner.Snapshot.ModeInfo.Block.BlockSize == blockSize)
                {
                    CopyWinnerTransformStates(
                        lumaWinner.GetTransformStates(Av1Plane.Y),
                        context.GetTransformStates(Av1Plane.Y));
                }
            }

            // The encode that follows an intra winner quantizes it again and publishes the coefficient contexts that it codes.
            bool publishSearchCoefficientContexts = publishCoefficientContexts &&
                !(intraEncodeFollows && modeInfo.Block.ReferenceFrame <= Av1ReferenceFrameType.Intra);

            if (publishContexts)
            {
                this.PublishPartitionLeafContexts(
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblockCoefficients,
                    macroBlock,
                    blockOrigin,
                    lumaArea,
                    chromaArea,
                    modeInfo,
                    block,
                    paletteInfo,
                    publishSearchCoefficientContexts);
            }

            return this.SelectedBlockStatistics;
        }

        /// <summary>
        /// Retains transform states and palette maps before the next candidate overwrites shared scratch.
        /// </summary>
        /// <summary>
        /// Copies the palette index map of a retained decision into its own storage, leaving every other
        /// part of that decision as the search left it.
        /// </summary>
        private void RetainPaletteMap(Av1EncoderPartitionTree.ModeContext context)
        {
            Av1EncoderPartitionTree.ModeSnapshot snapshot = context.Snapshot;
            int planeCount = snapshot.Block.HasChroma ? 3 : 1;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                if (plane == Av1Plane.V || snapshot.Palette.PaletteSizes[plane == Av1Plane.Y ? 0 : 1] == 0)
                {
                    continue;
                }

                Av1BlockSize planeSize = plane == Av1Plane.Y
                    ? snapshot.ModeInfo.Block.BlockSize
                    : snapshot.ModeInfo.Block.BlockSize.GetSubsampled(
                        this.source.ChromaSubsamplingX != 0, this.source.ChromaSubsamplingY != 0);

                Av1PlaneType planeType = plane == Av1Plane.Y ? Av1PlaneType.Y : Av1PlaneType.Uv;
                int width = planeSize.GetWidth();
                int height = planeSize.GetHeight();
                this.superblock.Workspace.GetPaletteMaps().GetMap(planeType, width, height).CopyTo(context.GetPaletteIndices(planeType));
            }
        }

        /// <summary>
        /// Copies the transform grid a luma search produced into a retained decision, covering every entry
        /// the later pass over that decision can read.
        /// </summary>
        private static void CopyWinnerTransformStates(
            ReadOnlySpan<Av1EncoderTransformBlockState> source, Span<Av1EncoderTransformBlockState> destination)
            => source[..Math.Min(source.Length, destination.Length)].CopyTo(destination);

        /// <summary>
        /// Clears the retained transform grids of a searched block. Then fills the chroma grids, and the luma grid of an
        /// inter or intra-block-copy block, from the superblock buffer, and copies the palette color maps into the context
        /// that keeps the decisions of the block. The luma grid of an intra block comes from its own search afterwards.
        /// </summary>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="context">The context that keeps the decisions of the block.</param>
        /// <param name="lumaArea">The offset of the luma coefficients of the block in the superblock buffer.</param>
        /// <param name="chromaArea">The offset of the chroma coefficients of the block in the superblock buffer.</param>
        /// <param name="lumaCount">The number of luma coefficient positions that the block covers.</param>
        /// <param name="chromaCount">The number of coefficient positions that the block covers in each chroma plane.</param>
        private void RetainModeContext(
            Span<int> superblockCoefficients,
            Av1EncoderPartitionTree.ModeContext context,
            int lumaArea,
            int chromaArea,
            int lumaCount,
            int chromaCount)
        {
            Av1EncoderPartitionTree.ModeSnapshot snapshot = context.Snapshot;
            int planeCount = snapshot.Block.HasChroma ? 3 : 1;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
                Av1BlockSize planeSize = plane == Av1Plane.Y
                    ? snapshot.ModeInfo.Block.BlockSize
                    : snapshot.ModeInfo.Block.BlockSize.GetSubsampled(this.source.ChromaSubsamplingX != 0, this.source.ChromaSubsamplingY != 0);

                int count = plane == Av1Plane.Y ? lumaCount : chromaCount;
                int area = plane == Av1Plane.Y ? lumaArea : chromaArea;

                // Every entry of the kept grid is written, not only the part that the coded area reached. An entry that another block left
                // can name a transform type that the size of this block does not allow.
                // The luma grid comes from the search of the block itself, never from the shared coefficient buffer, so no entry can belong
                // to another block. Chroma still reads that buffer.
                // The luma search of an intra block gives its own grid afterwards, so that copy lands on this clear. An inter block has no
                // such grid, so its luma still comes from the shared buffer that its own search wrote.
                // The estimated inter-frame search encodes an intra winner straight into the coefficient buffer and gives no grid. Thus its
                // leaf reads that buffer as an inter leaf does.
                bool lumaFromOwnSearch = plane == Av1Plane.Y &&
                    snapshot.ModeInfo.Block.ReferenceFrame <= Av1ReferenceFrameType.Intra &&
                    !snapshot.ModeInfo.Block.UseIntraBlockCopy &&
                    !this.UsesEstimatedInterSearch;

                Span<Av1EncoderTransformBlockState> retainedGrid = context.GetTransformStates(plane);
                retainedGrid.Clear();
                if (!lumaFromOwnSearch)
                {
                    this.coefficientBuffer.GetTransformBlockSpan(superblockCoefficients, plane)
                        .Slice(
                            area / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount,
                            count / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount)
                        .CopyTo(retainedGrid);
                }

                if (plane != Av1Plane.V && snapshot.Palette.PaletteSizes[plane == Av1Plane.Y ? 0 : 1] > 0)
                {
                    Av1PlaneType planeType = plane == Av1Plane.Y ? Av1PlaneType.Y : Av1PlaneType.Uv;
                    int width = planeSize.GetWidth();
                    int height = planeSize.GetHeight();
                    this.superblock.Workspace.GetPaletteMaps().GetMap(planeType, width, height).CopyTo(context.GetPaletteIndices(planeType));
                }
            }
        }

        /// <summary>
        /// Reconstructs a partition leaf from the decisions its context kept, so that later blocks predict from the
        /// winning samples.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="interWorkspace">The inter prediction buffers of the block.</param>
        /// <param name="transformPrediction">The prediction storage of one transform block.</param>
        /// <param name="interIntraAbove">The extended above edge of an inter-intra prediction.</param>
        /// <param name="interIntraLeft">The extended left edge of an inter-intra prediction.</param>
        /// <param name="firstIntermediate">The compound intermediate of the first reference.</param>
        /// <param name="secondIntermediate">The compound intermediate of the second reference.</param>
        /// <param name="compoundMask">The blend mask of a masked compound prediction.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="displacementVectors">The displacement vectors of the picture, one per allocation entry.</param>
        /// <param name="referenceContexts">The motion vector reference contexts of the picture, one per allocation entry.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="context">The context that keeps the decisions of the leaf.</param>
        /// <param name="publishContexts">Whether the leaf writes the neighbor contexts.</param>
        /// <param name="modeSearchReused">Whether the decisions come from an earlier search of the same leaf.</param>
        /// <param name="encodeFollows">Whether an encode of the leaf follows.</param>
        /// <returns>The rate and distortion of the leaf.</returns>
        private Av1RateDistortionStatistics ReconstructPartitionLeaf(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            in Av1EncoderInterPredictionWorkspace<TSample> interWorkspace,
            Span<TSample> transformPrediction,
            Span<TSample> interIntraAbove,
            Span<TSample> interIntraLeft,
            Span<ushort> firstIntermediate,
            Span<ushort> secondIntermediate,
            Span<byte> compoundMask,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<Av1EncoderDisplacementVector> displacementVectors,
            Span<Av1EncoderReferenceContext> referenceContexts,
            ReadOnlySpan<byte> encoderSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1EncoderPartitionTree.ModeContext context,
            bool publishContexts,
            bool modeSearchReused,
            bool encodeFollows)
        {
            Av1EncoderPartitionTree.ModeSnapshot snapshot = context.Snapshot;
            Av1BlockSize blockSize = snapshot.ModeInfo.Block.BlockSize;
            this.SetBlockGeometry(modeInfoGrid, modeInfoAllocation, blockOrigin, blockSize, snapshot.ModeInfo.Block.PartitionType);
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            this.picture.GetMacroBlockModeInfo(modeInfoAllocation, modeInfoPosition) = snapshot.ModeInfo;
            Av1TileWriter.SetModeInfoRowAndColumn(
                macroBlock,
                macroBlock.Tile,
                modeInfoPosition,
                blockSize,
                this.picture.Parent.Common.ModeInfoStride,
                this.picture.Parent.Common.ModeInfoRowCount,
                this.picture.Parent.Common.ModeInfoColumnCount);

            int lumaArea = this.codedAreaLuma;
            int chromaArea = this.codedAreaChroma;
            bool codesNothing = modeSearchReused &&
                snapshot.ModeInfo.Block.ReferenceFrame <= Av1ReferenceFrameType.Intra && !snapshot.ModeInfo.Block.UseIntraBlockCopy;

            // A lossless segment codes 4x4 transforms with one retained state per transform.
            bool snapshotLossless = this.picture.Parent.FrameHeader.LosslessArray[snapshot.ModeInfo.Block.SegmentId];

            if (snapshot.ModeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra || snapshot.ModeInfo.Block.UseIntraBlockCopy)
            {
                Av1TransformSize rootSize = snapshotLossless
                    ? Av1TransformSize.Size4x4
                    : blockSize.GetMaximumTransformSize();

                Av1TransformSize traversalSize = rootSize.GetSubSize().GetSubSize();
                Size lumaExtent = GetCodedTransformExtent(macroBlock, blockSize, Av1TransformSize.Size4x4, 0, 0);
                int leafCount = blockSize.GetWidth() * blockSize.GetHeight() / traversalSize.GetSize2d();
                ReadOnlySpan<Av1EncoderTransformBlockState> retainedLumaStates = context.GetTransformStates(Av1Plane.Y);
                InlineArray128<Av1EncoderTransformBlockState> stateStorage = default;
                Span<Av1EncoderTransformBlockState> states = stateStorage;
                int retainedArea = 0;
                int stateCount = 0;
                for (int leaf = 0; leaf < leafCount; leaf++)
                {
                    Point offset = rootSize.GetBlockPartitionOrigin(blockSize, traversalSize, leaf, 0, 0);
                    Av1TransformSize size = snapshot.ModeInfo.Block.InterTransformSizes[
                        snapshot.ModeInfo.Block.GetInterTransformSizeIndex(offset.Y >> 2, offset.X >> 2)];

                    if (offset.X >= lumaExtent.Width || offset.Y >= lumaExtent.Height ||
                        (offset.X % size.GetWidth()) != 0 || (offset.Y % size.GetHeight()) != 0)
                    {
                        continue;
                    }

                    if (!snapshotLossless)
                    {
                        states[stateCount++] = retainedLumaStates[
                            retainedArea / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];
                    }

                    retainedArea += size.GetSize2d();
                }

                if (snapshot.Block.HasChroma)
                {
                    int subX = this.source.ChromaSubsamplingX;
                    int subY = this.source.ChromaSubsamplingY;
                    Av1TransformSize chromaTransformSize = snapshotLossless
                            ? Av1TransformSize.Size4x4
                            : snapshot.ModeInfo.Block.BlockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

                    Av1BlockSize chromaBlockSize = snapshot.ModeInfo.Block.BlockSize.GetSubsampled(subX != 0, subY != 0);
                    Size chromaExtent = GetCodedTransformExtent(macroBlock, chromaBlockSize, chromaTransformSize, subX, subY);
                    int chromaTransformCount = chromaExtent.Width * chromaExtent.Height / chromaTransformSize.GetSize2d();
                    int chromaStateStride = chromaTransformSize.GetSize2d() / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                    ReadOnlySpan<Av1EncoderTransformBlockState> blueStates = context.GetTransformStates(Av1Plane.U);
                    ReadOnlySpan<Av1EncoderTransformBlockState> redStates = context.GetTransformStates(Av1Plane.V);
                    for (int index = 0; !snapshotLossless && index < chromaTransformCount; index++)
                    {
                        states[64 + index] = blueStates[index * chromaStateStride];
                        states[80 + index] = redStates[index * chromaStateStride];
                    }
                }

                // A block whose every transform lost its coefficients is written as skipped.
                if (!this.ReconstructSelectedInterBlock(
                    writer,
                    in tables,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    in interWorkspace,
                    transformPrediction,
                    interIntraAbove,
                    interIntraLeft,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    displacementVectors,
                    encoderSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    snapshot.ModeInfo,
                    snapshot.Block,
                    snapshot.Displacement,
                    snapshot.SecondaryDisplacement,
                    states))
                {
                    snapshot.ModeInfo.Block.Skip = true;
                    this.picture.GetMacroBlockModeInfo(modeInfoAllocation, modeInfoPosition).Block.Skip = true;
                }

                if (!context.Snapshot.ModeInfo.Block.Skip && !snapshotLossless)
                {
                    int unit = Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                    RetainEncodedZeroBlockTypes(
                        context.GetTransformStates(Av1Plane.Y)[..(retainedArea / unit)],
                        this.coefficientBuffer.GetTransformBlockSpan(superblockCoefficients, Av1Plane.Y).Slice(lumaArea / unit, retainedArea / unit));
                }

                this.picture.SetDisplacementVector(displacementVectors, modeInfoPosition, snapshot.Displacement);
                if (snapshot.ModeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                {
                    this.picture.SetSecondaryDisplacementVector(referenceContexts, modeInfoPosition, snapshot.SecondaryDisplacement);
                }

                this.codedAreaLuma += retainedArea;
                if (snapshot.Block.HasChroma)
                {
                    int subX = this.source.ChromaSubsamplingX;
                    int subY = this.source.ChromaSubsamplingY;
                    Av1BlockSize chromaSize = blockSize.GetSubsampled(subX != 0, subY != 0);
                    Av1TransformSize chromaTransform = blockSize.GetMaxUvTransformSize(subX != 0, subY != 0);
                    Size chromaExtent = GetCodedTransformExtent(macroBlock, chromaSize, chromaTransform, subX, subY);
                    this.codedAreaChroma += chromaExtent.Width * chromaExtent.Height;
                }
            }
            else if (modeSearchReused)
            {
                // A reused decision returns the rate, distortion and cost that it already measured, and codes nothing. The sibling encode
                // that follows this leaf leaves its samples for the next leaf to predict from.
                Size reusedLumaExtent = GetCodedTransformExtent(
                    macroBlock, blockSize, snapshot.ModeInfo.Block.TransformSize, 0, 0);

                this.codedAreaLuma += reusedLumaExtent.Width * reusedLumaExtent.Height;
                if (snapshot.Block.HasChroma)
                {
                    int subX = this.source.ChromaSubsamplingX;
                    int subY = this.source.ChromaSubsamplingY;
                    Av1TransformSize reusedChromaTransform = snapshotLossless
                        ? Av1TransformSize.Size4x4
                        : blockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

                    Size reusedChromaExtent = GetCodedTransformExtent(
                        macroBlock,
                        blockSize.GetSubsampled(subX != 0, subY != 0),
                        reusedChromaTransform,
                        subX,
                        subY);

                    this.codedAreaChroma += reusedChromaExtent.Width * reusedChromaExtent.Height;
                }
            }
            else
            {
                this.ReconstructSelectedIntraBlock(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    encoderSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    context);
            }

            if (publishContexts)
            {
                this.PublishPartitionLeafContexts(
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    superblockCoefficients,
                    macroBlock,
                    blockOrigin,
                    lumaArea,
                    chromaArea,
                    snapshot.ModeInfo,
                    snapshot.Block,
                    snapshot.Palette,
                    !codesNothing && !encodeFollows);
            }

            this.SelectedBlockStatistics = snapshot.Statistics;
            return snapshot.Statistics;
        }

#pragma warning disable CA1517 // False positive: https://github.com/dotnet/sdk/issues/53388
        /// <summary>
        /// Leaves DCT_DCT as the kept type of every luma transform block that an encode of an inter decision quantized to nothing. Thus a
        /// later encode of the same decision transforms it with the default type.
        /// </summary>
        /// <param name="retained">The retained luma transform states, one per coefficient unit.</param>
        /// <param name="encoded">The luma transform states the encode left, one per coefficient unit.</param>
        private static void RetainEncodedZeroBlockTypes(
            Span<Av1EncoderTransformBlockState> retained,
            ReadOnlySpan<Av1EncoderTransformBlockState> encoded)
        {
            for (int index = 0; index < retained.Length; index++)
            {
                if (encoded[index].EndOfBlock == 0)
                {
                    retained[index].TransformType = Av1TransformType.DctDct;
                }
            }
        }
#pragma warning restore CA1517

        /// <summary>
        /// Reconstructs the intra winner that a context kept, luma first and then chroma, into the frame.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="context">The context that keeps the intra winner.</param>
        private void ReconstructSelectedIntraBlock(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<byte> encoderSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1EncoderPartitionTree.ModeContext context)
        {
            Av1EncoderPartitionTree.ModeSnapshot snapshot = context.Snapshot;
            this.SetCodedBlockSegment(encoderSegmentMap, previousSegmentMap, blockOrigin, snapshot.ModeInfo.Block.BlockSize, snapshot.ModeInfo.Block.SegmentId);
            Av1BlockSize blockSize = snapshot.ModeInfo.Block.BlockSize;
            bool usesChromaFromLuma = snapshot.Block.HasChroma && snapshot.ModeInfo.Block.UvMode == Av1ChromaPredictionMode.ChromaFromLuma;
            Span<short> lumaQ3 = modeWorkspace.ChromaFromLumaSamples;
            int planeCount = snapshot.Block.HasChroma ? 3 : 1;
            int chromaArea = 0;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;

                // Every transform block of the plane writes into the same plane coefficients and states.
                Span<int> planeCoefficients = this.coefficientBuffer.GetPlaneSpan(superblockCoefficients, plane);
                Span<Av1EncoderTransformBlockState> planeStates = this.coefficientBuffer.GetTransformBlockSpan(superblockCoefficients, plane);
                int subX = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingX;
                int subY = planeIndex == 0 ? 0 : this.source.ChromaSubsamplingY;
                Size frameContextSize = new(
                    this.picture.Parent.FrameHeader.ModeInfoColumnCount >> subX,
                    this.picture.Parent.FrameHeader.ModeInfoRowCount >> subY);

                Point planeOrigin = planeIndex == 0 ? blockOrigin : Av1TileWriter.GetChromaBlockOrigin(blockOrigin, subX, subY);
                Av1BlockSize planeBlockSize = planeIndex == 0 ? blockSize : blockSize.GetSubsampled(subX != 0, subY != 0);
                int width = planeBlockSize.GetWidth();
                int height = planeBlockSize.GetHeight();
                Av1TransformSize transformSize = planeIndex == 0
                    ? snapshot.ModeInfo.Block.TransformSize
                    : this.BlockLossless
                        ? Av1TransformSize.Size4x4
                        : blockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

                Size codedExtent = GetCodedTransformExtent(macroBlock, planeBlockSize, transformSize, subX, subY);
                int transformWidth = transformSize.GetWidth();
                int transformHeight = transformSize.GetHeight();
                int sampleCount = transformSize.GetSize2d();
                int coefficientOffset = planeIndex == 0 ? this.codedAreaLuma : this.codedAreaChroma;
                Span<Av1EncoderTransformBlockState> states = context.GetTransformStates(plane);
                Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
                Av1PlaneRegion<TSample> destinationPlane = this.reconstruction.GetPlane(plane);
                Span<TSample> reconstructedBlock = Av1TransformBlockEncoder.GetPlaneSpan(
                    SelectPlane(plane, reconstructionLuma, reconstructionBlue, reconstructionRed), destinationPlane, planeOrigin);

                int frameStride = destinationPlane.Stride;

                // Each transform block predicts straight into the frame, where its residual is added in place. A CfL block
                // first computes its DC value in separate storage, outside transient CfL storage, because the zero-mean luma
                // surface survives both chroma planes while the residual and inverse-transform workspaces are reused.
                Span<TSample> chromaFromLumaDc = modeWorkspace.GetCandidateReconstruction(0)[..sampleCount];
                Span<short> residual = modeWorkspace.Residual[..sampleCount];
                Span<TSample> aboveStorage = modeWorkspace.GetReferenceSamples(0);
                Span<TSample> leftStorage = modeWorkspace.GetReferenceSamples(1);
                Av1PlaneType planeType = planeIndex == 0 ? Av1PlaneType.Y : Av1PlaneType.Uv;
                int contextWidth = planeBlockSize.Get4x4WideCount();
                int contextHeight = planeBlockSize.Get4x4HighCount();
                Span<byte> transformContexts = modeWorkspace.TransformContexts;
                Span<byte> topContexts = transformContexts[..contextWidth];
                Span<byte> leftContexts = transformContexts.Slice(contextWidth, contextHeight);
                Av1NeighborEdges<byte> neighbors = plane switch
                {
                    Av1Plane.Y => lumaCoefficientEdges,
                    Av1Plane.U => blueCoefficientEdges,
                    _ => redCoefficientEdges
                };

                neighbors.Top.Slice(neighbors.GetTopIndex(planeOrigin), contextWidth).CopyTo(topContexts);
                neighbors.Left.Slice(neighbors.GetLeftIndex(planeOrigin), contextHeight).CopyTo(leftContexts);
                Av1ComponentType component = planeIndex == 0 ? Av1ComponentType.Luminance : Av1ComponentType.Chroma;
                int paletteSize = snapshot.Palette.PaletteSizes[(int)planeType];
                Av1PlaneRegion<byte> paletteMap = default;
                if (paletteSize > 0)
                {
                    paletteMap = this.superblock.Workspace.GetPaletteMaps().GetMap(planeType, width, height);
                    if (plane != Av1Plane.V)
                    {
                        paletteMap.CopyFrom(context.GetPaletteIndices(planeType));
                    }
                }

                if (plane == Av1Plane.U && usesChromaFromLuma)
                {
                    Av1PlaneRegion<TSample> lumaReconstruction = this.reconstruction.GetPlane(Av1Plane.Y);
                    TOperator.PrepareChromaFromLuma(
                        Av1TransformBlockEncoder.GetPlaneSpan(
                            reconstructionLuma, lumaReconstruction, new Point(planeOrigin.X << subX, planeOrigin.Y << subY)),
                        lumaReconstruction.Stride,
                        lumaQ3,
                        transformSize,
                        this.GetChromaFromLumaExtent(
                            modeInfoAllocation, macroBlock, blockOrigin, blockSize, snapshot.ModeInfo.Block.TransformSize, subX, subY),
                        subX != 0,
                        subY != 0);
                }

                Av1BlockSize maximumUnit = planeIndex == 0
                    ? Av1BlockSize.Block64x64
                    : Av1BlockSize.Block64x64.GetSubsampled(subX != 0, subY != 0);

                int unitWidth = Math.Min(maximumUnit.GetWidth(), codedExtent.Width);
                int unitHeight = Math.Min(maximumUnit.GetHeight(), codedExtent.Height);
                int transformIndex = 0;

                // The coefficient storage of the plane and the edge filter strength of the block serve every
                // transform block, so they are read once.
                Span<Av1EncoderTransformBlockState> outputStates = planeStates;
                Span<int> outputCoefficients = planeCoefficients;
                bool smoothEdges = this.UseSmoothIntraEdges(modeInfoGrid, modeInfoAllocation, macroBlock, blockOrigin, blockSize, plane);
                ReadOnlySpan<TSample> sourceSamples = SelectPlane(plane, sourceLuma, sourceBlue, sourceRed);
                Av1PartitionType partitionType = macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, 0).Block.PartitionType;

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
                                ReadOnlySpan<TSample> sourceTransform = sourceSamples[sourcePlane.GetOffset(transformOrigin.X, transformOrigin.Y)..];
                                Span<TSample> prediction = reconstructedBlock[((y * frameStride) + x)..];
                                if (paletteSize > 0)
                                {
                                    TOperator.PreparePalette(
                                        sourceTransform,
                                        sourcePlane.Stride,
                                        snapshot.Palette.GetColors(plane),
                                        paletteMap.GetSubRegion(x, y, transformWidth, transformHeight),
                                        prediction,
                                        frameStride,
                                        residual,
                                        transformSize);
                                }
                                else
                                {
                                    // The edges are read before the prediction overwrites the frame at this transform block.
                                    this.PrepareTransformReferenceSamples(
                                        reconstructedBlock,
                                        frameStride,
                                        blockOrigin,
                                        blockSize,
                                        macroBlock,
                                        partitionType,
                                        y / transformHeight,
                                        x / transformWidth,
                                        frameStride,
                                        transformSize,
                                        subX,
                                        subY,
                                        reconstructedBlock,
                                        aboveStorage,
                                        leftStorage,
                                        true,
                                        out bool hasLeft,
                                        out bool hasAbove);

                                    ReadOnlySpan<TSample> above = aboveStorage.Slice(1, transformWidth + transformHeight);
                                    ReadOnlySpan<TSample> left = leftStorage.Slice(1, transformWidth + transformHeight);
                                    if (planeIndex > 0 && usesChromaFromLuma)
                                    {
                                        // The CfL predictor computes every sample from the DC value in the first sample.
                                        TOperator.PrepareChromaFromLumaDc(chromaFromLumaDc, above, left, hasLeft, hasAbove, transformSize, this.bitDepth);
                                        prediction[0] = chromaFromLumaDc[0];
                                        int jointSign = snapshot.Block.PredictionUnit.ChromaFromLumaSigns;
                                        int indices = snapshot.Block.PredictionUnit.ChromaFromLumaIndex;
                                        int sign = plane == Av1Plane.U ? Av1ChromaFromLumaMath.SignU(jointSign) : Av1ChromaFromLumaMath.SignV(jointSign);
                                        int magnitude = plane == Av1Plane.U ? Av1ChromaFromLumaMath.IndexU(indices) : Av1ChromaFromLumaMath.IndexV(indices);
                                        int alpha = sign == Av1ChromaFromLumaMath.SignZero
                                            ? 0
                                            : (magnitude + 1) * (sign == Av1ChromaFromLumaMath.SignNegative ? -1 : 1);

                                        TOperator.ApplyChromaFromLuma(lumaQ3, prediction, frameStride, alpha, transformSize, this.bitDepth);
                                        TOperator.SubtractPrediction(
                                            sourceTransform, sourcePlane.Stride, prediction, frameStride, residual, transformWidth, transformHeight);
                                    }
                                    else if (planeIndex == 0 && snapshot.Block.FilterIntraMode != Av1FilterIntraMode.AllFilterIntraModes)
                                    {
                                        TOperator.PrepareFilterIntra(
                                            transformWorkspace,
                                            sourceTransform,
                                            sourcePlane.Stride,
                                            prediction,
                                            frameStride,
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
                                            transformWorkspace,
                                            sourceTransform,
                                            sourcePlane.Stride,
                                            prediction,
                                            frameStride,
                                            above,
                                            left,
                                            hasLeft,
                                            hasAbove,
                                            mode,
                                            snapshot.Block.PredictionUnit.AngleDelta[(int)planeType],
                                            this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                                            smoothEdges,
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
                                    sourceLuma,
                                    sourceBlue,
                                    sourceRed,
                                    reconstructionLuma,
                                    reconstructionBlue,
                                    reconstructionRed,
                                    writer,
                                    in tables,
                                    transformCoefficients,
                                    dequantizedCoefficients,
                                    transformWorkspace,
                                    planeCoefficients,
                                    planeStates,
                                    blockContext,
                                    false,
                                    blockOrigin,
                                    blockSize,
                                    transformOrigin,
                                    plane,
                                    transformSize,
                                    prediction,
                                    frameStride,
                                    residual,
                                    transformSize.GetWidth(),
                                    states[stateIndex],
                                    snapshot.ModeInfo.Block.Skip,
                                    coefficientOffset + (transformIndex * sampleCount));

                                int outputOffset = coefficientOffset + (transformIndex * sampleCount);
                                Av1EncoderTransformBlockState outputState =
                                    outputStates[outputOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];

                                // The intra block encode returns a luma transform block that quantized to nothing to DCT_DCT. Thus a later pass
                                // over the same block transforms it with the default type, not with the type that the search picked.
                                if (plane == Av1Plane.Y && outputState.EndOfBlock == 0 && !this.keepSearchedZeroBlockTypes)
                                {
                                    states[stateIndex].TransformType = Av1TransformType.DctDct;
                                }

                                byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                                    outputCoefficients.Slice(outputOffset, sampleCount),
                                    transformSize,
                                    outputState.TransformType,
                                    outputState.EndOfBlock);

                                Av1TileWriter.UpdateCoefficientContexts(
                                    transformTop,
                                    transformLeft,
                                    coefficientContext,
                                    transformOrigin,
                                    frameContextSize);
                            }
                        }
                    }
                }

                if (planeIndex != 0)
                {
                    chromaArea = codedExtent.Width * codedExtent.Height;
                }
            }

            Size lumaExtent = GetCodedTransformExtent(macroBlock, blockSize, snapshot.ModeInfo.Block.TransformSize, 0, 0);
            this.codedAreaLuma += lumaExtent.Width * lumaExtent.Height;
            this.codedAreaChroma += chromaArea;
        }

        /// <summary>
        /// Resets the mode information of a block to its size and partition, and maps every 4x4 unit of the block to it.
        /// </summary>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="partitionType">The partition type that contains the block.</param>
        private void SetBlockGeometry(
            Span<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType)
        {
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            ref Av1MacroBlockModeInfo modeInfo = ref this.picture.GetMacroBlockModeInfo(modeInfoAllocation, modeInfoPosition);
            modeInfo.Block = new Av1EncoderBlockModeInfo
            {
                BlockSize = blockSize,
                PartitionType = partitionType
            };

            this.picture.MapModeInfoBlock(modeInfoGrid, modeInfoPosition, blockSize);
        }

        /// <summary>
        /// Publishes the transform size, coefficient and palette contexts of one selected partition leaf on the tile
        /// edges, so that the next block of the partition search reads them.
        /// </summary>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="lumaArea">The offset of the luma coefficients of the leaf in the superblock buffer.</param>
        /// <param name="chromaArea">The offset of the chroma coefficients of the leaf in the superblock buffer.</param>
        /// <param name="modeInfo">The selected modes of the leaf.</param>
        /// <param name="block">The selected block state of the leaf.</param>
        /// <param name="paletteInfo">The selected palette of the leaf.</param>
        /// <param name="publishCoefficientContexts">Whether the coefficient contexts of the leaf are published.</param>
        private void PublishPartitionLeafContexts(
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Span<int> superblockCoefficients,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            int lumaArea,
            int chromaArea,
            Av1MacroBlockModeInfo modeInfo,
            Av1EncoderBlockStruct block,
            Av1EncoderPaletteInfo paletteInfo,
            bool publishCoefficientContexts)
        {
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            Av1TransformSize transformSize = modeInfo.Block.TransformSize;
            Size blockDimensions = new(blockSize.GetWidth(), blockSize.GetHeight());
            Span<Av1EncoderTransformBlockState> lumaStates = this.coefficientBuffer.GetTransformBlockSpan(superblockCoefficients, Av1Plane.Y);

            Span<int> lumaCoefficients = this.coefficientBuffer.GetPlaneSpan(superblockCoefficients, Av1Plane.Y);
            Size frameContextSize = new(this.picture.Parent.FrameHeader.ModeInfoColumnCount, this.picture.Parent.FrameHeader.ModeInfoRowCount);
            bool interTransform = modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra || modeInfo.Block.UseIntraBlockCopy;

            // A lossless segment codes 4x4 transforms with one retained state per transform.
            bool leafLossless = this.picture.Parent.FrameHeader.LosslessArray[modeInfo.Block.SegmentId];
            if (interTransform && !leafLossless)
            {
                // The kept leaves publish their contexts in coefficient order. A root-sized write erases the smaller bottom and right edge
                // contexts that the next partition candidate needs.
                Av1TransformSize rootSize = blockSize.GetMaximumTransformSize();
                Av1TransformSize traversalSize = rootSize.GetSubSize().GetSubSize();
                Size extent = GetCodedTransformExtent(macroBlock, blockSize, Av1TransformSize.Size4x4, 0, 0);
                int leafCount = blockSize.GetWidth() * blockSize.GetHeight() / traversalSize.GetSize2d();
                int coefficientOffset = lumaArea;
                for (int leaf = 0; leaf < leafCount; leaf++)
                {
                    Point offset = rootSize.GetBlockPartitionOrigin(blockSize, traversalSize, leaf, 0, 0);
                    if (offset.X >= extent.Width || offset.Y >= extent.Height)
                    {
                        continue;
                    }

                    Av1TransformSize leafSize = modeInfo.Block.InterTransformSizes[
                        modeInfo.Block.GetInterTransformSizeIndex(offset.Y >> 2, offset.X >> 2)];

                    int width = leafSize.GetWidth();
                    int height = leafSize.GetHeight();
                    if ((offset.X % width) != 0 || (offset.Y % height) != 0)
                    {
                        continue;
                    }

                    Point origin = blockOrigin + new Size(offset.X, offset.Y);
                    Size leafDimensions = new(width, height);
                    transformEdges.Write((byte)width, origin, leafDimensions, Av1NeighborArrayUnit<byte>.UnitMask.Top);
                    transformEdges.Write((byte)height, origin, leafDimensions, Av1NeighborArrayUnit<byte>.UnitMask.Left);

                    if (publishCoefficientContexts)
                    {
                        byte context = lumaStates[
                            coefficientOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount]
                            .CoefficientContext;

                        Av1TileWriter.UpdateCoefficientContexts(
                            lumaCoefficientEdges.Top.Slice(lumaCoefficientEdges.GetTopIndex(origin), leafSize.Get4x4WideCount()),
                            lumaCoefficientEdges.Left.Slice(lumaCoefficientEdges.GetLeftIndex(origin), leafSize.Get4x4HighCount()),
                            context,
                            origin,
                            frameContextSize);
                    }

                    coefficientOffset += leafSize.GetSize2d();
                }
            }
            else
            {
                transformEdges.Write((byte)transformSize.GetWidth(), blockOrigin, blockDimensions, Av1NeighborArrayUnit<byte>.UnitMask.Top);
                transformEdges.Write((byte)transformSize.GetHeight(), blockOrigin, blockDimensions, Av1NeighborArrayUnit<byte>.UnitMask.Left);
            }

            // A reused decision codes nothing here, so its coefficients are not in the shared buffer yet. The
            // encode that follows it publishes their contexts.
            if (publishCoefficientContexts && (!interTransform || leafLossless))
            {
                PublishCoefficientContexts(
                    in lumaCoefficientEdges,
                    blockOrigin,
                    GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0),
                    transformSize,
                    Av1BlockSize.Block64x64,
                    transformSize,
                    frameContextSize,
                    lumaCoefficients[lumaArea..],
                    lumaStates[(lumaArea / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount)..]);
            }

            if (this.picture.Parent.FrameHeader.AllowScreenContentTools)
            {
                const Av1NeighborArrayUnit<Av1EncoderPaletteInfo>.UnitMask PaletteContextMask =
                    Av1NeighborArrayUnit<Av1EncoderPaletteInfo>.UnitMask.Top |
                    Av1NeighborArrayUnit<Av1EncoderPaletteInfo>.UnitMask.Left;

                paletteEdges.Write(paletteInfo, blockOrigin, blockDimensions, PaletteContextMask);
            }

            if (!block.HasChroma || !publishCoefficientContexts)
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
            Av1TransformSize chromaTransformSize = leafLossless
                ? Av1TransformSize.Size4x4
                : blockSize.GetMaxUvTransformSize(colorConfig.SubSamplingX, colorConfig.SubSamplingY);

            Av1BlockSize maximumChromaUnitBlockSize =
                Av1BlockSize.Block64x64.GetSubsampled(colorConfig.SubSamplingX, colorConfig.SubSamplingY);

            int chromaStateIndex =
                chromaArea / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            Span<Av1EncoderTransformBlockState> blueStates = this.coefficientBuffer.GetTransformBlockSpan(superblockCoefficients, Av1Plane.U);

            Span<Av1EncoderTransformBlockState> redStates = this.coefficientBuffer.GetTransformBlockSpan(superblockCoefficients, Av1Plane.V);

            Span<int> blueCoefficients = this.coefficientBuffer.GetPlaneSpan(superblockCoefficients, Av1Plane.U);
            Span<int> redCoefficients = this.coefficientBuffer.GetPlaneSpan(superblockCoefficients, Av1Plane.V);
            PublishCoefficientContexts(
                in blueCoefficientEdges,
                chromaOrigin,
                GetCodedTransformExtent(macroBlock, chromaBlockSize, chromaTransformSize, subsamplingX, subsamplingY),
                chromaTransformSize,
                maximumChromaUnitBlockSize,
                chromaTransformSize,
                new Size(
                    this.picture.Parent.FrameHeader.ModeInfoColumnCount >> subsamplingX,
                    this.picture.Parent.FrameHeader.ModeInfoRowCount >> subsamplingY),
                blueCoefficients[chromaArea..],
                blueStates[chromaStateIndex..]);

            PublishCoefficientContexts(
                in redCoefficientEdges,
                chromaOrigin,
                GetCodedTransformExtent(macroBlock, chromaBlockSize, chromaTransformSize, subsamplingX, subsamplingY),
                chromaTransformSize,
                maximumChromaUnitBlockSize,
                chromaTransformSize,
                new Size(
                    this.picture.Parent.FrameHeader.ModeInfoColumnCount >> subsamplingX,
                    this.picture.Parent.FrameHeader.ModeInfoRowCount >> subsamplingY),
                redCoefficients[chromaArea..],
                redStates[chromaStateIndex..]);
        }

        /// <summary>
        /// Publishes the coefficient context of each coded transform block of one plane on the tile edges, in the
        /// 64x64 region order that the coefficients were stored in.
        /// </summary>
        /// <param name="neighbors">The coefficient context edges of the plane, read once by the caller.</param>
        /// <param name="blockOrigin">The block origin in samples of the plane.</param>
        /// <param name="codedExtent">The visible coded extent of the block in samples of the plane.</param>
        /// <param name="transformSize">The size of each transform block.</param>
        /// <param name="maximumUnitBlockSize">The largest region that the coefficients are grouped in.</param>
        /// <param name="rootTransformSize">The size of each transform root.</param>
        /// <param name="frameContextSize">The coded plane extent in four-sample units.</param>
        /// <param name="coefficients">The coefficients of the block.</param>
        /// <param name="states">The transform block states of the block, which hold each stored context.</param>
        private static void PublishCoefficientContexts(
            in Av1NeighborEdges<byte> neighbors,
            Point blockOrigin,
            Size codedExtent,
            Av1TransformSize transformSize,
            Av1BlockSize maximumUnitBlockSize,
            Av1TransformSize rootTransformSize,
            Size frameContextSize,
            ReadOnlySpan<int> coefficients,
            ReadOnlySpan<Av1EncoderTransformBlockState> states)
        {
            int blockWidth = codedExtent.Width;
            int blockHeight = codedExtent.Height;
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
                    for (int rootRow = regionRow; rootRow < unitBottom; rootRow += rootTransformSize.GetHeight())
                    {
                        for (int rootColumn = regionColumn; rootColumn < unitRight; rootColumn += rootTransformSize.GetWidth())
                        {
                            int leafCount = rootTransformSize.GetSize2d() / transformSampleCount;
                            for (int leaf = 0; leaf < leafCount; leaf++)
                            {
                                Point offset = rootTransformSize.GetPartitionOrigin(transformSize, leaf);
                                int column = rootColumn + offset.X;
                                int row = rootRow + offset.Y;
                                if (column >= unitRight || row >= unitBottom)
                                {
                                    continue;
                                }

                                // The context a transform block hands on is the one stored beside it when it
                                // was coded, not a fresh reading of whatever coefficients the shared buffer
                                // now holds. A search that ended on a losing candidate leaves those behind.
                                byte context = states[transformStateOffset].CoefficientContext;

                                Point transformOrigin = blockOrigin + new Size(column, row);
                                Av1TileWriter.UpdateCoefficientContexts(
                                    neighbors.Top.Slice(neighbors.GetTopIndex(transformOrigin), transformSize.Get4x4WideCount()),
                                    neighbors.Left.Slice(neighbors.GetLeftIndex(transformOrigin), transformSize.Get4x4HighCount()),
                                    context,
                                    transformOrigin,
                                    frameContextSize);

                                coefficientOffset += transformSampleCount;
                                transformStateOffset += transformStateStride;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Saves the tile context edges that a partition trial of one block can change, so that a later trial starts
        /// from the same edges.
        /// </summary>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size of the partition node.</param>
        private void SavePartitionTrialContexts(
            Span<int> workspaceStorage,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Point blockOrigin,
            Av1BlockSize blockSize)
        {
            Span<byte> storage = Av1EncoderBlockWorkspace.GetPartitionContextStorage(workspaceStorage, blockSize);
            int offset = 0;
            SaveNeighborEdges(in partitionEdges, blockOrigin, blockSize.Get4x4WideCount(), blockSize.Get4x4HighCount(), storage, ref offset);
            SaveNeighborEdges(in lumaCoefficientEdges, blockOrigin, blockSize.Get4x4WideCount(), blockSize.Get4x4HighCount(), storage, ref offset);
            SaveNeighborEdges(in transformEdges, blockOrigin, blockSize.Get4x4WideCount(), blockSize.Get4x4HighCount(), storage, ref offset);
            if (this.picture.Parent.FrameHeader.AllowScreenContentTools)
            {
                SaveNeighborEdges(in paletteEdges, blockOrigin, blockSize.Get4x4WideCount(), blockSize.Get4x4HighCount(), storage, ref offset);
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

            int chromaWidth = chromaBlockSize.Get4x4WideCount();
            int chromaHeight = chromaBlockSize.Get4x4HighCount();
            SaveNeighborEdges(in blueCoefficientEdges, chromaOrigin, chromaWidth, chromaHeight, storage, ref offset);
            SaveNeighborEdges(in redCoefficientEdges, chromaOrigin, chromaWidth, chromaHeight, storage, ref offset);
        }

        /// <summary>
        /// Restores the tile context edges that <see cref="SavePartitionTrialContexts"/> saved before a partition
        /// trial of one block.
        /// </summary>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="partitionEdges">The partition context edges of the tile.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size of the partition node.</param>
        private void RestorePartitionTrialContexts(
            Span<int> workspaceStorage,
            in Av1NeighborEdges<Av1PartitionContext> partitionEdges,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            Point blockOrigin,
            Av1BlockSize blockSize)
        {
            ReadOnlySpan<byte> storage = Av1EncoderBlockWorkspace.GetPartitionContextStorage(workspaceStorage, blockSize);
            int offset = 0;
            RestoreNeighborEdges(in partitionEdges, blockOrigin, blockSize.Get4x4WideCount(), blockSize.Get4x4HighCount(), storage, ref offset);
            RestoreNeighborEdges(in lumaCoefficientEdges, blockOrigin, blockSize.Get4x4WideCount(), blockSize.Get4x4HighCount(), storage, ref offset);
            RestoreNeighborEdges(in transformEdges, blockOrigin, blockSize.Get4x4WideCount(), blockSize.Get4x4HighCount(), storage, ref offset);
            if (this.picture.Parent.FrameHeader.AllowScreenContentTools)
            {
                RestoreNeighborEdges(in paletteEdges, blockOrigin, blockSize.Get4x4WideCount(), blockSize.Get4x4HighCount(), storage, ref offset);
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

            int chromaWidth = chromaBlockSize.Get4x4WideCount();
            int chromaHeight = chromaBlockSize.Get4x4HighCount();
            RestoreNeighborEdges(in blueCoefficientEdges, chromaOrigin, chromaWidth, chromaHeight, storage, ref offset);
            RestoreNeighborEdges(in redCoefficientEdges, chromaOrigin, chromaWidth, chromaHeight, storage, ref offset);
        }

        /// <summary>
        /// Copies the part of the top and left edges that a block covers into the trial storage.
        /// </summary>
        /// <typeparam name="T">The context value type.</typeparam>
        /// <param name="neighbors">The edges of one neighbor array.</param>
        /// <param name="blockOrigin">The block origin in samples.</param>
        /// <param name="width">The number of top units that the block covers.</param>
        /// <param name="height">The number of left units that the block covers.</param>
        /// <param name="storage">The trial storage.</param>
        /// <param name="offset">The byte position in the storage, advanced past the copied edges.</param>
        private static void SaveNeighborEdges<T>(
            in Av1NeighborEdges<T> neighbors,
            Point blockOrigin,
            int width,
            int height,
            Span<byte> storage,
            ref int offset)
            where T : struct
        {
            Span<byte> top = MemoryMarshal.AsBytes(neighbors.Top.Slice(neighbors.GetTopIndex(blockOrigin), width));
            top.CopyTo(storage[offset..]);
            offset += top.Length;

            Span<byte> left = MemoryMarshal.AsBytes(neighbors.Left.Slice(neighbors.GetLeftIndex(blockOrigin), height));
            left.CopyTo(storage[offset..]);
            offset += left.Length;
        }

        /// <summary>
        /// Copies the saved part of the top and left edges of a block back from the trial storage.
        /// </summary>
        /// <typeparam name="T">The context value type.</typeparam>
        /// <param name="neighbors">The edges of one neighbor array.</param>
        /// <param name="blockOrigin">The block origin in samples.</param>
        /// <param name="width">The number of top units that the block covers.</param>
        /// <param name="height">The number of left units that the block covers.</param>
        /// <param name="storage">The trial storage.</param>
        /// <param name="offset">The byte position in the storage, advanced past the copied edges.</param>
        private static void RestoreNeighborEdges<T>(
            in Av1NeighborEdges<T> neighbors,
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

        /// <summary>
        /// Adds the rate of a coded skip flag to the rate of the selected intra modes.
        /// </summary>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="modeStatistics">The rate and distortion of the selected modes.</param>
        /// <returns>The rate and distortion of the block.</returns>
        private Av1RateDistortionStatistics GetRegularBlockCost(
            in Av1CoefficientTables tables,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Av1RateDistortionStatistics modeStatistics)
        {
            int skipContext = Av1TileWriter.GetSkipContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            int rateAdjustment = Av1SymbolEncoder.GetSkipCost(tables.ModeCosts, false, skipContext);

            return new(this.rateMultiplier, modeStatistics.Rate + rateAdjustment, modeStatistics.Distortion);
        }

        /// <summary>
        /// Inserts a completed candidate into the ordered winner list and retains its palette indices.
        /// </summary>
        /// <param name="modeWorkspace">The mode decision buffers that hold the retained palette maps.</param>
        /// <param name="candidate">The candidate syntax and completed search cost.</param>
        /// <param name="blockSize">The coding block dimensions.</param>
        private void RetainLumaCandidate(in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace, LumaCandidate candidate, Av1BlockSize blockSize)
        {
            if (candidate.Cost == long.MaxValue)
            {
                return;
            }

            int limit = this.picture.Parent.SpeedSettings.IntraWinnerCount;
            int sampleCount = blockSize.GetWidth() * blockSize.GetHeight();

            int position = this.lumaCandidateCount;
            while (position > 0 && candidate.Cost < this.lumaCandidates[position - 1].Cost)
            {
                position--;
            }

            if (position >= limit)
            {
                return;
            }

            // Metadata and color indices move together. The map storage is outside prediction scratch,
            // so palette clustering and later filter prediction cannot overwrite a retained candidate.
            int last = Math.Min(this.lumaCandidateCount, limit - 1);
            for (int index = last; index > position; index--)
            {
                this.lumaCandidates[index] = this.lumaCandidates[index - 1];
                if (this.lumaCandidates[index].Palette.PaletteSizes[0] > 0)
                {
                    modeWorkspace.GetWinnerPaletteMap(index - 1)[..sampleCount].CopyTo(modeWorkspace.GetWinnerPaletteMap(index));
                }
            }

            this.lumaCandidates[position] = candidate;
            this.lumaCandidateCount = Math.Min(this.lumaCandidateCount + 1, limit);
            if (candidate.Palette.PaletteSizes[0] > 0)
            {
                this.superblock.Workspace.GetPaletteMaps()
                    .GetMap(Av1PlaneType.Y, blockSize.GetWidth(), blockSize.GetHeight())
                    .CopyTo(modeWorkspace.GetWinnerPaletteMap(position));
            }
        }

        /// <summary>
        /// Searches the luma predictions, gives the winner's transform states back to the caller and, in an intra
        /// frame, refines the winner.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="blueCoefficientEdges">The blue-difference coefficient context edges of the tile.</param>
        /// <param name="redCoefficientEdges">The red-difference coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="previousSegmentMap">The segment map of the primary reference frame, or an empty map.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="retainedStates">The transform states of the luma winner.</param>
        /// <param name="interCostLimit">The cost of the inter winner, which bounds the intra search.</param>
        /// <param name="paletteInfo">The palette of the block.</param>
        /// <param name="selectedAngleDelta">The angle delta of the luma winner.</param>
        /// <param name="selectedFilterIntraMode">The filter intra mode of the luma winner.</param>
        /// <param name="selectedTransformSize">The transform size of the luma winner.</param>
        /// <param name="selectedStatistics">The rate and distortion of the luma winner.</param>
        /// <returns>The luma mode of the winner.</returns>
        private Av1PredictionMode SelectLumaMode(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            in Av1NeighborEdges<byte> blueCoefficientEdges,
            in Av1NeighborEdges<byte> redCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            ReadOnlySpan<byte> encoderSegmentMap,
            ReadOnlySpan<byte> previousSegmentMap,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Span<Av1EncoderTransformBlockState> retainedStates,
            long interCostLimit,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out Av1FilterIntraMode selectedFilterIntraMode,
            out Av1TransformSize selectedTransformSize,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            this.lumaCandidateCount = 0;
            this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Candidate;
            Av1PredictionMode mode = this.SelectLumaPrediction(
                writer,
                in tables,
                in modeWorkspace,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                transformTypeProbabilities,
                in transformEdges,
                in paletteEdges,
                in lumaCoefficientEdges,
                modeInfoGrid,
                modeInfoAllocation,
                superblockCoefficients,
                workspaceStorage,
                sourceLuma,
                sourceBlue,
                sourceRed,
                reconstructionLuma,
                reconstructionBlue,
                reconstructionRed,
                macroBlock,
                blockOrigin,
                blockSize,
                retainedStates,
                interCostLimit,
                ref paletteInfo,
                out selectedAngleDelta,
                out selectedFilterIntraMode,
                out selectedTransformSize,
                out selectedStatistics);

            if (!this.picture.Parent.FrameHeader.IsIntra && selectedStatistics.Cost != long.MaxValue)
            {
                // Every mode writes its best depth into the shared states, so they end with the last mode tried. The grid of the winner was
                // kept when it won. This copy gives it back, so the caller keeps the types of the winner.
                CopyWinnerTransformStates(Av1EncoderBlockWorkspace.GetIntraWinnerContext(workspaceStorage, 1).GetTransformStates(Av1Plane.Y), retainedStates);
            }

            if (selectedStatistics.Cost == long.MaxValue)
            {
                return mode;
            }

            // An inter frame keeps the luma winner of the mode search. Only an intra frame refines it.
            Av1PredictionMode refinedMode = !this.picture.Parent.FrameHeader.IsIntra
                ? mode
                : this.RefineLumaMode(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in transformEdges,
                    in paletteEdges,
                    in lumaCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    workspaceStorage,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    retainedStates,
                    mode,
                    ref paletteInfo,
                    ref selectedAngleDelta,
                    ref selectedFilterIntraMode,
                    ref selectedTransformSize,
                    ref selectedStatistics);

            // The chroma search encodes the luma plane again before it starts. Thus chroma predicts from the refined winner, not from the
            // candidate that the first search left, and every luma transform block that quantized to nothing returns to DCT_DCT. This
            // applies only to a block that can still choose chroma-from-luma, because only that mode reads the luma reconstruction.
            Point chromaReferencePosition = new(
                blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);

            bool storeLumaForChromaFromLuma = !this.source.IsMonochrome &&
                Av1TileReader.HasChroma(this.picture.Sequence.SequenceHeader, chromaReferencePosition, blockSize) &&
                blockSize.AllowsChromaFromLuma(
                    this.picture.Parent.FrameHeader.LosslessArray[0],
                    this.source.ChromaSubsamplingX != 0,
                    this.source.ChromaSubsamplingY != 0);

            if (selectedStatistics.Cost != long.MaxValue && storeLumaForChromaFromLuma)
            {
                Av1EncoderPartitionTree.ModeContext refinedWinner = Av1EncoderBlockWorkspace.GetIntraWinnerContext(workspaceStorage, 1);
                Av1MacroBlockModeInfo refinedModeInfo = macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, 0);
                refinedModeInfo.Block.Mode = refinedMode;
                refinedModeInfo.Block.TransformSize = selectedTransformSize;
                Av1EncoderBlockStruct refinedBlock = new() { FilterIntraMode = selectedFilterIntraMode };
                refinedBlock.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = (sbyte)selectedAngleDelta;
                refinedWinner.Snapshot = new Av1EncoderPartitionTree.ModeSnapshot
                {
                    ModeInfo = refinedModeInfo,
                    Block = refinedBlock,
                    Palette = paletteInfo,
                    Statistics = selectedStatistics
                };

                // The kept storage is laid out from the size that the snapshot records. Thus the snapshot names the searched block before
                // anything reads or writes its transform grid.
                refinedWinner.Snapshot.ModeInfo.Block.BlockSize = blockSize;

                // The winner context already carries the transform grid that its own search chose, so only the palette map moves here. A
                // second read of the shared coefficient buffer gives the block a grid that belongs to the last candidate tried.
                this.RetainPaletteMap(refinedWinner);
                int retainedLumaArea = this.codedAreaLuma;
                this.ReconstructSelectedIntraBlock(
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    in lumaCoefficientEdges,
                    in blueCoefficientEdges,
                    in redCoefficientEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    encoderSegmentMap,
                    previousSegmentMap,
                    superblockCoefficients,
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    macroBlock,
                    blockOrigin,
                    refinedWinner);

                this.codedAreaLuma = retainedLumaArea;
            }

            // The frame holds the luma that the search left. That is the winner when chroma-from-luma coded it again, and otherwise the last
            // luma trial. The chroma search writes only the chroma planes, so this measure is the same as one after the chroma search.
            Av1PlaneRegion<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> lumaFrameBlock = Av1TransformBlockEncoder.GetPlaneSpan(reconstructionLuma, lumaFrame, blockOrigin);
            if (selectedStatistics.Cost != long.MaxValue && this.ChargesSmoothing)
            {
                this.intraSmoothingOffset = this.GetIntraSmoothingOffset(
                    sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize, lumaFrameBlock, lumaFrame.Stride);
            }
            else if (selectedStatistics.Cost != long.MaxValue && this.ChargesHighBitDepthTextureLoss)
            {
                // At a high bit depth, the offset uses the mode of the luma winner. An intra mode never skips here.
                this.GetVarianceStatistics(
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    blockOrigin,
                    blockSize,
                    lumaFrameBlock,
                    lumaFrame.Stride,
                    out long sourceVariance,
                    out long sampleVariance);

                bool smoothMode = IsSmoothTextureMode(refinedMode);
                this.intraSmoothingOffset = GetTextureLossOffset(sourceVariance, sampleVariance, blockSize, false, smoothMode, false, default);
            }

            return refinedMode;
        }

        /// <summary>
        /// Repeats the transform search of every retained luma candidate from the same block edges and its own palette
        /// map, and keeps the candidate with the lowest cost.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="retainedStates">The transform states of the luma winner.</param>
        /// <param name="mode">The luma mode of the search winner.</param>
        /// <param name="paletteInfo">The palette of the block.</param>
        /// <param name="selectedAngleDelta">The angle delta of the luma winner.</param>
        /// <param name="selectedFilterIntraMode">The filter intra mode of the luma winner.</param>
        /// <param name="selectedTransformSize">The transform size of the luma winner.</param>
        /// <param name="selectedStatistics">The rate and distortion of the luma winner.</param>
        /// <returns>The luma mode of the winner.</returns>
        private Av1PredictionMode RefineLumaMode(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Span<Av1EncoderTransformBlockState> retainedStates,
            Av1PredictionMode mode,
            ref Av1EncoderPaletteInfo paletteInfo,
            ref int selectedAngleDelta,
            ref Av1FilterIntraMode selectedFilterIntraMode,
            ref Av1TransformSize selectedTransformSize,
            ref Av1RateDistortionStatistics selectedStatistics)
        {
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            bool refine = !this.BlockLossless &&
                (settings.IntraTransformTypeSearchLevel != 0 || settings.EnableWinnerCoefficientOptimization || settings.DeferTransformSizeSearch);

            if (refine && settings.PruneIntraWinnerByVariance)
            {
                int varianceThreshold = 64 - (48 * this.blockQIndex / 256);
                refine = this.GetSourceVariance(
                    sourceLuma, sourceBlue, sourceRed, modeWorkspace.GetCandidateReconstruction(0), blockOrigin, blockSize) >= varianceThreshold;
            }

            int selectedMapIndex = paletteInfo.PaletteSizes[0] > 0 ? 0 : -1;
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            Av1PlaneRegion<byte> map = default;
            bool paletteAllowed = Av1TileWriter.IsPaletteAllowed(this.picture.Parent.FrameHeader.AllowScreenContentTools, blockSize);
            if (paletteAllowed)
            {
                map = this.superblock.Workspace.GetPaletteMaps().GetMap(Av1PlaneType.Y, width, height);
            }

            if (refine)
            {
                this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Winner;
                Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
                Av1PlaneRegion<TSample> reconstructionPlane = this.reconstruction.GetPlane(Av1Plane.Y);
                int sizeContext = Av1TileWriter.GetTransformSizeContext(
                    in transformEdges,
                    modeInfoGrid,
                    modeInfoAllocation,
                    macroBlock,
                    blockOrigin,
                    blockSize);

                int paletteDisabledCost = paletteAllowed
                    ? Av1SymbolEncoder.GetPaletteYModeCost(
                        tables.ModeCosts,
                        false,
                        Av1TileWriter.GetPaletteBlockSizeContext(blockSize),
                        Av1TileWriter.GetPaletteYModeContext(in paletteEdges, macroBlock, blockOrigin))
                    : 0;

                int maximumDepth = this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select
                    ? width == height ? settings.IntraSquareTransformSearchDepth : settings.IntraRectangularTransformSearchDepth
                    : 0;

                int sourceVariance = maximumDepth > 1 || settings.PruneIntraTransformDepth
                    ? this.GetSourceVariance(sourceLuma, sourceBlue, sourceRed, modeWorkspace.GetCandidateReconstruction(0), blockOrigin, blockSize)
                    : 0;

                // Every retained predictor of the block shares these neighbor values, so they are read once.
                LumaBlockState blockState = this.GetLumaBlockState(in tables, modeInfoGrid, modeInfoAllocation, macroBlock, blockOrigin, blockSize);

                // The winner keeps its transform grid in the retained winner context, which is read once.
                Span<Av1EncoderTransformBlockState> winnerStates =
                    Av1EncoderBlockWorkspace.GetIntraWinnerContext(workspaceStorage, 1).GetTransformStates(Av1Plane.Y);

                // Repeat transform search for every retained predictor with winner-stage settings.
                // Each trial starts from the same external block edges and its own palette map.
                for (int index = 0; index < this.lumaCandidateCount; index++)
                {
                    LumaCandidate candidate = this.lumaCandidates[index];
                    int paletteSize = candidate.Palette.PaletteSizes[0];
                    if (paletteSize > 0)
                    {
                        map.CopyFrom(modeWorkspace.GetWinnerPaletteMap(index));
                    }

                    // With the breakout, the best candidate so far bounds the depths. Without it, the winner stage searches every depth in full.
                    long candidateLimit = selectedStatistics.Cost;
                    Av1RateDistortionStatistics statistics = this.ChooseUniformTransformSize(
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        reconstructionLuma,
                        reconstructionBlue,
                        reconstructionRed,
                        writer,
                        in tables,
                        in modeWorkspace,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        in lumaCoefficientEdges,
                        macroBlock,
                        sourcePlane,
                        reconstructionPlane,
                        blockOrigin,
                        blockSize,
                        in blockState,
                        blockSize.GetMaximumTransformSize(),
                        maximumDepth,
                        sourceVariance,
                        candidate.Mode,
                        candidate.AngleDelta,
                        candidate.FilterMode,
                        paletteSize,
                        candidate.Palette.GetColors(Av1Plane.Y),
                        candidate.PaletteHeaderRate,
                        paletteDisabledCost,
                        sizeContext,
                        settings.UseIntraTransformRdBreakout ? candidateLimit : long.MaxValue,
                        candidateLimit,
                        modeWorkspace.CandidateTransformBlocks,
                        retainedStates,
                        out Av1TransformSize size);

                    if (statistics.Cost < candidateLimit)
                    {
                        mode = candidate.Mode;
                        selectedAngleDelta = candidate.AngleDelta;
                        selectedFilterIntraMode = candidate.FilterMode;

                        // The winner keeps the chroma palette that its chroma search chose, because an inter frame searches chroma before
                        // this refinement. Only the luma palette changes.
                        paletteInfo.PaletteSizes[0] = (byte)paletteSize;
                        paletteInfo.SetColors(Av1Plane.Y, candidate.Palette.GetColors(Av1Plane.Y));
                        selectedTransformSize = size;
                        selectedStatistics = statistics;
                        selectedMapIndex = paletteSize > 0 ? index : -1;

                        // The winner keeps the transform grid that this candidate produced. Thus a later candidate that writes over the shared
                        // buffers cannot give it a type that its own size does not allow.
                        CopyWinnerTransformStates(retainedStates, winnerStates);
                    }
                }
            }

            this.blockWorkspace.EvaluationStage = this.picture.Parent.FrameHeader.IsIntra
                ? Av1EncoderEvaluationStage.Default
                : Av1EncoderEvaluationStage.Candidate;

            if (selectedMapIndex >= 0 && !this.BlockLossless)
            {
                map.CopyFrom(modeWorkspace.GetWinnerPaletteMap(selectedMapIndex));
            }

            return mode;
        }

        /// <summary>
        /// Searches the directional, smooth, filter intra and palette luma modes of an intra block, with their
        /// transform sizes, and retains the best candidates for the winner stage.
        /// </summary>
        /// <param name="writer">The symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="transformEdges">The transform size context edges of the tile.</param>
        /// <param name="paletteEdges">The palette color context edges of the tile.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="superblockCoefficients">The coefficients and transform block states of the superblock.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="retainedStates">The transform states of the luma winner.</param>
        /// <param name="interCostLimit">The cost of the inter winner, which bounds the intra search.</param>
        /// <param name="paletteInfo">The palette of the block.</param>
        /// <param name="selectedAngleDelta">The angle delta of the luma winner.</param>
        /// <param name="selectedFilterIntraMode">The filter intra mode of the luma winner.</param>
        /// <param name="selectedTransformSize">The transform size of the luma winner.</param>
        /// <param name="selectedStatistics">The rate and distortion of the luma winner.</param>
        /// <returns>The luma mode of the winner.</returns>
        private Av1PredictionMode SelectLumaPrediction(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1NeighborEdges<byte> transformEdges,
            in Av1NeighborEdges<Av1EncoderPaletteInfo> paletteEdges,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<int> superblockCoefficients,
            Span<int> workspaceStorage,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Span<Av1EncoderTransformBlockState> retainedStates,
            long interCostLimit,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out Av1FilterIntraMode selectedFilterIntraMode,
            out Av1TransformSize selectedTransformSize,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            bool intraFrame = this.picture.Parent.FrameHeader.IsIntra;
            bool lossless = this.BlockLossless;
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            Av1EncoderPartitionTree.ModeContext winner = Av1EncoderBlockWorkspace.GetIntraWinnerContext(workspaceStorage, 1);

            // The winner's storage is laid out from the size it records, so it names this block before
            // anything reads or writes its transform grid.
            winner.Snapshot.ModeInfo.Block.BlockSize = blockSize;
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Av1PlaneRegion<TSample> reconstructionPlane = this.reconstruction.GetPlane(Av1Plane.Y);

            // The frame block that every mode trial writes is read once.
            ReadOnlySpan<TSample> frameBlock = Av1TransformBlockEncoder.GetPlaneSpan(
                reconstructionLuma, reconstructionPlane, blockOrigin);

            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int sizeContext = Av1TileWriter.GetTransformSizeContext(in transformEdges, modeInfoGrid, modeInfoAllocation, macroBlock, blockOrigin, blockSize);

            Av1ModeCosts modeCosts = tables.ModeCosts;
            bool paletteAllowed = Av1TileWriter.IsPaletteAllowed(this.picture.Parent.FrameHeader.AllowScreenContentTools, blockSize);
            int paletteDisabledCost = paletteAllowed
                ? Av1SymbolEncoder.GetPaletteYModeCost(
                    modeCosts,
                    false,
                    Av1TileWriter.GetPaletteBlockSizeContext(blockSize),
                    Av1TileWriter.GetPaletteYModeContext(in paletteEdges, macroBlock, blockOrigin))
                : 0;

            Av1TransformSize maximumSize = lossless ? Av1TransformSize.Size4x4 : blockSize.GetMaximumTransformSize();
            int maximumDepth = lossless || settings.DeferTransformSizeSearch ||
                this.picture.Parent.FrameHeader.TransformMode != Av1TransformMode.Select
                    ? 0
                    : width == height ? settings.IntraSquareTransformSearchDepth : settings.IntraRectangularTransformSearchDepth;

            int sourceVariance = maximumDepth > 1 ? this.GetSourceVariance(
                sourceLuma, sourceBlue, sourceRed, modeWorkspace.GetCandidateReconstruction(0), blockOrigin, blockSize) : 0;

            int visibleWidth = width + (Math.Min(0, macroBlock.ToRightEdge) >> 3);
            int visibleHeight = height + (Math.Min(0, macroBlock.ToBottomEdge) >> 3);
            byte directionalMask = this.GetDirectionalModeSkipMask(
                in modeWorkspace, sourceLuma, sourceBlue, sourceRed, sourcePlane, blockOrigin, visibleHeight, visibleWidth);

            ReadOnlySpan<sbyte> angles = settings.PruneOddIntraAngleDeltas ? PrunedAngleDeltaSearchOrder : AngleDeltaSearchOrder;
            int directionalCount = (int)Av1PredictionMode.Directional67Degrees - (int)Av1PredictionMode.Vertical + 1;
            int modeCount = LumaModeSearchOrder.Length + (blockSize >= Av1BlockSize.Block8x8 ? directionalCount * angles.Length : 0);
            int filterStart = intraFrame ? modeCount : 1;
            int filterCount = (int)Av1FilterIntraMode.AllFilterIntraModes;
            InlineArray64<long> directionalStorage = default;
            Span<long> directionalCosts = directionalStorage[..(directionalCount * 7)];
            directionalCosts.Fill(long.MaxValue);
            InlineArray4<long> modelStorage = default;
            Span<long> modelCosts = modelStorage;
            modelCosts.Fill(long.MaxValue);
            long bestModelCost = long.MaxValue;
            Av1RateDistortionStatistics bestStatistics = Av1RateDistortionStatistics.Invalid;
            Av1RateDistortionStatistics dcStatistics = Av1RateDistortionStatistics.Invalid;
            Av1PredictionMode bestMode = Av1PredictionMode.DC;
            selectedAngleDelta = 0;
            selectedFilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
            selectedTransformSize = maximumSize;
            bool dcEvaluated = false;
            bool stopFilters = false;
            Av1PredictionMode filterBaseMode = Av1PredictionMode.DC;

            // The mode loop below tries many modes for the same block. Everything that does not depend on the mode
            // is read here once: the spans of the model cost, the smooth-edge state and partition type, the modes
            // of the left and above neighbors, and the inter frame syntax rate of an intra block.
            LumaBlockState blockState = this.GetLumaBlockState(in tables, modeInfoGrid, modeInfoAllocation, macroBlock, blockOrigin, blockSize);
            LumaModelInputs modelInputs = new(
                in modeWorkspace,
                transformCoefficients,
                transformWorkspace,
                sourceLuma,
                reconstructionPlane,
                reconstructionLuma,
                blockOrigin,
                blockState.PartitionType,
                blockState.SmoothEdges,
                this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter);

            bool hasLeftNeighbor = macroBlock.IsLeftAvailable;
            bool hasAboveNeighbor = macroBlock.IsUpAvailable;
            Av1PredictionMode leftNeighborMode = hasLeftNeighbor
                ? macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -1).Block.Mode
                : Av1PredictionMode.DC;

            Av1PredictionMode aboveNeighborMode = hasAboveNeighbor
                ? macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, -macroBlock.ModeInfoStride).Block.Mode
                : Av1PredictionMode.DC;

            int interFrameSyntaxRate = intraFrame ? 0 : blockState.IntraInterRate + blockState.NoSkipRate;

            // A predictor first chooses its own transform grid. Only that completed result competes
            // with other predictors, so an empty residual cannot change ranking midway through type search.
            // Filter predictors follow DC in inter pictures and follow spatial/palette search in intra pictures.
            for (int index = 0; index < modeCount + filterCount; index++)
            {
                bool filter = index >= filterStart && index < filterStart + filterCount;
                if (index == filterStart)
                {
                    if (intraFrame && paletteAllowed)
                    {
                        int dcModeCost = Av1TileWriter.GetLumaModeCost(
                            modeCosts,
                            modeInfoGrid,
                            modeInfoAllocation,
                            macroBlock,
                            blockSize,
                            Av1PredictionMode.DC,
                            0,
                            intraFrame);

                        if (this.SelectLumaPalette(
                            writer,
                            in tables,
                            in modeWorkspace,
                            transformCoefficients,
                            dequantizedCoefficients,
                            transformWorkspace,
                            transformTypeProbabilities,
                            in transformEdges,
                            in paletteEdges,
                            in lumaCoefficientEdges,
                            modeInfoGrid,
                            modeInfoAllocation,
                            sourceLuma,
                            sourceBlue,
                            sourceRed,
                            reconstructionLuma,
                            reconstructionBlue,
                            reconstructionRed,
                            macroBlock,
                            blockOrigin,
                            blockSize,
                            retainedStates,
                            64,
                            dcModeCost,
                            ref bestStatistics,
                            ref paletteInfo,
                            ref selectedTransformSize))
                        {
                            bestMode = Av1PredictionMode.DC;
                            selectedAngleDelta = 0;
                            selectedFilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
                            Av1MacroBlockModeInfo paletteMode = macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, 0);
                            paletteMode.Block.Mode = bestMode;
                            paletteMode.Block.TransformSize = selectedTransformSize;
                            winner.Snapshot = new Av1EncoderPartitionTree.ModeSnapshot
                            {
                                ModeInfo = paletteMode,
                                Block = new Av1EncoderBlockStruct { FilterIntraMode = selectedFilterIntraMode },
                                Palette = paletteInfo,
                                Statistics = bestStatistics
                            };

                            // The retained storage is laid out from the size the snapshot records, so it
                            // names the block that was searched before anything reads its transform grid.
                            winner.Snapshot.ModeInfo.Block.BlockSize = blockSize;

                            Size extent = GetCodedTransformExtent(macroBlock, blockSize, selectedTransformSize, 0, 0);
                            this.RetainModeContext(superblockCoefficients, winner, this.codedAreaLuma, this.codedAreaChroma, extent.Width * extent.Height, 0);
                            CopyWinnerTransformStates(retainedStates, winner.GetTransformStates(Av1Plane.Y));
                        }
                    }

                    filterBaseMode = bestMode;
                }

                // DC, with its filter intra trials, is the first mode of an inter frame. A luma cost above five quarters of the bound makes it
                // invalid, so no luma winner remains. Then the search stops, unless the intra-in-inter pruning is off.
                if (!intraFrame && index == filterStart + filterCount && dcStatistics.Cost != long.MaxValue &&
                    interCostLimit < long.MaxValue / 2 && dcStatistics.LumaCost > interCostLimit + (interCostLimit >> 2))
                {
                    bestStatistics = Av1RateDistortionStatistics.Invalid;
                    this.lumaCandidateCount = 0;
                    if (settings.IntraInInterPruningLevel != 0)
                    {
                        break;
                    }
                }

                Av1PredictionMode mode;
                int angleDelta = 0;
                Av1FilterIntraMode filterMode = Av1FilterIntraMode.AllFilterIntraModes;
                if (filter)
                {
                    if (stopFilters || settings.FilterIntraPruneLevel >= 2 || (intraFrame && bestStatistics.Cost == long.MaxValue) ||
                        !Av1TileWriter.IsFilterIntraAllowedBlockSize(this.picture.Sequence.SequenceHeader.EnableFilterIntra, blockSize))
                    {
                        continue;
                    }

                    if (!intraFrame && (!dcEvaluated ||
                        (dcStatistics.Cost == long.MaxValue ? settings.SkipFilterIntraAfterInvalidDc : dcStatistics.Cost / 2 > interCostLimit)))
                    {
                        continue;
                    }

                    mode = Av1PredictionMode.DC;
                    filterMode = (Av1FilterIntraMode)(index - filterStart);

                    // A cached decision without filter intra excludes every filter mode, and a cached filter mode excludes the others. The
                    // intra search of an inter frame does not read the cache.
                    if (intraFrame && this.activeModeCache.Active && filterMode != this.activeModeCache.FilterIntraMode)
                    {
                        continue;
                    }

                    if (settings.FilterIntraPruneLevel == 1 && !IsFilterIntraModeDerivedFromBestMode(filterMode, filterBaseMode))
                    {
                        continue;
                    }
                }
                else
                {
                    int modeIndex = index < filterStart ? index : index - filterCount;
                    if (modeIndex < LumaModeSearchOrder.Length)
                    {
                        mode = LumaModeSearchOrder[modeIndex];
                    }
                    else
                    {
                        int adjusted = modeIndex - LumaModeSearchOrder.Length;
                        mode = (Av1PredictionMode)((int)Av1PredictionMode.Vertical + (adjusted / angles.Length));
                        angleDelta = angles[adjusted % angles.Length];

                        // Only the intra frame search prunes odd deltas by cost. An inter frame only reorders them.
                        if (intraFrame && settings.PruneOddIntraAngleDeltas &&
                            ShouldPruneOddAngleDelta(mode, angleDelta, directionalCosts, Math.Min(bestStatistics.Cost, interCostLimit)))
                        {
                            continue;
                        }
                    }

                    if (settings.DisableSmoothIntra &&
                        (mode is Av1PredictionMode.SmoothHorizontal or Av1PredictionMode.SmoothVertical ||
                         (mode == Av1PredictionMode.Smooth && (!intraFrame || settings.FilterIntraPruneLevel == 0))))
                    {
                        continue;
                    }

                    // A cached decision limits the search to its mode. The angle delta stays free. The intra search of an inter frame does
                    // not read the cache.
                    if (intraFrame && this.activeModeCache.Active && mode != this.activeModeCache.Mode)
                    {
                        continue;
                    }

                    if (settings.RestrictLargeIntraBlocksToDc && maximumSize.GetSquareSize() >= Av1TransformSize.Size32x32 &&
                        mode != Av1PredictionMode.DC)
                    {
                        continue;
                    }

                    if (!intraFrame)
                    {
                        int knownRate = Av1SymbolEncoder.GetInterFrameLumaModeCost(modeCosts, mode, blockSize) + interFrameSyntaxRate;

                        // A mode whose known syntax already exceeds the bound is not searched. Then the search stops, unless the
                        // intra-in-inter pruning is off. The test comes before the directional pruning.
                        if (Av1RateDistortion.GetCost(this.rateMultiplier, knownRate, 0) > interCostLimit)
                        {
                            if (settings.IntraInInterPruningLevel != 0)
                            {
                                break;
                            }

                            continue;
                        }
                    }

                    // An inter frame prunes directional modes only for a block that can code an angle delta. An intra frame prunes them for
                    // every block.
                    if (mode is >= Av1PredictionMode.Vertical and <= Av1PredictionMode.Directional67Degrees &&
                        (intraFrame || blockSize >= Av1BlockSize.Block8x8) &&
                        (directionalMask & (1 << ((int)mode - (int)Av1PredictionMode.Vertical))) != 0)
                    {
                        continue;
                    }
                }

                if (!filter || intraFrame)
                {
                    long modelCost = this.GetLumaModelCost(
                        macroBlock, sourcePlane, reconstructionPlane, blockOrigin, blockSize, in modelInputs, mode, angleDelta, filterMode);

                    if (filter)
                    {
                        if (bestModelCost != long.MaxValue && modelCost > bestModelCost + (bestModelCost >> 2))
                        {
                            continue;
                        }

                        bestModelCost = Math.Min(bestModelCost, modelCost);
                    }
                    else if (ShouldPruneIntraModel(
                        modelCost,
                        mode,
                        hasLeftNeighbor,
                        leftNeighborMode,
                        hasAboveNeighbor,
                        aboveNeighborMode,
                        this.blockQIndex,
                        modelCosts,
                        settings.IntraModelCandidateCount,
                        settings.AdaptIntraModelCountToNeighbors,
                        ref bestModelCost))
                    {
                        continue;
                    }
                }

                if (!filter && mode == Av1PredictionMode.DC)
                {
                    dcEvaluated = true;
                }

                // In an intra frame, the best mode so far bounds the search of a mode. The inter bound also applies.
                Av1RateDistortionStatistics modeStatistics = this.ChooseUniformTransformSize(
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in lumaCoefficientEdges,
                    macroBlock,
                    sourcePlane,
                    reconstructionPlane,
                    blockOrigin,
                    blockSize,
                    in blockState,
                    maximumSize,
                    maximumDepth,
                    sourceVariance,
                    mode,
                    angleDelta,
                    filterMode,
                    0,
                    [],
                    0,
                    paletteDisabledCost,
                    sizeContext,
                    intraFrame ? Math.Min(bestStatistics.Cost, interCostLimit) : interCostLimit,
                    long.MaxValue,
                    modeWorkspace.CandidateTransformBlocks,
                    retainedStates,
                    out Av1TransformSize bestSize);

                if (this.picture.Parent.EncoderOptions.IsAllIntra && modeStatistics.Cost != long.MaxValue)
                {
                    // The predictor ranking changes only after its transform grid is selected. The raw rate and distortion stay unchanged for
                    // the chroma and partition cost sums. The frame holds the samples of the last grid trial, not of the winning grid, because
                    // the choice of a grid restores only the transform type map.
                    double varianceFactor = this.GetIntraVarianceFactor(
                        workspaceStorage, sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize, frameBlock, reconstructionPlane.Stride);

                    modeStatistics.Cost = (long)(modeStatistics.Cost * varianceFactor);
                }

                if (!intraFrame && filter)
                {
                    if (modeStatistics.Cost != long.MaxValue && modeStatistics.Cost / 2 > interCostLimit)
                    {
                        stopFilters = true;
                        continue;
                    }

                    if (modeStatistics.Cost >= dcStatistics.Cost)
                    {
                        continue;
                    }
                }

                if (!filter && mode is >= Av1PredictionMode.Vertical and <= Av1PredictionMode.Directional67Degrees)
                {
                    int direction = (int)mode - (int)Av1PredictionMode.Vertical;
                    directionalCosts[(direction * 7) + angleDelta + 3] = modeStatistics.Cost;
                }

                // A mode whose luma cost exceeds five quarters of the bound is not counted. Then the search stops, unless the intra-in-inter
                // pruning is off.
                if (!intraFrame && !filter && mode != Av1PredictionMode.DC &&
                    modeStatistics.Cost != long.MaxValue && interCostLimit < long.MaxValue / 2 &&
                    modeStatistics.LumaCost > interCostLimit + (interCostLimit >> 2))
                {
                    if (settings.IntraInInterPruningLevel != 0)
                    {
                        break;
                    }

                    continue;
                }

                if (mode == Av1PredictionMode.DC && (!filter || !intraFrame))
                {
                    dcStatistics = modeStatistics;
                }

                // In an inter frame, sharpness 3 adds a charge to the luma cost of a mode that is smoother than the source. The charge comes
                // after the search of the mode and before the comparison. The frame holds the samples of the last grid trial.
                if (modeStatistics.LumaCost != long.MaxValue && this.ChargesSmoothing)
                {
                    long smoothingOffset = this.GetIntraSmoothingOffset(
                        sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize, frameBlock, reconstructionPlane.Stride);

                    modeStatistics.LumaCost += Av1RateDistortion.GetCost(this.rateMultiplier, 0, smoothingOffset);
                }
                else if (modeStatistics.LumaCost != long.MaxValue && !intraFrame && this.ChargesHighBitDepthTextureLoss)
                {
                    // An intra mode never skips here, and a smooth mode pays four times.
                    bool smoothMode = IsSmoothTextureMode(mode);
                    this.GetVarianceStatistics(
                        sourceLuma,
                        sourceBlue,
                        sourceRed,
                        blockOrigin,
                        blockSize,
                        frameBlock,
                        reconstructionPlane.Stride,
                        out long candidateSourceVariance,
                        out long candidateSampleVariance);

                    long offset = GetTextureLossOffset(candidateSourceVariance, candidateSampleVariance, blockSize, false, smoothMode, false, default);
                    modeStatistics.LumaCost = this.ChargeTextureLossCost(
                        sourceLuma, sourceBlue, sourceRed, modeStatistics.LumaCost, offset, blockOrigin, blockSize, false, false, smoothMode);
                }

                bool improves = intraFrame ? modeStatistics.Cost < Math.Min(bestStatistics.Cost, interCostLimit)
                    : modeStatistics.LumaCost < bestStatistics.LumaCost || (filter && modeStatistics.Cost != long.MaxValue);

                if (improves)
                {
                    bestStatistics = modeStatistics;
                    bestMode = mode;
                    selectedAngleDelta = angleDelta;
                    selectedFilterIntraMode = filterMode;
                    selectedTransformSize = bestSize;
                    paletteInfo.PaletteSizes[0] = 0;
                    Av1MacroBlockModeInfo selectedMode = macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, 0);
                    selectedMode.Block.Mode = mode;
                    selectedMode.Block.TransformSize = bestSize;
                    Av1EncoderBlockStruct selectedBlock = new() { FilterIntraMode = filterMode };
                    selectedBlock.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = (sbyte)angleDelta;
                    winner.Snapshot = new Av1EncoderPartitionTree.ModeSnapshot
                    {
                        ModeInfo = selectedMode,
                        Block = selectedBlock,
                        Palette = paletteInfo,
                        Statistics = bestStatistics
                    };

                    // The kept storage is laid out from the size that the snapshot records. Thus the snapshot names the searched block
                    // before anything reads its transform grid.
                    winner.Snapshot.ModeInfo.Block.BlockSize = blockSize;

                    Size extent = GetCodedTransformExtent(macroBlock, blockSize, bestSize, 0, 0);
                    this.RetainModeContext(superblockCoefficients, winner, this.codedAreaLuma, this.codedAreaChroma, extent.Width * extent.Height, 0);

                    // The winner keeps the transform grid that its own search produced. A later mode writes over the shared candidate
                    // buffers, so the states move when the winner changes, not when the block is coded again.
                    CopyWinnerTransformStates(retainedStates, winner.GetTransformStates(Av1Plane.Y));

                    if (!intraFrame)
                    {
                        this.lumaCandidateCount = 0;
                    }
                }

                if (intraFrame || improves)
                {
                    this.RetainLumaCandidate(
                        in modeWorkspace,
                        new LumaCandidate { Mode = mode, AngleDelta = angleDelta, FilterMode = filterMode, Cost = modeStatistics.Cost },
                        blockSize);
                }
            }

            // The luma search leaves whatever its last candidate wrote in the plane. It does not put the winner back. The chroma search
            // codes luma again when chroma-from-luma is still open, and the block encode writes the winner in every case.
            selectedStatistics = bestStatistics;
            return bestMode;
        }

        /// <summary>
        /// Returns the number of luma coefficients that an inter block codes. The method walks the transform tree in its smallest
        /// traversal units and counts each transform once, at its origin, when that origin lies inside the coded frame.
        /// </summary>
        /// <param name="macroBlock">The neighbor availability and frame edges of the block.</param>
        /// <param name="modeInfo">The syntax of the block, with its transform sizes.</param>
        /// <returns>The coded luma area, in coefficients.</returns>
        private static int GetInterLumaCodedArea(Av1MacroBlockD macroBlock, ref Av1EncoderBlockModeInfo modeInfo)
        {
            Av1TransformSize rootSize = modeInfo.TransformSize == Av1TransformSize.Size4x4
                ? Av1TransformSize.Size4x4
                : modeInfo.BlockSize.GetMaximumTransformSize();

            Av1TransformSize traversalSize = rootSize.GetSubSize().GetSubSize();
            Size extent = GetCodedTransformExtent(macroBlock, modeInfo.BlockSize, Av1TransformSize.Size4x4, 0, 0);
            int leafCount = modeInfo.BlockSize.GetWidth() * modeInfo.BlockSize.GetHeight() / traversalSize.GetSize2d();
            int area = 0;
            for (int leaf = 0; leaf < leafCount; leaf++)
            {
                Point offset = rootSize.GetBlockPartitionOrigin(modeInfo.BlockSize, traversalSize, leaf, 0, 0);
                Av1TransformSize size = modeInfo.InterTransformSizes[modeInfo.GetInterTransformSizeIndex(offset.Y >> 2, offset.X >> 2)];
                if (offset.X < extent.Width && offset.Y < extent.Height &&
                    (offset.X % size.GetWidth()) == 0 && (offset.Y % size.GetHeight()) == 0)
                {
                    area += size.GetSize2d();
                }
            }

            return area;
        }

        /// <summary>
        /// Chooses the uniform transform size of one luma candidate. Every depth from the start size is measured. The depths compete on
        /// the transform cost alone (coefficients, non-skip flag and size syntax), because the mode or palette syntax of the candidate is
        /// the same at every depth. Only the depth that wins that comparison represents the candidate. That depth is copied into the kept
        /// storage when its whole cost, with the mode syntax, is below the selection limit. The intra mode search, the palette search and
        /// the winner refinement all use this method.
        /// </summary>
        /// <remarks>
        /// A depth with a lower transform cost never has a higher whole cost, because the two costs differ only by the fixed mode rate and
        /// one rounding step. Thus a depth that replaces a copied depth is copied too. The kept storage always holds the final best depth
        /// when that depth passes the selection limit.
        /// </remarks>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="writer">The tile symbol encoder.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the caller read once.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the type estimates.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="macroBlock">The block's neighbor state.</param>
        /// <param name="sourcePlane">The source luma plane.</param>
        /// <param name="reconstructionPlane">The reconstruction luma plane.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="blockState">The block values that every mode trial shares, read once by the caller.</param>
        /// <param name="startSize">The largest transform size searched.</param>
        /// <param name="maximumDepth">The number of splits the search can make below the start size.</param>
        /// <param name="sourceVariance">The source variance that gates the depth prune.</param>
        /// <param name="mode">The luma prediction mode.</param>
        /// <param name="angleDelta">The luma angle delta.</param>
        /// <param name="filterMode">The filter intra mode.</param>
        /// <param name="paletteSize">The luma palette size, or zero.</param>
        /// <param name="paletteColors">The luma palette colors.</param>
        /// <param name="paletteHeaderRate">The palette syntax rate.</param>
        /// <param name="paletteDisabledCost">The rate that signals no palette.</param>
        /// <param name="sizeContext">The transform size context.</param>
        /// <param name="referenceLimit">The bound that the search arrives with.</param>
        /// <param name="selectionLimit">The whole cost the best depth must be below to be retained.</param>
        /// <param name="candidateStates">The candidate transform block storage.</param>
        /// <param name="retainedStates">The retained transform block states.</param>
        /// <param name="bestSize">The transform size of the best depth.</param>
        /// <returns>The statistics of the best depth, or invalid statistics when no depth completed.</returns>
        private Av1RateDistortionStatistics ChooseUniformTransformSize(
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            Av1MacroBlockD macroBlock,
            Av1PlaneRegion<TSample> sourcePlane,
            Av1PlaneRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            Av1BlockSize blockSize,
            in LumaBlockState blockState,
            Av1TransformSize startSize,
            int maximumDepth,
            int sourceVariance,
            Av1PredictionMode mode,
            int angleDelta,
            Av1FilterIntraMode filterMode,
            int paletteSize,
            scoped ReadOnlySpan<ushort> paletteColors,
            int paletteHeaderRate,
            int paletteDisabledCost,
            int sizeContext,
            long referenceLimit,
            long selectionLimit,
            Span<Av1EncoderTransformBlockState> candidateStates,
            Span<Av1EncoderTransformBlockState> retainedStates,
            out Av1TransformSize bestSize)
        {
            bool breakout = this.picture.Parent.SpeedSettings.UseIntraTransformRdBreakout;
            Av1RateDistortionStatistics best = Av1RateDistortionStatistics.Invalid;
            long previousTransformCost = long.MaxValue;
            bestSize = startSize;
            Av1TransformSize size = startSize;
            for (int depth = 0; depth <= maximumDepth; depth++, size = size.GetSubSize())
            {
                // With the breakout, a later depth also stops when it passes the best depth measured so far.
                long costLimit = breakout ? Math.Min(referenceLimit, best.TransformCost) : referenceLimit;
                Av1RateDistortionStatistics statistics = this.GetUniformLumaCandidateCost(
                    sourceLuma,
                    sourceBlue,
                    sourceRed,
                    reconstructionLuma,
                    reconstructionBlue,
                    reconstructionRed,
                    writer,
                    in tables,
                    in modeWorkspace,
                    transformCoefficients,
                    dequantizedCoefficients,
                    transformWorkspace,
                    transformTypeProbabilities,
                    in lumaCoefficientEdges,
                    macroBlock,
                    sourcePlane,
                    reconstructionPlane,
                    blockOrigin,
                    blockSize,
                    in blockState,
                    size,
                    sourceVariance,
                    mode,
                    angleDelta,
                    filterMode,
                    paletteSize,
                    paletteColors,
                    paletteHeaderRate,
                    paletteDisabledCost,
                    sizeContext,
                    costLimit,
                    candidateStates,
                    out bool skipSmaller);

                if (statistics.TransformCost < best.TransformCost)
                {
                    best = statistics;
                    bestSize = size;
                    if (statistics.Cost < selectionLimit)
                    {
                        Size codedExtent = GetCodedTransformExtent(macroBlock, blockSize, size, 0, 0);
                        CopyTiledCandidate(candidateStates, codedExtent, size, retainedStates);
                    }
                }

                // A low-contrast block stops splitting when a split costs more than the depth above it. A depth that did not complete has
                // the maximum transform cost, so it also stops the search when the depth above it completed.
                if (skipSmaller || size == Av1TransformSize.Size4x4 ||
                    (depth > 0 && depth < maximumDepth && sourceVariance < 256 && statistics.TransformCost > previousTransformCost))
                {
                    break;
                }

                previousTransformCost = statistics.TransformCost;
            }

            return best;
        }

        /// <summary>
        /// Evaluates an intra transform grid with local reconstruction and coefficient contexts, stopping at the supplied cost bound.
        /// </summary>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="writer">The tile symbol encoder.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the caller read once.</param>
        /// <param name="modeWorkspace">The mode decision buffers of the block.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the type estimates.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="lumaCoefficientEdges">The luma coefficient context edges of the tile.</param>
        /// <param name="macroBlock">The block's neighbor state.</param>
        /// <param name="sourcePlane">The source luma plane.</param>
        /// <param name="reconstructionPlane">The reconstruction luma plane.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="blockState">The block values that every mode trial shares, read once by the caller.</param>
        /// <param name="transformSize">The transform size of the grid.</param>
        /// <param name="sourceVariance">The source variance of the block.</param>
        /// <param name="mode">The luma prediction mode.</param>
        /// <param name="angleDelta">The luma angle delta.</param>
        /// <param name="filterIntraMode">The filter intra mode.</param>
        /// <param name="paletteSize">The luma palette size, or zero.</param>
        /// <param name="paletteColors">The luma palette colors.</param>
        /// <param name="paletteHeaderRate">The palette syntax rate.</param>
        /// <param name="paletteDisabledCost">The rate that signals no palette.</param>
        /// <param name="transformSizeContext">The transform size context.</param>
        /// <param name="costLimit">The cost at which the evaluation stops.</param>
        /// <param name="candidateTransformBlocks">The candidate transform block storage.</param>
        /// <param name="skipSmallerTransforms">Whether smaller transform sizes need no search.</param>
        /// <returns>The statistics of the grid, or invalid statistics when the cost limit stopped it.</returns>
        private Av1RateDistortionStatistics GetUniformLumaCandidateCost(
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            in Av1NeighborEdges<byte> lumaCoefficientEdges,
            Av1MacroBlockD macroBlock,
            Av1PlaneRegion<TSample> sourcePlane,
            Av1PlaneRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            Av1BlockSize blockSize,
            in LumaBlockState blockState,
            Av1TransformSize transformSize,
            int sourceVariance,
            Av1PredictionMode mode,
            int angleDelta,
            Av1FilterIntraMode filterIntraMode,
            int paletteSize,
            scoped ReadOnlySpan<ushort> paletteColors,
            int paletteHeaderRate,
            int paletteDisabledCost,
            int transformSizeContext,
            long costLimit,
            Span<Av1EncoderTransformBlockState> candidateTransformBlocks,
            out bool skipSmallerTransforms)
        {
            skipSmallerTransforms = false;
            int blockWidth = blockSize.GetWidth();
            int blockHeight = blockSize.GetHeight();
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int transformSampleCount = transformSize.GetSize2d();
            int transformWidth4x4 = transformSize.Get4x4WideCount();
            int transformHeight4x4 = transformSize.Get4x4HighCount();
            int contextWidth = blockSize.Get4x4WideCount();
            int contextHeight = blockSize.Get4x4HighCount();
            Size frameContextSize = new(this.picture.Parent.FrameHeader.ModeInfoColumnCount, this.picture.Parent.FrameHeader.ModeInfoRowCount);
            Span<TSample> reconstructionSamples = reconstructionLuma;
            Span<TSample> frameBlock = reconstructionSamples[reconstructionPlane.GetOffset(blockOrigin.X, blockOrigin.Y)..];
            int frameStride = reconstructionPlane.Stride;
            ReadOnlySpan<TSample> sourceSamples = sourceLuma;
            Av1PartitionType partitionType = blockState.PartitionType;

            // Each transform block predicts straight into the frame. The second candidate plane holds the two transform
            // reconstructions of the type search, because the frame keeps the prediction that every type reads.
            Span<TSample> transformSamples = modeWorkspace.GetCandidateReconstruction(1);
            Span<TSample> candidateTransformReconstruction = transformSamples[..transformSampleCount];
            Span<TSample> bestTransformReconstruction = transformSamples.Slice(
                transformSampleCount,
                transformSampleCount);

            // The second coefficient plane holds four transform-sized spans: the candidate and best quantized coefficients, and the candidate
            // and best dequantized coefficients. The spans swap on each improvement, so the reconstruction input of the winner stays without
            // a copy.
            Span<int> transformCoefficientStorage = modeWorkspace.GetCandidateCoefficients(1);
            Span<int> candidateTransformCoefficients = transformCoefficientStorage[..transformSampleCount];
            Span<int> bestTransformCoefficients = transformCoefficientStorage.Slice(
                transformSampleCount,
                transformSampleCount);

            Span<int> candidateDequantizedCoefficients = transformCoefficientStorage.Slice(
                transformSampleCount * 2,
                transformSampleCount);

            Span<int> bestDequantizedCoefficients = transformCoefficientStorage.Slice(
                transformSampleCount * 3,
                transformSampleCount);

            Span<short> residual = (paletteSize > 0 ? modeWorkspace.Palette.GetResidual(0) : modeWorkspace.Residual)[..transformSampleCount];
            Span<byte> contexts = modeWorkspace.TransformContexts;
            Span<byte> topContexts = contexts[..contextWidth];
            Span<byte> leftContexts = contexts.Slice(contextWidth, contextHeight);
            int topIndex = lumaCoefficientEdges.GetTopIndex(blockOrigin);
            int leftIndex = lumaCoefficientEdges.GetLeftIndex(blockOrigin);
            lumaCoefficientEdges.Top.Slice(topIndex, contextWidth).CopyTo(topContexts);
            lumaCoefficientEdges.Left.Slice(leftIndex, contextHeight).CopyTo(leftContexts);

            // The edge filter strength depends on the neighbors of the block alone, so every transform block shares it.
            bool smoothEdges = blockState.SmoothEdges;

            // The prediction and transform-size syntax belongs to the coding block. Each residual transform adds its own coefficient cost.
            // Lossless blocks and fixed-size modes do not signal a size choice.
            bool lossless = this.BlockLossless;

            // The size costs nothing while a stage searches the largest transform only. Such a stage runs with the largest-transform mode,
            // not the selecting mode, and only the selecting mode codes the size. From all-intra speed 4, the mode evaluation uses the
            // largest-transform method and the winner evaluation keeps the selecting method. This flag stands for that split. The frame
            // still signals the size that it settled on.
            bool selectsTransformSize = !this.picture.Parent.SpeedSettings.DeferTransformSizeSearch ||
                this.blockWorkspace.EvaluationStage != Av1EncoderEvaluationStage.Candidate;

            Av1ModeCosts modeCosts = tables.ModeCosts;
            int rate = !lossless && blockSize > Av1BlockSize.Block4x4 && selectsTransformSize &&
                this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select
                ? Av1SymbolEncoder.GetTransformSizeCost(modeCosts, blockSize, transformSize, transformSizeContext)
                : 0;

            int transformSizeRate = rate;
            if (paletteSize > 0)
            {
                rate += paletteHeaderRate;
            }
            else
            {
                rate += Av1TileWriter.GetLumaModeCost(
                    modeCosts,
                    blockSize,
                    mode,
                    angleDelta,
                    this.picture.Parent.FrameHeader.IsIntra,
                    blockState.TopModeContext,
                    blockState.LeftModeContext);

                // The block state holds zero for an intra frame, which codes no intra/inter flag.
                rate += blockState.IntraInterRate;

                if (mode == Av1PredictionMode.DC)
                {
                    rate += paletteDisabledCost;
                    if (Av1TileWriter.IsFilterIntraAllowedBlockSize(this.picture.Sequence.SequenceHeader.EnableFilterIntra, blockSize))
                    {
                        rate += Av1SymbolEncoder.GetFilterIntraModeCost(modeCosts, filterIntraMode, blockSize);
                    }
                }
            }

            // Every intra candidate of a frame that allows intra block copy signals that it does not copy.
            if (this.picture.Parent.FrameHeader.AllowIntraBlockCopy)
            {
                rate += Av1SymbolEncoder.GetUseIntraBlockCopyCost(modeCosts, false);
            }

            Av1PlaneRegion<byte> colorIndexMap = default;
            if (paletteSize > 0)
            {
                colorIndexMap = this.superblock.Workspace
                    .GetPaletteMaps()
                    .GetMap(Av1PlaneType.Y, blockWidth, blockHeight);
            }

            int modeRate = rate - transformSizeRate;
            bool hasCoefficients = false;
            long distortion = 0;

            // The transform budget starts with the cost of the non-skip flag and the transform-size syntax, because an intra block always
            // signals non-skip. Then the cost of each transform is added, and the candidate drops as soon as the running cost passes the
            // bound.
            int noSkipRate = blockState.NoSkipRate;

            // Lossless coding starts the running cost at zero.
            long runningCost = lossless ? 0 : Av1RateDistortion.GetCost(this.rateMultiplier, noSkipRate + transformSizeRate, 0);

            // A grid whose header alone passes the budget searches no transform block.
            if (runningCost > costLimit)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            // A predicted empty block prices its skip with the coefficient contexts at the block origin. These do
            // not change while the grid is searched, so they are read once for every transform block.
            Av1TransformBlockContext originContext = Av1TileWriter.GetTransformBlockContexts(
                Av1ComponentType.Luminance, in lumaCoefficientEdges, blockOrigin, blockSize, transformSize);

            // Complete each bounded 64x64 region before moving to the next. Smaller transforms
            // consume the reconstructed edges and coefficient contexts produced earlier in that region.
            Size codedExtent = GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0);
            int transformIndex = 0;

            // The prediction scratch and the reference edge slots serve every transform block, so they are read
            // once. The parent mode search retains reference slots 0 and 1. Use the other pair here because these
            // transform edges also include earlier reconstructions in this candidate.
            Span<TSample> aboveStorage = modeWorkspace.GetReferenceSamples(2);
            Span<TSample> leftStorage = modeWorkspace.GetReferenceSamples(3);
            for (int regionY = 0; regionY < codedExtent.Height; regionY += Av1Constants.MaxTransformSize)
            {
                int bottom = Math.Min(regionY + Av1Constants.MaxTransformSize, codedExtent.Height);
                for (int regionX = 0; regionX < codedExtent.Width; regionX += Av1Constants.MaxTransformSize)
                {
                    int right = Math.Min(regionX + Av1Constants.MaxTransformSize, codedExtent.Width);
                    for (int y = regionY; y < bottom; y += transformHeight)
                    {
                        int transformRow = y / transformHeight;
                        for (int x = regionX; x < right; x += transformWidth)
                        {
                            int transformColumn = x / transformWidth;
                            Point transformOrigin = blockOrigin + new Size(
                                transformColumn * transformWidth,
                                transformRow * transformHeight);

                            ReadOnlySpan<TSample> sourceTransform = sourceSamples[sourcePlane.GetOffset(transformOrigin.X, transformOrigin.Y)..];

                            // The prediction goes straight into the frame, which keeps it until the winner replaces it. The
                            // residual, the depth prune, the noise pattern test and the type search all read it there.
                            Span<TSample> prediction = frameBlock[((y * frameStride) + x)..];
                            if (paletteSize > 0)
                            {
                                // Palette prediction is block-local. A view over the retained map avoids copying indices or
                                // preparing reconstructed neighbor edges that this prediction mode cannot consume.
                                TOperator.PreparePalette(
                                    sourceTransform,
                                    sourcePlane.Stride,
                                    paletteColors,
                                    colorIndexMap.GetSubRegion(
                                        transformColumn * transformWidth,
                                        transformRow * transformHeight,
                                        transformWidth,
                                        transformHeight),
                                    prediction,
                                    frameStride,
                                    residual,
                                    transformSize);
                            }
                            else
                            {
                                // The earlier transform blocks of this candidate are already in the frame, so the edges
                                // inside the block come from the frame too, as they do for every other edge.
                                this.PrepareTransformReferenceSamples(
                                    frameBlock,
                                    frameStride,
                                    blockOrigin,
                                    blockSize,
                                    macroBlock,
                                    partitionType,
                                    transformRow,
                                    transformColumn,
                                    frameStride,
                                    transformSize,
                                    0,
                                    0,
                                    frameBlock,
                                    aboveStorage,
                                    leftStorage,
                                    mode.IsDirectional(),
                                    out bool hasLeft,
                                    out bool hasAbove);

                                if (filterIntraMode == Av1FilterIntraMode.AllFilterIntraModes)
                                {
                                    TOperator.PrepareIntra(
                                        transformWorkspace,
                                        sourceTransform,
                                        sourcePlane.Stride,
                                        prediction,
                                        frameStride,
                                        aboveStorage.Slice(1, transformWidth + transformHeight),
                                        leftStorage.Slice(1, transformWidth + transformHeight),
                                        hasLeft,
                                        hasAbove,
                                        mode,
                                        angleDelta,
                                        this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter,
                                        smoothEdges,
                                        residual,
                                        transformSize,
                                        this.bitDepth);
                                }
                                else
                                {
                                    // Filter-intra prediction is recursive within each transform unit, so rebuild it from
                                    // the reconstructed edges established by preceding transforms.
                                    TOperator.PrepareFilterIntra(
                                        transformWorkspace,
                                        sourceTransform,
                                        sourcePlane.Stride,
                                        prediction,
                                        frameStride,
                                        aboveStorage.Slice(1, transformWidth + transformHeight),
                                        leftStorage.Slice(1, transformWidth + transformHeight),
                                        residual,
                                        filterIntraMode,
                                        transformSize,
                                        this.bitDepth);
                                }
                            }

                            // The depth prune leaves the grid with the prediction in the frame.
                            if (this.picture.Parent.SpeedSettings.PruneIntraTransformDepth &&
                                this.blockWorkspace.EvaluationStage == Av1EncoderEvaluationStage.Winner &&
                                !lossless && this.bitDepth.GetBitCount() == 8 &&
                                blockSize == Av1BlockSize.Block8x8 && transformSize == Av1TransformSize.Size8x8)
                            {
                                int dcQuantizer = Av1QuantizationLookup.GetDcQuant(this.blockQIndex, 0, this.bitDepth);
                                int depthChoice = PredictIntraTransformDepth(residual, sourceVariance, dcQuantizer);
                                skipSmallerTransforms = depthChoice < 0;
                                if (depthChoice > 0)
                                {
                                    return Av1RateDistortionStatistics.Invalid;
                                }
                            }

                            Av1TransformBlockContext blockContext = Av1TileWriter.GetTransformBlockContexts(
                                Av1ComponentType.Luminance,
                                topContexts.Slice(transformColumn * transformWidth4x4, transformWidth4x4),
                                leftContexts.Slice(transformRow * transformHeight4x4, transformHeight4x4),
                                blockSize,
                                transformSize);

                            // The type search receives the budget that this block has left after its earlier transform blocks.
                            long remainingCostLimit = costLimit == long.MaxValue ? long.MaxValue : costLimit - runningCost;

                            // A later transform block of this block predicts from this one, unless this one is the last.
                            bool laterBlockPredicts = (y + transformHeight) < blockHeight || (x + transformWidth) < blockWidth;
                            this.blockWorkspace.LumaNoisePattern = this.IsLumaNoisePattern(
                                sourceLuma,
                                sourceBlue,
                                sourceRed,
                                reconstructionLuma,
                                reconstructionBlue,
                                reconstructionRed,
                                reconstructionPlane,
                                blockOrigin,
                                blockSize);

                            TransformTypeSearchResult searchResult = this.SearchTransformType(
                                writer,
                                in tables,
                                transformCoefficients,
                                dequantizedCoefficients,
                                transformWorkspace,
                                transformTypeProbabilities,
                                Av1Plane.Y,
                                false,
                                blockContext,
                                originContext,
                                sourceTransform,
                                sourcePlane.Stride,
                                transformOrigin,
                                transformSize,
                                mode,
                                filterIntraMode,
                                Av1TransformType.AllTransformTypes,
                                remainingCostLimit,
                                laterBlockPredicts ? prediction : default,
                                prediction,
                                frameStride,
                                residual,
                                transformWidth,
                                ref candidateTransformReconstruction,
                                ref bestTransformReconstruction,
                                ref candidateTransformCoefficients,
                                ref bestTransformCoefficients,
                                ref candidateDequantizedCoefficients,
                                ref bestDequantizedCoefficients);

                            this.blockWorkspace.LumaNoisePattern = false;
                            Av1TransformType bestTransformType = searchResult.Type;
                            int bestTransformRate = searchResult.Rate;
                            long bestTransformDistortion = searchResult.Distortion;
                            Av1EncoderTransformBlockState bestTransformState = searchResult.State;

                            // Later transforms read the samples of the winner from the frame and its coefficient context from the local edge
                            // arrays. The quantized coefficients are not kept, because the block encode quantizes the selected mode again.

                            // The last transform block of the block keeps its prediction here, and so does a block that quantized to nothing.
                            // Nothing inside the block predicts from the last one, so the search never adds its residual back. The block
                            // encode does that later.
                            bool publishReconstruction = bestTransformState.EndOfBlock != 0 && laterBlockPredicts;

                            // The frame keeps the prediction of the block, or its reconstruction when a later block of the grid
                            // predicts from it. The search reconstructed such a winner in the frame already, unless it measured
                            // the winner in pixels during the type loop. That reconstruction is copied, because it costs less
                            // than a second inverse transform.
                            if (publishReconstruction && !searchResult.WinnerInDestination)
                            {
                                Av1TransformBlockEncoder.WriteFrameSamples(
                                    reconstructionPlane,
                                    reconstructionSamples,
                                    transformOrigin,
                                    bestTransformReconstruction,
                                    transformWidth,
                                    transformWidth,
                                    transformHeight);
                            }

                            hasCoefficients |= bestTransformState.EndOfBlock != 0;
                            rate += bestTransformRate;
                            distortion += bestTransformDistortion;
                            candidateTransformBlocks[transformIndex] = bestTransformState;

                            // The transform search kept the coefficient context of the winning type with its state.
                            Av1TileWriter.UpdateCoefficientContexts(
                                topContexts.Slice(transformColumn * transformWidth4x4, transformWidth4x4),
                                leftContexts.Slice(transformRow * transformHeight4x4, transformHeight4x4),
                                bestTransformState.CoefficientContext,
                                transformOrigin,
                                frameContextSize);

                            runningCost += Av1RateDistortion.GetCost(
                                this.rateMultiplier, bestTransformRate, bestTransformDistortion);

                            if (runningCost > costLimit)
                            {
                                return Av1RateDistortionStatistics.Invalid;
                            }

                            transformIndex++;
                        }
                    }
                }
            }

            return new(this.rateMultiplier, rate, distortion)
            {
                ResidualRate = rate - modeRate,
                TransformCost = Av1RateDistortion.GetCost(this.rateMultiplier, rate - modeRate + noSkipRate, distortion),
                HasCoefficients = hasCoefficients,

                // The luma cost of an intra candidate in an inter frame is its token and mode rate without a skip flag, because its transform
                // search reports no skipped plane.
                LumaCost = Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion)
            };
        }

        /// <summary>
        /// Returns true when the frame luma of a block is a noise pattern at high bit depth sharpness 3. The block is a noise pattern when
        /// its frame samples are smoother than the source and the source has low detail. Then the trellis keeps more coefficients. The
        /// frame holds all writes of the trials of the block so far, also from earlier trials.
        /// </summary>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="plane">The luma plane of the frame.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <returns>True when the block is a noise pattern.</returns>
        private readonly bool IsLumaNoisePattern(
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1PlaneRegion<TSample> plane,
            Point blockOrigin,
            Av1BlockSize blockSize)
        {
            if (!this.UsesHighBitDepthSharpness)
            {
                return false;
            }

            // Both callers pass the reconstructed luma plane.
            ReadOnlySpan<TSample> frameBlock = Av1TransformBlockEncoder.GetPlaneSpan(reconstructionLuma, plane, blockOrigin);
            this.GetVarianceStatistics(
                sourceLuma, sourceBlue, sourceRed, blockOrigin, blockSize, frameBlock, plane.Stride, out long sourceVariance, out long sampleVariance);

            return sourceVariance > sampleVariance && sourceVariance / (blockSize.GetWidth() * blockSize.GetHeight()) < 64;
        }

        /// <summary>
        /// Collects the samples next to one transform block that intra prediction reads: the corner sample, the row
        /// above (with the samples above and to the right), and the column to the left (with the samples below and to
        /// the left). If an edge is inside the block, the samples come from the candidate surface, which holds the
        /// earlier transform blocks of this block. If an edge is on the block boundary, the samples come from the frame.
        /// The partition type and the block position decide if the samples above and to the right, and below and to the
        /// left, are already decoded and thus available.
        /// </summary>
        /// <param name="planeBlock">The frame plane from the plane block origin, which the caller reads once outside its loops.</param>
        /// <param name="planeStride">The number of samples between rows of <paramref name="planeBlock"/>.</param>
        /// <param name="lumaBlockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The luma block size.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="partitionType">The partition type of the block, which selects the top-right and bottom-left availability.</param>
        /// <param name="transformRow">The transform row in the block.</param>
        /// <param name="transformColumn">The transform column in the block.</param>
        /// <param name="candidateStride">The number of samples between rows of <paramref name="candidateReconstruction"/>.</param>
        /// <param name="transformSize">The transform size.</param>
        /// <param name="subsamplingX">The horizontal subsampling of the plane.</param>
        /// <param name="subsamplingY">The vertical subsampling of the plane.</param>
        /// <param name="candidateReconstruction">The surface that holds the earlier transform blocks of the block.</param>
        /// <param name="aboveStorage">The corner followed by the top edge.</param>
        /// <param name="leftStorage">The corner followed by the left edge.</param>
        /// <param name="directional">
        /// Whether a directional mode reads the edges. Only a directional mode reads past the transform width and height,
        /// so the other modes need neither the samples above and to the right nor those below and to the left.
        /// </param>
        /// <param name="hasLeft">Whether the left edge is available.</param>
        /// <param name="hasAbove">Whether the top edge is available.</param>
        private void PrepareTransformReferenceSamples(
            ReadOnlySpan<TSample> planeBlock,
            int planeStride,
            Point lumaBlockOrigin,
            Av1BlockSize blockSize,
            Av1MacroBlockD macroBlock,
            Av1PartitionType partitionType,
            int transformRow,
            int transformColumn,
            int candidateStride,
            Av1TransformSize transformSize,
            int subsamplingX,
            int subsamplingY,
            ReadOnlySpan<TSample> candidateReconstruction,
            Span<TSample> aboveStorage,
            Span<TSample> leftStorage,
            bool directional,
            out bool hasLeft,
            out bool hasAbove)
        {
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int rowOffset = transformRow * transformHeight;
            int columnOffset = transformColumn * transformWidth;
            int modeInfoRow = lumaBlockOrigin.Y >> Av1Constants.ModeInfoSizeLog2;
            int modeInfoColumn = lumaBlockOrigin.X >> Av1Constants.ModeInfoSizeLog2;

            // Internal edges read the supplied surface at its own stride. Trials supply their isolated mosaic. The reconstruction of the
            // selected mode supplies the frame that contains the earlier transforms.
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

            // The availability of edge samples ends at the coded frame edge, even when a transform reaches into padded storage. The final
            // available sample extends past that edge, so the padding is never read as a neighbor.
            Av1BlockSize planeBlockSize = blockSize.GetSubsampled(subsamplingX != 0, subsamplingY != 0);
            int remainingWidth = planeBlockSize.GetWidth() +
                (macroBlock.ToRightEdge >> (3 + subsamplingX)) - columnOffset;

            int remainingHeight = planeBlockSize.GetHeight() +
                (macroBlock.ToBottomEdge >> (3 + subsamplingY)) - rowOffset;

            // Only a directional mode reads the samples above and to the right or below and to the left, so only it
            // needs their availability and the edges past the transform width and height.
            bool hasTopRight = false;
            bool hasBottomLeft = false;
            if (directional)
            {
                bool rightAvailable =
                    modeInfoColumn +
                        ((transformColumn4x4 + transformSize.Get4x4WideCount()) << subsamplingX) <
                    macroBlock.Tile.ModeInfoColumnEnd;

                // Samples below the transform exist only while coded rows remain below it.
                bool bottomAvailable = remainingHeight > transformHeight &&
                    modeInfoRow +
                        ((transformRow4x4 + transformSize.Get4x4HighCount()) << subsamplingY) <
                    macroBlock.Tile.ModeInfoRowEnd;

                // Availability tables describe prediction blocks. Subsampled chroma of a luma block narrower or
                // shorter than eight samples belongs to the enclosing 8x8 region, so its geometry uses that region.
                Av1BlockSize availabilityBlockSize = Av1IntraReferenceAvailability.ScaleChromaBlockSize(
                    blockSize, subsamplingX != 0, subsamplingY != 0);

                hasTopRight = Av1IntraReferenceAvailability.HasTopRight(
                    this.picture.Sequence.SequenceHeader.SuperblockSize,
                    availabilityBlockSize,
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

                hasBottomLeft = Av1IntraReferenceAvailability.HasBottomLeft(
                    this.picture.Sequence.SequenceHeader.SuperblockSize,
                    availabilityBlockSize,
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
            }

            Span<TSample> above = aboveStorage.Slice(1, directional ? transformWidth + transformHeight : transformWidth);
            Span<TSample> left = leftStorage.Slice(1, directional ? transformWidth + transformHeight : transformHeight);
            int topCount = hasAbove ? Math.Clamp(remainingWidth, 0, transformWidth) : 0;
            int leftCount = hasLeft ? Math.Clamp(remainingHeight, 0, transformHeight) : 0;
            hasAbove = topCount > 0;
            hasLeft = leftCount > 0;

            // Every frame neighbor is a fixed offset from the block origin. The frame border above and to the left of
            // the block makes the negative offsets valid.
            ref TSample planeBase = ref MemoryMarshal.GetReference(planeBlock);
            ref TSample candidateBase = ref MemoryMarshal.GetReference(candidateReconstruction);
            if (hasAbove)
            {
                int topRightCount = hasTopRight
                    ? Math.Min(Math.Min(transformWidth, transformHeight), remainingWidth - transformWidth)
                    : 0;

                int copyCount = topCount + Math.Max(topRightCount, 0);
                if (transformRow > 0)
                {
                    MemoryMarshal.CreateReadOnlySpan(
                        ref Unsafe.Add(ref candidateBase, ((rowOffset - 1) * candidateStride) + columnOffset), copyCount).CopyTo(above);
                }
                else
                {
                    MemoryMarshal.CreateReadOnlySpan(
                        ref Unsafe.Add(ref planeBase, columnOffset - planeStride), copyCount).CopyTo(above);
                }

                topCount = copyCount;
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

                ref TSample column = ref transformColumn > 0
                    ? ref Unsafe.Add(ref candidateBase, (rowOffset * candidateStride) + columnOffset - 1)
                    : ref Unsafe.Add(ref planeBase, (rowOffset * planeStride) - 1);

                int columnStride = transformColumn > 0 ? candidateStride : planeStride;
                ref TSample leftBase = ref MemoryMarshal.GetReference(left);
                for (int row = 0; row < leftCount; row++)
                {
                    Unsafe.Add(ref leftBase, row) = Unsafe.Add(ref column, row * columnStride);
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
                    ? Unsafe.Add(ref candidateBase, ((rowOffset - 1) * candidateStride) + columnOffset - 1)
                    : Unsafe.Add(ref planeBase, ((rowOffset - 1) * planeStride) + columnOffset - 1)
                : hasAbove
                    ? above[0]
                    : hasLeft
                        ? left[0]
                        : TOperator.CreateSample(midpoint);

            aboveStorage[0] = corner;
            leftStorage[0] = corner;
        }

        /// <summary>
        /// Calculates the luma extent that the chroma-from-luma surface of a block holds.
        /// </summary>
        /// <remarks>
        /// The encoder stores each coded luma transform of the block, so the surface ends at the last transform that starts inside the
        /// coded frame. A sub-8x8 luma block shares its surface with the siblings that complete its 8x8 luma region. That region always
        /// lies inside the coded frame, because the mode-information grid is eight-sample aligned. But a sibling can align the shared
        /// dimension to another transform size.
        /// </remarks>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="macroBlock">The block's frame edges.</param>
        /// <param name="blockOrigin">The luma block origin in samples.</param>
        /// <param name="blockSize">The luma block size.</param>
        /// <param name="lumaTransformSize">The luma transform size of the block.</param>
        /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
        /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
        /// <returns>The stored luma extent in samples.</returns>
        private Size GetChromaFromLumaExtent(
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Av1TransformSize lumaTransformSize,
            int subsamplingX,
            int subsamplingY)
        {
            Size extent = GetCodedTransformExtent(macroBlock, blockSize, lumaTransformSize, 0, 0);
            bool sharedWidth = subsamplingX != 0 && blockSize.GetWidth() == 4;
            bool sharedHeight = subsamplingY != 0 && blockSize.GetHeight() == 4;
            if (!sharedWidth && !sharedHeight)
            {
                return extent;
            }

            int width = sharedWidth ? 8 : extent.Width;
            int height = sharedHeight ? 8 : extent.Height;
            if (sharedWidth && !sharedHeight)
            {
                Av1TransformSize siblingSize = this.GetRetainedLumaTransformSize(modeInfoAllocation, new Point(blockOrigin.X ^ 4, blockOrigin.Y));
                Size sibling = GetCodedTransformExtent(macroBlock, blockSize, siblingSize, 0, 0);

                height = Math.Max(height, sibling.Height);
            }
            else if (sharedHeight && !sharedWidth)
            {
                Av1TransformSize siblingSize = this.GetRetainedLumaTransformSize(modeInfoAllocation, new Point(blockOrigin.X, blockOrigin.Y ^ 4));
                Size sibling = GetCodedTransformExtent(macroBlock, blockSize, siblingSize, 0, 0);

                width = Math.Max(width, sibling.Width);
            }

            return new Size(width, height);
        }

        /// <summary>
        /// Reads the luma transform size a coded neighbor retained.
        /// </summary>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="blockOrigin">The neighbor's luma origin in samples.</param>
        /// <returns>The retained transform size.</returns>
        private Av1TransformSize GetRetainedLumaTransformSize(Span<Av1MacroBlockModeInfo> modeInfoAllocation, Point blockOrigin)
            => this.picture.GetMacroBlockModeInfo(
                modeInfoAllocation,
                new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2)).Block.TransformSize;

        /// <summary>
        /// Returns the part of a plane block that the coded transforms cover. The extent is the visible part of the block, rounded down
        /// to whole 4x4 units and then up to whole transforms.
        /// </summary>
        /// <param name="macroBlock">The frame edges of the block.</param>
        /// <param name="planeBlockSize">The block size in the plane.</param>
        /// <param name="transformSize">The transform size.</param>
        /// <param name="subsamplingX">The horizontal subsampling shift of the plane.</param>
        /// <param name="subsamplingY">The vertical subsampling shift of the plane.</param>
        /// <returns>The coded extent in plane samples.</returns>
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

            // A transform that intersects the coded frame is encoded in full. Only transforms wholly in the padded border are omitted. The
            // coefficient stream packs the remaining transforms.
            width += Math.Min(0, macroBlock.ToRightEdge >> (3 + subsamplingX));
            height += Math.Min(0, macroBlock.ToBottomEdge >> (3 + subsamplingY));
            width &= ~((1 << Av1Constants.ModeInfoSizeLog2) - 1);
            height &= ~((1 << Av1Constants.ModeInfoSizeLog2) - 1);
            return new Size(
                (width + transformWidth - 1) & -transformWidth,
                (height + transformHeight - 1) & -transformHeight);
        }

        /// <summary>
        /// Determines whether an odd directional angle can be rejected from its neighboring even-angle costs. The angle is rejected when
        /// both neighbors cost more than nine eighths of the best cost. A missing neighbor counts as the maximum cost.
        /// </summary>
        /// <param name="mode">The directional prediction mode.</param>
        /// <param name="angleDelta">The angle delta, from -3 to 3.</param>
        /// <param name="directionalCosts">The measured costs, seven angle deltas for each directional mode.</param>
        /// <param name="bestCost">The best cost so far.</param>
        /// <returns><see langword="true"/> when the odd angle is pruned.</returns>
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

        /// <summary>
        /// Measures the complete luma prediction using square Hadamard tiles without residual reconstruction.
        /// </summary>
        /// <param name="macroBlock">The block, which gives the visible size at the frame edge.</param>
        /// <param name="sourcePlane">The source luma plane.</param>
        /// <param name="reconstructionPlane">The reconstructed luma plane, which gets each tile prediction.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="inputs">The spans and block values that every mode trial of the block uses.</param>
        /// <param name="mode">The intra prediction mode.</param>
        /// <param name="angleDelta">The signed directional angle adjustment.</param>
        /// <param name="filterMode">The filter intra mode, or <see cref="Av1FilterIntraMode.AllFilterIntraModes"/> for none.</param>
        /// <returns>The sum of the Hadamard costs of all visible tiles.</returns>
        private long GetLumaModelCost(
            Av1MacroBlockD macroBlock,
            Av1PlaneRegion<TSample> sourcePlane,
            Av1PlaneRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            Av1BlockSize blockSize,
            in LumaModelInputs inputs,
            Av1PredictionMode mode,
            int angleDelta,
            Av1FilterIntraMode filterMode)
        {
            Av1TransformSize transformSize = blockSize.GetMaximumTransformSize().GetSquareSize();
            if (transformSize > Av1TransformSize.Size32x32)
            {
                transformSize = Av1TransformSize.Size32x32;
            }

            int tileSize = transformSize.GetWidth();
            int blockWidth = blockSize.GetWidth();
            int visibleWidth = blockWidth + (Math.Min(0, macroBlock.ToRightEdge) >> 3);
            int visibleHeight = blockSize.GetHeight() + (Math.Min(0, macroBlock.ToBottomEdge) >> 3);

            // The caller reads every span and block value once for all mode trials, so the tile loop below only
            // indexes them.
            Span<short> residual = inputs.Residual;
            Span<TSample> aboveStorage = inputs.AboveStorage;
            Span<TSample> leftStorage = inputs.LeftStorage;
            Span<int> transformCoefficients = inputs.TransformCoefficients;
            Span<int> transformWorkspace = inputs.TransformWorkspace;
            Span<TSample> frameBlock = inputs.FrameBlock;
            ReadOnlySpan<TSample> sourceSamples = inputs.SourceSamples;
            bool smoothEdges = inputs.SmoothEdges;
            bool enableEdgeFilter = inputs.EnableEdgeFilter;
            Av1PartitionType partitionType = inputs.PartitionType;
            int frameStride = reconstructionPlane.Stride;
            int sourceStride = sourcePlane.Stride;
            bool highBitDepth = this.bitDepth != Av1BitDepth.EightBit;
            long cost = 0;

            // Each tile uses the predictions of the tiles before it, with no quantization and no inverse transform. Each prediction goes into
            // the frame, and a later tile reads its edges there. As a result, a rejected mode leaves its samples in the frame.
            for (int y = 0; y < visibleHeight; y += tileSize)
            {
                for (int x = 0; x < visibleWidth; x += tileSize)
                {
                    this.PrepareTransformReferenceSamples(
                        frameBlock,
                        frameStride,
                        blockOrigin,
                        blockSize,
                        macroBlock,
                        partitionType,
                        y / tileSize,
                        x / tileSize,
                        frameStride,
                        transformSize,
                        0,
                        0,
                        frameBlock,
                        aboveStorage,
                        leftStorage,
                        mode.IsDirectional(),
                        out bool hasLeft,
                        out bool hasAbove);

                    Point transformOrigin = new(blockOrigin.X + x, blockOrigin.Y + y);
                    ReadOnlySpan<TSample> sourceTransform = sourceSamples[sourcePlane.GetOffset(transformOrigin.X, transformOrigin.Y)..];
                    ReadOnlySpan<TSample> above = aboveStorage.Slice(1, 2 * tileSize);
                    ReadOnlySpan<TSample> left = leftStorage.Slice(1, 2 * tileSize);

                    // The tile predicts straight into the frame, after its edges were read, and its residual reads it there.
                    Span<TSample> prediction = frameBlock[((y * frameStride) + x)..];
                    if (filterMode == Av1FilterIntraMode.AllFilterIntraModes)
                    {
                        TOperator.PrepareIntra(
                            transformWorkspace,
                            sourceTransform,
                            sourceStride,
                            prediction,
                            frameStride,
                            above,
                            left,
                            hasLeft,
                            hasAbove,
                            mode,
                            angleDelta,
                            enableEdgeFilter,
                            smoothEdges,
                            residual,
                            transformSize,
                            this.bitDepth);
                    }
                    else
                    {
                        TOperator.PrepareFilterIntra(
                            transformWorkspace,
                            sourceTransform,
                            sourceStride,
                            prediction,
                            frameStride,
                            above,
                            left,
                            residual,
                            filterMode,
                            transformSize,
                            this.bitDepth);
                    }

                    // The residual of a tile that crosses the picture edge is padded the same way as the coded
                    // residual, so the model cost sees the same values that the final encode sees.
                    Av1TransformBlockEncoder.PadBorderResidual(
                        this.blockWorkspace, Av1Plane.Y, transformOrigin, residual, tileSize, tileSize, tileSize, Av1TransformType.DctDct);

                    cost += Av1ForwardTransformer.GetHadamardCost(residual, tileSize, tileSize, highBitDepth, transformCoefficients, transformWorkspace);
                }
            }

            return cost;
        }

        /// <summary>
        /// Reads the luma block values that depend only on the block and its neighbors, not on the mode that is tried.
        /// </summary>
        /// <param name="tables">The rate tables of the tile.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="macroBlock">The block and its neighbor availability.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <returns>The values that every mode trial of the block shares.</returns>
        private LumaBlockState GetLumaBlockState(
            in Av1CoefficientTables tables,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize)
        {
            // Only an intra frame codes the luma mode with neighbor contexts. In an inter frame a neighbor can hold an
            // inter mode, which has no intra context, so the contexts stay zero there.
            bool intraFrame = this.picture.Parent.FrameHeader.IsIntra;
            byte topModeContext = 0;
            byte leftModeContext = 0;
            if (intraFrame)
            {
                Av1TileWriter.GetYModeContext(modeInfoGrid, modeInfoAllocation, macroBlock, out topModeContext, out leftModeContext);
            }

            int skipContext = Av1TileWriter.GetSkipContext(modeInfoGrid, modeInfoAllocation, macroBlock);
            return new(
                macroBlock.GetRelativeModeInfo(modeInfoGrid, modeInfoAllocation, 0).Block.PartitionType,
                this.UseSmoothIntraEdges(modeInfoGrid, modeInfoAllocation, macroBlock, blockOrigin, blockSize, Av1Plane.Y),
                Av1SymbolEncoder.GetSkipCost(tables.ModeCosts, false, skipContext),
                intraFrame ? 0 : tables.ModeCosts.GetIntraInter(Av1TileWriter.GetIntraInterContext(modeInfoGrid, modeInfoAllocation, macroBlock), 0),
                topModeContext,
                leftModeContext);
        }

        /// <summary>
        /// Keeps the lowest model costs seen so far and decides if a mode is too expensive to search further.
        /// </summary>
        /// <param name="modelCost">The model cost of the mode.</param>
        /// <param name="mode">The intra prediction mode.</param>
        /// <param name="hasLeft">Whether the left neighbor is available.</param>
        /// <param name="leftMode">The mode of the left neighbor.</param>
        /// <param name="hasAbove">Whether the above neighbor is available.</param>
        /// <param name="aboveMode">The mode of the above neighbor.</param>
        /// <param name="qIndex">The quantizer index of the block.</param>
        /// <param name="topModelCosts">The lowest model costs so far, in increasing order.</param>
        /// <param name="topModelCount">The number of costs that <paramref name="topModelCosts"/> keeps.</param>
        /// <param name="adaptToNeighbors">Whether a mode that differs from the neighbor modes is pruned more.</param>
        /// <param name="bestModelCost">The lowest model cost so far.</param>
        /// <returns><see langword="true"/> if the mode is pruned.</returns>
        private static bool ShouldPruneIntraModel(
            long modelCost,
            Av1PredictionMode mode,
            bool hasLeft,
            Av1PredictionMode leftMode,
            bool hasAbove,
            Av1PredictionMode aboveMode,
            int qIndex,
            Span<long> topModelCosts,
            int topModelCount,
            bool adaptToNeighbors,
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
            if (adaptToNeighbors)
            {
                bool leftDiffers = hasLeft && leftMode != mode;
                bool aboveDiffers = hasAbove && aboveMode != mode;

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
        {
            Av1FilterIntraMode derivedMode = bestMode switch
            {
                Av1PredictionMode.Vertical => Av1FilterIntraMode.Vertical,
                Av1PredictionMode.Horizontal => Av1FilterIntraMode.Horizontal,
                Av1PredictionMode.Directional157Degrees => Av1FilterIntraMode.Directional157,
                Av1PredictionMode.Paeth => Av1FilterIntraMode.Paeth,
                _ => Av1FilterIntraMode.DC
            };

            return filterIntraMode == Av1FilterIntraMode.DC || filterIntraMode == derivedMode;
        }

        /// <summary>
        /// Builds the directional-mode skip mask that the gradient histogram pruning level of the speed settings selects.
        /// </summary>
        /// <param name="modeWorkspace">The mode decision buffers, which hold the gradient cache of the superblock.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="sourcePlane">The source luma plane.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="visibleHeight">The number of source rows inside the coded image.</param>
        /// <param name="visibleWidth">The number of source columns inside the coded image.</param>
        /// <returns>The bit mask for the eight directional modes, or zero when HOG pruning is disabled.</returns>
        private byte GetDirectionalModeSkipMask(
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Av1PlaneRegion<TSample> sourcePlane,
            Point blockOrigin,
            int visibleHeight,
            int visibleWidth)
        {
            int pruningLevel = this.picture.Parent.SpeedSettings.IntraHogPruningLevel;
            if (pruningLevel == 0)
            {
                return 0;
            }

            // Inter pictures use stronger directional pruning than independent and key pictures.
            float threshold = this.picture.Parent.FrameHeader.IsIntra
                ? pruningLevel == 4 ? 0.4F : pruningLevel == 3 ? -0.6F : -1.2F
                : pruningLevel == 4 ? 1.2F : pruningLevel == 1 ? -1.2F : 0F;

            return this.GetBlockDirectionalModeSkipMask(
                in modeWorkspace, Av1PlaneType.Y, sourcePlane, sourceLuma, blockOrigin, visibleHeight, visibleWidth, 1, threshold);
        }

        /// <summary>
        /// Codes one transform block of the selected mode and adds its reconstruction to the frame.
        /// </summary>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionLuma">The samples of the complete reconstructed luma plane, read once per frame pass.</param>
        /// <param name="reconstructionBlue">The samples of the complete reconstructed blue-difference plane, read once per frame pass.</param>
        /// <param name="reconstructionRed">The samples of the complete reconstructed red-difference plane, read once per frame pass.</param>
        /// <param name="writer">The tile symbol encoder that prices the coefficients.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the caller read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="planeCoefficients">The coefficients of the plane of the superblock.</param>
        /// <param name="planeStates">The transform block states of the plane of the superblock.</param>
        /// <param name="context">The coefficient context of the transform block.</param>
        /// <param name="isInter">Whether the block is an inter block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="planeOrigin">The transform block origin in plane samples.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="transformSize">The transform size.</param>
        /// <param name="prediction">
        /// The prediction of the transform block. When it is the frame at the transform block, the frame needs no copy of it.
        /// </param>
        /// <param name="predictionStride">The stride of the prediction.</param>
        /// <param name="residual">The residual of the transform block.</param>
        /// <param name="inputStride">The stride of the residual.</param>
        /// <param name="selectedState">The transform type and contexts that the search selected.</param>
        /// <param name="skipTransform">Whether the block skips its residual.</param>
        /// <param name="coefficientOffset">The offset of the transform block in the plane coefficients.</param>
        private void ReconstructSelectedTransform(
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
            Span<TSample> reconstructionLuma,
            Span<TSample> reconstructionBlue,
            Span<TSample> reconstructionRed,
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            Span<int> planeCoefficients,
            Span<Av1EncoderTransformBlockState> planeStates,
            Av1TransformBlockContext context,
            bool isInter,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Point planeOrigin,
            Av1Plane plane,
            Av1TransformSize transformSize,
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            Span<short> residual,
            int inputStride,
            Av1EncoderTransformBlockState selectedState,
            bool skipTransform,
            int coefficientOffset)
        {
            Av1PlaneRegion<TSample> destinationPlane = this.reconstruction.GetPlane(plane);
            Span<TSample> destination = Av1TransformBlockEncoder.GetPlaneSpan(
                SelectPlane(plane, reconstructionLuma, reconstructionBlue, reconstructionRed), destinationPlane, planeOrigin);

            // An intra block predicts straight into the frame, so only a prediction held elsewhere is copied there.
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            if (!Unsafe.AreSame(ref MemoryMarshal.GetReference(prediction), ref MemoryMarshal.GetReference(destination)))
            {
                for (int row = 0; row < height; row++)
                {
                    prediction.Slice(row * predictionStride, width).CopyTo(destination.Slice(row * destinationPlane.Stride, width));
                }
            }

            Span<int> coefficients = planeCoefficients.Slice(coefficientOffset, transformSize.GetSize2d());
            ref Av1EncoderTransformBlockState state = ref planeStates[coefficientOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];

            bool lossless = this.BlockLossless;
            state = default;
            state.EntropyContext = lossless
                ? (byte)(context.SkipContext | (context.DcSignContext << 4))
                : selectedState.EntropyContext;

            // The block encode transforms, quantizes and optimizes every transform block of a block that the mode decision did not mark as
            // skipped, even when the winning search candidate left it empty. Only the block skip flag suppresses the residual.
            if (skipTransform)
            {
                coefficients.Clear();
                state.TransformType = Av1TransformType.DctDct;
                return;
            }

            // The mode and transform decisions are already fixed. This code generates their coefficients and adds the inverse transform
            // directly to the frame, without another distortion scan or candidate copy. The residual uses the border padding of the
            // selected type.
            int planeIndex = (int)plane;
            Av1TransformBlockEncoder.PadBorderResidual(
                this.blockWorkspace, plane, planeOrigin, residual, inputStride, width, height, selectedState.TransformType);

            // At high bit depth sharpness 3, the trellis of a luma transform block tests the whole block in the frame for a noise pattern.
            // The frame holds the reconstruction of the earlier transform blocks, the prediction of this one, and what the search left
            // after it.
            this.blockWorkspace.LumaNoisePattern = plane == Av1Plane.Y && this.IsLumaNoisePattern(
                sourceLuma, sourceBlue, sourceRed, reconstructionLuma, reconstructionBlue, reconstructionRed, destinationPlane, blockOrigin, blockSize);

            Av1TransformBlockEncoder.EncodeLossyCandidate(
                this.blockWorkspace,
                writer,
                in tables,
                transformCoefficients,
                dequantizedCoefficients,
                transformWorkspace,
                context,
                residual,
                inputStride,
                coefficients,
                transformSize,
                selectedState.TransformType,
                this.blockQIndex,
                this.BlockLossless,
                this.quantization.DeltaQDc[planeIndex],
                this.quantization.DeltaQAc[planeIndex],
                this.bitDepth,
                plane == Av1Plane.Y ? Av1ComponentType.Luminance : Av1ComponentType.Chroma,
                this.rateMultiplier,
                isInter,
                this.picture.Parent.SpeedSettings.UseChromaTrellisRateMultiplier,
                true,
                0,
                ref state);

            this.blockWorkspace.LumaNoisePattern = false;
            if (state.EndOfBlock > 0)
            {
                TOperator.AddSelectedResidual(
                    dequantizedCoefficients,
                    transformWorkspace,
                    destination,
                    destinationPlane.Stride,
                    transformSize,
                    plane,
                    this.bitDepth,
                    this.BlockLossless,
                    state);
            }
            else if (plane == Av1Plane.Y && !this.keepSearchedZeroBlockTypes)
            {
                // A luma transform block that quantized to nothing returns to DCT_DCT. Thus a later pass over the same block transforms it
                // with the default type, not with the type that this search picked.
                state.TransformType = Av1TransformType.DctDct;
            }
        }

        /// <summary>
        /// Holds the luma block values that depend only on the block and its neighbors. A mode search reads them
        /// once before it tries its modes and transform sizes.
        /// </summary>
        private readonly struct LumaBlockState
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="LumaBlockState"/> struct.
            /// </summary>
            /// <param name="partitionType">The partition type of the block.</param>
            /// <param name="smoothEdges">Whether a neighbor of the block uses a smooth mode.</param>
            /// <param name="noSkipRate">The rate of the flag that says the block has coefficients.</param>
            /// <param name="intraInterRate">The rate of the flag that says the block is intra, or zero in an intra frame.</param>
            /// <param name="topModeContext">The luma mode context of the block above.</param>
            /// <param name="leftModeContext">The luma mode context of the block to the left.</param>
            public LumaBlockState(
                Av1PartitionType partitionType,
                bool smoothEdges,
                int noSkipRate,
                int intraInterRate,
                byte topModeContext,
                byte leftModeContext)
            {
                this.PartitionType = partitionType;
                this.SmoothEdges = smoothEdges;
                this.NoSkipRate = noSkipRate;
                this.IntraInterRate = intraInterRate;
                this.TopModeContext = topModeContext;
                this.LeftModeContext = leftModeContext;
            }

            /// <summary>
            /// Gets the luma mode context of the block above.
            /// </summary>
            public byte TopModeContext { get; }

            /// <summary>
            /// Gets the luma mode context of the block to the left.
            /// </summary>
            public byte LeftModeContext { get; }

            /// <summary>
            /// Gets the partition type of the block.
            /// </summary>
            public Av1PartitionType PartitionType { get; }

            /// <summary>
            /// Gets a value indicating whether a neighbor of the block uses a smooth mode, which selects the edge filter strength.
            /// </summary>
            public bool SmoothEdges { get; }

            /// <summary>
            /// Gets the rate of the flag that says the block has coefficients.
            /// </summary>
            public int NoSkipRate { get; }

            /// <summary>
            /// Gets the rate of the flag that says the block is intra, or zero in an intra frame.
            /// </summary>
            public int IntraInterRate { get; }
        }

        /// <summary>
        /// Holds the spans and block values that every luma model trial of one block uses. The mode search builds
        /// it once before its mode loop, so no trial reads a plane, a workspace buffer or the mode grid again.
        /// </summary>
        private readonly ref struct LumaModelInputs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="LumaModelInputs"/> struct.
            /// </summary>
            /// <param name="modeWorkspace">The mode decision workspace of the block.</param>
            /// <param name="transformCoefficients">The forward transform output of the block.</param>
            /// <param name="transformWorkspace">The transform intermediate buffer of the block.</param>
            /// <param name="sourceSamples">The samples of the complete source luma plane, read once by the caller.</param>
            /// <param name="reconstructionPlane">The reconstructed luma plane.</param>
            /// <param name="reconstructionSamples">The samples of the complete reconstructed luma plane, read once by the caller.</param>
            /// <param name="blockOrigin">The block origin in luma samples.</param>
            /// <param name="partitionType">The partition type of the block.</param>
            /// <param name="smoothEdges">Whether a neighbor of the block uses a smooth mode, which selects the edge filter strength.</param>
            /// <param name="enableEdgeFilter">Whether the sequence enables the intra edge filter.</param>
            public LumaModelInputs(
                in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
                Span<int> transformCoefficients,
                Span<int> transformWorkspace,
                ReadOnlySpan<TSample> sourceSamples,
                Av1PlaneRegion<TSample> reconstructionPlane,
                Span<TSample> reconstructionSamples,
                Point blockOrigin,
                Av1PartitionType partitionType,
                bool smoothEdges,
                bool enableEdgeFilter)
            {
                this.Residual = modeWorkspace.Residual;
                this.AboveStorage = modeWorkspace.GetReferenceSamples(2);
                this.LeftStorage = modeWorkspace.GetReferenceSamples(3);
                this.TransformCoefficients = transformCoefficients;
                this.TransformWorkspace = transformWorkspace;
                this.SourceSamples = sourceSamples;
                this.FrameBlock = reconstructionSamples[reconstructionPlane.GetOffset(blockOrigin.X, blockOrigin.Y)..];
                this.PartitionType = partitionType;
                this.SmoothEdges = smoothEdges;
                this.EnableEdgeFilter = enableEdgeFilter;
            }

            /// <summary>
            /// Gets the buffer that receives each tile residual.
            /// </summary>
            public Span<short> Residual { get; }

            /// <summary>
            /// Gets the buffer for the corner sample and the top edge of a tile.
            /// </summary>
            public Span<TSample> AboveStorage { get; }

            /// <summary>
            /// Gets the buffer for the corner sample and the left edge of a tile.
            /// </summary>
            public Span<TSample> LeftStorage { get; }

            /// <summary>
            /// Gets the buffer for the Hadamard coefficients.
            /// </summary>
            public Span<int> TransformCoefficients { get; }

            /// <summary>
            /// Gets the intermediate buffer of the Hadamard transform.
            /// </summary>
            public Span<int> TransformWorkspace { get; }

            /// <summary>
            /// Gets all samples of the source luma plane.
            /// </summary>
            public ReadOnlySpan<TSample> SourceSamples { get; }

            /// <summary>
            /// Gets the reconstructed luma plane from the block origin. Each tile reads its edges here and then predicts into it.
            /// </summary>
            public Span<TSample> FrameBlock { get; }

            /// <summary>
            /// Gets the partition type of the block.
            /// </summary>
            public Av1PartitionType PartitionType { get; }

            /// <summary>
            /// Gets a value indicating whether a neighbor of the block uses a smooth mode.
            /// </summary>
            public bool SmoothEdges { get; }

            /// <summary>
            /// Gets a value indicating whether the sequence enables the intra edge filter.
            /// </summary>
            public bool EnableEdgeFilter { get; }
        }

        /// <summary>
        /// One asymmetric sub-block's cached luma decision.
        /// </summary>
        private struct Av1AsymmetricModeCacheEntry
        {
            /// <summary>
            /// Whether a source candidate produced this decision.
            /// </summary>
            public bool Active;

            /// <summary>
            /// The luma prediction mode that the source candidate selected.
            /// </summary>
            public Av1PredictionMode Mode;

            /// <summary>
            /// The filter-intra mode of the source candidate, or
            /// <see cref="Av1FilterIntraMode.AllFilterIntraModes"/> when it used none.
            /// </summary>
            public Av1FilterIntraMode FilterIntraMode;

            /// <summary>
            /// The first reference of the source candidate, or <see cref="Av1ReferenceFrameType.Intra"/> for an intra
            /// decision.
            /// </summary>
            public Av1ReferenceFrameType ReferenceFrame;

            /// <summary>
            /// The second reference of a compound source candidate. For any other candidate, a value that is not an inter reference.
            /// </summary>
            public Av1ReferenceFrameType SecondaryReferenceFrame;
        }

        private struct LumaCandidate
        {
            public Av1PredictionMode Mode;
            public int AngleDelta;
            public Av1FilterIntraMode FilterMode;
            public Av1EncoderPaletteInfo Palette;
            public int PaletteHeaderRate;
            public long Cost;
        }
    }
}
