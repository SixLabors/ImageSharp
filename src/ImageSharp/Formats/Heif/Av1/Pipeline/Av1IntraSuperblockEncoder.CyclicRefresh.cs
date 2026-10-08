// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Updates the cyclic refresh segment of the blocks the estimated inter search codes.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// The libaom encode that a block of the estimated inter search stands for. The search here also codes the block,
    /// so this decides whether the block updates its cyclic refresh segment and whether the frame counts it.
    /// Reference: the dry_run of encode_b_nonrd().
    /// </summary>
    private enum EstimatedLeafEncode
    {
        /// <summary>
        /// The block is coded for output. Reference: encode_b_nonrd() with dry_run 0.
        /// </summary>
        Output,

        /// <summary>
        /// The block is only searched. Reference: the unsplit pick_sb_modes_nonrd() call of try_merge().
        /// </summary>
        Search,

        /// <summary>
        /// The block is a split child of a merge trial, coded as a dry run. It is not coded when it is the last child
        /// or when the split already costs more than the unsplit block. Reference: the split loop of try_merge().
        /// </summary>
        MergeSplitTrial
    }

    internal partial struct ModeDecision<TSample, TOperator>
    {
        /// <summary>
        /// Gets a value indicating whether the current block is in a boosted cyclic refresh segment. Reference:
        /// cyclic_refresh_segment_id_boosted() of mbmi->segment_id.
        /// </summary>
        private readonly bool IsCyclicRefreshBoosted =>
            this.picture.Parent.CyclicRefresh is not null &&
            this.picture.Parent.FrameHeader.SegmentationParameters.Enabled &&
            Av1CyclicRefresh.IsBoosted(this.blockSegmentId);

        /// <summary>
        /// Returns whether the selected block updates its cyclic refresh segment before it is coded, and whether the
        /// frame counts its segment.
        /// </summary>
        /// <param name="statistics">The block's searched rate and distortion.</param>
        /// <param name="countBlocks">Whether the frame counts the block's segment units.</param>
        /// <returns>Whether the block updates its segment.</returns>
        private readonly bool UpdatesCyclicRefreshSegment(Av1RateDistortionStatistics statistics, out bool countBlocks)
        {
            countBlocks = false;
            if (this.picture.Parent.CyclicRefresh is null || !this.picture.Parent.FrameHeader.SegmentationParameters.Enabled)
            {
                return false;
            }

            switch (this.estimatedLeafEncode)
            {
                case EstimatedLeafEncode.Output:
                    countBlocks = true;
                    return true;
                case EstimatedLeafEncode.Search:
                    return false;
                default:
                    if (this.mergeChildIndex == 3)
                    {
                        return false;
                    }

                    Av1RateDistortionStatistics split = this.mergeSplitStatistics;
                    split.Add(this.mergeRateMultiplier, statistics);
                    return !(this.mergeNoneCost < split.Cost);
            }
        }

        /// <summary>
        /// Updates the cyclic refresh segment of a selected block and sets the quantizer it is coded with. Reference:
        /// the av1_cyclic_refresh_update_segment() and av1_init_plane_quantizers() calls of av1_update_state().
        /// </summary>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="modeInfo">The block's selected syntax, whose segment is updated.</param>
        /// <param name="block">The block's coding state, whose segment is updated.</param>
        /// <param name="vector">The block's first motion vector, or zero for an intra block.</param>
        /// <param name="statistics">
        /// The block's searched rate, distortion and skip. Reference: ctx->rd_stats with its skip_txfm.
        /// </param>
        /// <param name="countBlocks">Whether the frame counts the block's segment units.</param>
        private void UpdateCyclicRefreshSegment(
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            Point blockOrigin,
            ref Av1EncoderBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            Av1MotionVector vector,
            Av1RateDistortionStatistics statistics,
            bool countBlocks)
        {
            Av1PictureParentControlSet parent = this.picture.Parent;
            int segmentId = modeInfo.SegmentId;
            parent.CyclicRefresh!.UpdateSegment(
                encoderSegmentMap,
                searchSegmentMap,
                new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2),
                modeInfo.BlockSize,
                ref segmentId,
                modeInfo.ReferenceFrame > Av1ReferenceFrameType.Intra,
                modeInfo.SecondaryReferenceFrame > Av1ReferenceFrameType.Intra,
                vector,
                statistics.Rate,
                statistics.Distortion,
                statistics.AllTransformsEmpty,
                parent.NoiseLevel,
                countBlocks);

            this.SetCyclicRefreshSegment(segmentId, ref modeInfo, ref block);
        }

        /// <summary>
        /// Gives a coded skipped block the predicted segment. Reference: the av1_cyclic_reset_segment_skip() call of
        /// encode_b_nonrd().
        /// </summary>
        /// <param name="encoderSegmentMap">The segment identifiers that the encoder keeps for the frame.</param>
        /// <param name="searchSegmentMap">The segment map that the frame buffer holds while the frame is searched.</param>
        /// <param name="macroBlock">The block's neighbor availability.</param>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="modeInfo">The block's coded syntax, whose segment is updated.</param>
        /// <param name="block">The block's coding state, whose segment is updated.</param>
        /// <param name="codedSkip">Whether the coded block has no coefficients. Reference: mbmi->skip_txfm.</param>
        /// <param name="countBlocks">Whether the frame counts the block's segment units.</param>
        private void ResetCyclicRefreshSkip(
            Span<byte> encoderSegmentMap,
            Span<byte> searchSegmentMap,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ref Av1EncoderBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            bool codedSkip,
            bool countBlocks)
        {
            if (!codedSkip)
            {
                return;
            }

            Av1PictureParentControlSet parent = this.picture.Parent;
            int predictedSegmentId = Av1TileWriter.GetSpatialSegmentationPrediction(parent.Common, searchSegmentMap, macroBlock, blockOrigin, out _);
            int segmentId = modeInfo.SegmentId;
            parent.CyclicRefresh!.ResetSegmentSkip(
                encoderSegmentMap,
                searchSegmentMap,
                new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2),
                modeInfo.BlockSize,
                ref segmentId,
                predictedSegmentId,
                countBlocks);

            this.SetCyclicRefreshSegment(segmentId, ref modeInfo, ref block);
        }

        /// <summary>
        /// Counts the still 8x8 units of a block coded for output, which the noise estimate reads. Reference: the
        /// update_zeromv_cnt() call of encode_superblock().
        /// </summary>
        /// <param name="blockOrigin">The block origin in luma samples.</param>
        /// <param name="modeInfo">The block's coded syntax.</param>
        /// <param name="vector">The block's first motion vector, or zero for an intra block.</param>
        private readonly void CountNoiseStillBlock(Point blockOrigin, Av1EncoderBlockModeInfo modeInfo, Av1MotionVector vector)
            => this.picture.Parent.NoiseEstimate?.CountStillBlock(
                new Point(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2),
                modeInfo.BlockSize,
                modeInfo.ReferenceFrame == Av1ReferenceFrameType.Last,
                vector);

        /// <summary>
        /// Stores a block's cyclic refresh segment and sets the quantizer of the segment.
        /// </summary>
        /// <param name="segmentId">The block's segment.</param>
        /// <param name="modeInfo">The block's syntax.</param>
        /// <param name="block">The block's coding state.</param>
        private void SetCyclicRefreshSegment(int segmentId, ref Av1EncoderBlockModeInfo modeInfo, ref Av1EncoderBlockStruct block)
        {
            ObuSegmentationParameters segmentation = this.picture.Parent.FrameHeader.SegmentationParameters;
            modeInfo.SegmentId = segmentId;
            block.SegmentId = segmentId;
            this.blockSegmentId = segmentId;
            this.blockQIndex = Av1QuantizationLookup.GetQIndex(segmentation, segmentId, this.superblockQIndex);
        }
    }
}
