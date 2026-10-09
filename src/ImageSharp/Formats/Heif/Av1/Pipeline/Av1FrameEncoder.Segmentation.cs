// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Keeps the segmentation of a sequence across its frames.
/// </content>
internal static partial class Av1FrameEncoder
{
    internal abstract partial class SequenceEncoder
    {
        /// <summary>
        /// The segment map the encoder keeps across frames. The skipped blocks of a frame that updates its map write
        /// it, and complexity adaptive quantization resets it and writes each searched block. Cyclic refresh resets it
        /// every real-time frame, marks the refreshed superblocks and writes each coded block. Reference:
        /// cpi->enc_seg.map.
        /// </summary>
        private byte[] encoderSegmentMap = [];

        /// <summary>
        /// Clears the segmentation of an intra frame before any trial encode of the frame. Reference: the intra-only
        /// av1_reset_segment_features() of av1_encode(), which comes before av1_determine_sc_tools_with_encoding().
        /// </summary>
        private protected void ResetIntraSegmentation()
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            if (frameHeader.IsIntra)
            {
                ObuSegmentationParameters segmentation = frameHeader.SegmentationParameters;
                segmentation.Enabled = false;
                segmentation.SegmentationUpdateMap = 0;
                segmentation.SegmentationUpdateData = 0;
                segmentation.ClearFeatures();
            }
        }

        /// <summary>
        /// Sets the segmentation of the frame about to be coded, after its quantizer and its primary reference are
        /// known, and after <see cref="ResetIntraSegmentation"/> has cleared an intra frame. Variance and complexity
        /// adaptive quantization refresh the segment quantizers on an intra or error resilient frame, an alternate
        /// reference and a golden frame that is not an overlay, which only a frame that may be coded again sets up.
        /// Cyclic refresh sets up every real-time frame. Any other frame keeps the segmentation of its primary
        /// reference. Reference: av1_vaq_frame_setup(), av1_setup_in_frame_q_adj() and av1_cyclic_refresh_setup(),
        /// with the segfeatures_copy() that follow av1_setup_frame() in encode_with_recode_loop() and
        /// encode_without_recode().
        /// </summary>
        /// <typeparam name="TSample">The sample type of the reference pool.</typeparam>
        /// <param name="pool">The reference pool of the sequence.</param>
        /// <param name="current">The buffer the frame is coded into.</param>
        /// <param name="parent">The frame state.</param>
        /// <param name="allowsRecode">
        /// Whether the frame may be coded again, which a sequence with first-pass statistics allows. Reference: the
        /// DISALLOW_RECODE of a one-pass sequence without statistics.
        /// </param>
        /// <param name="averageEnergy">The log of the frame's first-pass intra error. Reference: mb_av_energy.</param>
        /// <param name="bestQIndex">The lowest allowed quantizer index.</param>
        /// <param name="worstQIndex">The highest allowed quantizer index.</param>
        /// <param name="superblockTargetRate">The frame's target rate per 64x64 area. Reference: rc->sb64_target_rate.</param>
        private protected void BeginSegmentation<TSample>(
            Av1EncoderReferencePool<TSample> pool,
            Av1EncoderReferencePool<TSample>.Entry current,
            Av1PictureParentControlSet parent,
            bool allowsRecode,
            double averageEnergy,
            int bestQIndex,
            int worstQIndex,
            int superblockTargetRate)
            where TSample : unmanaged
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            ObuSegmentationParameters segmentation = frameHeader.SegmentationParameters;
            Av1EncoderReferencePool<TSample>.Entry? primary = frameHeader.PrimaryReferenceFrame == Av1Constants.PrimaryReferenceFrameNone
                ? null
                : pool.GetSlot((int)frameHeader.GetReferenceFrameIndices()[(int)frameHeader.PrimaryReferenceFrame]);

            // A frame that starts a new context clears the features and the buffer's map, and an inter frame without a
            // primary reference must code both. Reference: av1_setup_frame() with av1_setup_past_independence().
            int mapLength = parent.Common.ModeInfoColumnCount * parent.Common.ModeInfoRowCount;
            bool shownKeyFrame = frameHeader.FrameType == ObuFrameType.KeyFrame && frameHeader.ShowFrame;
            if (frameHeader.IsIntra || frameHeader.ErrorResilientMode || (!shownKeyFrame && primary is null))
            {
                segmentation.ClearFeatures();
                current.GetSegmentMap(mapLength).Span.Clear();
                if (!shownKeyFrame && primary is null)
                {
                    segmentation.SegmentationUpdateMap = 1;
                    segmentation.SegmentationUpdateData = 1;
                }
            }

            if (this.encoderSegmentMap.Length != mapLength)
            {
                this.encoderSegmentMap = new byte[mapLength];
            }

            // Both modes refresh their segments on an intra or error resilient frame, an alternate reference and a
            // golden frame that is not an overlay. Reference: is_frame_aq_enabled() and av1_vaq_frame_setup().
            bool refreshFrame = frameHeader.IsIntra || frameHeader.ErrorResilientMode || parent.RefreshesAlternate ||
                (parent.RefreshesGolden && !parent.IsSourceAlternateReference);

