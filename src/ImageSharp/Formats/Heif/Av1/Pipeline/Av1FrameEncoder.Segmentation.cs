// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Keeps the segmentation of a sequence across its frames.
/// </content>
internal static partial class Av1FrameEncoder
{
    internal abstract partial class SequenceEncoder
    {
        /// <summary>
        /// The segment map the encoder keeps across frames, which only the skipped blocks of a frame that updates its
        /// map write. Reference: cpi->enc_seg.map.
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
        /// known, and after <see cref="ResetIntraSegmentation"/> has cleared an intra frame. Variance adaptive
        /// quantization refreshes the segment quantizers on an intra frame, an alternate reference and a golden frame
        /// that is not an overlay, which only a frame that may be coded again sets up. Any other frame keeps the
        /// segmentation of its primary reference. Reference: av1_vaq_frame_setup() with the segfeatures_copy() that
        /// follow av1_setup_frame() in encode_with_recode_loop().
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
        private protected void BeginSegmentation<TSample>(
            Av1EncoderReferencePool<TSample> pool,
            Av1EncoderReferencePool<TSample>.Entry current,
            Av1PictureParentControlSet parent,
            bool allowsRecode,
            double averageEnergy,
            int bestQIndex,
            int worstQIndex)
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

            bool refresh = false;
            if (allowsRecode && this.Options.AdaptiveQuantizationMode == Av1AdaptiveQuantizationMode.Variance)
            {
                if (frameHeader.IsIntra || parent.RefreshesAlternate || (parent.RefreshesGolden && !parent.IsSourceAlternateReference))
                {
                    refresh = true;
                    Av1VarianceAdaptiveQuantization.SetupRefreshFrame(
                        segmentation,
                        frameHeader.FrameType == ObuFrameType.KeyFrame,
                        parent.IsScreenContent,
                        frameHeader.QuantizationParameters.BaseQIndex,
                        averageEnergy,
                        this.SequenceHeader.ColorConfig.BitDepth,
                        bestQIndex,
                        worstQIndex);
                }
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

            // The encoder map holds no identifier above the last active segment. Reference: the last_active_segid
            // clamp of cpi->enc_seg.map in encode_frame_internal().
            if (this.encoderSegmentMap.Length != mapLength)
            {
                this.encoderSegmentMap = new byte[mapLength];
            }

            if (segmentation.Enabled && segmentation.SegmentationUpdateMap == 1)
            {
                byte lastActiveSegmentId = (byte)segmentation.LastActiveSegmentId;
                for (int index = 0; index < this.encoderSegmentMap.Length; index++)
                {
                    this.encoderSegmentMap[index] = Math.Min(this.encoderSegmentMap[index], lastActiveSegmentId);
                }
            }

            parent.EncoderSegmentMap = this.encoderSegmentMap;
            parent.VarianceSegmentRefresh = refresh;
            parent.SearchSegmentMap = current.GetSegmentMap(mapLength);
            parent.PreviousSegmentMap = primary is { Segmentation.Enabled: true } ? primary.GetSegmentMap(mapLength) : default;
            parent.SpatialSegmentCost = 0;
            parent.TemporalSegmentCost = 0;
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
