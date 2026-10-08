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
        /// <summary>
        /// The number of combined reference types: the single references and every compound pair.
        /// Reference: MODE_CTX_REF_FRAMES.
        /// </summary>
        private const int ModeContextReferenceFrameCount = Av1Constants.ReferenceFrameCount + (4 * 3) + 9;

        /// <summary>
        /// The leaf bound that stands for a bound already negative at the multiplier of the caller, with which the
        /// leaf search returns before it sets up the block. Reference: the use_best_rd_for_pruning return of
        /// pick_sb_modes().
        /// </summary>
        private const long NegativeLeafBound = long.MinValue;

        private readonly Av1EncoderFrame<TSample>.PlanarView source;
        private readonly ReadOnlyMemory<Av1EncoderFrame<TSample>> references;

        /// <summary>
        /// The frames the motion search reads, indexed by reference type: each reference, or its copy resized to the
        /// size of the coded frame when the reference has another size. Reference: av1_get_scaled_ref_frame().
        /// </summary>
        private readonly ReadOnlyMemory<Av1EncoderFrame<TSample>> searchReferences;

        /// <summary>
        /// One bit per available reference type whose frame has another size than the current frame, which the
        /// prediction scales and the motion search reads through its resized copy. Reference: av1_is_scaled() of the
        /// reference scale factors.
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

        // The quantizer of the superblock: the frame quantizer moved by any delta quantizer. Reference: the
        // current_qindex of setup_delta_q().
        private readonly int superblockQIndex;

        // The quantizer of the current block or partition node: the superblock quantizer moved by the segment of the
        // last block setup. Reference: x->qindex from av1_init_plane_quantizers().
        private int blockQIndex;

        // The segment of the block being searched. Reference: mbmi->segment_id in pick_sb_modes().
        private int blockSegmentId;

        // The libaom encode that a block of the estimated inter search stands for, which decides its cyclic refresh
        // update. Reference: the dry_run of encode_b_nonrd() and the pick_sb_modes_nonrd() calls of try_merge().
        private EstimatedLeafEncode estimatedLeafEncode;

        // The cost of the unsplit block that a split trial child of a merge trial compares against. Reference:
        // none_rdc.rdcost in try_merge().
        private long mergeNoneCost;

        // The split cost of the children before the current split trial child. Reference: split_rdc in try_merge().
        private Av1RateDistortionStatistics mergeSplitStatistics;

        // The position of the current split trial child, from 0 to 3. Reference: i in the split loop of try_merge().
        private int mergeChildIndex;

        // The multiplier the merge trial prices its costs with. Reference: x->rdmult in try_merge().
        private int mergeRateMultiplier;

        /// <summary>
        /// The prediction error of the best new vector of each single reference in the block being searched, or
        /// <see cref="int.MaxValue"/> before one is found. Reference: best_single_sse_in_refs.
        /// </summary>
        private InlineArray8<uint> bestSingleReferenceSses;

        /// <summary>
        /// The luma prediction error that the last model or fractional search of each first reference left in the block
        /// being searched, or <see cref="int.MaxValue"/> before one. A forced integer vector skips the fractional search
        /// and reads the value an earlier candidate left. Reference: x->pred_sse.
        /// </summary>
        private InlineArray8<uint> predictionSses;

        /// <summary>
        /// The best estimate of the mode loop when the transform search of the retained candidates found no mode
        /// below the block budget, or invalid otherwise. The skip mode comparison still reads it. Reference: the
        /// rd_cost that update_search_state() fills during the mode loop, which tx_search_best_inter_candidates()
        /// keeps when it resets best_rd and best_mode_index.
        /// </summary>
        private Av1RateDistortionStatistics leftoverInterEstimate;

        /// <summary>
        /// The luma cost that an intra mode of an inter frame must beat: the luma cost of the inter winner, or, after
        /// the transform search of the retained candidates, the luma cost of the cheapest candidate that search
        /// completed, even one above the block budget. Reference: best_inter_yrd in av1_rd_pick_inter_mode(), which
        /// tx_search_best_inter_candidates() resets and sets from best_rd_in_this_partition, and the yrd_threshold of
        /// search_intra_modes_in_interframe().
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

        // The cost of the last completed inter trial before the image tune bias. Reference: curr_rd in motion_mode_rd().
        private long unbiasedInterTrialCost;

        // The sharpness 3 distortion offset of the luma samples that the intra luma search of an inter frame leaves
        // for its chroma search. Reference: the pd->dst that adjust_rdcost() reads on intra_rd_stats in
        // search_intra_modes_in_interframe().
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
        /// Set while the estimated real-time search predicts each reference of another size from its copy resized to
        /// the frame size, without scaling. The selected block then predicts from the reference itself. Reference:
        /// the sf_no_scale that av1_nonrd_pick_inter_mode_sb() gives block_ref_scale_factors for a scaled
        /// reference, and the rebuilt prediction of encode_superblock().
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

        // The skip flag that the last completed transform search of the inter mode search leaves behind. The final
        // encode of an inter winner adds it to the winner's own flags. Reference: x->txfm_search_info.skip_txfm.
        private bool transformSearchSkip;

        // Whether a motion mode trial of the current mode search reached its transform search; each such trial clears
        // the leftover skip flag before searching. Reference: the txfm_info->skip_txfm reset of each mode_index in
        // motion_mode_rd().
        private bool transformSearchReset;

        // The reference types that a rectangular block does not search, one bit per reference type.
        // Reference: skip_ref_frame_mask of av1_rd_pick_inter_mode().
        private int skipReferenceFrameMask;

        // The predicted-vector SAD of each reference, and the best of the references that precede and follow the
        // frame. Reference: x->pred_mv_sad and x->best_pred_mv_sad.
        private InlineArray8<int> predictionVectorSads;
        private int bestPastPredictionVectorSad;
        private int bestFuturePredictionVectorSad;
        private bool searchingRetainedCandidates;
        private long interSourceVarianceCost;
        private int interSourceVariance;
        private bool mustFindValidPartition;

        // The selected reference-coded block kept no coefficient, so the frame grid marks it skipped while the
        // partition context keeps the searched flag. Reference: the skip_txfm initialization of av1_encode_sb().
        private bool encodedWithoutCoefficients;

        // Set while a leaf is reconstructed for the partition search. The mode search keeps the types it
        // searched; only an encode of the decision writes DCT_DCT for a luma block that quantized to nothing.
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
        private int forceZeroMotionLevel;
        private Av1ReferenceFrameType partitionReference;
        private bool usePartitionMotion;

        /// <summary>
        /// The inter-prediction buffer that holds the luma prediction the estimated search built last, or -1.
        /// </summary>
        private int lastLumaPredictionBuffer = -1;

        /// <summary>
        /// The color sensitivity of the block searched last, after its block-level check. Reference:
        /// x->color_sensitivity, which av1_nonrd_pick_inter_mode_sb() copies from x->color_sensitivity_sb and
        /// set_color_sensitivity() resolves.
        /// </summary>
        private InlineArray2<byte> blockColorSensitivity;
        private int estimatedReferencePruning;
        private InlineArray2<byte> superblockColorSensitivity;

        // Whether each 64x64 unit of the superblock still leaves CDEF off, in raster order.
        // Reference: the cdef_strength flag that encode_nonrd_sb() sets and pick_sb_modes_nonrd() narrows.
        private InlineArray4<bool> cdefSkipUnits;
        private InlineArray2<byte> goldenColorSensitivity;
        private InlineArray2<byte> alternateColorSensitivity;
        private InlineArray2<uint> superblockChromaSad;

        /// <summary>
        /// Whether the temporal dependency model keeps each reference type, INTRA to ALTREF, from the selective
        /// reference pruning in the current superblock. Reference: x->tpl_keep_ref_frame.
        /// </summary>
        private InlineArray8<bool> tplKeepReferenceFrames;

        /// <summary>
        /// The number of 16x16 model blocks of the superblock inside the frame whose costs and vectors the block
        /// workspace holds, or zero without them. Reference: sb_enc->tpl_data_count.
        /// </summary>
        private int tplSuperblockBlockCount;

        /// <summary>
        /// The number of model blocks per superblock row of the gathered costs and vectors. Reference:
        /// sb_enc->tpl_stride.
        /// </summary>
        private int tplSuperblockStride;

        /// <summary>
        /// Whether the inter mode search of the current block skips modes by the model's reference costs. Reference:
        /// prune_modes_based_on_tpl in handle_inter_mode().
        /// </summary>
        private bool tplInterModePruning;

        /// <summary>
        /// The model prediction error of each reference LAST to ALTREF summed over the current block. Reference: the
        /// ref_inter_cost of PruneInfoFromTpl.
        /// </summary>
        private InlineArray7<long> tplReferenceInterCosts;

        /// <summary>
        /// The smallest nonzero entry of <see cref="tplReferenceInterCosts"/> among the references the selective
        /// pruning keeps. Reference: the best_inter_cost of PruneInfoFromTpl.
        /// </summary>
        private long tplBestInterCost;

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

            // The simple motion searches read ALTREF in a frame coded from its source, and LAST otherwise. They, the
            // variance partition, and the estimated real-time search read a resized reference through its copy at
            // the frame size. Reference: the is_src_frame_alt_ref choice of ref_list in
            // av1_simple_motion_search_based_split(), and av1_get_scaled_ref_frame().
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
            blockWorkspace.SetQuantizationMatrixLevels(picture.Parent.FrameHeader.QuantizationParameters);

            // A partition never exceeds the superblock it sits in, so the speed cap comes down to the
            // superblock size before anything reads it. Reference: the second AOMMIN of
            // set_max_min_partition_size(), against cm->seq_params->sb_size.
            this.maximumPartitionSize = (Av1BlockSize)Math.Min(
                (int)picture.Parent.SpeedSettings.MaximumPartitionSize,
                (int)picture.Sequence.SequenceHeader.SuperblockSize);

            this.blockCostLimit = long.MaxValue;

            // Each superblock starts without a block variance, which reads as the largest unsigned value until a block
            // search measures one. Reference: the x->source_variance = UINT_MAX of encode_sb_row().
            this.interSourceVariance = -1;
            blockWorkspace.SourceLogVariances.Fill(-1D);
            blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Default;
            this.quantization = picture.Parent.FrameHeader.QuantizationParameters;
            this.bitDepth = picture.Sequence.SequenceHeader.ColorConfig.BitDepth;

            // A delta quantizer mode picks the superblock quantizer, rounded to the delta resolution against the
            // previous coded superblock of the tile. The full search also measures its rate multiplier at that
            // quantizer; the estimated search keeps the frame multiplier. Reference: setup_delta_q(),
            // setup_delta_q_nonrd() and the av1_get_cb_rdmult() call of setup_block_rdmult().
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
                    // The quantizer follows the superblock's importance, and the regularized importance it leaves
                    // scales the coding block rate multipliers. Reference: av1_get_q_for_deltaq_objective() in
                    // setup_delta_q().
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

                // Only the full search updates the anchor after each coded superblock; the estimated search keeps
                // the frame quantizer as the anchor for the whole tile. Reference: the current_base_qindex update of
                // encode_b(), which encode_b_nonrd() does not make.
                bool estimated = picture.Sequence.SequenceHeader.IsStillPicture
                    ? picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level8
                    : picture.Parent.SpeedSettings.IsRealtime;

                int anchorQIndex = estimated ? baseQIndex : picture.Parent.PreviousQIndex.Span[superblock.TileIndex];
                this.superblockQIndex = Av1VarianceBoost.AdjustToResolution(deltaQ.Resolution, anchorQIndex, wantedQIndex);
                picture.Parent.SuperblockDeltaQIndex = this.superblockQIndex - baseQIndex;
                picture.Parent.DeltaQUsed |= this.superblockQIndex != baseQIndex;
                if (!estimated)
                {
                    rateQIndex = this.superblockQIndex;
                }

                // The quantizer setup derives the motion vector error per bit from the multiplier at the superblock
                // quantizer, without the coding block scaling. Reference: the av1_set_error_per_bit() call of
                // av1_init_plane_quantizers(), which setup_delta_q() and setup_delta_q_nonrd() make.
                blockWorkspace.ErrorPerBitRateMultiplier = parent.GetRateMultiplier(
                    this.superblockQIndex + this.quantization.DeltaQDc[0], this.bitDepth);
            }

            this.blockQIndex = this.superblockQIndex;

            // The multiplier at the frame quantizer is cpi->rd.RDMULT; at the superblock quantizer it is
            // set_rdmult(cpi, x, -1). Both take the layer depth and golden boost of stat consumption.
            this.baseRateMultiplier = parent.GetRateMultiplier(rateQIndex + this.quantization.DeltaQDc[0], this.bitDepth);

            // The superblock starts from the references the temporal dependency model keeps against the selective
            // reference pruning, and from none without statistics or with adaptive quantization. Reference:
            // init_ref_frame_space() in init_encode_rd_sb().
            this.tplKeepReferenceFrames[..].Clear();

            // Every superblock starts without model blocks, so a frame or superblock without statistics never reads
            // the vectors an earlier superblock gathered. Reference: the tpl_data_count reset at the start of
            // av1_get_tpl_stats_sb() and after av1_rd_pick_partition() in encode_rd_sb().
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

                // The recursive partition search gathers the model costs and vectors of the superblock's 16x16
                // blocks; the variance-based partition search does not. Reference: the av1_get_tpl_stats_sb() call
                // of encode_rd_sb().
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

            // rd_pick_partition measures the superblock at its root. The
            // variance-based partition search of the fastest speeds does not run it, so its rate weight
            // stays at 128 there.
            this.rateMultiplierModifier = 128;
            if (picture.Sequence.SequenceHeader.IsStillPicture && !UsesGivenPartition(picture))
            {
                // Measure 4x4 source variation once for the entire superblock. Mixed flat and detailed
                // regions need a lower rate weight, shared by every partition and mode decision below it.
                int superblockSize = 1 << picture.Sequence.SequenceHeader.SuperblockSizeLog2;
                int originX = (superblock.Index % coefficientBuffer.SuperblockColumnCount) * superblockSize;
                int originY = (superblock.Index / coefficientBuffer.SuperblockColumnCount) * superblockSize;
                int right = Math.Min(originX + superblockSize, picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2);
                int bottom = Math.Min(originY + superblockSize, picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);
                (double minimumLogVariance, double maximumLogVariance) = this.GetLogSubBlockVariance(
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
                // Activity compares successive source pictures, so quantization noise in reconstructed
                // references cannot make a stationary source look like motion. Border samples complete
                // the superblock at the right and bottom edges without a separate clipped-block rule.
                int side = picture.Sequence.SequenceHeader.SuperblockSize.GetWidth();
                Point origin = new(
                    (superblock.Index % coefficientBuffer.SuperblockColumnCount) * side,
                    (superblock.Index / coefficientBuffer.SuperblockColumnCount) * side);

                int columns = (picture.Parent.FrameHeader.FrameSize.FrameWidth + 63) >> 6;
                int rows = (picture.Parent.FrameHeader.FrameSize.FrameHeight + 63) >> 6;
                int column = origin.X >> 6;
                int row = origin.Y >> 6;
                ulong cachedSad = ulong.MaxValue;

                // Scene detection keeps no block errors for a frame whose encoder size is not the image size. Reference:
                // the NULL src_sad_blk_64x64 test of get_sb_source_sad().
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
                    if (squaredError != 0 && !picture.Parent.IsScreenContent && !picture.Parent.HighSourceSad &&
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
                (!picture.Parent.FrameHeader.IsIntra && picture.Parent.SpeedSettings.GetEstimatedPartitionMergeLevel(picture.Parent.IsScreenContent) != 0))
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
        /// Gets a value indicating whether the frame decides its blocks with the estimated inter-frame search, which
        /// encodes each winner in place. Reference: the use_nonrd_pick_mode branch of pick_sb_modes_nonrd().
        /// </summary>
        private readonly bool UsesEstimatedInterSearch =>
            !this.picture.Parent.FrameHeader.IsIntra && this.picture.Parent.SpeedSettings.UseEstimatedInterModeDecision;

        /// <summary>
        /// Gets a value indicating whether the full-pixel searches cap their first mesh interval: screen content
        /// alternate references at good quality speeds up to 2. Reference: use_fine_search_interval().
        /// </summary>
        private readonly bool UsesFineSearchInterval =>
            this.picture.Parent.IsScreenContent &&
            this.picture.Parent.FrameUpdateType == Av1FrameUpdateType.Alternate &&
            !this.picture.Parent.SpeedSettings.IsRealtime &&
            this.picture.Parent.EncodingSpeed <= HeifEncodingSpeed.Level2;

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
                    // The variance analysis reads the superblock's own edge availability, not the one the
                    // previously coded block left behind. Reference: the av1_set_offsets() call on the
                    // superblock that precedes av1_choose_var_based_partitioning().
                    Av1TileWriter.SetModeInfoRowAndColumn(
                        this.picture,
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
                        this.PrepareVariancePartitions(macroBlock, blockOrigin);
                    }

                    if (searchesLeaves)
                    {
                        // Every leaf of the superblock is searched before the writer encodes any of them.
                        // Reference: the av1_rd_use_partition() call of encode_rd_sb(), with do_recon set.
                        this.SearchVariancePartition(writer, macroBlock, blockOrigin, tileIndex, blockSize, 0, true);
                    }
                }

                Av1PartitionType variancePartition = (Av1PartitionType)this.superblock.Workspace.PartitionSearchTypes[nodeIndex];
                if (!this.picture.Parent.FrameHeader.IsIntra && variancePartition == Av1PartitionType.Split &&
                    blockSize <= Av1BlockSize.Block64x64)
                {
                    variancePartition = this.RefineEstimatedLeafPartition(writer, macroBlock, blockOrigin, tileIndex, blockSize, nodeIndex);
                }

                this.PreparePartitionGeometry(blockOrigin, blockSize, variancePartition);
                if (searchesLeaves)
                {
                    // The writer reaches each leaf in partition order and encodes the decision the search kept.
                    this.replayNodeIndex = nodeIndex;
                    this.replayPartition = variancePartition;
                    this.replayPartitionOrigin = blockOrigin;
                    this.replayParentSize = blockSize;
                }
                else if (variancePartition == Av1PartitionType.None && !this.picture.Parent.FrameHeader.IsIntra &&
                    this.picture.Parent.SpeedSettings.GetEstimatedPartitionMergeLevel(this.picture.Parent.IsScreenContent) != 0 &&
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
                    macroBlock,
                    blockOrigin,
                    tileIndex,
                    blockSize,
                    nodeIndex,
                    Av1RateDistortionStatistics.Invalid,
                    out _,
                    out _,
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

        /// <summary>
        /// Compares an unsplit block with the four leaves selected by variance partitioning.
        /// </summary>
        private Av1PartitionType RefineEstimatedLeafPartition(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            int nodeIndex)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            int mergeLevel = parent.SpeedSettings.GetEstimatedPartitionMergeLevel(parent.IsScreenContent);

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

            // try_merge() compares the two costs with x->rdmult, which still holds the preceding frame's
            // multiplier: the block searches set it and restore it.
            int mergeMultiplier = this.blockWorkspace.PreviousFrameRateMultiplier;
            int savedLumaArea = this.codedAreaLuma;
            int savedChromaArea = this.codedAreaChroma;
            this.SavePartitionTrialContexts(blockOrigin, tileIndex, blockSize);
            Av1NeighborEdges<Av1PartitionContext> partitionEdges = this.picture.PartitionContexts[tileIndex].GetEdges();
            int noneRate = Av1TileWriter.GetPartitionCost(this.picture, writer, blockSize, Av1PartitionType.None, blockOrigin, in partitionEdges);
            int splitRate = Av1TileWriter.GetPartitionCost(this.picture, writer, blockSize, Av1PartitionType.Split, blockOrigin, in partitionEdges);

            Av1EncoderPartitionTree.ModeContext noneContext = this.blockWorkspace.PartitionTree.GetContext(nodeIndex, Av1PartitionType.None, 0);
            this.estimatedLeafEncode = EstimatedLeafEncode.Search;
            Av1RateDistortionStatistics none = this.EvaluatePartitionLeaf(
                writer, macroBlock, blockOrigin, tileIndex, blockSize, Av1PartitionType.None, noneContext, long.MaxValue, false, true);

            this.estimatedLeafEncode = EstimatedLeafEncode.Output;

            // The search's skip decision, not the encoded block's. Reference: none_rdc.skip_txfm in try_merge().
            bool noneSkip = none.AllTransformsEmpty;
            Av1RateDistortionStatistics noneSyntax = new(mergeMultiplier, noneRate, 0);
            none.Add(mergeMultiplier, noneSyntax);
            this.ResetPartitionTrial(blockOrigin, tileIndex, blockSize, savedLumaArea, savedChromaArea);

            // try_merge() compares the split when the merge level is below 2, the merged block keeps a residual,
            // or it codes NEWMV; calc_do_split_flag() then decides.
            Av1PredictionMode noneMode = noneContext.Snapshot.ModeInfo.Block.Mode;
            bool evaluateSplit = false;
            if (mergeLevel < 2 || !noneSkip || noneMode == Av1PredictionMode.NewMotionVector)
            {
                bool noneBoosted = this.picture.Parent.CyclicRefresh is not null &&
                    this.picture.Parent.FrameHeader.SegmentationParameters.Enabled &&
                    Av1CyclicRefresh.IsBoosted(noneContext.Snapshot.ModeInfo.Block.SegmentId);

                evaluateSplit = this.CalculateDoSplit(mergeLevel, noneSkip, noneBoosted, noneMode, blockSize, blockOrigin);
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
                        writer, macroBlock, childOrigin, tileIndex, childSize, Av1PartitionType.None, childContext, long.MaxValue, child < 3, true);

                    this.estimatedLeafEncode = EstimatedLeafEncode.Output;

                    // A leaf that found no mode retained nothing, so nothing may read its decision back.
                    // Reference: the rd_mode_is_ready flag of pick_sb_modes(), which a caller sets only
                    // after av1_rd_pick_partition() reports a best partition.
                    childContext.Snapshot.Ready = childStatistics.Cost != long.MaxValue;
                    this.superblock.Workspace.PartitionSearchTypes[firstChild + child] = (byte)Av1PartitionType.None;
                    split.Add(mergeMultiplier, childStatistics);
                    if (none.Cost < split.Cost)
                    {
                        break;
                    }
                }

                this.ResetPartitionTrial(blockOrigin, tileIndex, blockSize, savedLumaArea, savedChromaArea);
            }

            Av1PartitionType selected = none.Cost < split.Cost ? Av1PartitionType.None : Av1PartitionType.Split;
            noneContext.Snapshot.Ready = selected == Av1PartitionType.None;
            this.superblock.Workspace.PartitionSearchTypes[nodeIndex] = (byte)selected;
            return selected;
        }

        /// <summary>
        /// Decides whether a merge trial compares the four sub-blocks. Reference: calc_do_split_flag().
        /// </summary>
        /// <param name="mergeLevel">The partition merge level. Reference: nonrd_check_partition_merge_mode.</param>
        /// <param name="noneSkip">Whether the merged block's search skipped its residual.</param>
        /// <param name="noneBoosted">Whether the merged block is in a boosted cyclic refresh segment.</param>
        /// <param name="noneMode">The merged block's selected mode.</param>
        /// <param name="blockSize">The merged block size.</param>
        /// <param name="blockOrigin">The merged block origin.</param>
        /// <returns><see langword="true"/> when the split is compared.</returns>
        private bool CalculateDoSplit(int mergeLevel, bool noneSkip, bool noneBoosted, Av1PredictionMode noneMode, Av1BlockSize blockSize, Point blockOrigin)
        {
            bool largerQuantizer = this.picture.Parent.FrameHeader.QuantizationParameters.BaseQIndex > 100;
            bool doSplit = mergeLevel != 3 || blockSize <= Av1BlockSize.Block32x32 || (largerQuantizer && blockSize <= Av1BlockSize.Block64x64);
            if (this.picture.Parent.IsScreenContent || mergeLevel < 2 || noneBoosted || !noneSkip)
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
                ReadOnlySpan<TSample> prediction = this.GetLastLumaPrediction();
                Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
                ReadOnlySpan<TSample> sourceSamples = sourcePlane.Samples;
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
        /// Runs <see cref="SelectBestPartitionCore"/> at the rate multiplier of the block. Reference: the setup_block_rdmult() call of
        /// rd_pick_partition().
        /// </summary>
        private Av1PartitionType SelectBestPartition(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            int nodeIndex,
            Av1RateDistortionStatistics costLimit,
            out Av1RateDistortionStatistics selectedStatistics,
            out long noneCost,
            out byte rectangleWins)
        {
            // A negative bound returns before the block setup. Reference: the best_rdc.rdcost test that precedes
            // setup_block_rdmult() in av1_rd_pick_partition().
            int savedRateMultiplier = this.rateMultiplier;
            if (costLimit.Cost >= 0)
            {
                // Reference: the av1_set_offsets() call of av1_rd_pick_partition().
                this.SetBlockSegment(blockOrigin, blockSize);
            }

            this.rateMultiplier = costLimit.Cost < 0
                ? this.GetBlockRateMultiplier(blockOrigin, blockSize)
                : this.SetupBlockRateMultiplier(blockOrigin, blockSize);

            // The bound arrives at the multiplier of the parent. Reference: the av1_rd_cost_update() call of
            // av1_rd_pick_partition().
            costLimit.UpdateCost(this.rateMultiplier);

            // A 16x16 node of a frame that refreshes its variance segments measures the segment its smaller blocks
            // take. Reference: the x->mb_energy update of av1_rd_pick_partition().
            if (costLimit.Cost >= 0 && blockSize == Av1BlockSize.Block16x16 && this.picture.Parent.VarianceSegmentRefresh)
            {
                this.blockWorkspace.MacroblockEnergy = (int)this.GetLogBlockVariance(blockOrigin, blockSize);
            }

            Av1PartitionType result = this.SelectBestPartitionCore(writer, macroBlock, blockOrigin, tileIndex, blockSize, nodeIndex, costLimit, out selectedStatistics, out noneCost, out rectangleWins);
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
            // Reference: the use_best_rd_for_pruning return of pick_sb_modes(), and the best_rdc test that opens
            // av1_rd_pick_partition().
            if (remainingCost.Cost < 0)
            {
                return NegativeLeafBound;
            }

            // The leaf search measures the remaining bound at its own multiplier. Reference: the av1_rd_cost_update()
            // call of pick_sb_modes().
            remainingCost.UpdateCost(this.GetBlockRateMultiplier(leafOrigin, leafSize));

            // A 4x4 child measures the bound at its own multiplier when its partition search opens, and its unsplit
            // search tests it again before the block setup. Reference: the av1_rd_cost_update() call of
            // av1_rd_pick_partition() and the best_remain_rdcost that none_partition_search() passes to pick_sb_modes().
            if (searchesOwnPartition && remainingCost.Cost < 0)
            {
                return NegativeLeafBound;
            }

            return remainingCost.Cost;
        }

        /// <summary>
        /// Gets the rate multiplier of a block: the superblock multiplier, scaled with ready temporal dependency
        /// statistics by the block's importance over the superblock's regularized importance when the frame derives
        /// coding block multipliers, then by the SSIM factors of the block for the SSIM and image tunes, then by the
        /// all-intra superblock modifier. Reference: setup_block_rdmult() with av1_get_cb_rdmult().
        /// </summary>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="segmentId">The segment whose quantizer prices the block, or -1 for the superblock quantizer.</param>
        /// <returns>The rate multiplier, at least one.</returns>
        private readonly int GetBlockRateMultiplier(Point blockOrigin, Av1BlockSize blockSize, int segmentId = -1)
        {
            int multiplier = this.GetTunedRateMultiplier(blockOrigin, blockSize, segmentId);

            // The reference widens the product before the shift.
            multiplier = (int)(((long)multiplier * this.rateMultiplierModifier) >> 7);
            return Math.Max(multiplier, 1);
        }

        /// <summary>
        /// Sets the segment and quantizer of a block or partition node when the frame uses segmentation. A frame that
        /// refreshes its variance segments searches every block in segment 0; any other frame takes the smallest
        /// segment the previous map holds under the block. Reference: the segment setup of av1_set_offsets(), with
        /// av1_init_plane_quantizers().
        /// </summary>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        private void SetBlockSegment(Point blockOrigin, Av1BlockSize blockSize)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            ObuSegmentationParameters segmentation = parent.FrameHeader.SegmentationParameters;
            this.blockSegmentId = 0;
            if (!segmentation.Enabled)
            {
                return;
            }

            // Reference: the seg->update_map choice between cpi->enc_seg.map and cm->last_frame_seg_map.
            ReadOnlySpan<byte> map = segmentation.SegmentationUpdateMap == 1
                ? parent.EncoderSegmentMap.Span
                : parent.PreviousSegmentMap.Span;

            if (!parent.VarianceSegmentRefresh && !map.IsEmpty)
            {
                this.blockSegmentId = Av1SymbolContextHelper.GetSegmentId(
                    parent.Common,
                    map,
                    blockSize,
                    new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2));
            }

            this.blockQIndex = Av1QuantizationLookup.GetQIndex(segmentation, this.blockSegmentId, this.superblockQIndex);
        }

        /// <summary>
        /// Adds the estimated rates of coding a coded block's segment spatially and against the primary reference's
        /// map. The spatial predictor reads the map that the frame buffer still holds from its previous frame, and the
        /// prediction flag context is 0 because the encoder never sets the flag. Reference: the seg_tmp_pred_cost
        /// update of av1_update_state().
        /// </summary>
        /// <param name="writer">The symbol encoder whose rates price the syntax.</param>
        /// <param name="macroBlock">The block's neighbor availability.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="segmentId">The block's segment.</param>
        private readonly void AddSegmentPredictionCosts(
            Av1SymbolEncoder writer,
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

            int prediction = Av1TileWriter.GetSpatialSegmentationPrediction(
                parent.Common, parent.SearchSegmentMap.Span, macroBlock, blockOrigin, out int context);

            int codedId = Av1SymbolContextHelper.NegativeInterleave(segmentId, prediction, segmentation.LastActiveSegmentId + 1);
            int spatialCost = writer.ModeCosts.GetSegmentId(context, codedId);
            parent.SpatialSegmentCost += spatialCost;

            int previousSegmentId = parent.PreviousSegmentMap.IsEmpty
                ? 0
                : Av1SymbolContextHelper.GetSegmentId(
                    parent.Common,
                    parent.PreviousSegmentMap.Span,
                    blockSize,
                    new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2));

            bool predicted = previousSegmentId == segmentId;
            parent.TemporalSegmentCost += writer.ModeCosts.GetSegmentIdPredicted(0, predicted ? 1 : 0);
            if (!predicted)
            {
                parent.TemporalSegmentCost += spatialCost;
            }
        }

        /// <summary>
        /// Sets the quantizer of a selected block from its coded segment before the block is coded. Reference: the
        /// av1_init_plane_quantizers() call of av1_update_state().
        /// </summary>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="segmentId">The segment the search gave the selected block.</param>
        private void SetCodedBlockSegment(Point blockOrigin, Av1BlockSize blockSize, int segmentId)
        {
            ObuSegmentationParameters segmentation = this.picture.Parent.FrameHeader.SegmentationParameters;
            if (this.picture.Parent.EncoderOptions.AdaptiveQuantizationMode != Av1AdaptiveQuantizationMode.None)
            {
                this.blockSegmentId = this.GetCodedBlockSegment(blockOrigin, blockSize, segmentId);
                this.blockQIndex = Av1QuantizationLookup.GetQIndex(segmentation, this.blockSegmentId, this.superblockQIndex);
            }
        }

        /// <summary>
        /// Returns the segment a selected block is coded with. Complexity adaptive quantization takes it from the
        /// segment map, which the search of the block and its neighbors wrote after the block's own setup. Any other
        /// mode keeps the searched segment. Reference: the COMPLEXITY_AQ segment copy of av1_update_state().
        /// </summary>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="segmentId">The segment the search gave the selected block.</param>
        /// <returns>The coded segment.</returns>
        private readonly int GetCodedBlockSegment(Point blockOrigin, Av1BlockSize blockSize, int segmentId)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            ObuSegmentationParameters segmentation = parent.FrameHeader.SegmentationParameters;
            if (!segmentation.Enabled || parent.EncoderOptions.AdaptiveQuantizationMode != Av1AdaptiveQuantizationMode.Complexity)
            {
                return segmentId;
            }

            // Reference: the seg->update_map choice between cpi->enc_seg.map and cm->last_frame_seg_map.
            ReadOnlySpan<byte> map = segmentation.SegmentationUpdateMap == 1
                ? parent.EncoderSegmentMap.Span
                : parent.PreviousSegmentMap.Span;

            return map.IsEmpty
                ? 0
                : Av1SymbolContextHelper.GetSegmentId(
                    parent.Common,
                    map,
                    blockSize,
                    new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2));
        }

        /// <summary>
        /// Returns the variance segment of a block: the mean log variance of its 4x4 luma blocks inside the
        /// mode-information grid, at most 7. A block of 16x16 or smaller takes the value its 16x16 partition node
        /// measured. Reference: the VARIANCE_AQ branch of setup_block_rdmult() with av1_log_block_var().
        /// </summary>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <returns>The segment identifier.</returns>
        private int GetVarianceSegmentId(Point blockOrigin, Av1BlockSize blockSize)
            => blockSize <= Av1BlockSize.Block16x16
                ? this.blockWorkspace.MacroblockEnergy
                : (int)this.GetLogBlockVariance(blockOrigin, blockSize);

        /// <summary>
        /// Returns the mean log variance of the 4x4 luma blocks of a block inside the mode-information grid, at most 7.
        /// Reference: av1_log_block_var().
        /// </summary>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <returns>The mean log variance.</returns>
        private double GetLogBlockVariance(Point blockOrigin, Av1BlockSize blockSize)
        {
            Av1EncoderCommon common = this.picture.Parent.Common;
            int width = Math.Min(blockSize.GetWidth(), (common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) - blockOrigin.X);
            int height = Math.Min(blockSize.GetHeight(), (common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2) - blockOrigin.Y);
            double sum = 0;
            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    sum += this.GetSourceLogVariance(new Point(blockOrigin.X + x, blockOrigin.Y + y));
                }
            }

            // The divisor counts whole 4x4 blocks, evaluated left to right. Reference: var /= (bw / 4 * bh / 4).
            sum /= width / 4 * height / 4;
            return Math.Min(sum, 7);
        }

        /// <summary>
        /// Gets the rate multiplier of a block as <see cref="GetBlockRateMultiplier"/> does, and with the SSIM and image
        /// tunes records the multiplier the motion vector error per bit derives from. Reference: setup_block_rdmult(),
        /// whose av1_set_ssim_rdmult() call sets x->errorperbit before the all-intra modifier.
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
        /// Gets the rate multiplier of a block before the all-intra superblock modifier: the superblock multiplier,
        /// scaled by the coding block importance and by the SSIM factors. Reference: setup_block_rdmult() up to its
        /// av1_set_ssim_rdmult() call.
        /// </summary>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="segmentId">The segment whose quantizer prices the block, or -1 for the superblock quantizer.</param>
        /// <returns>The rate multiplier, at least zero.</returns>
        private readonly int GetTunedRateMultiplier(Point blockOrigin, Av1BlockSize blockSize, int segmentId = -1)
        {
            // A segment prices the block at the segment quantizer of the frame quantizer, unless the coding block
            // multiplier replaces it. Adaptive quantization then keeps the superblock multiplier. Cyclic refresh
            // prices only a boosted block at the multiplier of the first boosted segment. Reference: set_rdmult() in
            // the aq branches of setup_block_rdmult(), with av1_cyclic_refresh_get_rdmult(), and the aq_mode return of
            // av1_get_cb_rdmult().
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

        private Av1PartitionType SelectBestPartitionCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            int nodeIndex,
            Av1RateDistortionStatistics costLimit,
            out Av1RateDistortionStatistics selectedStatistics,
            out long noneCost,
            out byte rectangleWins)
        {
            // The superblock retry keeps the rectangle decisions of the first search. Reference: the
            // BEGIN_PARTITION_SEARCH label of av1_rd_pick_partition(), which follows the pruning before the search,
            // and reset_part_limitations(), which leaves do_rectangular_split and prune_rect_part unchanged.
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
                this.picture,
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

                Span<Av1SimpleMotionData> nodes = this.blockWorkspace.SimpleMotionData;
                nodes.Clear();
                foreach (ref Av1SimpleMotionData node in nodes)
                {
                    node.Starts[(int)Av1ReferenceFrameType.Last] = fullStart;
                }

                Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;

                // An intra picture has no motion to predict a partition size from, so it keeps the speed cap.
                // Reference: the frame_is_intra_only() term of use_auto_max_partition().
                if (settings.MaximumPartitionPredictionMode != Av1EncoderSpeedSettings.MaximumPartitionPrediction.Disabled &&
                    !this.picture.Parent.FrameHeader.IsIntra &&
                    blockSize == Av1BlockSize.Block128x128 && !this.picture.Parent.FrameHeader.AllowScreenContentTools &&
                    this.picture.Parent.FrameUpdateType is not (Av1FrameUpdateType.Overlay or Av1FrameUpdateType.IntermediateOverlay) &&
                    blockOrigin.X + 128 <= (this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) &&
                    blockOrigin.Y + 128 <= (this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2))
                {
                    this.maximumPartitionSize = (Av1BlockSize)Math.Clamp(
                        (int)this.PredictMaximumPartition(blockOrigin),
                        (int)settings.MinimumPartitionSize,
                        Math.Min((int)settings.MaximumPartitionSize, (int)this.picture.Sequence.SequenceHeader.SuperblockSize));
                }
            }

            int savedLumaArea = this.codedAreaLuma;
            int savedChromaArea = this.codedAreaChroma;
            this.SavePartitionTrialContexts(blockOrigin, tileIndex, blockSize);
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
                  (macroBlock.GetRelativeModeInfo(-1).Block.BlockSize > Av1BlockSize.Block8x8 ||
                   macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block.BlockSize > Av1BlockSize.Block8x8)));

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
                    blockOrigin, blockSize, intraPruningLevel, ref allowMotionNone, ref allowMotionSplit, ref allowRectangularSplit);
            }

            if (!this.mustFindValidPartition && !frameHeader.IsIntra && motionAggressiveness >= 0 &&
                frameHeader.FrameSize.SuperResolutionDenominator == Av1Constants.ScaleNumerator)
            {
                this.PrunePartitionsBySimpleMotion(
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

            // A block above the largest partition may only split, whatever the models above decided.
            // Reference: av1_prune_partitions_by_max_min_bsize(), which av1_rd_pick_partition() applies
            // after av1_prune_partitions_before_search().
            if (blockSize > this.maximumPartitionSize)
            {
                allowMotionNone = false;
                allowMotionSplit = true;
                allowRectangularSplit = false;
            }

            if (this.picture.Sequence.SequenceHeader.IsStillPicture &&
                (blockSize >= Av1BlockSize.Block16x16 || (!this.mustFindValidPartition && partitionSettings.Speed >= HeifEncodingSpeed.Level6)))
            {
                int right = Math.Min(
                    blockOrigin.X + blockSize.GetWidth(),
                    this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2);

                int bottom = Math.Min(
                    blockOrigin.Y + blockSize.GetHeight(),
                    this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2);

                (double minimum, double maximum) = this.GetLogSubBlockVariance(
                    new Rectangle(blockOrigin.X, blockOrigin.Y, right - blockOrigin.X, bottom - blockOrigin.Y));

                // Separate sharp detail from an almost-flat quarter before ringing spreads across the larger block.
                // This can re-enable square splitting after the learned model suppressed it.
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
            Span<Av1SimpleMotionData> simpleMotionData = frameHeader.IsIntra ? default : this.blockWorkspace.SimpleMotionData;
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                Av1PartitionType partitionType = searchOrder[candidateIndex];

                // Neither the unsplit candidate nor the split candidate produced a cost, so nothing
                // remains that this block can code. A candidate the search skipped counts as one that
                // produced no cost. Reference: the early_term_after_none_split check that
                // av1_rd_pick_partition() makes after its split search.
                if (candidateIndex > SplitSearchOrderIndex && noneInvalid && splitInvalid &&
                    !this.mustFindValidPartition &&
                    partitionSettings.TerminatePartitionSearchAfterInvalidNoneAndSplit &&
                    blockSize != this.picture.Sequence.SequenceHeader.SuperblockSize)
                {
                    break;
                }

                // An unsearched split stage still runs the pruning that follows it, with no split cost. Reference:
                // prune_partitions_after_split() after a split_partition_search() that do_square_split skips.
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
                                parentSourceVariance = this.GetSourceVariance(blockOrigin, blockSize);
                            }

                            fourStripMask = this.ClassifyFourStripPartitions(
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

                        // The strips are searched only when the frame keeps the simple motion reference. Reference: the
                        // ref_frame_flags return of prune_part4_using_sms().
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
                            Av1NeighborEdges<Av1PartitionContext> partitionEdges = this.picture.PartitionContexts[tileIndex].GetEdges();
                            for (int direction = 0; direction < 2; direction++)
                            {
                                Av1PartitionType stripPartition = direction == 0 ? Av1PartitionType.Horizontal4 : Av1PartitionType.Vertical4;
                                long squaredError = 0;
                                for (int strip = 0; strip < 4; strip++)
                                {
                                    GetPartitionLeafGeometry(blockOrigin, blockSize, stripPartition, strip, out Point origin, out Av1BlockSize size);
                                    this.SearchSimpleMotion(origin, size, start, true, out int error, out _);
                                    squaredError += error;
                                }

                                int rate = Av1TileWriter.GetPartitionCost(
                                    this.picture, writer, blockSize, stripPartition, blockOrigin, in partitionEdges);

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
                        parentSourceVariance = this.GetSourceVariance(blockOrigin, blockSize);
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

                            // The superblock root has no rectangle win record, so a win there is the current best
                            // partition being that rectangle. Reference: the rect_part_win_info == NULL case of
                            // evaluate_ab_partition_based_on_split().
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

                Span<long> childCosts = partitionType switch
                {
                    Av1PartitionType.Split => splitNoneCosts,
                    Av1PartitionType.Horizontal => horizontalCosts,
                    Av1PartitionType.Vertical => verticalCosts,
                    _ => []
                };

                // An asymmetric sub-block keeps the luma mode of the candidate that already covered
                // the same samples. Reference: set_mode_cache_for_partition_ab().
                this.asymmetricModeCache = default;
                if (cacheAsymmetricModes && partitionType is >= Av1PartitionType.HorizontalA and <= Av1PartitionType.VerticalB)
                {
                    SetAsymmetricModeCache(
                        ref this.asymmetricModeCache, partitionType, splitModeCache, horizontalModeCache, verticalModeCache);
                }

                // A block above the maximum partition size keeps what its split children leave behind,
                // so the last child's own dry run leaves its contexts as every earlier child's does.
                // Reference: the dry run encode_sb() of each child, and the av1_restore_context() that
                // split_partition_search() skips for such a block.
                bool keepsSplitContexts = blockSize > this.maximumPartitionSize &&
                    blockSize != this.picture.Sequence.SequenceHeader.SuperblockSize;

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

                    // A failed unsplit search leaves no cost. Reference: the rate != INT_MAX test before part_none_rd
                    // is set in none_partition_search().
                    nonePartitionCost = noneInvalid ? long.MaxValue : accumulatedCost;

                    // The unsplit cost is priced again from the block's rate and distortion at the multiplier of the
                    // node, which drops the cost bias of the image tune. Reference: the av1_rd_cost_update() before
                    // none_rd is set in none_partition_search().
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

                    // The unsplit search leaves the block variance it measured, whether or not it found a mode. A search
                    // that returned on a negative bound leaves the variance of the block searched before it, and none
                    // at all before the first search of the superblock. Reference: the pb_source_variance assignment of
                    // none_partition_search().
                    parentSourceVariance = this.interSourceVariance;

                    if (candidateStatistics.Cost < bestStatistics.Cost && !this.picture.Parent.FrameHeader.IsIntra &&
                        !this.picture.Parent.FrameHeader.CodedLossless && (allowMotionSplit || allowRectangularSplit) &&
                        IsSkippable(this.blockWorkspace.PartitionTree.GetContext(nodeIndex, Av1PartitionType.None, 0).Snapshot))
                    {
                        // Scale distortion by block area relative to a maximum superblock. Rate uses the
                        // logarithmic sample count, so both tests must pass before smaller partitions stop.
                        int sampleCountLog2 = BitOperations.Log2((uint)GetBlockArea(blockSize));
                        Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
                        long distortionThreshold = settings.PartitionBreakoutDistortionThreshold >>
                            ((2 * Av1Constants.MaxSuperBlockSizeLog2) - sampleCountLog2);

                        int rateThreshold = settings.PartitionBreakoutRateThreshold * sampleCountLog2;

                        // The breakout clears the square and rectangular searches without ending the search: a
                        // rectangle on an active image edge and the pruning after the split stage still run.
                        // Reference: prune_partitions_after_none(), which clears do_square_split and
                        // do_rectangular_split, with the active edge test of is_rect_part_allowed().
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

                // The model runs in the retry that must find a partition too. Reference: the gate of
                // av1_simple_motion_search_early_term_none() in prune_partitions_after_none().
                if (partitionType == Av1PartitionType.None && candidateStatistics.Cost < bestStatistics.Cost &&
                    !terminateAfterNone &&
                    motionTerminateNone && frameHeader.ShowFrame && !frameHeader.IsIntra && blockSize >= Av1BlockSize.Block16x16 &&
                    blockOrigin.X + halfWidth < (this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) &&
                    blockOrigin.Y + halfHeight < (this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2) &&
                    candidateStatistics.Cost is >= 0 and < long.MaxValue && candidateStatistics.Rate is >= 0 and < int.MaxValue &&
                    (allowMotionSplit || allowRectangularSplit))
                {
                    terminateAfterNone = this.ShouldTerminateAfterMotionNone(macroBlock, blockOrigin, blockSize, nodeIndex, candidateStatistics);
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

                // A block above the maximum partition size never reconstructs its winning subtree
                // again, so the split children keep the contexts and coefficients they produced.
                // Reference: split_partition_search().
                if (partitionType != Av1PartitionType.Split || !keepsSplitContexts)
                {
                    this.ResetPartitionTrial(
                        blockOrigin,
                        tileIndex,
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
                this.blockWorkspace.SimpleMotionData[nodeIndex].Partition = selectedPartition;
            }

            return selectedPartition;
        }

        /// <summary>
        /// Measures the minimum and maximum log variance of visible 4x4 source blocks.
        /// </summary>
        private (double Minimum, double Maximum) GetLogSubBlockVariance(Rectangle bounds)
        {
            double minimum = double.MaxValue;
            double maximum = 0;
            for (int y = bounds.Top; y < bounds.Bottom; y += 4)
            {
                for (int x = bounds.Left; x < bounds.Right; x += 4)
                {
                    double variance = this.GetSourceLogVariance(new Point(x, y));
                    minimum = Math.Min(minimum, variance);
                    maximum = Math.Max(maximum, variance);
                }
            }

            return (minimum, maximum);
        }

        private double GetSourceLogVariance(Point origin)
        {
            int side = 1 << this.picture.Sequence.SequenceHeader.SuperblockSizeLog2;
            int index = (((origin.Y & (side - 1)) >> 2) * (side >> 2)) + ((origin.X & (side - 1)) >> 2);
            ref double variance = ref this.blockWorkspace.SourceLogVariances[index];
            if (variance < 0)
            {
                variance = this.GetLogVariance(this.source.GetPlane(Av1Plane.Y), origin);
            }

            return variance;
        }

        private double GetLogVariance(Av1PlaneRegion<TSample> plane, Point origin)
        {
            InlineArray4<TSample> zero = default;
            TOperator.GetMoments(
                Av1TransformBlockEncoder.GetPlaneSpan(plane, origin),
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
        private double GetIntraVarianceFactor(
            Point origin, Av1BlockSize blockSize, ReadOnlySpan<TSample> reconstruction, int reconstructionStride)
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
                    sourceVariance += this.GetSourceLogVariance(new Point(x, y));
                    reconstructionVariance += GetLogVariance(
                        reconstruction[(((y - origin.Y) * reconstructionStride) + (x - origin.X))..],
                        reconstructionStride,
                        this.bitDepth);
                }
            }

            int cells = (right - origin.X) * (bottom - origin.Y) / 16;
            sourceVariance = (sourceVariance / cells) + 0.000001D;
            reconstructionVariance = (reconstructionVariance / cells) + 0.000001D;

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

        private double GetIntraVarianceFactor(Point origin, Av1BlockSize blockSize)
        {
            double threshold = 1D - (0.25D * (int)this.picture.Parent.EncodingSpeed);
            if (threshold <= 0)
            {
                return 1D;
            }

            int right = Math.Min(origin.X + blockSize.GetWidth(), this.picture.Parent.Common.ModeInfoColumnCount << 2);
            int bottom = Math.Min(origin.Y + blockSize.GetHeight(), this.picture.Parent.Common.ModeInfoRowCount << 2);
            Av1PlaneRegion<TSample> plane = this.reconstruction.GetPlane(Av1Plane.Y);
            double sourceVariance = 0;
            double reconstructionVariance = 0;
            for (int y = origin.Y; y < bottom; y += 4)
            {
                for (int x = origin.X; x < right; x += 4)
                {
                    Point cell = new(x, y);
                    sourceVariance += this.GetSourceLogVariance(cell);
                    reconstructionVariance += this.GetLogVariance(plane, cell);
                }
            }

            int cells = (right - origin.X) * (bottom - origin.Y) / 16;
            sourceVariance = (sourceVariance / cells) + 0.000001D;
            reconstructionVariance = (reconstructionVariance / cells) + 0.000001D;

            // Penalize detail loss in flat reconstructions more strongly than added variation.
            // The small offset keeps flat source blocks finite; the cap bounds their influence.
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

        private static int GetBlockArea(Av1BlockSize blockSize)
            => blockSize.GetWidth() * blockSize.GetHeight();

        /// <summary>
        /// Prunes the partitions that follow the split stage, whether or not that stage searched the split. A
        /// skippable unsplit block without a new vector drops the rectangles, the after-split model can end the
        /// search, and the rectangle model can drop either direction.
        /// Reference: the skip_non_sq_part_based_on_none test and prune_partitions_after_split() in
        /// av1_rd_pick_partition().
        /// </summary>
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

                // A zero-residual square makes further shape refinement optional. The stronger setting also excludes
                // ordinary rectangles, but only for inherited motion at lower quantizers. Both read the search's
                // skippable result, not the skip flag that the cost comparison chooses. Reference:
                // pc_tree->none->skippable in prune_ext_part_none_skippable() and av1_rd_pick_partition().
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

            // The model runs in the retry that must find a partition too. Reference: the gate of
            // av1_ml_early_term_after_split() in prune_partitions_after_split(), which does not read
            // must_find_valid_partition.
            if (!terminated && !frameHeader.IsIntra && settings.AfterSplitTerminationLevel != 0 &&
                allowRectangularSplit && rectangleAllowed)
            {
                terminated = this.ShouldTerminateAfterSplit(
                    blockOrigin, blockSize, nodeIndex, bestCost, nonePartitionCost, splitCost, splitCosts);
            }

            if (!terminated && !frameHeader.IsIntra && settings.EnableRectanglePartitionModel &&
                !pruneHorizontalRectangle && !pruneVerticalRectangle && rectangleAllowed)
            {
                if (parentSourceVariance < 0)
                {
                    parentSourceVariance = this.GetSourceVariance(blockOrigin, blockSize);
                }

                this.PruneRectangularPartitions(
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
        /// Gets whether a selected mode is skippable for the partition search: the skip_txfm of its rate statistics.
        /// An inter mode is skippable when its transform search left every block empty. A regular intra mode of an
        /// inter frame never is, because its statistics always code the non-skip flag; a palette winner is when both
        /// its luma and its chroma quantized to nothing. This is not the coded skip flag. Reference: ctx->skippable,
        /// which store_coding_context() takes from best_mode_skippable, with the intra_rd_stats.skip_txfm = 0 of
        /// handle_intra_y_mode() callers and the this_skippable of av1_search_palette_mode().
        /// </summary>
        private static bool IsSkippable(Av1EncoderPartitionTree.ModeSnapshot snapshot)
            => snapshot.Statistics.AllTransformsEmpty;

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
        /// Gets a value indicating whether a given partition keeps its leaves' full mode search, rather than
        /// the estimated one: a fixed partition, or a variance partition while use_nonrd_pick_mode is off.
        /// Reference: the FIXED_PARTITION and VAR_BASED_PARTITION branches of encode_rd_sb().
        /// </summary>
        private static bool SearchesVariancePartitionLeaves(Av1PictureControlSet picture)
            => picture.Parent.FixedPartitionSize != Av1BlockSize.Invalid ||
                (picture.Parent.SpeedSettings.UseVarianceBasedPartition &&
                picture.Parent.FrameHeader.IsIntra &&
                picture.Parent.EncodingSpeed < (picture.Sequence.SequenceHeader.IsStillPicture
                    ? HeifEncodingSpeed.Level8
                    : HeifEncodingSpeed.Level7));

        /// <summary>
        /// Gets a value indicating whether the superblock searches only the modes of a given partition. Reference:
        /// the VAR_BASED_PARTITION and FIXED_PARTITION partition_search_type of encode_rd_sb().
        /// </summary>
        private static bool UsesGivenPartition(Av1PictureControlSet picture)
            => picture.Parent.SpeedSettings.UseVarianceBasedPartition || picture.Parent.FixedPartitionSize != Av1BlockSize.Invalid;

        /// <summary>
        /// Searches the modes of the partition the variance analysis chose. A finished subtree that a later
        /// block predicts from is encoded again from the decisions it kept, and the entropy contexts return to
        /// their state before the subtree, so the writer encodes the superblock afresh afterwards.
        /// Reference: av1_rd_use_partition().
        /// </summary>
        /// <summary>
        /// Runs <see cref="SearchVariancePartitionCore"/> at the rate multiplier of the block. Reference: the setup_block_rdmult() call of
        /// av1_rd_use_partition().
        /// </summary>
        private bool SearchVariancePartition(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            int nodeIndex,
            bool reconstruct)
        {
            // A block outside the frame returns before the block setup. Reference: the mi_rows and mi_cols test that
            // precedes setup_block_rdmult() in av1_rd_use_partition().
            int savedRateMultiplier = this.rateMultiplier;
            this.rateMultiplier = this.IsBlockOriginInsideFrame(blockOrigin)
                ? this.SetupBlockRateMultiplier(blockOrigin, blockSize)
                : this.GetBlockRateMultiplier(blockOrigin, blockSize);

            bool result = this.SearchVariancePartitionCore(writer, macroBlock, blockOrigin, tileIndex, blockSize, nodeIndex, reconstruct);
            this.rateMultiplier = savedRateMultiplier;
            return result;
        }

        private bool SearchVariancePartitionCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
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
            this.SavePartitionTrialContexts(blockOrigin, tileIndex, blockSize);
            bool valid = true;
            switch (partition)
            {
                case Av1PartitionType.None:
                    valid = this.EvaluatePartitionLeaf(
                        writer,
                        macroBlock,
                        blockOrigin,
                        tileIndex,
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
                        writer, macroBlock, blockOrigin, tileIndex, subSize, partition, first, long.MaxValue, false, false).Cost != long.MaxValue;

                    GetPartitionLeafGeometry(blockOrigin, blockSize, partition, 1, out Point secondOrigin, out _);
                    if (valid && this.IsBlockOriginInsideFrame(secondOrigin))
                    {
                        // The first half is encoded before the second is searched, so the second predicts from
                        // it. Reference: the av1_update_state() and encode_superblock() dry run between the
                        // two pick_sb_modes() calls.
                        int firstLumaArea = this.codedAreaLuma;
                        int firstChromaArea = this.codedAreaChroma;
                        if (first.Snapshot.ModeInfo.Block.UseIntraBlockCopy)
                        {
                            this.ReconstructPartitionLeaf(writer, macroBlock, blockOrigin, tileIndex, first, true, false, false);
                        }
                        else
                        {
                            this.ReconstructSelectedIntraBlock(writer, macroBlock, blockOrigin, tileIndex, first);
                            this.PublishPartitionLeafContexts(
                                macroBlock,
                                blockOrigin,
                                tileIndex,
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
                            macroBlock,
                            secondOrigin,
                            tileIndex,
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
                            writer, macroBlock, childOrigin, tileIndex, childSize, (nodeIndex * 4) + child + 1, child != 3);
                    }

                    break;
            }

            this.ResetPartitionTrial(blockOrigin, tileIndex, blockSize, savedLumaArea, savedChromaArea);
            if (reconstruct && blockSize != this.picture.Sequence.SequenceHeader.SuperblockSize)
            {
                // Reference: the encode_sb() dry run that closes av1_rd_use_partition() when do_recon is set.
                _ = this.EvaluatePartitionCandidate(
                    writer,
                    macroBlock,
                    blockOrigin,
                    tileIndex,
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

                Av1TileWriter.UpdatePartitionContexts(
                    this.picture.PartitionContexts[tileIndex],
                    blockOrigin,
                    partition.GetBlockSubSize(blockSize),
                    blockSize,
                    partition);
            }

            return valid;
        }

        private Av1RateDistortionStatistics EvaluateSelectedPartitionTree(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
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
                    macroBlock,
                    blockOrigin,
                    tileIndex,
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
                macroBlock,
                blockOrigin,
                tileIndex,
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
                    this.picture.PartitionContexts[tileIndex],
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
        /// This reconstruction exists only to leave the winning samples where a later trial of the
        /// surrounding block predicts from them. A block above the maximum partition size always
        /// splits, so its search result is already final. The fourth child is the last one searched,
        /// so no sibling predicts from it, and its parent reconstructs the whole subtree afterwards.
        /// Reference: should_do_dry_run_encode_for_current_block().
        /// </remarks>
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
            bool publishFinalContexts,
            Span<long> childCosts,
            Span<byte> childRectangleWins,
            out int stoppedAtLeaf,
            out long accumulatedCost)
        {
            int rate = Av1TileWriter.GetPartitionCost(
                this.picture,
                writer,
                blockSize,
                partitionType,
                blockOrigin,
                this.picture.PartitionContexts[tileIndex].GetEdges());

            Av1RateDistortionStatistics statistics = new(this.rateMultiplier, rate, 0);
            int leafCount = GetPartitionLeafCount(partitionType);

            // The sub-blocks of the asymmetric and four-way partitions are costed at their own rate multipliers,
            // and the sum returns to the multiplier of the node at the end. Reference: rd_try_subblock(),
            // rd_test_partition3() and rd_pick_4partition().
            bool asymmetric = partitionType is >= Av1PartitionType.HorizontalA and <= Av1PartitionType.Vertical4;
            int nodeRateMultiplier = this.rateMultiplier;
            stoppedAtLeaf = 0;
            accumulatedCost = statistics.Cost;

            // Each child consumes part of the parent's bound. A losing prefix cannot be recovered by
            // later nonnegative rates or distortion, so it must stop before another child changes contexts.
            for (int leafIndex = 0; leafIndex < leafCount; leafIndex++)
            {
                stoppedAtLeaf = leafIndex;

                // Only a square split compares the partition symbol alone against the bound. Every
                // other shape measures its first sub-block first and leaves those samples behind.
                // Reference: the loop condition of split_partition_search(), against
                // none_partition_search(), rectangular_partition_search() and rd_try_subblock().
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
                    // Reference: the setup_block_rdmult() call of rd_try_subblock().
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

                // A leaf the search follows with an encode leaves its coefficient contexts to that encode, which
                // reads the neighbours' contexts, not the leaf's own. Reference: pick_sb_modes(), which updates
                // no entropy context, against the encode_superblock() that rd_try_subblock() and
                // rectangular_partition_search() make after it, and the dry run encode_sb() of a 4x4 child.
                bool siblingEncodeFollows = searchChildren &&
                    leafIndex + 1 < leafCount && (partitionType != Av1PartitionType.Split || blockSize <= Av1BlockSize.Block8x8);

                bool encodeFollows = siblingEncodeFollows && this.picture.Parent.FrameHeader.IsIntra;

                // A replay of a finished subtree encodes every leaf at the multiplier of that leaf. Reference: the
                // setup_block_rdmult() call of encode_b(), which encode_sb() reaches for each leaf.
                if (!searchChildren && !(partitionType == Av1PartitionType.Split && blockSize > Av1BlockSize.Block8x8))
                {
                    this.rateMultiplier = this.SetupBlockRateMultiplier(leafOrigin, leafSize);
                }

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
                        publishContexts,
                        out childNoneCost,
                        out childWins)
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
                            this.GetLeafCostLimit(remainingCost, leafOrigin, leafSize, partitionType == Av1PartitionType.Split),
                            publishContexts,
                            !encodeFollows,
                            siblingEncodeFollows)
                        : this.ReconstructPartitionLeaf(
                            writer,
                            macroBlock,
                            leafOrigin,
                            tileIndex,
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

                // A 4x4 split child is an unsplit search of its own, which records the reference it picked for the
                // rectangles of its parent. Reference: the av1_update_picked_ref_frames_mask() call that
                // none_partition_search() makes for a result within its budget.
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
                    // Split classification uses each child's unsplit cost, independently of its winner.
                    // Rectangular leaves have no child partition search and retain their complete mode cost,
                    // measured again at the multiplier of the node. Reference: the av1_rd_cost_update() call of
                    // rd_pick_rect_partition() before it stores rect_part_rd.
                    Av1RateDistortionStatistics nodeLeafCost = childStatistics;
                    if (partitionType != Av1PartitionType.Split)
                    {
                        nodeLeafCost.UpdateCost(nodeRateMultiplier);
                    }

                    childCosts[leafIndex] = partitionType == Av1PartitionType.Split && blockSize > Av1BlockSize.Block8x8
                        ? childNoneCost
                        : nodeLeafCost.Cost;
                }

                // Every split child is searched as a partition of its own, and it reports a result only when
                // that result stays below the budget it received. A 4x4 child is evaluated here directly, so
                // the same test applies to it. Reference: the found_best_partition result of
                // av1_rd_pick_partition(), which split_partition_search() turns into an invalid split.
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

                // A sibling predicts from the reconstruction this leaf leaves behind, so encode the leaf
                // across every plane before moving on. The mode search alone leaves the last chroma
                // candidate in the plane, not the winner. A split child encodes itself when its own
                // partition search ends. A leaf that already spent the bound encodes nothing, because
                // the search stops instead of measuring the sibling, and a rectangle whose second half
                // falls outside the frame encodes nothing either. Reference: the encode_superblock()
                // call that rectangular_partition_search() makes between its two sub-partitions, under
                // its cost and has_rows / has_cols gates, and the one that rd_try_subblock() makes for
                // every sub-block but the last, after its own cost gate.
                // A split child normally reconstructs its own winning subtree when its partition
                // search ends. A 4x4 child has no partition search of its own, so the parent leaves
                // those samples behind instead, for every child but the last.
                // Reference: the dry run encode_sb() that closes av1_rd_pick_partition(), under
                // should_do_dry_run_encode_for_current_block().
                bool reconstructSplitLeaf = partitionType == Av1PartitionType.Split &&
                    blockSize <= Av1BlockSize.Block8x8 && leafIndex + 1 < leafCount;

                bool reconstructSibling = partitionType != Av1PartitionType.Split && leafIndex + 1 < leafCount &&
                    (asymmetric || statistics.Cost < costLimit.Cost) &&
                    this.IsPartitionSiblingInsideFrame(blockOrigin, blockSize, partitionType, leafIndex);

                // An inter leaf keeps the samples of its own search, but the encode it stands for still writes
                // DCT_DCT for each luma block that quantized to nothing. An intra leaf, in an inter frame as in an
                // intra frame, is quantized again by encode_block_intra(), which a leaf whose search was reused
                // also needs for its samples.
                bool siblingIsIntra = (reconstructSplitLeaf || reconstructSibling) &&
                    (this.picture.Parent.FrameHeader.IsIntra ||
                     this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex).Snapshot.ModeInfo.Block.ReferenceFrame <= Av1ReferenceFrameType.Intra);

                if (searchChildren && !siblingIsIntra && (reconstructSplitLeaf || reconstructSibling))
                {
                    Av1EncoderPartitionTree.ModeContext sibling =
                        this.blockWorkspace.PartitionTree.GetContext(nodeIndex, partitionType, leafIndex);

                    if (!this.picture.Parent.FrameHeader.CodedLossless && !sibling.Snapshot.ModeInfo.Block.Skip)
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
                        // A copied block is encoded from its displacement, not from an intra predictor.
                        // Reference: the is_inter_block() branch of encode_superblock().
                        this.ReconstructPartitionLeaf(writer, macroBlock, leafOrigin, tileIndex, sibling, true, false, false);
                    }
                    else
                    {
                        this.ReconstructSelectedIntraBlock(writer, macroBlock, leafOrigin, tileIndex, sibling);

                        // The encode leaves the entropy contexts of what it coded for the next leaf, from the
                        // coefficients it just wrote. Reference: the av1_update_txb_context() call of
                        // encode_superblock().
                        this.PublishPartitionLeafContexts(
                            macroBlock,
                            leafOrigin,
                            tileIndex,
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

                // The sub-block of an asymmetric partition encodes its sibling state before its multiplier returns
                // to the node. Reference: the encode_superblock() call and the restore of rd_try_subblock().
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
        /// Reports whether the sub-partition that follows this one starts inside the frame. A rectangle
        /// whose second half falls outside the frame codes only its first half, so nothing predicts from
        /// the reconstruction that half would leave behind. Every other shape measures its sibling either
        /// way. Reference: the has_rows and has_cols entries of is_not_edge_block in
        /// rectangular_partition_search().
        /// </summary>
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

            // Reaching the configured minimum closes every rectangle, whether or not the frame reaches
            // this block's midpoint. What remains is one candidate: the unsplit block when the frame does
            // reach both midpoints, and the square split when it does not. A block below 8x8 has no square
            // split to fall back on and keeps the unsplit candidate either way. Reference: the
            // is_le_min_sq_part branch of av1_prune_partitions_by_max_min_bsize(), against the
            // do_square_split of init_partition_search_state_params(). The retry that follows a search
            // with no valid partition sets its own limits, so it keeps the alphabet it had.
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

            // Above the square-only limit, rectangular leaves remain necessary only where the
            // frame ends before the midpoint. Extended rectangles require both halves and are excluded.
            if (!this.mustFindValidPartition && blockSize > speedSettings.SquareOnlyPartitionThreshold &&
                ((partitionType == Av1PartitionType.Horizontal && hasRows) ||
                 (partitionType == Av1PartitionType.Vertical && hasColumns) ||
                 partitionType >= Av1PartitionType.HorizontalA))
            {
                return false;
            }

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

        /// <summary>
        /// Selects the source candidate of every asymmetric sub-block's cached luma decision.
        /// </summary>
        /// <remarks>Reference: set_mode_cache_for_partition_ab().</remarks>
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
        /// Retains the luma decision of a completed square or rectangular candidate, for the
        /// asymmetric candidates that cover the same samples.
        /// </summary>
        /// <remarks>
        /// The reference keeps the unsplit context of each square child and the mode context of each
        /// rectangular half, and offers it only when that search produced a result.
        /// Reference: copy_partition_mode_from_mode_context().
        /// </remarks>
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

            // The leaf that ended the search early still keeps the context its own search filled, so
            // a split child whose partition search found nothing within its budget still offers its
            // unsplit mode. Reference: copy_partition_mode_from_pc_tree(), which tests only the rate of
            // pc_tree->split[i]->none.
            int count = Math.Min(childCosts.Length, stoppedAtLeaf + 1);
            for (int leaf = 0; leaf < count; leaf++)
            {
                // A leaf that was never searched reports no cost, and offers no mode. A square child that
                // never searched its unsplit shape is the same: its unsplit context exists but still holds
                // the invalid statistics it was created with. Reference: set_none_partition_params(),
                // which allocates the context whether or not the shape is allowed, and av1_alloc_pmc(),
                // which invalidates its rd_stats.
                if (childCosts[leaf] == long.MaxValue || childCosts[leaf] == 0)
                {
                    continue;
                }

                // A candidate outside the coded frame keeps no decision, in the same way that the
                // reference holds a null context for a sub-block it never searched.
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
        /// Runs <see cref="EncodeSelectedBlock"/> at the rate multiplier of the block. Reference: the setup_block_rdmult() call of
        /// encode_b().
        /// </summary>
        public void EncodeBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            int savedRateMultiplier = this.rateMultiplier;

            // A block the estimated inter search codes now takes its segment from the map, and cyclic refresh prices
            // a boosted block at its own multiplier, which also sets the motion vector error per bit. Reference: the
            // av1_set_offsets(), setup_block_rdmult() and av1_set_error_per_bit() calls of pick_sb_modes_nonrd().
            int segmentId = -1;
            if (this.replayNodeIndex < 0 && this.UsesEstimatedInterSearch)
            {
                this.SetBlockSegment(blockOrigin, modeInfo.Block.BlockSize);
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

            this.EncodeSelectedBlock(writer, macroBlock, blockOrigin, tileIndex, ref modeInfo, ref block, ref paletteInfo);
            this.rateMultiplier = savedRateMultiplier;
        }

        public void EncodeSelectedBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            this.encodedWithoutCoefficients = false;
            this.EncodeBlockCore(writer, macroBlock, blockOrigin, tileIndex, ref modeInfo, ref block, ref paletteInfo, true);
            if (this.encodedWithoutCoefficients)
            {
                modeInfo.Block.Skip = true;
            }

            if (this.picture.Parent.SpeedSettings.SkipCdefSuperblock)
            {
                this.UpdateCdefSkip(blockOrigin, modeInfo.Block.Mode);
            }
        }

        /// <summary>
        /// Narrows the CDEF skip flag of the 64x64 unit that holds a coded block, and stores it on the unit's
        /// first block, which carries the unit's strength index. A unit keeps CDEF off only when skipping is
        /// allowed for the frame and none of its blocks is intra or uses a new motion vector.
        /// Reference: the skip_cdef_sb step at the end of pick_sb_modes_nonrd(), with skip_cdef_sb 1 and the
        /// speed 9 spatial-variance threshold of UINT_MAX.
        /// </summary>
        /// <param name="blockOrigin">The luma origin of the coded block.</param>
        /// <param name="mode">The selected luma prediction mode.</param>
        private void UpdateCdefSkip(Point blockOrigin, Av1PredictionMode mode)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            bool allowSkipping = parent.FramesSinceKey > 10 && !parent.HighSourceSad &&
                this.blockColorSensitivity[0] == 0 && this.blockColorSensitivity[1] == 0;

            int unit = (((blockOrigin.Y >> 6) & 1) << 1) | ((blockOrigin.X >> 6) & 1);
            ref bool skip = ref this.cdefSkipUnits[unit];
            skip = skip && allowSkipping && !(mode < Av1PredictionMode.IntraModeEnd || mode == Av1PredictionMode.NewMotionVector);
            Point unitOrigin = new((blockOrigin.X & ~63) >> Av1Constants.ModeInfoSizeLog2, (blockOrigin.Y & ~63) >> Av1Constants.ModeInfoSizeLog2);
            this.picture.GetMacroBlockModeInfo(unitOrigin).CdefStrength = skip ? 1 : 0;
        }

        /// <summary>
        /// Selects the modes of one block without encoding the winner afterwards.
        /// Reference: pick_sb_modes().
        /// </summary>
        private void SearchBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            this.EncodeBlockCore(writer, macroBlock, blockOrigin, tileIndex, ref modeInfo, ref block, ref paletteInfo, false);
        }

        private void EncodeBlockCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
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

                // Reference: the COMPLEXITY_AQ segment copy of av1_update_state(), before its segment costs.
                int codedSegmentId = this.GetCodedBlockSegment(blockOrigin, modeInfo.Block.BlockSize, modeInfo.Block.SegmentId);
                modeInfo.Block.SegmentId = codedSegmentId;
                block.SegmentId = codedSegmentId;

                // A block that a merge trial of the estimated inter search kept updates its cyclic refresh segment when
                // it is coded for output, and always prices its segment, because the estimated search never sets the
                // skip flag that av1_update_state() tests. Reference: the encode_b_nonrd() calls with dry_run 0 that
                // close try_merge().
                bool countSegments = false;
                bool cyclicRefreshEncode = encodeSelected && this.UsesEstimatedInterSearch &&
                    this.UpdatesCyclicRefreshSegment(context.Snapshot.Statistics, out countSegments);

                if (cyclicRefreshEncode)
                {
                    Av1MotionVector firstVector = modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra
                        ? context.Snapshot.Displacement
                        : default;

                    this.UpdateCyclicRefreshSegment(
                        blockOrigin, ref modeInfo.Block, ref block, firstVector, context.Snapshot.Statistics, countSegments);

                    // The intra reconstruction reads its segment from the retained decision.
                    context.Snapshot.ModeInfo.Block.SegmentId = modeInfo.Block.SegmentId;
                    context.Snapshot.Block.SegmentId = block.SegmentId;
                }

                bool searchSkip = !cyclicRefreshEncode && modeInfo.Block.Skip;
                if (encodeSelected && !searchSkip)
                {
                    this.AddSegmentPredictionCosts(writer, macroBlock, blockOrigin, modeInfo.Block.BlockSize, modeInfo.Block.SegmentId);
                }

                if (modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra || modeInfo.Block.UseIntraBlockCopy)
                {
                    InlineArray128<Av1EncoderTransformBlockState> stateStorage = default;
                    Span<Av1EncoderTransformBlockState> states = stateStorage;
                    Av1TransformSize replayRootSize = this.picture.Parent.FrameHeader.CodedLossless
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

                        if (!this.picture.Parent.FrameHeader.CodedLossless)
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
                        Av1TransformSize chromaTransformSize = this.picture.Parent.FrameHeader.CodedLossless
                            ? Av1TransformSize.Size4x4
                            : modeInfo.Block.BlockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

                        Av1BlockSize chromaBlockSize = modeInfo.Block.BlockSize.GetSubsampled(subX != 0, subY != 0);
                        Size chromaExtent = GetCodedTransformExtent(macroBlock, chromaBlockSize, chromaTransformSize, subX, subY);
                        int chromaTransformCount = chromaExtent.Width * chromaExtent.Height / chromaTransformSize.GetSize2d();
                        int chromaStateStride = chromaTransformSize.GetSize2d() / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                        ReadOnlySpan<Av1EncoderTransformBlockState> blueStates = context.GetTransformStates(Av1Plane.U);
                        ReadOnlySpan<Av1EncoderTransformBlockState> redStates = context.GetTransformStates(Av1Plane.V);
                        for (int index = 0; !this.picture.Parent.FrameHeader.CodedLossless && index < chromaTransformCount; index++)
                        {
                            states[64 + index] = blueStates[index * chromaStateStride];
                            states[80 + index] = redStates[index * chromaStateStride];
                        }
                    }

                    this.encodedWithoutCoefficients = !this.ReconstructSelectedInterBlock(
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

                    this.codedAreaLuma += replayArea;
                    if (block.HasChroma)
                    {
                        int subX = this.source.ChromaSubsamplingX;
                        int subY = this.source.ChromaSubsamplingY;
                        Av1BlockSize chromaBlockSize = modeInfo.Block.BlockSize.GetSubsampled(subX != 0, subY != 0);
                        Av1TransformSize chromaTransformSize = this.picture.Parent.FrameHeader.CodedLossless
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
                    this.ReconstructSelectedIntraBlock(writer, macroBlock, blockOrigin, tileIndex, context);
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
                        macroBlock, blockOrigin, ref modeInfo.Block, ref block, modeInfo.Block.Skip || this.encodedWithoutCoefficients, countSegments);
                }

                this.SelectedBlockStatistics = context.Snapshot.Statistics;
                return;
            }

            this.interTransformNoSplitCosts[..].Fill(long.MaxValue);
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            this.interSourceVariance = this.GetSourceVariance(blockOrigin, blockSize);
            Av1PartitionType partitionType = modeInfo.Block.PartitionType;
            Av1TransformSize maximumLumaTransformSize = this.picture.Parent.FrameHeader.CodedLossless
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

            // The delta quantizer syntax codes the superblock quantizer, not the segment's. Reference: mbmi->current_qindex.
            block.QuantizationIndex = this.superblockQIndex;
            block.SegmentId = this.blockSegmentId;

            bool stillPicture = this.picture.Sequence.SequenceHeader.IsStillPicture;
            if (this.picture.Parent.FrameHeader.IsIntra &&
                this.picture.Parent.EncodingSpeed >= (stillPicture ? HeifEncodingSpeed.Level8 : HeifEncodingSpeed.Level7))
            {
                int sourceVariance = this.interSourceVariance;
                bool useFullSearch = blockSize < Av1BlockSize.Block16x16 &&
                    (!stillPicture || (this.picture.Parent.EncodingSpeed == HeifEncodingSpeed.Level8 && sourceVariance >= 101));

                if (!useFullSearch)
                {
                    this.EncodeEstimatedIntraBlock(
                        writer, macroBlock, blockOrigin, blockSize, tileIndex, sourceVariance, ref modeInfo, ref block, ref paletteInfo);

                    return;
                }
            }

            bool isInterFrame = !this.picture.Parent.FrameHeader.IsIntra;
            if (isInterFrame && this.picture.Parent.SpeedSettings.UseEstimatedInterModeDecision)
            {
                this.EncodeEstimatedInterBlock(writer, macroBlock, blockOrigin, tileIndex, ref modeInfo, ref block, ref paletteInfo);
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

            Span<Av1EncoderTransformBlockState> lumaTransformBlocks =
                this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.Y);

            int lumaTransformIndex = this.codedAreaLuma /
                Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            Span<Av1EncoderTransformBlockState> retainedLumaStates = lumaTransformBlocks[lumaTransformIndex..];
            bool skipIntra = false;
            if (isInterFrame)
            {
                Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;

                // A block above the intra size limit skips intra search when its best single-reference motion
                // is small and its source is not flat. Without a valid inter mode the best mode is zeroed.
                // Reference: the prune_intra_mode_based_on_mv_range test of skip_intra_modes_in_interframe().
                bool validInter = interStatistics.Cost != long.MaxValue;
                if (settings.IntraModeMotionRangePruneLevel != 0 && blockSize > settings.MaximumIntraBlockSize &&
                    !(validInter && interModeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra))
                {
                    Av1MotionVector bestVector = validInter ? interVector : default;
                    int threshold = 16 << settings.IntraModeMotionRangePruneLevel;
                    skipIntra = Math.Abs(bestVector.Row) < threshold && Math.Abs(bestVector.Column) < threshold &&
                        this.interSourceVariance > 128;
                }

                // The mean model intra and inter costs of the block stay -1 without the statistics of a whole
                // superblock, and at speeds 0 and 1 for frames whose shorter side exceeds 480 lines. Reference: the
                // do_pruning test and calculate_cost_from_tpl_data() in av1_rd_pick_inter_mode().
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

                // Without a valid inter mode the best mode is zeroed, so it neither skips its transform nor codes
                // a new vector. Reference: the av1_zero() of best_mbmode in init_inter_mode_search_state().
                bool bestSkip = validInter && interModeInfo.Block.Skip;
                if (!skipIntra && settings.IntraInInterPruningLevel != 0 && this.interSourceVariance > 1)
                {
                    if (settings.IntraInInterPruningLevel >= 2 && bestSkip)
                    {
                        bool newMotion = interModeInfo.Block.Mode is Av1PredictionMode.NewMotionVector or
                            Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NewNearestMotionVector or
                            Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector or Av1PredictionMode.NewNewMotionVector;

                        // Preserve intra search for nearly flat sources. Otherwise a skipped residual from
                        // inherited motion is sufficient evidence, with the weaker policy limited to Q <= 200.
                        // Without model costs, the strongest policies also accept searched motion.
                        skipIntra = (!newMotion && (settings.IntraInInterPruningLevel >= 3 || qIndex <= 200)) ||
                            (settings.IntraInInterPruningLevel >= 4 && (tplInterCost < 0 || tplIntraCost < 0));
                    }

                    // With both model costs, a small network decides from them, the best mode's transform skip, the
                    // block shape and the quantizer. Reference: the neural network branch of
                    // skip_intra_modes_in_interframe().
                    if (!skipIntra)
                    {
                        skipIntra = Av1TplModePruning.SkipIntraByNetwork(
                            bestSkip, blockSize, tplIntraCost, tplInterCost, qIndex, this.bitDepth, minimumFrameDimension);
                    }
                }

                // A nonzero sharpness skips the intra search of a block wider or taller than 16 samples.
                // High bit depth sharpness 3 is an exception: it searches all sizes.
                // Reference: allow_larger_intra and the sharpness return at the start of search_intra_modes_in_interframe().
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
                // Reference: the skip_txfm reset of each luma mode in search_intra_modes_in_interframe().
                this.transformSearchSkip = false;
                modeInfo.Block.Mode = this.SelectLumaMode(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    retainedLumaStates,
                    Math.Min(this.blockCostLimit, interStatistics.Cost),
                    ref paletteInfo,
                    out lumaAngleDelta,
                    out filterIntraMode,
                    out lumaTransformSize,
                    out lumaStatistics);
            }

            // Chroma search is useful only after luma beats the inter luma cost: the winner's, or after the retained
            // candidate search the cheapest completed candidate's, even above the budget. Empty luma residuals use
            // the skip-symbol estimate for this gate; final intra syntax remains coded. Reference: the yrd_threshold
            // test of search_intra_modes_in_interframe().
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

                Av1TransformSize chromaTransformSize = this.picture.Parent.FrameHeader.CodedLossless
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

            // A still picture at the non-RD speeds never searches a copy, even in the blocks that take the full
            // search, although its header may still allow one. Reference: the use_nonrd_pick_mode and
            // rt_use_intrabc test that opens rd_pick_intrabc_mode_sb().
            bool allowIntraBlockCopy = this.picture.Parent.FrameHeader.AllowIntraBlockCopy;
            bool nonRdWithoutCopy = this.picture.Sequence.SequenceHeader.IsStillPicture &&
                this.picture.Parent.EncodingSpeed >= HeifEncodingSpeed.Level8;

            bool searchIntraBlockCopy = allowIntraBlockCopy && !nonRdWithoutCopy &&
                this.picture.Parent.MotionSearchSettings.AllowIntraBlockCopy &&
                (!this.picture.Parent.MotionSearchSettings.UseFastIntraBlockCopySearch ||
                 blockSize is Av1BlockSize.Block4x4 or Av1BlockSize.Block8x8 or Av1BlockSize.Block16x16);

            Av1RateDistortionStatistics regularStatistics = lumaStatistics.Cost == long.MaxValue
                ? Av1RateDistortionStatistics.Invalid
                : this.GetRegularBlockCost(writer, macroBlock, lumaStatistics);

            // Sharpness 3 charges the merged intra result of an inter frame by the luma samples that its luma search
            // left, before the budget comparison. Reference: adjust_rdcost() on intra_rd_stats in
            // search_intra_modes_in_interframe().
            if (regularStatistics.Cost != long.MaxValue && this.ChargesSmoothing)
            {
                regularStatistics.AddSmoothingOffset(this.rateMultiplier, this.intraSmoothingOffset);
            }
            else if (regularStatistics.Cost != long.MaxValue && isInterFrame && this.ChargesHighBitDepthTextureLoss)
            {
                // At a high bit depth, the charge also uses the mode of the luma winner, and its extra cost scales the merged cost.
                // Reference: the high bit depth branch of adjust_rdcost() on intra_rd_stats. Its rdcost is this_rd.
                bool smoothMode = IsSmoothTextureMode(modeInfo.Block.Mode);
                long currentCost = regularStatistics.Cost;
                this.ChargeTextureLoss(ref regularStatistics, currentCost, this.intraSmoothingOffset, blockOrigin, blockSize, false, false, smoothMode);
            }

            // An inter frame keeps the intra result only when it beats the budget of the block, so a block
            // without a mode below the budget has no winner to refine. Reference: the best_rd test before
            // update_search_state() in search_intra_modes_in_interframe().
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

                // Reference: bypass_winner_mode_processing(), with the winner's skip flag and its all-empty result.
                bool bypassWinner = winnerSettings.GetInterWinnerPruningLevel(this.picture.Parent.FrameUpdateType) switch
                {
                    2 => !hasNewMotion && interStatistics.AllTransformsEmpty,
                    3 => !hasNewMotion && (interStatistics.AllTransformsEmpty || (this.blockQIndex <= 127 && modeInfo.Block.Skip)),
                    4 => !(winnerSettings.CoefficientOptimizationLevel >= 5 && this.blockQIndex <= 70) &&
                        (modeInfo.Block.Skip || interStatistics.AllTransformsEmpty),
                    _ => false
                };

                // The partition search reads whether the mode-evaluation winner kept any coefficient. The
                // winner refinement does not change it. Reference: best_mode_skippable in
                // av1_rd_pick_inter_mode(), which refine_winner_mode_tx() leaves unchanged.
                bool skippable = interStatistics.AllTransformsEmpty;
                if (!bypassWinner && (winnerSettings.EnableWinnerCoefficientOptimization ||
                    winnerSettings.DeferTransformSizeSearch || winnerSettings.UseWinnerInterpolation ||
                    winnerSettings.InterTransformTypeProbabilityThreshold != int.MaxValue))
                {
                    this.blockWorkspace.EvaluationStage = Av1EncoderEvaluationStage.Winner;
                    this.SetWarpedPrediction(macroBlock, blockOrigin, modeInfo.Block.PartitionType, modeInfo.Block, interVector);
                    this.RefineInterTransformSize(
                        writer,
                        macroBlock,
                        blockOrigin,
                        tileIndex,
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
                        macroBlock,
                        blockOrigin,
                        tileIndex,
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
                Av1EncoderPartitionTree.ModeContext winner = this.blockWorkspace.GetIntraWinnerContext(block.HasChroma ? 3 : 1);
                winner.Snapshot = new Av1EncoderPartitionTree.ModeSnapshot
                {
                    ModeInfo = modeInfo,
                    Block = block,
                    Palette = paletteInfo,
                    Statistics = this.SelectedBlockStatistics
                };

                Size retainedExtent = GetCodedTransformExtent(macroBlock, blockSize, modeInfo.Block.TransformSize, 0, 0);
                this.RetainModeContext(
                    winner, this.codedAreaLuma, this.codedAreaChroma, retainedExtent.Width * retainedExtent.Height, chromaArea);

                // The winner keeps the transform grid its own luma search chose. The shared coefficient
                // buffer belongs to whichever candidate wrote it last, and a grid taken from there can hand
                // this block a transform type its size does not allow. Reference: the
                // av1_copy_array(ctx->tx_type_map, xd->tx_type_map, ctx->num_4x4_blk) call of
                // av1_rd_pick_intra_sby_mode().
                CopyWinnerTransformStates(retainedLumaStates, winner.GetTransformStates(Av1Plane.Y));

                // The palette search reads the mode search result and its chroma from before the winner refinement,
                // which keeps its own best cost. Reference: search_state.best_rd, which refine_winner_mode_tx() leaves
                // unchanged, as the bound and the comparison of av1_search_palette_mode(), and the rate_uv_intra and
                // dist_uvs of the intra search that it adds.
                Av1RateDistortionStatistics modeSearchStatistics = this.SelectedBlockStatistics;
                Av1RateDistortionStatistics modeSearchChroma = chromaStatistics;
                Av1EncoderSpeedSettings speedSettings = this.picture.Parent.SpeedSettings;
                if (!this.picture.Parent.FrameHeader.CodedLossless &&
                    (speedSettings.IntraTransformTypeSearchLevel != 0 ||
                     speedSettings.EnableWinnerCoefficientOptimization || speedSettings.DeferTransformSizeSearch))
                {
                    // The luma refinement writes each improving trial into the luma winner context, which
                    // shares storage with this winner. Keep the winner's grid so that a rejected refinement
                    // leaves it as the mode search chose it. Reference: refine_winner_mode_tx(), which copies
                    // ctx->tx_type_map only when this_rd is below best_rd.
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
                        macroBlock,
                        blockOrigin,
                        blockSize,
                        tileIndex,
                        retainedLumaStates,
                        modeInfo.Block.Mode,
                        ref refinedPalette,
                        ref refinedAngle,
                        ref refinedFilter,
                        ref refinedSize,
                        ref refinedLuma);

                    // Refine the selected UV mode against the newly reconstructed luma. Keep its mode,
                    // angle, palette, and CfL alpha fixed; only transform coefficients are reconsidered.
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
                            writer, macroBlock, blockOrigin, tileIndex, refinedModeInfo, refinedBlock, refinedPalette, chromaStatistics);

                        refinedLuma.Add(this.rateMultiplier, refinedChroma);
                        Av1RateDistortionStatistics refinedStatistics = this.GetRegularBlockCost(
                            writer, macroBlock, refinedLuma);

                        if (this.UsesHighBitDepthSharpness)
                        {
                            this.ChargeRefinedIntraTextureLoss(ref refinedStatistics, blockOrigin, blockSize, refinedMode);
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
                                winner, this.codedAreaLuma, this.codedAreaChroma, refinedExtent.Width * refinedExtent.Height, chromaArea);

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

                // The search leaves the last transform block of each plane as its prediction, so the winner is
                // encoded again whether or not a refinement ran, and the next block predicts from it and stores
                // its luma for chroma-from-luma. Reference: the encode_superblock() dry run that
                // rd_try_subblock() and rectangular_partition_search() make after pick_sb_modes(), which runs
                // encode_block_intra() over every transform block. A leaf of its own partition search has no
                // such encode, so the winner keeps its searched types here, and the partition search writes
                // DCT_DCT for a sibling that quantized to nothing. Reference: the dry run encode_sb() that
                // closes av1_rd_pick_partition(), the first to run update_txk_array() for the leaf.
                int reconstructedLumaArea = this.codedAreaLuma;
                int reconstructedChromaArea = this.codedAreaChroma;
                this.keepSearchedZeroBlockTypes = true;
                this.ReconstructSelectedIntraBlock(writer, macroBlock, blockOrigin, tileIndex, winner);
                this.keepSearchedZeroBlockTypes = false;
                this.codedAreaLuma = reconstructedLumaArea;
                this.codedAreaChroma = reconstructedChromaArea;

                Av1RateDistortionStatistics paletteStatistics = modeSearchStatistics;
                Av1EncoderPaletteInfo candidatePalette = paletteInfo;
                Av1TransformSize paletteTransformSize = modeInfo.Block.TransformSize;
                if (Av1TileWriter.IsPaletteAllowed(this.picture.Parent.FrameHeader.AllowScreenContentTools, blockSize) &&
                    this.SelectLumaPalette(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    retainedLumaStates,
                    64,
                    writer.GetInterFrameLumaModeCost(Av1PredictionMode.DC, blockSize),
                    ref paletteStatistics,
                    ref candidatePalette,
                    ref paletteTransformSize))
                {
                    // An intra transform block always signals coefficients in the rate-distortion search, so a palette
                    // is never skippable, even when it quantized to nothing, and pays for its residual syntax and the
                    // cleared skip flag. Reference: the skip_txfm of 0 that block_rd_txfm() gives an intra block, which
                    // makes this_skippable of av1_search_palette_mode() zero.
                    int rate = writer.GetIsInterCost(false, Av1TileWriter.GetIntraInterContext(macroBlock)) +
                        paletteStatistics.Rate + modeSearchChroma.Rate +
                        writer.GetSkipCost(false, Av1TileWriter.GetSkipContext(macroBlock));

                    Av1RateDistortionStatistics combinedStatistics = new(
                        this.rateMultiplier, rate, paletteStatistics.Distortion + modeSearchChroma.Distortion);

                    if (combinedStatistics.Cost < modeSearchStatistics.Cost)
                    {
                        // The coded skip flag stays clear, so the final encode quantizes every transform block again.
                        // Reference: the skip_txfm of 0 that av1_search_palette_mode() leaves, which
                        // encode_block_intra() reads.
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
                            winner, this.codedAreaLuma, this.codedAreaChroma, paletteExtent.Width * paletteExtent.Height, chromaArea);

                        CopyWinnerTransformStates(retainedLumaStates, winner.GetTransformStates(Av1Plane.Y));
                    }

                    // Rebuild the retained winner after palette trials. This also regenerates CfL chroma
                    // from a winning palette's reconstructed luma, while preserving its selected UV mode.
                    int savedLumaArea = this.codedAreaLuma;
                    int savedChromaArea = this.codedAreaChroma;
                    this.keepSearchedZeroBlockTypes = true;
                    this.ReconstructSelectedIntraBlock(writer, macroBlock, blockOrigin, tileIndex, winner);
                    this.keepSearchedZeroBlockTypes = false;
                    this.codedAreaLuma = savedLumaArea;
                    this.codedAreaChroma = savedChromaArea;
                }
            }
            else if (encodeSelected && modeInfo.Block.ReferenceFrame <= Av1ReferenceFrameType.Intra &&
                !modeInfo.Block.UseIntraBlockCopy && this.SelectedBlockStatistics.Cost != long.MaxValue)
            {
                // The search leaves the last transform block of each plane as its prediction, so the
                // winner is encoded again before the syntax is written and the next block predicts from it.
                // Reference: the encode_superblock() that encode_sb() makes after pick_sb_modes(), which
                // runs encode_block_intra() over every transform block.
                Av1EncoderPartitionTree.ModeContext winner = this.blockWorkspace.GetIntraWinnerContext(block.HasChroma ? 3 : 1);
                winner.Snapshot = new Av1EncoderPartitionTree.ModeSnapshot
                {
                    ModeInfo = modeInfo,
                    Block = block,
                    Palette = paletteInfo,
                    Statistics = this.SelectedBlockStatistics
                };

                Size selectedExtent = GetCodedTransformExtent(macroBlock, blockSize, modeInfo.Block.TransformSize, 0, 0);
                this.RetainModeContext(
                    winner, this.codedAreaLuma, this.codedAreaChroma, selectedExtent.Width * selectedExtent.Height, chromaArea);

                CopyWinnerTransformStates(retainedLumaStates, winner.GetTransformStates(Av1Plane.Y));
                int savedLumaArea = this.codedAreaLuma;
                int savedChromaArea = this.codedAreaChroma;
                this.ReconstructSelectedIntraBlock(writer, macroBlock, blockOrigin, tileIndex, winner);
                this.codedAreaLuma = savedLumaArea;
                this.codedAreaChroma = savedChromaArea;
            }

            if (isInterFrame)
            {
                // Skip mode is a one-sided compound when every reference is in the past, so a frame that drops those
                // pairs neither searches it nor prices the non-skip-mode symbol. Sharpness 3 never searches it.
                // Reference: the disable_onesided_comp return of rd_pick_skip_mode(), and the sharpness test before its
                // call in av1_rd_pick_inter_mode().
                ObuSkipModeParameters skipModeParameters = this.picture.Parent.FrameHeader.SkipModeParameters;
                if (skipModeParameters.SkipModeFlag && Math.Min(blockSize.GetWidth(), blockSize.GetHeight()) >= 8 &&
                    this.blockWorkspace.EncoderOptions.Sharpness != 3 &&
                    !this.picture.Parent.PrunesAllCompoundReferences)
                {
                    int skipModeContext = Av1TileWriter.GetSkipModeContext(macroBlock);
                    Av1RateDistortionStatistics selectedStatistics = this.SelectedBlockStatistics;

                    // A block whose retained candidates all failed still compares skip mode with the best estimate
                    // of the mode loop, and keeps no mode when skip mode loses. Reference: the rd_cost that
                    // rd_pick_skip_mode() reads, which tx_search_best_inter_candidates() does not reset, with the
                    // best_mode_index return at the end of av1_rd_pick_inter_mode().
                    bool comparesLeftoverEstimate = selectedStatistics.Cost == long.MaxValue &&
                        this.leftoverInterEstimate.Cost != long.MaxValue;

                    if (comparesLeftoverEstimate)
                    {
                        selectedStatistics = this.leftoverInterEstimate;
                    }

                    Av1RateDistortionStatistics syntaxStatistics = new(
                        this.rateMultiplier, writer.GetSkipModeCost(false, skipModeContext), 0);

                    // The non-skip-mode symbol is priced only once the reference lists of the pair exist. Reference:
                    // the ref_mv_count return of rd_pick_skip_mode() before it adds skip_mode_cost[ctx][0].
                    bool skipModeListsReady = this.HasSkipModeReferenceLists(
                        skipModeParameters.FirstReferenceFrame, skipModeParameters.SecondReferenceFrame);

                    byte availableReferences = this.picture.Parent.AvailableReferenceMask;
                    bool searchesSkipMode = (availableReferences & (1 << (int)skipModeParameters.FirstReferenceFrame)) != 0 &&
                        (availableReferences & (1 << (int)skipModeParameters.SecondReferenceFrame)) != 0 &&
                        skipModeListsReady;

                    // A searched skip mode prices the symbol itself, once its vectors pass the build_cur_mv() test.
                    if (selectedStatistics.Cost != long.MaxValue && skipModeListsReady && !searchesSkipMode)
                    {
                        selectedStatistics.Add(this.rateMultiplier, syntaxStatistics);
                    }

                    if (searchesSkipMode)
                    {
                        this.SelectSkipModeBlock(
                            writer,
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

                    // Skip mode replaces a result that nothing else produced, but a skip mode at or above the block
                    // budget still leaves the block without one. Reference: the best_rd >= best_rd_so_far return at
                    // the end of av1_rd_pick_inter_mode(), after rd_pick_skip_mode() sets best_rd.
                    bool noMode = modeInfo.Block.SkipMode
                        ? selectedStatistics.Cost >= this.blockCostLimit
                        : comparesLeftoverEstimate;

                    this.SelectedBlockStatistics = noMode ? Av1RateDistortionStatistics.Invalid : selectedStatistics;
                }

                if (modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra)
                {
                    // The block codes no residual when the last transform search left its skip flag set, or when the
                    // mode search winner was skippable, even after a winner refinement that kept coefficients. The
                    // rate-distortion result keeps the refined cost. Reference: the skip_txfm updates at the end of
                    // av1_rd_pick_inter_mode(), stored by store_coding_context() for av1_encode_sb().
                    if (!modeInfo.Block.Skip && (this.transformSearchSkip || this.SelectedBlockStatistics.AllTransformsEmpty))
                    {
                        Av1TransformSize maximumSize = this.picture.Parent.FrameHeader.CodedLossless
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
                        macroBlock,
                        tileIndex,
                        blockOrigin,
                        modeInfo,
                        block,
                        interVector,
                        interSecondaryVector,
                        interStates);

                    this.keepSearchedZeroBlockTypes = false;
                    this.picture.SetDisplacementVector(modeInfoPosition, interVector);
                    if (modeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                    {
                        // The encoder's spatial motion stack must retain both vectors of the selected pair; otherwise
                        // later compound blocks would derive a different nearest pair than the decoder.
                        this.picture.SetSecondaryDisplacementVector(modeInfoPosition, interSecondaryVector);
                    }
                }
            }

            if (this.SelectedBlockStatistics.Cost == long.MaxValue)
            {
                return;
            }

            // A frame coded from the source of an alternate reference leaves the thresholds as they were.
            // Reference: the is_src_frame_alt_ref test before av1_update_rd_thresh_fact() in av1_rd_pick_inter_mode().
            if (isInterFrame && this.picture.Parent.SpeedSettings.AdaptiveModeThresholdLevel != 0 &&
                !this.picture.Parent.IsSourceAlternateReference)
            {
                // Update only after residual refinement, palette, and skip-mode selection have all finished.
                // Partition replay returns earlier and must not count the retained winner a second time.
                Av1ModeThresholds.Update(
                    this.blockWorkspace.ModeThresholdFactors,
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
        /// Runs <see cref="EvaluatePartitionLeafCore"/> at the rate multiplier of the block. Reference: the setup_block_rdmult() call of
        /// pick_sb_modes().
        /// </summary>
        private Av1RateDistortionStatistics EvaluatePartitionLeaf(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize,
            Av1PartitionType partitionType,
            Av1EncoderPartitionTree.ModeContext context,
            long costLimit,
            bool publishContexts,
            bool publishCoefficientContexts,
            bool intraEncodeFollows = false)
        {
            // A bound already negative returns before the block setup, so neither the block variance nor the error per
            // bit changes. Reference: the use_best_rd_for_pruning return of pick_sb_modes().
            if (costLimit == NegativeLeafBound)
            {
                return Av1RateDistortionStatistics.Invalid;
            }

            // The block takes its segment, and the variance and complexity modes price it at the segment quantizer. A
            // frame that refreshes its variance segments places the block by its source variance, but still searches
            // it with the quantizer of segment 0. Reference: av1_set_offsets() and the aq branches of
            // setup_block_rdmult() in pick_sb_modes().
            this.SetBlockSegment(blockOrigin, blockSize);
            int rateSegmentId = -1;
            Av1AdaptiveQuantizationMode adaptiveQuantization = this.picture.Parent.EncoderOptions.AdaptiveQuantizationMode;
            if (adaptiveQuantization == Av1AdaptiveQuantizationMode.Variance)
            {
                if (this.picture.Parent.VarianceSegmentRefresh)
                {
                    this.blockSegmentId = this.GetVarianceSegmentId(blockOrigin, blockSize);
                }

                rateSegmentId = this.blockSegmentId;
            }
            else if (adaptiveQuantization is Av1AdaptiveQuantizationMode.Complexity or Av1AdaptiveQuantizationMode.CyclicRefresh)
            {
                rateSegmentId = this.blockSegmentId;
            }

            // The search sets the motion vector error per bit from the block multiplier. Reference: the
            // av1_set_error_per_bit() call of pick_sb_modes().
            int savedRateMultiplier = this.rateMultiplier;
            this.rateMultiplier = this.GetBlockRateMultiplier(blockOrigin, blockSize, rateSegmentId);
            this.blockWorkspace.ErrorPerBitRateMultiplier = this.rateMultiplier;
            Av1RateDistortionStatistics result = this.EvaluatePartitionLeafCore(writer, macroBlock, blockOrigin, tileIndex, blockSize, partitionType, context, costLimit, publishContexts, publishCoefficientContexts, intraEncodeFollows);
            this.rateMultiplier = savedRateMultiplier;

            // Complexity adaptive quantization places each searched block of 16x16 or larger by its rate. The block
            // size order puts the 4:1 sizes after 128x128. Reference: the av1_caq_select_segment() call of
            // pick_sb_modes().
            if (result.Rate != int.MaxValue && blockSize >= Av1BlockSize.Block16x16 &&
                adaptiveQuantization == Av1AdaptiveQuantizationMode.Complexity && this.picture.Parent.ComplexitySegmentRefresh)
            {
                this.SelectComplexitySegment(blockOrigin, blockSize, result.Rate);
            }

            return result;
        }

        /// <summary>
        /// Writes the complexity segment of a searched block into the encoder segment map. Reference:
        /// av1_caq_select_segment().
        /// </summary>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="projectedRate">The block's searched rate.</param>
        private void SelectComplexitySegment(Point blockOrigin, Av1BlockSize blockSize, int projectedRate)
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
                this.GetLogBlockVariance(blockOrigin, blockSize),
                this.quantization.BaseQIndex,
                this.bitDepth);

            // Reference: set_segment_id().
            Span<byte> map = parent.EncoderSegmentMap.Span;
            for (int row = 0; row < visibleRows; row++)
            {
                map.Slice(((modeInfoRow + row) * common.ModeInfoColumnCount) + modeInfoColumn, visibleColumns).Fill(segment);
            }
        }

        private Av1RateDistortionStatistics EvaluatePartitionLeafCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
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
            this.encodedWithoutCoefficients = false;
            this.SearchBlock(
                writer,
                macroBlock,
                blockOrigin,
                tileIndex,
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
                Displacement = modeInfo.Block.UseIntraBlockCopy ? this.picture.GetDisplacementVector(modeInfoPosition) : default,
                SecondaryDisplacement = default,
                Ready = false
            };

            if (modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra || modeInfo.Block.UseIntraBlockCopy)
            {
                context.Snapshot.Displacement = this.picture.GetDisplacementVector(modeInfoPosition);
                context.Snapshot.SecondaryDisplacement = modeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra
                    ? this.picture.GetSecondaryDisplacementVector(modeInfoPosition)
                    : default;
            }

            this.RetainModeContext(context, lumaArea, chromaArea, this.codedAreaLuma - lumaArea, this.codedAreaChroma - chromaArea);
            if (this.encodedWithoutCoefficients)
            {
                modeInfo.Block.Skip = true;
            }

            // The leaf keeps the transform grid its own search produced. The shared coefficient buffer
            // belongs to whichever block wrote it last, so a grid taken from there can name a transform
            // type this block's size does not allow. Reference: the
            // av1_copy_array(ctx->tx_type_map, xd->tx_type_map, ctx->num_4x4_blk) call of
            // av1_rd_pick_intra_sby_mode(), against the per-block tx_type_map_ of pick_sb_modes().
            if (modeInfo.Block.ReferenceFrame <= Av1ReferenceFrameType.Intra && !modeInfo.Block.UseIntraBlockCopy &&
                !this.UsesEstimatedInterSearch)
            {
                // Only a luma search that ran for this block left its grid here. A winner that names
                // another size belongs to a different block, so this leaf keeps what it retained.
                Av1EncoderPartitionTree.ModeContext lumaWinner = this.blockWorkspace.GetIntraWinnerContext(1);
                if (lumaWinner.Snapshot.ModeInfo.Block.BlockSize == blockSize)
                {
                    CopyWinnerTransformStates(
                        lumaWinner.GetTransformStates(Av1Plane.Y),
                        context.GetTransformStates(Av1Plane.Y));
                }
            }

            // An intra winner is quantized again by the encode that follows it, which publishes the coefficient
            // contexts it codes. Reference: the encode_superblock() after pick_sb_modes().
            bool publishSearchCoefficientContexts = publishCoefficientContexts &&
                !(intraEncodeFollows && modeInfo.Block.ReferenceFrame <= Av1ReferenceFrameType.Intra);

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

        private void RetainModeContext(
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

                // Every entry of the retained grid is written, not just the part the coded area reached.
                // An entry left behind by another block can name a transform type this block's size does not
                // allow. Reference: av1_copy_array(ctx->tx_type_map, ..., ctx->num_4x4_blk), which covers the
                // whole map.
                // The luma grid comes from the block's own search, never from the shared coefficient
                // buffer, so no entry can belong to another block. Chroma still reads that buffer.
                // Reference: av1_update_state(), which points xd->tx_type_map at ctx->tx_type_map with
                // stride mi_size_wide[bsize].
                // An intra block's luma search hands its own grid over afterwards, so this clear is what
                // that copy lands on. An inter block has no such grid, so its luma still comes from the
                // shared buffer its own search wrote.
                // The estimated inter-frame search encodes an intra winner straight into the coefficient buffer
                // and hands no grid over, so its leaf reads that buffer like an inter leaf. Reference: the
                // tx_type_map that encode_block_intra() reads after av1_nonrd_pick_intra_mode().
                bool lumaFromOwnSearch = plane == Av1Plane.Y &&
                    snapshot.ModeInfo.Block.ReferenceFrame <= Av1ReferenceFrameType.Intra &&
                    !snapshot.ModeInfo.Block.UseIntraBlockCopy &&
                    !this.UsesEstimatedInterSearch;

                Span<Av1EncoderTransformBlockState> retainedGrid = context.GetTransformStates(plane);
                retainedGrid.Clear();
                if (!lumaFromOwnSearch)
                {
                    this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, plane)
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

        private Av1RateDistortionStatistics ReconstructPartitionLeaf(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1EncoderPartitionTree.ModeContext context,
            bool publishContexts,
            bool modeSearchReused,
            bool encodeFollows)
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
            bool codesNothing = modeSearchReused &&
                snapshot.ModeInfo.Block.ReferenceFrame <= Av1ReferenceFrameType.Intra && !snapshot.ModeInfo.Block.UseIntraBlockCopy;

            if (snapshot.ModeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra || snapshot.ModeInfo.Block.UseIntraBlockCopy)
            {
                Av1TransformSize rootSize = this.picture.Parent.FrameHeader.CodedLossless
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

                    if (!this.picture.Parent.FrameHeader.CodedLossless)
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
                    Av1TransformSize chromaTransformSize = this.picture.Parent.FrameHeader.CodedLossless
                            ? Av1TransformSize.Size4x4
                            : snapshot.ModeInfo.Block.BlockSize.GetMaxUvTransformSize(subX != 0, subY != 0);

                    Av1BlockSize chromaBlockSize = snapshot.ModeInfo.Block.BlockSize.GetSubsampled(subX != 0, subY != 0);
                    Size chromaExtent = GetCodedTransformExtent(macroBlock, chromaBlockSize, chromaTransformSize, subX, subY);
                    int chromaTransformCount = chromaExtent.Width * chromaExtent.Height / chromaTransformSize.GetSize2d();
                    int chromaStateStride = chromaTransformSize.GetSize2d() / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                    ReadOnlySpan<Av1EncoderTransformBlockState> blueStates = context.GetTransformStates(Av1Plane.U);
                    ReadOnlySpan<Av1EncoderTransformBlockState> redStates = context.GetTransformStates(Av1Plane.V);
                    for (int index = 0; !this.picture.Parent.FrameHeader.CodedLossless && index < chromaTransformCount; index++)
                    {
                        states[64 + index] = blueStates[index * chromaStateStride];
                        states[80 + index] = redStates[index * chromaStateStride];
                    }
                }

                // A block whose every transform lost its coefficients is written as skipped.
                // Reference: the skip_txfm initialization of av1_encode_sb().
                if (!this.ReconstructSelectedInterBlock(
                    writer,
                    macroBlock,
                    tileIndex,
                    blockOrigin,
                    snapshot.ModeInfo,
                    snapshot.Block,
                    snapshot.Displacement,
                    snapshot.SecondaryDisplacement,
                    states))
                {
                    snapshot.ModeInfo.Block.Skip = true;
                    this.picture.GetMacroBlockModeInfo(modeInfoPosition).Block.Skip = true;
                }

                if (!context.Snapshot.ModeInfo.Block.Skip && !this.picture.Parent.FrameHeader.CodedLossless)
                {
                    int unit = Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;
                    RetainEncodedZeroBlockTypes(
                        context.GetTransformStates(Av1Plane.Y)[..(retainedArea / unit)],
                        this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.Y).Slice(lumaArea / unit, retainedArea / unit));
                }

                this.picture.SetDisplacementVector(modeInfoPosition, snapshot.Displacement);
                if (snapshot.ModeInfo.Block.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra)
                {
                    this.picture.SetSecondaryDisplacementVector(modeInfoPosition, snapshot.SecondaryDisplacement);
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
                // A reused decision hands back the cost it already measured and codes nothing. The sibling
                // encode that follows this leaf leaves its samples behind for the next leaf to predict from.
                // Reference: the rd_mode_is_ready branch of pick_sb_modes(), which returns the stored rate,
                // distortion and cost and does no more.
                Size reusedLumaExtent = GetCodedTransformExtent(
                    macroBlock, blockSize, snapshot.ModeInfo.Block.TransformSize, 0, 0);

                this.codedAreaLuma += reusedLumaExtent.Width * reusedLumaExtent.Height;
                if (snapshot.Block.HasChroma)
                {
                    int subX = this.source.ChromaSubsamplingX;
                    int subY = this.source.ChromaSubsamplingY;
                    Av1TransformSize reusedChromaTransform = this.picture.Parent.FrameHeader.CodedLossless
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
                this.ReconstructSelectedIntraBlock(writer, macroBlock, blockOrigin, tileIndex, context);
            }

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
                    snapshot.Palette,
                    !codesNothing && !encodeFollows);
            }

            this.SelectedBlockStatistics = snapshot.Statistics;
            return snapshot.Statistics;
        }

#pragma warning disable CA1517 // False positive: https://github.com/dotnet/sdk/issues/53388
        /// <summary>
        /// Leaves DCT_DCT as the retained type of every luma transform block that an encode of an inter decision
        /// quantized to nothing, so a later encode of the same decision transforms it with the default type.
        /// Reference: the update_txk_array() call of encode_block(), which writes the tx_type_map of the block's
        /// PICK_MODE_CONTEXT.
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

        private void ReconstructSelectedIntraBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            Av1EncoderPartitionTree.ModeContext context)
        {
            Av1EncoderPartitionTree.ModeSnapshot snapshot = context.Snapshot;
            this.SetCodedBlockSegment(blockOrigin, snapshot.ModeInfo.Block.BlockSize, snapshot.ModeInfo.Block.SegmentId);
            Av1BlockSize blockSize = snapshot.ModeInfo.Block.BlockSize;
            bool usesChromaFromLuma = snapshot.Block.HasChroma && snapshot.ModeInfo.Block.UvMode == Av1ChromaPredictionMode.ChromaFromLuma;
            Av1EncoderModeDecisionWorkspace<TSample> workspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>();
            Span<short> lumaQ3 = workspace.ChromaFromLumaSamples;
            int planeCount = snapshot.Block.HasChroma ? 3 : 1;
            int chromaArea = 0;

            // Every transform block of every plane uses the same workspace buffers and rate tables.
            Av1TransformBlockBuffers transformBuffers = new(this.blockWorkspace, writer);
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Av1Plane plane = (Av1Plane)planeIndex;
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
                    : this.picture.Parent.FrameHeader.CodedLossless
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
                Span<byte> transformContexts = workspace.TransformContexts;
                Span<byte> topContexts = transformContexts[..contextWidth];
                Span<byte> leftContexts = transformContexts.Slice(contextWidth, contextHeight);
                Av1NeighborArrayUnit<byte> neighborArray = plane switch
                {
                    Av1Plane.Y => this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex],
                    Av1Plane.U => this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex],
                    _ => this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex]
                };

                Av1NeighborEdges<byte> neighbors = neighborArray.GetEdges();
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
                        Av1TransformBlockEncoder.GetPlaneSpan(lumaReconstruction, new Point(planeOrigin.X << subX, planeOrigin.Y << subY)),
                        lumaReconstruction.Stride,
                        lumaQ3,
                        transformSize,
                        this.GetChromaFromLumaExtent(
                            macroBlock, blockOrigin, blockSize, snapshot.ModeInfo.Block.TransformSize, subX, subY),
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
                Span<Av1EncoderTransformBlockState> outputStates = this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, plane);
                Span<int> outputCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, plane);
                bool smoothEdges = this.UseSmoothIntraEdges(macroBlock, blockOrigin, blockSize, plane);
                ReadOnlySpan<TSample> sourceSamples = sourcePlane.Samples;
                Av1PartitionType partitionType = macroBlock.GetRelativeModeInfo(0).Block.PartitionType;

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
                                if (paletteSize > 0)
                                {
                                    TOperator.PreparePalette(
                                        sourceTransform,
                                        sourcePlane.Stride,
                                        snapshot.Palette.GetColors(plane),
                                        paletteMap.GetSubRegion(x, y, transformWidth, transformHeight),
                                        prediction,
                                        residual,
                                        transformSize);
                                }
                                else
                                {
                                    this.PrepareTransformReferenceSamples(
                                        reconstructedBlock,
                                        destinationPlane.Stride,
                                        blockOrigin,
                                        blockSize,
                                        macroBlock,
                                        partitionType,
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
                                        TOperator.SubtractPrediction(
                                            sourceTransform, sourcePlane.Stride, prediction, residual, transformSize.GetWidth(), transformSize.GetHeight());
                                    }
                                    else if (planeIndex == 0 && snapshot.Block.FilterIntraMode != Av1FilterIntraMode.AllFilterIntraModes)
                                    {
                                        TOperator.PrepareFilterIntra(
                                            transformBuffers.TransformWorkspace,
                                            sourceTransform,
                                            sourcePlane.Stride,
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
                                            transformBuffers.TransformWorkspace,
                                            sourceTransform,
                                            sourcePlane.Stride,
                                            prediction,
                                            transformSize.GetWidth(),
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
                                    in transformBuffers,
                                    blockContext,
                                    false,
                                    blockOrigin,
                                    blockSize,
                                    transformOrigin,
                                    plane,
                                    transformSize,
                                    prediction,
                                    residual,
                                    transformSize.GetWidth(),
                                    states[stateIndex],
                                    snapshot.ModeInfo.Block.Skip,
                                    coefficientOffset + (transformIndex * sampleCount));

                                int outputOffset = coefficientOffset + (transformIndex * sampleCount);
                                Av1EncoderTransformBlockState outputState =
                                    outputStates[outputOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];

                                // encode_block_intra returns a luma transform block that quantized to nothing
                                // to DCT_DCT, so a later pass over the same block transforms it with the
                                // default type instead of the one the search happened to pick.
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
            Av1EncoderPaletteInfo paletteInfo,
            bool publishCoefficientContexts)
        {
            Av1BlockSize blockSize = modeInfo.Block.BlockSize;
            Av1TransformSize transformSize = modeInfo.Block.TransformSize;
            Size blockDimensions = new(blockSize.GetWidth(), blockSize.GetHeight());

            // The tile edges serve every leaf, so they are read once.
            Av1NeighborEdges<byte> transformContexts = this.picture.TransformFunctionContexts[tileIndex].GetEdges();
            Av1NeighborEdges<byte> coefficientContexts = this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
            Span<Av1EncoderTransformBlockState> lumaStates =
                this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, Av1Plane.Y);

            Span<int> lumaCoefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, Av1Plane.Y);
            Size frameContextSize = new(this.picture.Parent.FrameHeader.ModeInfoColumnCount, this.picture.Parent.FrameHeader.ModeInfoRowCount);
            bool interTransform = modeInfo.Block.ReferenceFrame > Av1ReferenceFrameType.Intra || modeInfo.Block.UseIntraBlockCopy;
            if (interTransform && !this.picture.Parent.FrameHeader.CodedLossless)
            {
                // Publish the retained leaves in coefficient order. A root-sized write would erase the smaller
                // bottom and right edge contexts needed by the next partition candidate.
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
                    transformContexts.Write((byte)width, origin, leafDimensions, Av1NeighborArrayUnit<byte>.UnitMask.Top);
                    transformContexts.Write((byte)height, origin, leafDimensions, Av1NeighborArrayUnit<byte>.UnitMask.Left);

                    if (publishCoefficientContexts)
                    {
                        byte context = lumaStates[
                            coefficientOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount]
                            .CoefficientContext;

                        Av1TileWriter.UpdateCoefficientContexts(
                            coefficientContexts.Top.Slice(coefficientContexts.GetTopIndex(origin), leafSize.Get4x4WideCount()),
                            coefficientContexts.Left.Slice(coefficientContexts.GetLeftIndex(origin), leafSize.Get4x4HighCount()),
                            context,
                            origin,
                            frameContextSize);
                    }

                    coefficientOffset += leafSize.GetSize2d();
                }
            }
            else
            {
                transformContexts.Write((byte)transformSize.GetWidth(), blockOrigin, blockDimensions, Av1NeighborArrayUnit<byte>.UnitMask.Top);
                transformContexts.Write((byte)transformSize.GetHeight(), blockOrigin, blockDimensions, Av1NeighborArrayUnit<byte>.UnitMask.Left);
            }

            // A reused decision codes nothing here, so its coefficients are not in the shared buffer yet. The
            // encode that follows it publishes their contexts.
            if (publishCoefficientContexts && (!interTransform || this.picture.Parent.FrameHeader.CodedLossless))
            {
                PublishCoefficientContexts(
                    in coefficientContexts,
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

                this.picture.PaletteContexts[tileIndex].UnitModeWrite(
                    paletteInfo,
                    blockOrigin,
                    blockDimensions,
                    PaletteContextMask);
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
            Av1NeighborEdges<byte> blueEdges = this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
            Av1NeighborEdges<byte> redEdges = this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
            PublishCoefficientContexts(
                in blueEdges,
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
                in redEdges,
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

        private void SavePartitionTrialContexts(
            Point blockOrigin,
            ushort tileIndex,
            Av1BlockSize blockSize)
        {
            Span<byte> storage = this.blockWorkspace.GetPartitionContextStorage(blockSize);
            int offset = 0;
            SaveNeighborEdges(
                this.picture.PartitionContexts[tileIndex].GetEdges(),
                blockOrigin,
                blockSize.Get4x4WideCount(),
                blockSize.Get4x4HighCount(),
                storage,
                ref offset);

            SaveNeighborEdges(
                this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex].GetEdges(),
                blockOrigin,
                blockSize.Get4x4WideCount(),
                blockSize.Get4x4HighCount(),
                storage,
                ref offset);

            SaveNeighborEdges(
                this.picture.TransformFunctionContexts[tileIndex].GetEdges(),
                blockOrigin,
                blockSize.Get4x4WideCount(),
                blockSize.Get4x4HighCount(),
                storage,
                ref offset);

            if (this.picture.Parent.FrameHeader.AllowScreenContentTools)
            {
                SaveNeighborEdges(
                    this.picture.PaletteContexts[tileIndex].GetEdges(),
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
                this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex].GetEdges(),
                chromaOrigin,
                chromaBlockSize.Get4x4WideCount(),
                chromaBlockSize.Get4x4HighCount(),
                storage,
                ref offset);

            SaveNeighborEdges(
                this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex].GetEdges(),
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
                this.picture.PartitionContexts[tileIndex].GetEdges(),
                blockOrigin,
                blockSize.Get4x4WideCount(),
                blockSize.Get4x4HighCount(),
                storage,
                ref offset);

            RestoreNeighborEdges(
                this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex].GetEdges(),
                blockOrigin,
                blockSize.Get4x4WideCount(),
                blockSize.Get4x4HighCount(),
                storage,
                ref offset);

            RestoreNeighborEdges(
                this.picture.TransformFunctionContexts[tileIndex].GetEdges(),
                blockOrigin,
                blockSize.Get4x4WideCount(),
                blockSize.Get4x4HighCount(),
                storage,
                ref offset);

            if (this.picture.Parent.FrameHeader.AllowScreenContentTools)
            {
                RestoreNeighborEdges(
                    this.picture.PaletteContexts[tileIndex].GetEdges(),
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
                this.picture.CbDcSignLevelCoefficientNeighbors[tileIndex].GetEdges(),
                chromaOrigin,
                chromaBlockSize.Get4x4WideCount(),
                chromaBlockSize.Get4x4HighCount(),
                storage,
                ref offset);

            RestoreNeighborEdges(
                this.picture.CrDcSignLevelCoefficientNeighbors[tileIndex].GetEdges(),
                chromaOrigin,
                chromaBlockSize.Get4x4WideCount(),
                chromaBlockSize.Get4x4HighCount(),
                storage,
                ref offset);
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

        private Av1RateDistortionStatistics GetRegularBlockCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Av1RateDistortionStatistics modeStatistics)
        {
            int rateAdjustment = writer.GetSkipCost(false, Av1TileWriter.GetSkipContext(macroBlock));

            return new(this.rateMultiplier, modeStatistics.Rate + rateAdjustment, modeStatistics.Distortion);
        }

        /// <summary>
        /// Inserts a completed candidate into the ordered winner list and retains its palette indices.
        /// </summary>
        /// <param name="candidate">The candidate syntax and completed search cost.</param>
        /// <param name="blockSize">The coding block dimensions.</param>
        private void RetainLumaCandidate(LumaCandidate candidate, Av1BlockSize blockSize)
        {
            if (candidate.Cost == long.MaxValue)
            {
                return;
            }

            Av1EncoderModeDecisionWorkspace<TSample> workspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>();
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
                    workspace.GetWinnerPaletteMap(index - 1)[..sampleCount].CopyTo(workspace.GetWinnerPaletteMap(index));
                }
            }

            this.lumaCandidates[position] = candidate;
            this.lumaCandidateCount = Math.Min(this.lumaCandidateCount + 1, limit);
            if (candidate.Palette.PaletteSizes[0] > 0)
            {
                this.superblock.Workspace.GetPaletteMaps()
                    .GetMap(Av1PlaneType.Y, blockSize.GetWidth(), blockSize.GetHeight())
                    .CopyTo(workspace.GetWinnerPaletteMap(position));
            }
        }

        private Av1PredictionMode SelectLumaMode(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Span<Av1EncoderTransformBlockState> retainedStates,
            long interCostLimit,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out Av1FilterIntraMode selectedFilterIntraMode,
            out Av1TransformSize selectedTransformSize,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            return this.SelectLumaModeCore(
                writer,
                macroBlock,
                blockOrigin,
                blockSize,
                tileIndex,
                retainedStates,
                interCostLimit,
                ref paletteInfo,
                out selectedAngleDelta,
                out selectedFilterIntraMode,
                out selectedTransformSize,
                out selectedStatistics);
        }

        private Av1PredictionMode SelectLumaModeCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
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
                macroBlock,
                blockOrigin,
                blockSize,
                tileIndex,
                retainedStates,
                interCostLimit,
                ref paletteInfo,
                out selectedAngleDelta,
                out selectedFilterIntraMode,
                out selectedTransformSize,
                out selectedStatistics);

            if (!this.picture.Parent.FrameHeader.IsIntra && selectedStatistics.Cost != long.MaxValue)
            {
                // Every mode writes its best depth into the shared states, so they end with the last mode tried.
                // The winner's grid was kept when it won; hand it back so the caller retains the winner's types.
                // Reference: the ctx->tx_type_map that av1_rd_pick_intra_sby_mode() copies on each improvement.
                CopyWinnerTransformStates(this.blockWorkspace.GetIntraWinnerContext(1).GetTransformStates(Av1Plane.Y), retainedStates);
            }

            if (selectedStatistics.Cost == long.MaxValue)
            {
                return mode;
            }

            // An inter frame keeps the luma winner of the mode search; only an intra frame refines it.
            // Reference: av1_search_intra_uv_modes_in_interframe() after the luma loop of
            // search_intra_modes_in_interframe(), against av1_rd_pick_intra_sby_mode().
            Av1PredictionMode refinedMode = !this.picture.Parent.FrameHeader.IsIntra
                ? mode
                : this.RefineLumaMode(
                    writer,
                    macroBlock,
                    blockOrigin,
                    blockSize,
                    tileIndex,
                    retainedStates,
                    mode,
                    ref paletteInfo,
                    ref selectedAngleDelta,
                    ref selectedFilterIntraMode,
                    ref selectedTransformSize,
                    ref selectedStatistics);

            // The chroma search encodes the luma plane again before it starts, so chroma predicts from
            // the refined winner rather than the candidate the first search left behind, and every luma
            // transform block that quantized away returns to DCT_DCT. It does so only for a block that
            // may still choose chroma-from-luma, because only that mode reads the luma reconstruction.
            // Reference: the store_cfl_required_rdo() gate on the av1_encode_intra_block_plane() call of
            // av1_rd_pick_intra_sbuv_mode().
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
                Av1EncoderPartitionTree.ModeContext refinedWinner = this.blockWorkspace.GetIntraWinnerContext(1);
                Av1MacroBlockModeInfo refinedModeInfo = macroBlock.GetRelativeModeInfo(0);
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

                // The retained storage is laid out from the size the snapshot records, so it names the
                // block that was searched before anything reads or writes its transform grid.
                refinedWinner.Snapshot.ModeInfo.Block.BlockSize = blockSize;

                // The winner context already carries the transform grid its own search chose, so only the
                // palette map moves across here. Reading the shared coefficient buffer again would lend the
                // block a grid that belongs to the last candidate tried.
                this.RetainPaletteMap(refinedWinner);
                int retainedLumaArea = this.codedAreaLuma;
                this.ReconstructSelectedIntraBlock(writer, macroBlock, blockOrigin, tileIndex, refinedWinner);
                this.codedAreaLuma = retainedLumaArea;
            }

            // The frame holds the luma that the search left. That is the winner when chroma-from-luma coded it again, else the last luma trial.
            // The chroma search writes only the chroma planes, so this measure is the same as one after the chroma search.
            // Reference: the pd->dst that adjust_rdcost() reads on intra_rd_stats in search_intra_modes_in_interframe().
            Av1PlaneRegion<TSample> lumaFrame = this.reconstruction.GetPlane(Av1Plane.Y);
            ReadOnlySpan<TSample> lumaFrameBlock = Av1TransformBlockEncoder.GetPlaneSpan(lumaFrame, blockOrigin);
            if (selectedStatistics.Cost != long.MaxValue && this.ChargesSmoothing)
            {
                this.intraSmoothingOffset = this.GetIntraSmoothingOffset(blockOrigin, blockSize, lumaFrameBlock, lumaFrame.Stride);
            }
            else if (selectedStatistics.Cost != long.MaxValue && this.ChargesHighBitDepthTextureLoss)
            {
                // At a high bit depth, the offset uses the mode of the luma winner. An intra mode never skips here.
                // Reference: the high bit depth branch of adjust_rdcost() on intra_rd_stats.
                this.GetVarianceStatistics(blockOrigin, blockSize, lumaFrameBlock, lumaFrame.Stride, out long sourceVariance, out long sampleVariance);
                bool smoothMode = IsSmoothTextureMode(refinedMode);
                this.intraSmoothingOffset = GetTextureLossOffset(sourceVariance, sampleVariance, blockSize, false, smoothMode, false, default);
            }

            return refinedMode;
        }

        private Av1PredictionMode RefineLumaMode(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Span<Av1EncoderTransformBlockState> retainedStates,
            Av1PredictionMode mode,
            ref Av1EncoderPaletteInfo paletteInfo,
            ref int selectedAngleDelta,
            ref Av1FilterIntraMode selectedFilterIntraMode,
            ref Av1TransformSize selectedTransformSize,
            ref Av1RateDistortionStatistics selectedStatistics)
        {
            return this.RefineLumaModeCore(
                writer,
                macroBlock,
                blockOrigin,
                blockSize,
                tileIndex,
                retainedStates,
                mode,
                ref paletteInfo,
                ref selectedAngleDelta,
                ref selectedFilterIntraMode,
                ref selectedTransformSize,
                ref selectedStatistics);
        }

        private Av1PredictionMode RefineLumaModeCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Span<Av1EncoderTransformBlockState> retainedStates,
            Av1PredictionMode mode,
            ref Av1EncoderPaletteInfo paletteInfo,
            ref int selectedAngleDelta,
            ref Av1FilterIntraMode selectedFilterIntraMode,
            ref Av1TransformSize selectedTransformSize,
            ref Av1RateDistortionStatistics selectedStatistics)
        {
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            bool refine = !this.picture.Parent.FrameHeader.CodedLossless &&
                (settings.IntraTransformTypeSearchLevel != 0 || settings.EnableWinnerCoefficientOptimization || settings.DeferTransformSizeSearch);

            if (refine && settings.PruneIntraWinnerByVariance)
            {
                int varianceThreshold = 64 - (48 * this.blockQIndex / 256);
                refine = this.GetSourceVariance(blockOrigin, blockSize) >= varianceThreshold;
            }

            int selectedMapIndex = paletteInfo.PaletteSizes[0] > 0 ? 0 : -1;
            Av1EncoderModeDecisionWorkspace<TSample> workspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>();
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
                    this.picture.TransformFunctionContexts[tileIndex].GetEdges(), macroBlock, blockOrigin, blockSize);

                int paletteDisabledCost = paletteAllowed
                    ? writer.GetPaletteYModeCost(
                        false,
                        Av1TileWriter.GetPaletteBlockSizeContext(blockSize),
                        Av1TileWriter.GetPaletteYModeContext(this.picture.PaletteContexts[tileIndex].GetEdges(), macroBlock, blockOrigin))
                    : 0;

                int maximumDepth = this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select
                    ? width == height ? settings.IntraSquareTransformSearchDepth : settings.IntraRectangularTransformSearchDepth
                    : 0;

                int sourceVariance = maximumDepth > 1 || settings.PruneIntraTransformDepth
                    ? this.GetSourceVariance(blockOrigin, blockSize)
                    : 0;

                Span<TSample> samples = workspace.GetCandidateReconstruction(0);

                // Every retained predictor of the block shares these neighbor values, so they are read once.
                LumaBlockState blockState = this.GetLumaBlockState(writer, macroBlock, blockOrigin, blockSize);

                // Repeat transform search for every retained predictor with winner-stage settings.
                // Each trial starts from the same external block edges and its own palette map.
                for (int index = 0; index < this.lumaCandidateCount; index++)
                {
                    LumaCandidate candidate = this.lumaCandidates[index];
                    int paletteSize = candidate.Palette.PaletteSizes[0];
                    if (paletteSize > 0)
                    {
                        map.CopyFrom(workspace.GetWinnerPaletteMap(index));
                    }

                    // With the breakout, the depths are bounded by the best candidate so far; without it the winner
                    // stage searches every depth in full. Reference: the ref_best_rd of intra_block_yrd().
                    long candidateLimit = selectedStatistics.Cost;
                    Av1RateDistortionStatistics statistics = this.ChooseUniformTransformSize(
                        writer,
                        macroBlock,
                        sourcePlane,
                        reconstructionPlane,
                        blockOrigin,
                        blockSize,
                        in blockState,
                        blockSize.GetMaximumTransformSize(),
                        maximumDepth,
                        tileIndex,
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
                        samples,
                        workspace.CandidateTransformBlocks,
                        retainedStates,
                        out Av1TransformSize size);

                    if (statistics.Cost < candidateLimit)
                    {
                        mode = candidate.Mode;
                        selectedAngleDelta = candidate.AngleDelta;
                        selectedFilterIntraMode = candidate.FilterMode;

                        // The winner keeps the chroma palette that its chroma search chose; an inter frame searches
                        // chroma before this refinement. Reference: the winner_mode_stats mbmi that
                        // refine_winner_mode_tx() restores, which carries palette_size[1] and the chroma colors.
                        paletteInfo.PaletteSizes[0] = (byte)paletteSize;
                        paletteInfo.SetColors(Av1Plane.Y, candidate.Palette.GetColors(Av1Plane.Y));
                        selectedTransformSize = size;
                        selectedStatistics = statistics;
                        selectedMapIndex = paletteSize > 0 ? index : -1;

                        // The winner keeps the transform grid this candidate produced, so a later candidate that
                        // writes over the shared buffers cannot lend it a type its own size does not allow.
                        // Reference: the av1_copy_array(ctx->tx_type_map, xd->tx_type_map, ctx->num_4x4_blk) call
                        // inside the this_rd < best_rd branch of intra_block_yrd().
                        CopyWinnerTransformStates(
                            retainedStates, this.blockWorkspace.GetIntraWinnerContext(1).GetTransformStates(Av1Plane.Y));
                    }
                }
            }

            this.blockWorkspace.EvaluationStage = this.picture.Parent.FrameHeader.IsIntra
                ? Av1EncoderEvaluationStage.Default
                : Av1EncoderEvaluationStage.Candidate;

            if (selectedMapIndex >= 0 && !this.picture.Parent.FrameHeader.CodedLossless)
            {
                map.CopyFrom(workspace.GetWinnerPaletteMap(selectedMapIndex));
            }

            return mode;
        }

        private Av1PredictionMode SelectLumaPrediction(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Span<Av1EncoderTransformBlockState> retainedStates,
            long interCostLimit,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out Av1FilterIntraMode selectedFilterIntraMode,
            out Av1TransformSize selectedTransformSize,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            return this.SelectLumaPredictionCore(
                writer,
                macroBlock,
                blockOrigin,
                blockSize,
                tileIndex,
                retainedStates,
                interCostLimit,
                ref paletteInfo,
                out selectedAngleDelta,
                out selectedFilterIntraMode,
                out selectedTransformSize,
                out selectedStatistics);
        }

        private Av1PredictionMode SelectLumaPredictionCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            ushort tileIndex,
            Span<Av1EncoderTransformBlockState> retainedStates,
            long interCostLimit,
            ref Av1EncoderPaletteInfo paletteInfo,
            out int selectedAngleDelta,
            out Av1FilterIntraMode selectedFilterIntraMode,
            out Av1TransformSize selectedTransformSize,
            out Av1RateDistortionStatistics selectedStatistics)
        {
            bool intraFrame = this.picture.Parent.FrameHeader.IsIntra;
            bool lossless = this.picture.Parent.FrameHeader.CodedLossless;
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            Av1EncoderModeDecisionWorkspace<TSample> workspace = this.blockWorkspace.GetModeDecisionWorkspace<TSample>();
            Av1EncoderPartitionTree.ModeContext winner = this.blockWorkspace.GetIntraWinnerContext(1);

            // The winner's storage is laid out from the size it records, so it names this block before
            // anything reads or writes its transform grid.
            winner.Snapshot.ModeInfo.Block.BlockSize = blockSize;
            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(Av1Plane.Y);
            Av1PlaneRegion<TSample> reconstructionPlane = this.reconstruction.GetPlane(Av1Plane.Y);

            // The frame block that every mode trial writes is read once.
            ReadOnlySpan<TSample> frameBlock = Av1TransformBlockEncoder.GetPlaneSpan(reconstructionPlane, blockOrigin);
            int width = blockSize.GetWidth();
            int height = blockSize.GetHeight();
            int sampleCount = width * height;
            Span<TSample> samples = workspace.GetCandidateReconstruction(0)[..sampleCount];
            int sizeContext = Av1TileWriter.GetTransformSizeContext(
                this.picture.TransformFunctionContexts[tileIndex].GetEdges(), macroBlock, blockOrigin, blockSize);

            bool paletteAllowed = Av1TileWriter.IsPaletteAllowed(this.picture.Parent.FrameHeader.AllowScreenContentTools, blockSize);
            int paletteDisabledCost = paletteAllowed
                ? writer.GetPaletteYModeCost(
                    false,
                    Av1TileWriter.GetPaletteBlockSizeContext(blockSize),
                    Av1TileWriter.GetPaletteYModeContext(this.picture.PaletteContexts[tileIndex].GetEdges(), macroBlock, blockOrigin))
                : 0;

            Av1TransformSize maximumSize = lossless ? Av1TransformSize.Size4x4 : blockSize.GetMaximumTransformSize();
            int maximumDepth = lossless || settings.DeferTransformSizeSearch ||
                this.picture.Parent.FrameHeader.TransformMode != Av1TransformMode.Select
                    ? 0
                    : width == height ? settings.IntraSquareTransformSearchDepth : settings.IntraRectangularTransformSearchDepth;

            int sourceVariance = maximumDepth > 1 ? this.GetSourceVariance(blockOrigin, blockSize) : 0;
            int visibleWidth = width + (Math.Min(0, macroBlock.ToRightEdge) >> 3);
            int visibleHeight = height + (Math.Min(0, macroBlock.ToBottomEdge) >> 3);
            byte directionalMask = this.GetDirectionalModeSkipMask(sourcePlane, blockOrigin, visibleHeight, visibleWidth);
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
            LumaBlockState blockState = this.GetLumaBlockState(writer, macroBlock, blockOrigin, blockSize);
            LumaModelInputs modelInputs = new(
                workspace,
                this.blockWorkspace,
                sourcePlane,
                reconstructionPlane,
                blockOrigin,
                blockState.PartitionType,
                blockState.SmoothEdges,
                this.picture.Sequence.SequenceHeader.EnableIntraEdgeFilter);

            bool hasLeftNeighbor = macroBlock.IsLeftAvailable;
            bool hasAboveNeighbor = macroBlock.IsUpAvailable;
            Av1PredictionMode leftNeighborMode = hasLeftNeighbor ? macroBlock.GetRelativeModeInfo(-1).Block.Mode : Av1PredictionMode.DC;
            Av1PredictionMode aboveNeighborMode = hasAboveNeighbor
                ? macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride).Block.Mode
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
                        if (this.SelectLumaPalette(
                            writer,
                            macroBlock,
                            blockOrigin,
                            blockSize,
                            tileIndex,
                            retainedStates,
                            64,
                            Av1TileWriter.GetLumaModeCost(writer, macroBlock, blockSize, Av1PredictionMode.DC, 0, intraFrame),
                            ref bestStatistics,
                            ref paletteInfo,
                            ref selectedTransformSize))
                        {
                            bestMode = Av1PredictionMode.DC;
                            selectedAngleDelta = 0;
                            selectedFilterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
                            Av1MacroBlockModeInfo paletteMode = macroBlock.GetRelativeModeInfo(0);
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
                            this.RetainModeContext(winner, this.codedAreaLuma, this.codedAreaChroma, extent.Width * extent.Height, 0);
                            CopyWinnerTransformStates(retainedStates, winner.GetTransformStates(Av1Plane.Y));
                        }
                    }

                    filterBaseMode = bestMode;
                }

                // DC, with its filter intra trials, is the first mode of an inter frame. A luma cost above five
                // quarters of the bound makes it invalid, so no luma winner remains, and stops the search unless
                // the intra-in-inter pruning is off. Reference: the skip_intra_modes return of
                // av1_handle_intra_y_mode(), whose result search_intra_modes_in_interframe() does not count, and its
                // skip_intra_in_interframe break.
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

                    // A cached decision without filter intra excludes every filter mode, and a cached
                    // filter mode excludes the others. The intra search of an inter frame does not read the
                    // cache. Reference: rd_pick_filter_intra_sby(), which only av1_rd_pick_intra_sby_mode() calls.
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

                        // Only the intra frame search prunes odd deltas by cost; an inter frame only reorders them.
                        // Reference: prune_luma_odd_delta_angles_using_rd_cost() in av1_rd_pick_intra_sby_mode(),
                        // against set_y_mode_and_delta_angle() in search_intra_modes_in_interframe().
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

                    // Reference: the mode cache test in av1_rd_pick_intra_sby_mode().
                    // The angle delta stays free. The intra search of an inter frame does not read the cache.
                    // Reference: search_intra_modes_in_interframe().
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
                        int knownRate = writer.GetInterFrameLumaModeCost(mode, blockSize) + interFrameSyntaxRate;

                        // A mode whose known syntax already exceeds the bound is not searched, and the search stops
                        // unless the intra-in-inter pruning is off. The test comes before the directional pruning.
                        // Reference: the known_rd return at the start of av1_handle_intra_y_mode() and the
                        // skip_intra_in_interframe break of search_intra_modes_in_interframe().
                        if (Av1RateDistortion.GetCost(this.rateMultiplier, knownRate, 0) > interCostLimit)
                        {
                            if (settings.IntraInInterPruningLevel != 0)
                            {
                                break;
                            }

                            continue;
                        }
                    }

                    // An inter frame prunes directional modes only for a block that can code an angle delta; an intra
                    // frame prunes them for every block. Reference: the av1_use_angle_delta() test before
                    // prune_intra_mode_with_hog() in av1_handle_intra_y_mode(), against av1_rd_pick_intra_sby_mode().
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

                // The search of a mode arrives bounded by the best mode of an intra frame, and by the inter bound.
                // Reference: the best_rd that av1_rd_pick_intra_sby_mode() passes to
                // av1_pick_uniform_tx_size_type_yrd().
                Av1RateDistortionStatistics modeStatistics = this.ChooseUniformTransformSize(
                    writer,
                    macroBlock,
                    sourcePlane,
                    reconstructionPlane,
                    blockOrigin,
                    blockSize,
                    in blockState,
                    maximumSize,
                    maximumDepth,
                    tileIndex,
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
                    samples,
                    workspace.CandidateTransformBlocks,
                    retainedStates,
                    out Av1TransformSize bestSize);

                if (this.picture.Sequence.SequenceHeader.IsStillPicture && modeStatistics.Cost != long.MaxValue)
                {
                    // Adjust predictor ranking only after its transform grid has been selected. Raw rate
                    // and distortion remain unchanged for chroma and partition cost accumulation.
                    // The frame holds the samples of the last grid trial, not of the winning grid. The choice of a grid restores only the transform type map.
                    // Reference: the tail of choose_tx_size_type_from_rd(). It copies best_txk_type_map into xd->tx_type_map.
                    // Then pd->dst keeps the last uniform_txfm_yrd() trial.
                    double varianceFactor = this.GetIntraVarianceFactor(blockOrigin, blockSize, frameBlock, reconstructionPlane.Stride);
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

                // A mode whose luma cost exceeds five quarters of the bound is not counted, and the search stops
                // unless the intra-in-inter pruning is off. Reference: the skip_intra_modes return of
                // av1_handle_intra_y_mode() and the skip_intra_in_interframe break of
                // search_intra_modes_in_interframe().
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

                // In an inter frame, sharpness 3 adds a charge to the luma cost of a mode that is smoother than the source.
                // The charge comes after the search of the mode and before the comparison. The frame holds the samples of the last grid trial.
                // Reference: adjust_cost() on intra_rd_y in search_intra_modes_in_interframe().
                if (modeStatistics.LumaCost != long.MaxValue && this.ChargesSmoothing)
                {
                    long smoothingOffset = this.GetIntraSmoothingOffset(blockOrigin, blockSize, frameBlock, reconstructionPlane.Stride);
                    modeStatistics.LumaCost += Av1RateDistortion.GetCost(this.rateMultiplier, 0, smoothingOffset);
                }
                else if (modeStatistics.LumaCost != long.MaxValue && !intraFrame && this.ChargesHighBitDepthTextureLoss)
                {
                    // An intra mode never skips here, and a smooth mode pays four times.
                    // Reference: the mbmi->skip_txfm = 0 of search_intra_modes_in_interframe() and the high bit depth branch of adjust_cost().
                    bool smoothMode = IsSmoothTextureMode(mode);
                    this.GetVarianceStatistics(
                        blockOrigin, blockSize, frameBlock, reconstructionPlane.Stride, out long candidateSourceVariance, out long candidateSampleVariance);

                    long offset = GetTextureLossOffset(candidateSourceVariance, candidateSampleVariance, blockSize, false, smoothMode, false, default);
                    modeStatistics.LumaCost = this.ChargeTextureLossCost(modeStatistics.LumaCost, offset, blockOrigin, blockSize, false, false, smoothMode);
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
                    Av1MacroBlockModeInfo selectedMode = macroBlock.GetRelativeModeInfo(0);
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

                    // The retained storage is laid out from the size the snapshot records, so it names the
                    // block that was searched before anything reads its transform grid.
                    winner.Snapshot.ModeInfo.Block.BlockSize = blockSize;

                    Size extent = GetCodedTransformExtent(macroBlock, blockSize, bestSize, 0, 0);
                    this.RetainModeContext(winner, this.codedAreaLuma, this.codedAreaChroma, extent.Width * extent.Height, 0);

                    // The winner keeps the transform grid its own search produced. A later mode writes over
                    // the shared candidate buffers, so the states move across the moment the winner changes
                    // rather than when the block is coded again. Reference: the
                    // av1_copy_array(ctx->tx_type_map, xd->tx_type_map, ctx->num_4x4_blk) call inside the
                    // this_rd < best_rd branch of av1_rd_pick_intra_sby_mode().
                    CopyWinnerTransformStates(retainedStates, winner.GetTransformStates(Av1Plane.Y));

                    if (!intraFrame)
                    {
                        this.lumaCandidateCount = 0;
                    }
                }

                if (intraFrame || improves)
                {
                    this.RetainLumaCandidate(
                        new LumaCandidate { Mode = mode, AngleDelta = angleDelta, FilterMode = filterMode, Cost = modeStatistics.Cost }, blockSize);
                }
            }

            // The luma search leaves whatever its last candidate wrote in the plane. It does not put the
            // winner back: the chroma search codes luma again when chroma-from-luma is still open, and the
            // block encode writes the winner in every case. Reference: the tail of
            // av1_rd_pick_intra_sby_mode(), which restores best_mbmi and the transform type map only.
            selectedStatistics = bestStatistics;
            return bestMode;
        }

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
        /// Chooses the uniform transform size of one luma candidate. Every depth from the start size is measured; the
        /// depths compete on the transform cost alone (coefficients, non-skip flag and size syntax), because the
        /// candidate's own mode or palette syntax is the same at every depth. Only the depth that wins that comparison
        /// represents the candidate, and it is copied into the retained storage when its whole cost, with the mode
        /// syntax, is below the selection limit. Reference: choose_tx_size_type_from_rd(), which
        /// av1_pick_uniform_tx_size_type_yrd() runs for the intra mode search, palette_rd_y() and intra_block_yrd().
        /// </summary>
        /// <remarks>
        /// A depth with a lower transform cost never has a higher whole cost, because the two costs differ only by
        /// the fixed mode rate and one rounding step. A depth that replaces a copied depth is therefore copied too,
        /// and the retained storage always holds the final best depth when that depth passes the selection limit.
        /// </remarks>
        /// <param name="writer">The tile symbol encoder.</param>
        /// <param name="macroBlock">The block's neighbor state.</param>
        /// <param name="sourcePlane">The source luma plane.</param>
        /// <param name="reconstructionPlane">The reconstruction luma plane.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="blockState">The block values that every mode trial shares, read once by the caller.</param>
        /// <param name="startSize">The largest transform size searched.</param>
        /// <param name="maximumDepth">The number of splits the search can make below the start size.</param>
        /// <param name="tileIndex">The tile index.</param>
        /// <param name="sourceVariance">The source variance that gates the depth prune.</param>
        /// <param name="mode">The luma prediction mode.</param>
        /// <param name="angleDelta">The luma angle delta.</param>
        /// <param name="filterMode">The filter intra mode.</param>
        /// <param name="paletteSize">The luma palette size, or zero.</param>
        /// <param name="paletteColors">The luma palette colors.</param>
        /// <param name="paletteHeaderRate">The palette syntax rate.</param>
        /// <param name="paletteDisabledCost">The rate that signals no palette.</param>
        /// <param name="sizeContext">The transform size context.</param>
        /// <param name="referenceLimit">The bound the search arrives with. Reference: ref_best_rd.</param>
        /// <param name="selectionLimit">The whole cost the best depth must be below to be retained.</param>
        /// <param name="samples">The candidate reconstruction storage.</param>
        /// <param name="candidateStates">The candidate transform block storage.</param>
        /// <param name="retainedStates">The retained transform block states.</param>
        /// <param name="bestSize">The transform size of the best depth.</param>
        /// <returns>The statistics of the best depth, or invalid statistics when no depth completed.</returns>
        private Av1RateDistortionStatistics ChooseUniformTransformSize(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Av1PlaneRegion<TSample> sourcePlane,
            Av1PlaneRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            Av1BlockSize blockSize,
            in LumaBlockState blockState,
            Av1TransformSize startSize,
            int maximumDepth,
            ushort tileIndex,
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
            Span<TSample> samples,
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
                // With the breakout, a later depth also stops once it passes the best depth measured so far.
                // Reference: the rd_thresh of choose_tx_size_type_from_rd().
                long costLimit = breakout ? Math.Min(referenceLimit, best.TransformCost) : referenceLimit;
                Av1RateDistortionStatistics statistics = this.GetUniformLumaCandidateCost(
                    writer,
                    macroBlock,
                    sourcePlane,
                    reconstructionPlane,
                    blockOrigin,
                    blockSize,
                    in blockState,
                    size,
                    tileIndex,
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
                    samples,
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

                // A low-contrast block stops splitting once a split costs more than the depth above it; a depth
                // that did not complete never stops the search. Reference: the source_variance < 256 prune of
                // choose_tx_size_type_from_rd().
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
        /// <param name="writer">The tile symbol encoder.</param>
        /// <param name="macroBlock">The block's neighbor state.</param>
        /// <param name="sourcePlane">The source luma plane.</param>
        /// <param name="reconstructionPlane">The reconstruction luma plane.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="blockState">The block values that every mode trial shares, read once by the caller.</param>
        /// <param name="transformSize">The transform size of the grid.</param>
        /// <param name="tileIndex">The tile index.</param>
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
        /// <param name="candidateReconstruction">The candidate reconstruction storage.</param>
        /// <param name="candidateTransformBlocks">The candidate transform block storage.</param>
        /// <param name="skipSmallerTransforms">Whether smaller transform sizes need no search.</param>
        /// <returns>The statistics of the grid, or invalid statistics when the cost limit stopped it.</returns>
        private Av1RateDistortionStatistics GetUniformLumaCandidateCost(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Av1PlaneRegion<TSample> sourcePlane,
            Av1PlaneRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            Av1BlockSize blockSize,
            in LumaBlockState blockState,
            Av1TransformSize transformSize,
            ushort tileIndex,
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
            Span<TSample> candidateReconstruction,
            Span<Av1EncoderTransformBlockState> candidateTransformBlocks,
            out bool skipSmallerTransforms)
        {
            return this.GetUniformLumaCandidateCostCore(
                writer,
                macroBlock,
                sourcePlane,
                reconstructionPlane,
                blockOrigin,
                blockSize,
                in blockState,
                transformSize,
                tileIndex,
                sourceVariance,
                mode,
                angleDelta,
                filterIntraMode,
                paletteSize,
                paletteColors,
                paletteHeaderRate,
                paletteDisabledCost,
                transformSizeContext,
                costLimit,
                candidateReconstruction,
                candidateTransformBlocks,
                out skipSmallerTransforms);
        }

        /// <inheritdoc cref="GetUniformLumaCandidateCost"/>
        private Av1RateDistortionStatistics GetUniformLumaCandidateCostCore(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Av1PlaneRegion<TSample> sourcePlane,
            Av1PlaneRegion<TSample> reconstructionPlane,
            Point blockOrigin,
            Av1BlockSize blockSize,
            in LumaBlockState blockState,
            Av1TransformSize transformSize,
            ushort tileIndex,
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
            Span<TSample> candidateReconstruction,
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
            Av1EncoderModeDecisionWorkspace<TSample> workspace =
                this.blockWorkspace.GetModeDecisionWorkspace<TSample>();

            Span<TSample> reconstructionSamples = reconstructionPlane.Samples;
            ReadOnlySpan<TSample> frameBlock = reconstructionSamples[reconstructionPlane.GetOffset(blockOrigin.X, blockOrigin.Y)..];
            ReadOnlySpan<TSample> sourceSamples = sourcePlane.Samples;
            Av1PartitionType partitionType = blockState.PartitionType;

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

            // The second coefficient plane holds four transform-sized spans: the candidate and best quantized
            // coefficients, and the candidate and best dequantized coefficients. Swapping spans on improvement
            // keeps the winner's reconstruction input without copying it, as search_tx_type swaps dqcoeff.
            Span<int> transformCoefficientStorage = workspace.GetCandidateCoefficients(1);
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

            Span<short> residual = (paletteSize > 0 ? workspace.Palette.GetResidual(0) : workspace.Residual)[..transformSampleCount];
            Span<byte> contexts = workspace.TransformContexts;
            Span<byte> topContexts = contexts[..contextWidth];
            Span<byte> leftContexts = contexts.Slice(contextWidth, contextHeight);
            Av1NeighborEdges<byte> coefficientEdges = this.picture.LuminanceDcSignLevelCoefficientNeighbors[tileIndex].GetEdges();
            int topIndex = coefficientEdges.GetTopIndex(blockOrigin);
            int leftIndex = coefficientEdges.GetLeftIndex(blockOrigin);
            coefficientEdges.Top.Slice(topIndex, contextWidth).CopyTo(topContexts);
            coefficientEdges.Left.Slice(leftIndex, contextHeight).CopyTo(leftContexts);

            // The non-RD palette search of screen content codes DCT_DCT alone. Reference: dct_only_palette_nonrd.
            bool dctOnlyPalette = paletteSize > 0 && !this.picture.Parent.FrameHeader.IsIntra &&
                this.picture.Parent.IsScreenContent && this.picture.Parent.SpeedSettings.UseEstimatedInterModeDecision;

            // The edge filter strength depends on the neighbors of the block alone, so every transform block shares it.
            bool smoothEdges = blockState.SmoothEdges;

            // Prediction and transform-size syntax belongs to the coding block. Each residual transform
            // contributes its own coefficient cost; lossless and fixed-size modes do not signal a size choice.
            bool codedLossless = this.picture.Parent.FrameHeader.CodedLossless;

            // The size costs nothing while a stage searches the largest transform only. That is not a
            // rule of its own: such a stage runs with the largest transform mode rather than the
            // selecting one, and the size is coded only by the selecting mode. From all-intra speed 4
            // the reference gives mode evaluation the largest-transform method and keeps the
            // selecting one for winner evaluation, which is what this flag stands for. The frame
            // still signals the size it settled on.
            // Reference: tx_size_cost() against select_tx_mode() and tx_size_search_methods.
            bool selectsTransformSize = !this.picture.Parent.SpeedSettings.DeferTransformSizeSearch ||
                this.blockWorkspace.EvaluationStage != Av1EncoderEvaluationStage.Candidate;

            int rate = !codedLossless && blockSize > Av1BlockSize.Block4x4 && selectsTransformSize &&
                this.picture.Parent.FrameHeader.TransformMode == Av1TransformMode.Select
                ? writer.GetTransformSizeCost(blockSize, transformSize, transformSizeContext)
                : 0;

            int transformSizeRate = rate;
            if (paletteSize > 0)
            {
                rate += paletteHeaderRate;
            }
            else
            {
                rate += Av1TileWriter.GetLumaModeCost(
                    writer,
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
                        rate += writer.GetFilterIntraModeCost(
                            filterIntraMode,
                            blockSize);
                    }
                }
            }

            // Every intra candidate of a frame that allows intra block copy signals that it does not copy.
            // Reference: the intrabc_cost term of intra_mode_info_cost_y().
            if (this.picture.Parent.FrameHeader.AllowIntraBlockCopy)
            {
                rate += writer.GetUseIntraBlockCopyCost(false);
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

            // uniform_txfm_yrd opens the transform budget with the cost of the
            // non-skip flag and the transform-size syntax, because an intra block always signals non-skip.
            // block_rd_txfm (L3122-3140) then adds the rate-distortion cost of each transform and drops the
            // candidate as soon as the running cost passes the reference.
            int noSkipRate = blockState.NoSkipRate;

            // Lossless coding starts the running cost at zero. Reference: the current_rd of 0 that
            // choose_smallest_tx_size() passes to av1_txfm_rd_in_plane().
            long runningCost = codedLossless ? 0 : Av1RateDistortion.GetCost(this.rateMultiplier, noSkipRate + transformSizeRate, 0);

            // Complete each bounded 64x64 region before moving to the next. Smaller transforms
            // consume the reconstructed edges and coefficient contexts produced earlier in that region.
            Size codedExtent = GetCodedTransformExtent(macroBlock, blockSize, transformSize, 0, 0);
            int transformIndex = 0;

            // The prediction scratch and the reference edge slots serve every transform block, so they are read
            // once. The parent mode search retains reference slots 0 and 1. Use the other pair here because these
            // transform edges also include earlier reconstructions in this candidate.
            Span<int> transformWorkspace = this.blockWorkspace.TransformWorkspace;
            Span<TSample> aboveStorage = workspace.GetReferenceSamples(2);
            Span<TSample> leftStorage = workspace.GetReferenceSamples(3);
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
                            int reconstructionOffset =
                                (transformRow * transformHeight * blockWidth) + (transformColumn * transformWidth);

                            Point transformOrigin = blockOrigin + new Size(
                                transformColumn * transformWidth,
                                transformRow * transformHeight);

                            ReadOnlySpan<TSample> sourceTransform = sourceSamples[sourcePlane.GetOffset(transformOrigin.X, transformOrigin.Y)..];
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
                                    residual,
                                    transformSize);
                            }
                            else
                            {
                                this.PrepareTransformReferenceSamples(
                                    frameBlock,
                                    reconstructionPlane.Stride,
                                    blockOrigin,
                                    blockSize,
                                    macroBlock,
                                    partitionType,
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
                                        transformWorkspace,
                                        sourceTransform,
                                        sourcePlane.Stride,
                                        prediction,
                                        transformSize.GetWidth(),
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
                                        aboveStorage.Slice(1, transformWidth + transformHeight),
                                        leftStorage.Slice(1, transformWidth + transformHeight),
                                        residual,
                                        filterIntraMode,
                                        transformSize,
                                        this.bitDepth);
                                }
                            }

                            // The prediction goes into the frame. Reference: av1_predict_intra_block_facade() into pd->dst in block_rd_txfm().
                            Av1TransformBlockEncoder.WriteFrameSamples(
                                reconstructionPlane, reconstructionSamples, transformOrigin, prediction, transformWidth, transformWidth, transformHeight);

                            if (this.picture.Parent.SpeedSettings.PruneIntraTransformDepth &&
                                this.blockWorkspace.EvaluationStage == Av1EncoderEvaluationStage.Winner &&
                                !codedLossless && this.bitDepth.GetBitCount() == 8 &&
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

                            // The type search receives the budget that this block has left after its earlier transform
                            // blocks. Reference: the ref_best_rd that block_rd_txfm() gives search_tx_type().
                            long remainingCostLimit = costLimit == long.MaxValue ? long.MaxValue : costLimit - runningCost;

                            // A later transform block of this block predicts from this one unless this one is the last.
                            // Reference: the position test of recon_intra().
                            bool laterBlockPredicts = (y + transformHeight) < blockHeight || (x + transformWidth) < blockWidth;
                            this.blockWorkspace.LumaNoisePattern = this.IsLumaNoisePattern(reconstructionPlane, blockOrigin, blockSize);
                            TransformTypeSearchResult searchResult = this.SearchTransformType(
                                writer,
                                Av1Plane.Y,
                                false,
                                blockContext,
                                Av1TileWriter.GetTransformBlockContexts(
                                    Av1ComponentType.Luminance, in coefficientEdges, blockOrigin, blockSize, transformSize),
                                sourceTransform,
                                sourcePlane.Stride,
                                transformOrigin,
                                transformSize,
                                mode,
                                filterIntraMode,
                                Av1TransformType.AllTransformTypes,
                                dctOnlyPalette,
                                remainingCostLimit,
                                laterBlockPredicts,
                                prediction,
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

                            // Later transforms read the pixels of the winner from the block mosaic and its coefficient context
                            // from the local edge arrays. The quantized coefficients are not kept: as in libaom, the block encode
                            // quantizes the selected mode again. Reference: best_txb_ctx in search_tx_type().

                            // The last transform block of the block keeps its prediction here, and so does a
                            // block that quantized to nothing. Nothing inside the block predicts from the last
                            // one, so the search never adds its residual back; the block encode does that
                            // later. Reference: the end of block and position gates of recon_intra().
                            bool publishReconstruction = bestTransformState.EndOfBlock != 0 && laterBlockPredicts;

                            ReadOnlySpan<TSample> publishedSamples =
                                publishReconstruction ? bestTransformReconstruction : prediction;

                            for (int row = 0; row < transformHeight; row++)
                            {
                                publishedSamples.Slice(row * transformWidth, transformWidth)
                                    .CopyTo(
                                        candidateReconstruction.Slice(
                                            reconstructionOffset + (row * blockWidth),
                                            transformWidth));
                            }

                            // The frame gets the same samples. Reference: recon_intra() into pd->dst.
                            Av1TransformBlockEncoder.WriteFrameSamples(
                                reconstructionPlane, reconstructionSamples, transformOrigin, publishedSamples, transformWidth, transformWidth, transformHeight);

                            hasCoefficients |= bestTransformState.EndOfBlock != 0;
                            rate += bestTransformRate;
                            distortion += bestTransformDistortion;
                            candidateTransformBlocks[transformIndex] = bestTransformState;
                            byte coefficientContext = Av1SymbolContextHelper.GetCoefficientContext(
                                bestTransformCoefficients,
                                transformSize,
                                bestTransformType,
                                bestTransformState.EndOfBlock);

                            Av1TileWriter.UpdateCoefficientContexts(
                                topContexts.Slice(transformColumn * transformWidth4x4, transformWidth4x4),
                                leftContexts.Slice(transformRow * transformHeight4x4, transformHeight4x4),
                                coefficientContext,
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

                // The luma cost of an intra candidate in an inter frame is its token and mode rate without a
                // skip flag, because its transform search reports no skipped plane. Reference: the rd_y of
                // av1_handle_intra_y_mode().
                LumaCost = Av1RateDistortion.GetCost(this.rateMultiplier, rate, distortion)
            };
        }

        /// <summary>
        /// Returns true when the frame luma of a block is a noise pattern at high bit depth sharpness 3.
        /// The block is a noise pattern when its frame samples are smoother than the source and the source has low detail.
        /// Then the trellis keeps more coefficients.
        /// The frame holds all writes of the trials of the block so far, also from earlier trials.
        /// Reference: is_noise_pattern in av1_optimize_txb(), from av1_get_variance_stats() on pd->dst.
        /// </summary>
        /// <param name="plane">The luma plane of the frame.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <returns>True when the block is a noise pattern.</returns>
        private readonly bool IsLumaNoisePattern(Av1PlaneRegion<TSample> plane, Point blockOrigin, Av1BlockSize blockSize)
        {
            if (!this.UsesHighBitDepthSharpness)
            {
                return false;
            }

            ReadOnlySpan<TSample> frameBlock = Av1TransformBlockEncoder.GetPlaneSpan(plane, blockOrigin);
            this.GetVarianceStatistics(blockOrigin, blockSize, frameBlock, plane.Stride, out long sourceVariance, out long sampleVariance);
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
            out bool hasLeft,
            out bool hasAbove)
        {
            this.PrepareTransformReferenceSamplesCore(
                planeBlock,
                planeStride,
                lumaBlockOrigin,
                blockSize,
                macroBlock,
                partitionType,
                transformRow,
                transformColumn,
                candidateStride,
                transformSize,
                subsamplingX,
                subsamplingY,
                candidateReconstruction,
                aboveStorage,
                leftStorage,
                out hasLeft,
                out hasAbove);
        }

        /// <summary>
        /// Prepares the edge samples of one transform block, without the work counter.
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
        /// <param name="hasLeft">Whether the left edge is available.</param>
        /// <param name="hasAbove">Whether the top edge is available.</param>
        private void PrepareTransformReferenceSamplesCore(
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

            // Reference availability ends at the coded frame edge, even when a transform reaches into
            // padded storage. Extend the final available sample instead of reading padding as a neighbor.
            Av1BlockSize planeBlockSize = blockSize.GetSubsampled(subsamplingX != 0, subsamplingY != 0);
            int remainingWidth = planeBlockSize.GetWidth() +
                (macroBlock.ToRightEdge >> (3 + subsamplingX)) - columnOffset;

            int remainingHeight = planeBlockSize.GetHeight() +
                (macroBlock.ToBottomEdge >> (3 + subsamplingY)) - rowOffset;

            // Samples below the transform exist only while coded rows remain below it.
            bool bottomAvailable = remainingHeight > transformHeight &&
                modeInfoRow +
                    ((transformRow4x4 + transformSize.Get4x4HighCount()) << subsamplingY) <
                macroBlock.Tile.ModeInfoRowEnd;

            // Availability tables describe prediction blocks. Subsampled chroma of a luma block narrower or
            // shorter than eight samples belongs to the enclosing 8x8 region, so its geometry uses that region.
            Av1BlockSize availabilityBlockSize = Av1IntraReferenceAvailability.ScaleChromaBlockSize(
                blockSize, subsamplingX != 0, subsamplingY != 0);

            bool hasTopRight = Av1IntraReferenceAvailability.HasTopRight(
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

            bool hasBottomLeft = Av1IntraReferenceAvailability.HasBottomLeft(
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

            Span<TSample> above = aboveStorage.Slice(1, transformWidth + transformHeight);
            Span<TSample> left = leftStorage.Slice(1, transformWidth + transformHeight);
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
        /// The reference stores each coded luma transform of the block (<c>cfl_store_tx</c>), so
        /// the surface ends at the last transform that starts inside the coded frame. A sub-8x8 luma block shares
        /// its surface with the siblings that complete its 8x8 luma region (<c>sub8x8_adjust_offset</c>).
        /// That region always lies inside the coded frame, because the mode-information grid is
        /// eight-sample aligned, but a sibling can align the shared dimension to a different transform size.
        /// </remarks>
        /// <param name="macroBlock">The block's frame edges.</param>
        /// <param name="blockOrigin">The luma block origin in samples.</param>
        /// <param name="blockSize">The luma block size.</param>
        /// <param name="lumaTransformSize">The luma transform size of the block.</param>
        /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
        /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
        /// <returns>The stored luma extent in samples.</returns>
        private Size GetChromaFromLumaExtent(
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
                Size sibling = GetCodedTransformExtent(
                    macroBlock, blockSize, this.GetRetainedLumaTransformSize(new Point(blockOrigin.X ^ 4, blockOrigin.Y)), 0, 0);

                height = Math.Max(height, sibling.Height);
            }
            else if (sharedHeight && !sharedWidth)
            {
                Size sibling = GetCodedTransformExtent(
                    macroBlock, blockSize, this.GetRetainedLumaTransformSize(new Point(blockOrigin.X, blockOrigin.Y ^ 4)), 0, 0);

                width = Math.Max(width, sibling.Width);
            }

            return new Size(width, height);
        }

        /// <summary>
        /// Reads the luma transform size a coded neighbor retained.
        /// </summary>
        /// <param name="blockOrigin">The neighbor's luma origin in samples.</param>
        /// <returns>The retained transform size.</returns>
        private Av1TransformSize GetRetainedLumaTransformSize(Point blockOrigin)
            => this.picture.GetMacroBlockModeInfo(
                new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2)).Block.TransformSize;

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
            return this.GetLumaModelCostCore(
                macroBlock, sourcePlane, reconstructionPlane, blockOrigin, blockSize, in inputs, mode, angleDelta, filterMode);
        }

        /// <summary>
        /// Measures the complete luma prediction using square Hadamard tiles, without the work counter.
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
        private long GetLumaModelCostCore(
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
            Span<TSample> prediction = inputs.Prediction;
            Span<short> residual = inputs.Residual;
            Span<TSample> aboveStorage = inputs.AboveStorage;
            Span<TSample> leftStorage = inputs.LeftStorage;
            Span<int> transformCoefficients = inputs.TransformCoefficients;
            Span<int> transformWorkspace = inputs.TransformWorkspace;
            Span<TSample> reconstructionSamples = inputs.ReconstructionSamples;
            ReadOnlySpan<TSample> frameBlock = inputs.FrameBlock;
            ReadOnlySpan<TSample> sourceSamples = inputs.SourceSamples;
            bool smoothEdges = inputs.SmoothEdges;
            bool enableEdgeFilter = inputs.EnableEdgeFilter;
            Av1PartitionType partitionType = inputs.PartitionType;
            int frameStride = reconstructionPlane.Stride;
            int sourceStride = sourcePlane.Stride;
            bool highBitDepth = this.bitDepth != Av1BitDepth.EightBit;
            long cost = 0;

            // Each tile uses the predictions of the tiles before it, with no quantization and no inverse transform.
            // Each prediction goes into the frame, and a later tile reads its edges there, so a rejected mode leaves
            // its samples in the frame. This is the same as the reference encoder, which writes each model prediction
            // into the frame and reads the edges of the next tile from there.
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
                        out bool hasLeft,
                        out bool hasAbove);

                    Point transformOrigin = new(blockOrigin.X + x, blockOrigin.Y + y);
                    ReadOnlySpan<TSample> sourceTransform = sourceSamples[sourcePlane.GetOffset(transformOrigin.X, transformOrigin.Y)..];
                    ReadOnlySpan<TSample> above = aboveStorage.Slice(1, 2 * tileSize);
                    ReadOnlySpan<TSample> left = leftStorage.Slice(1, 2 * tileSize);
                    if (filterMode == Av1FilterIntraMode.AllFilterIntraModes)
                    {
                        TOperator.PrepareIntra(
                            transformWorkspace,
                            sourceTransform,
                            sourceStride,
                            prediction,
                            tileSize,
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

                    Av1TransformBlockEncoder.WriteFrameSamples(
                        reconstructionPlane, reconstructionSamples, transformOrigin, prediction, tileSize, tileSize, tileSize);
                }
            }

            return cost;
        }

        /// <summary>
        /// Reads the luma block values that depend only on the block and its neighbors, not on the mode that is tried.
        /// </summary>
        /// <param name="writer">The symbol costs of the tile.</param>
        /// <param name="macroBlock">The block and its neighbor availability.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="blockSize">The block size.</param>
        /// <returns>The values that every mode trial of the block shares.</returns>
        private LumaBlockState GetLumaBlockState(Av1SymbolEncoder writer, Av1MacroBlockD macroBlock, Point blockOrigin, Av1BlockSize blockSize)
        {
            // Only an intra frame codes the luma mode with neighbor contexts. In an inter frame a neighbor can hold an
            // inter mode, which has no intra context, so the contexts stay zero there.
            bool intraFrame = this.picture.Parent.FrameHeader.IsIntra;
            byte topModeContext = 0;
            byte leftModeContext = 0;
            if (intraFrame)
            {
                Av1TileWriter.GetYModeContext(macroBlock, out topModeContext, out leftModeContext);
            }

            return new(
                macroBlock.GetRelativeModeInfo(0).Block.PartitionType,
                this.UseSmoothIntraEdges(macroBlock, blockOrigin, blockSize, Av1Plane.Y),
                writer.GetSkipCost(false, Av1TileWriter.GetSkipContext(macroBlock)),
                intraFrame ? 0 : writer.GetIsInterCost(false, Av1TileWriter.GetIntraInterContext(macroBlock)),
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
        /// Builds the directional-mode skip mask selected by libaom's all-intra speed policy.
        /// </summary>
        /// <param name="sourcePlane">The source luma plane.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="visibleHeight">The number of source rows inside the coded image.</param>
        /// <param name="visibleWidth">The number of source columns inside the coded image.</param>
        /// <returns>The bit mask for the eight directional modes, or zero when HOG pruning is disabled.</returns>
        private byte GetDirectionalModeSkipMask(
            Av1PlaneRegion<TSample> sourcePlane,
            Point blockOrigin,
            int visibleHeight,
            int visibleWidth)
        {
            return this.GetDirectionalModeSkipMaskCore(sourcePlane, blockOrigin, visibleHeight, visibleWidth);
        }

        private byte GetDirectionalModeSkipMaskCore(
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

            return GetDirectionalModeSkipMask(
                sourcePlane,
                blockOrigin,
                visibleHeight,
                visibleWidth,
                1,
                threshold);
        }

        /// <summary>
        /// Codes one transform block of the selected mode and adds its reconstruction to the frame.
        /// </summary>
        /// <param name="buffers">The workspace buffers and rate tables, which the caller reads once for its loops.</param>
        /// <param name="context">The coefficient context of the transform block.</param>
        /// <param name="isInter">Whether the block is an inter block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="planeOrigin">The transform block origin in plane samples.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="transformSize">The transform size.</param>
        /// <param name="prediction">The prediction of the transform block.</param>
        /// <param name="residual">The residual of the transform block.</param>
        /// <param name="inputStride">The stride of the prediction and the residual.</param>
        /// <param name="selectedState">The transform type and contexts that the search selected.</param>
        /// <param name="skipTransform">Whether the block skips its residual.</param>
        /// <param name="coefficientOffset">The offset of the transform block in the plane coefficients.</param>
        private void ReconstructSelectedTransform(
            in Av1TransformBlockBuffers buffers,
            Av1TransformBlockContext context,
            bool isInter,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Point planeOrigin,
            Av1Plane plane,
            Av1TransformSize transformSize,
            ReadOnlySpan<TSample> prediction,
            Span<short> residual,
            int inputStride,
            Av1EncoderTransformBlockState selectedState,
            bool skipTransform,
            int coefficientOffset)
        {
            this.ReconstructSelectedTransformCore(
                in buffers,
                context,
                isInter,
                blockOrigin,
                blockSize,
                planeOrigin,
                plane,
                transformSize,
                prediction,
                residual,
                inputStride,
                selectedState,
                skipTransform,
                coefficientOffset);
        }

        /// <summary>
        /// Codes one transform block of the selected mode and adds its reconstruction to the frame.
        /// Reference: the transform block steps of encode_block_intra() and encode_block().
        /// </summary>
        /// <param name="buffers">The workspace buffers and rate tables, which the caller reads once for its loops.</param>
        /// <param name="context">The coefficient context of the transform block.</param>
        /// <param name="isInter">Whether the block is an inter block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="planeOrigin">The transform block origin in plane samples.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="transformSize">The transform size.</param>
        /// <param name="prediction">The prediction of the transform block.</param>
        /// <param name="residual">The residual of the transform block.</param>
        /// <param name="inputStride">The stride of the prediction and the residual.</param>
        /// <param name="selectedState">The transform type and contexts that the search selected.</param>
        /// <param name="skipTransform">Whether the block skips its residual.</param>
        /// <param name="coefficientOffset">The offset of the transform block in the plane coefficients.</param>
        private void ReconstructSelectedTransformCore(
            in Av1TransformBlockBuffers buffers,
            Av1TransformBlockContext context,
            bool isInter,
            Point blockOrigin,
            Av1BlockSize blockSize,
            Point planeOrigin,
            Av1Plane plane,
            Av1TransformSize transformSize,
            ReadOnlySpan<TSample> prediction,
            Span<short> residual,
            int inputStride,
            Av1EncoderTransformBlockState selectedState,
            bool skipTransform,
            int coefficientOffset)
        {
            Av1PlaneRegion<TSample> destinationPlane = this.reconstruction.GetPlane(plane);
            Span<TSample> destination = Av1TransformBlockEncoder.GetPlaneSpan(destinationPlane, planeOrigin);
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            for (int row = 0; row < height; row++)
            {
                prediction.Slice(row * inputStride, width).CopyTo(destination.Slice(row * destinationPlane.Stride, width));
            }

            Span<int> coefficients = this.coefficientBuffer.GetPlaneSpan(this.superblock.Index, plane)
                .Slice(coefficientOffset, transformSize.GetSize2d());

            ref Av1EncoderTransformBlockState state = ref this.coefficientBuffer.GetTransformBlockSpan(this.superblock.Index, plane)[
                coefficientOffset / Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount];

            bool lossless = this.picture.Parent.FrameHeader.CodedLossless;
            state = default;
            state.EntropyContext = lossless
                ? (byte)(context.SkipContext | (context.DcSignContext << 4))
                : selectedState.EntropyContext;

            // encode_block_intra transforms, quantizes and optimizes every transform
            // block of a block that the mode decision did not mark as skipped, even when the winning search
            // candidate left it empty. Only the block skip flag suppresses the residual.
            if (skipTransform)
            {
                coefficients.Clear();
                state.TransformType = Av1TransformType.DctDct;
                return;
            }

            // Mode and transform decisions are already fixed. Generate their coefficients and add the
            // inverse transform directly to the frame, without another distortion scan or candidate copy.
            // encode_block_intra() and encode_block() subtract with the border padding of the selected type.
            int planeIndex = (int)plane;
            Av1TransformBlockEncoder.PadBorderResidual(
                this.blockWorkspace, plane, planeOrigin, residual, inputStride, width, height, selectedState.TransformType);

            // At high bit depth sharpness 3, the trellis of a luma transform block tests the whole block in the frame for a noise pattern.
            // The frame holds the reconstruction of the earlier transform blocks, the prediction of this one, and what the search left
            // after it. Reference: is_noise_pattern in av1_optimize_txb(), from encode_block_intra() and encode_block().
            this.blockWorkspace.LumaNoisePattern = plane == Av1Plane.Y && this.IsLumaNoisePattern(destinationPlane, blockOrigin, blockSize);
            Av1TransformBlockEncoder.EncodeLossyCandidate(
                in buffers,
                context,
                residual,
                inputStride,
                coefficients,
                transformSize,
                selectedState.TransformType,
                this.blockQIndex,
                this.quantization.DeltaQDc[planeIndex],
                this.quantization.DeltaQAc[planeIndex],
                this.bitDepth,
                plane == Av1Plane.Y ? Av1ComponentType.Luminance : Av1ComponentType.Chroma,
                this.rateMultiplier,
                isInter,
                this.picture.Sequence.SequenceHeader.IsStillPicture,
                true,
                0,
                ref state);

            this.blockWorkspace.LumaNoisePattern = false;
            if (state.EndOfBlock > 0)
            {
                TOperator.AddSelectedResidual(
                    this.blockWorkspace,
                    destination,
                    destinationPlane.Stride,
                    transformSize,
                    plane,
                    this.bitDepth,
                    this.blockQIndex == 0,
                    state);
            }
            else if (plane == Av1Plane.Y && !this.keepSearchedZeroBlockTypes)
            {
                // A luma transform block that quantized to nothing returns to DCT_DCT, so a later pass
                // over the same block transforms it with the default type rather than the one this
                // search picked. Reference: the update_txk_array() calls of encode_block_intra() and
                // encode_block().
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
            /// <param name="workspace">The mode decision workspace of the block.</param>
            /// <param name="blockWorkspace">The block workspace, which owns the transform buffers.</param>
            /// <param name="sourcePlane">The source luma plane.</param>
            /// <param name="reconstructionPlane">The reconstructed luma plane.</param>
            /// <param name="blockOrigin">The block origin in luma samples.</param>
            /// <param name="partitionType">The partition type of the block.</param>
            /// <param name="smoothEdges">Whether a neighbor of the block uses a smooth mode, which selects the edge filter strength.</param>
            /// <param name="enableEdgeFilter">Whether the sequence enables the intra edge filter.</param>
            public LumaModelInputs(
                Av1EncoderModeDecisionWorkspace<TSample> workspace,
                Av1EncoderBlockWorkspace blockWorkspace,
                Av1PlaneRegion<TSample> sourcePlane,
                Av1PlaneRegion<TSample> reconstructionPlane,
                Point blockOrigin,
                Av1PartitionType partitionType,
                bool smoothEdges,
                bool enableEdgeFilter)
            {
                this.Prediction = workspace.Prediction;
                this.Residual = workspace.Residual;
                this.AboveStorage = workspace.GetReferenceSamples(2);
                this.LeftStorage = workspace.GetReferenceSamples(3);
                this.TransformCoefficients = blockWorkspace.TransformCoefficients;
                this.TransformWorkspace = blockWorkspace.TransformWorkspace;
                this.SourceSamples = sourcePlane.Samples;
                this.ReconstructionSamples = reconstructionPlane.Samples;
                this.FrameBlock = this.ReconstructionSamples[reconstructionPlane.GetOffset(blockOrigin.X, blockOrigin.Y)..];
                this.PartitionType = partitionType;
                this.SmoothEdges = smoothEdges;
                this.EnableEdgeFilter = enableEdgeFilter;
            }

            /// <summary>
            /// Gets the buffer that receives each tile prediction.
            /// </summary>
            public Span<TSample> Prediction { get; }

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
            /// Gets all samples of the reconstructed luma plane.
            /// </summary>
            public Span<TSample> ReconstructionSamples { get; }

            /// <summary>
            /// Gets the reconstructed luma plane from the block origin.
            /// </summary>
            public ReadOnlySpan<TSample> FrameBlock { get; }

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
            /// The second reference of a compound source candidate; otherwise a value that is not an inter reference.
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