            bool varianceRefresh = false;
            bool complexityRefresh = false;
            bool keyFrame = frameHeader.FrameType == ObuFrameType.KeyFrame;
            Av1BitDepth bitDepth = this.SequenceHeader.ColorConfig.BitDepth;
            int baseQIndex = frameHeader.QuantizationParameters.BaseQIndex;
            if (allowsRecode && refreshFrame)
            {
                switch (this.Options.AdaptiveQuantizationMode)
                {
                    case Av1AdaptiveQuantizationMode.Variance:
                        varianceRefresh = true;
                        Av1VarianceAdaptiveQuantization.SetupRefreshFrame(
                            segmentation, keyFrame, parent.IsScreenContent, baseQIndex, averageEnergy, bitDepth, bestQIndex, worstQIndex);

                        break;
                    case Av1AdaptiveQuantizationMode.Complexity:
                        complexityRefresh = Av1ComplexityAdaptiveQuantization.IsSuperblockEnabled(superblockTargetRate);
                        Av1ComplexityAdaptiveQuantization.SetupRefreshFrame(
                            segmentation,
                            this.encoderSegmentMap,
                            keyFrame,
                            parent.IsScreenContent,
                            baseQIndex,
                            superblockTargetRate,
                            bitDepth,
                            bestQIndex,
                            worstQIndex);

                        break;
                }
            }

            // Cyclic refresh sets the segments of every real-time frame, which a frame without recoding codes.
            // Reference: the av1_cyclic_refresh_setup() call of encode_without_recode().
            if (this.cyclicRefresh is not null)
            {
                this.cyclicRefresh.Setup(
                    segmentation,
                    this.encoderSegmentMap,
                    this.rateControl!,
                    parent,
                    frameHeader.IsIntra,
                    parent.HighSourceSad,
                    this.Options.Speed,
                    parent.FramesSinceKey,
                    this.SequenceHeader.SuperblockModeInfoSize,
                    parent.SourceBlockSad.Span);

                // The frame counts its own boosted units. Reference: the actual_num_seg1_blocks and
                // actual_num_seg2_blocks reset of encode_frame_internal().
                this.cyclicRefresh.FirstSegmentBlockCount = 0;
                this.cyclicRefresh.SecondSegmentBlockCount = 0;
            }

            if (segmentation.Enabled)
            {
                if (segmentation.SegmentationUpdateData != 1 && primary is not null)
                {
                    // Reference: segfeatures_copy(), which also copies the derived identifier bounds.
                    segmentation.CopyFeaturesFrom(primary.Segmentation);
                    segmentation.SegmentIdPrecedesSkip = primary.Segmentation.SegmentIdPrecedesSkip;
                    segmentation.LastActiveSegmentId = primary.Segmentation.LastActiveSegmentId;
                    segmentation.Enabled = primary.Segmentation.Enabled;
                }
                else
                {
                    segmentation.CalculateSegmentData();
                }
            }

            if (!segmentation.Enabled)
            {
                // Reference: the memset() of cm->seg.
                segmentation.SegmentationUpdateMap = 0;
                segmentation.SegmentationUpdateData = 0;
                segmentation.SegmentationTemporalUpdate = 0;
                segmentation.SegmentIdPrecedesSkip = false;
                segmentation.LastActiveSegmentId = 0;
                segmentation.ClearFeatures();
            }

            // Reference: the segfeatures_copy() to cm->cur_frame->seg.
            current.Segmentation.CopyFeaturesFrom(segmentation);
            current.Segmentation.SegmentIdPrecedesSkip = segmentation.SegmentIdPrecedesSkip;
            current.Segmentation.LastActiveSegmentId = segmentation.LastActiveSegmentId;
            current.Segmentation.Enabled = segmentation.Enabled;

            // The segment quantizers set the lossless flags and matrix levels of each segment. Reference: the
            // xd->lossless and qmatrix_level loop of encode_frame_internal().
            Av1QuantizationLookup.UpdateFrameQuantizationState(frameHeader);

            // The segment quantizers can change whether every segment codes losslessly, and only such a frame fixes
            // its transforms at 4x4.
            frameHeader.TransformMode = frameHeader.CodedLossless
                ? Av1TransformMode.Only4x4
                : Av1TransformMode.Select;

            // The encoder map holds no identifier above the last active segment. Reference: the last_active_segid
            // clamp of cpi->enc_seg.map in encode_frame_internal().
            if (segmentation.Enabled && segmentation.SegmentationUpdateMap == 1)
            {
                byte lastActiveSegmentId = (byte)segmentation.LastActiveSegmentId;
                for (int index = 0; index < this.encoderSegmentMap.Length; index++)
                {
                    this.encoderSegmentMap[index] = Math.Min(this.encoderSegmentMap[index], lastActiveSegmentId);
                }
            }

            parent.EncoderSegmentMap = this.encoderSegmentMap;
            parent.VarianceSegmentRefresh = varianceRefresh;
            parent.ComplexitySegmentRefresh = complexityRefresh;
            parent.SuperblockTargetRate = superblockTargetRate;
            parent.SearchSegmentMap = current.GetSegmentMap(mapLength);
            parent.PreviousSegmentMap = primary is { Segmentation.Enabled: true } ? primary.GetSegmentMap(mapLength) : default;
            parent.SpatialSegmentCost = 0;
            parent.TemporalSegmentCost = 0;
        }

        /// <summary>
        /// Updates the noise estimate of a real-time frame, after an intra frame clears the still block counts.
        /// Reference: the consec_zero_mv reset and the av1_update_noise_estimate() call of encode_without_recode().
        /// </summary>
        /// <param name="parent">The frame state.</param>
        /// <param name="source">The luma plane of the frame's source.</param>
        /// <param name="lastSource">The luma plane of the previous source.</param>
        /// <param name="hasPreviousSource">Whether the encoder keeps a previous source buffer.</param>
        private protected void UpdateNoiseEstimate(
            Av1PictureParentControlSet parent,
            Av1PlaneRegion<byte> source,
            Av1PlaneRegion<byte> lastSource,
            bool hasPreviousSource)
        {
            if (this.noiseEstimate is null)
            {
                return;
            }

            // An intra frame and a frame of a new size restart the still block counts. Reference: the
            // frame_is_intra_only() || resize_pending test of encode_without_recode().
            if (this.FrameHeader.IsIntra || this.IsResizePending)
            {
                this.noiseEstimate.ResetStillBlocks();
            }

            // The first frame of the sequence has no previous source. Reference: cpi->last_source.
            this.noiseEstimate.Update(
                (int)this.frameNumber,
                this.codedFrameCount,
                parent.FramesSinceKey,
                parent.AverageFrameLowMotion,
                parent.HighSourceSad,
                source,
                lastSource,
                hasPreviousSource && this.codedFrameCount > 0);
        }

        /// <summary>
        /// Sets the cyclic refresh segmentation of a real-time frame after its quantizer is chosen. A sequence without
        /// cyclic refresh codes no segments. Reference: the intra-only av1_reset_segment_features() of av1_encode(),
        /// then encode_without_recode().
        /// </summary>
        /// <typeparam name="TSample">The sample type of the reference pool.</typeparam>
        /// <param name="pool">The reference pool of the sequence.</param>
        /// <param name="current">The buffer the frame is coded into.</param>
        /// <param name="parent">The frame state.</param>
        private protected void BeginCyclicRefreshSegmentation<TSample>(
            Av1EncoderReferencePool<TSample> pool,
            Av1EncoderReferencePool<TSample>.Entry current,
            Av1PictureParentControlSet parent)
            where TSample : unmanaged
        {
            if (this.cyclicRefresh is null)
            {
                return;
            }

            this.ResetIntraSegmentation();
            this.BeginSegmentation(
                pool, current, parent, allowsRecode: false, 0, 0, 0, this.rateControl!.SuperblockTargetRate);
        }

        /// <summary>
        /// Keeps the segment map of a coded real-time frame when the sequence uses cyclic refresh.
        /// </summary>
        /// <typeparam name="TSample">The sample type of the reference pool.</typeparam>
        /// <param name="current">The buffer the frame was coded into.</param>
        /// <param name="picture">The coded picture, whose segment map the bitstream wrote.</param>
        private protected void CompleteCyclicRefreshSegmentation<TSample>(Av1EncoderReferencePool<TSample>.Entry current, Av1PictureControlSet picture)
            where TSample : unmanaged
        {
            if (this.cyclicRefresh is not null)
            {
                this.CompleteSegmentation(current, picture);
            }
        }

        /// <summary>
        /// Keeps the segment map of a coded frame in its buffer: the map the bitstream wrote, or the primary reference's
        /// map when the frame did not update it, and clears the one-shot update flags. Reference: the seg_map copy and
        /// the update_map and update_data reset at the end of encode_frame_to_data_rate().
        /// </summary>
        /// <typeparam name="TSample">The sample type of the reference pool.</typeparam>
        /// <param name="current">The buffer the frame was coded into.</param>
        /// <param name="picture">The coded picture, whose segment map the bitstream wrote.</param>
        private protected void CompleteSegmentation<TSample>(Av1EncoderReferencePool<TSample>.Entry current, Av1PictureControlSet picture)
            where TSample : unmanaged
        {
            ObuSegmentationParameters segmentation = this.FrameHeader.SegmentationParameters;
            Av1PictureParentControlSet parent = picture.Parent;
            int mapLength = parent.Common.ModeInfoColumnCount * parent.Common.ModeInfoRowCount;
            if (segmentation.Enabled)
            {
                Span<byte> map = current.GetSegmentMap(mapLength).Span;
                if (segmentation.SegmentationUpdateMap == 1)
                {
                    picture.SegmentationNeighborMap.Span[..mapLength].CopyTo(map);
                }
                else if (!parent.PreviousSegmentMap.IsEmpty)
                {
                    parent.PreviousSegmentMap.Span.CopyTo(map);
                }
            }

            segmentation.SegmentationUpdateMap = 0;
            segmentation.SegmentationUpdateData = 0;
        }
    }
}
