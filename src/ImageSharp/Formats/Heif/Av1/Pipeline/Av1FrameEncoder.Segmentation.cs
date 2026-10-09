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
        /// The segment map the encoder keeps across frames. The skipped blocks of a frame that updates its map write it. Complexity adaptive
        /// quantization resets it and writes each searched block. Cyclic refresh resets it every real-time frame, marks the refreshed superblocks
        /// and writes each coded block.
        /// </summary>
        private byte[] encoderSegmentMap = [];

        /// <summary>
        /// Clears the segmentation of an intra frame. The encoder calls this method before any trial encode of the frame, including the screen
        /// content trial.
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
        /// Sets the segmentation of the frame about to be coded. The caller calls it after the quantizer and the primary reference of the frame are
        /// known, and after <see cref="ResetIntraSegmentation"/> cleared an intra frame. Variance and complexity adaptive quantization refresh the
        /// segment quantizers on these frames: an intra or error resilient frame, an alternate reference, and a golden frame that is not an overlay.
        /// They do this only for a frame that the encoder can code again. Cyclic refresh sets up every real-time frame. Any other frame keeps the
        /// segmentation of its primary reference.
        /// </summary>
        /// <typeparam name="TSample">The sample type of the reference pool.</typeparam>
        /// <param name="pool">The reference pool of the sequence.</param>
        /// <param name="current">The buffer the frame is coded into.</param>
        /// <param name="parent">The frame state.</param>
        /// <param name="allowsRecode">
        /// Whether the encoder can code the frame again. A sequence with first-pass statistics allows this. A one-pass sequence without statistics
        /// does not.
        /// </param>
        /// <param name="averageEnergy">The log of the first-pass intra error of the frame.</param>
        /// <param name="bestQIndex">The lowest allowed quantizer index.</param>
        /// <param name="worstQIndex">The highest allowed quantizer index.</param>
        /// <param name="superblockTargetRate">The target rate of the frame per 64x64 area.</param>
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

            // A frame that starts a new context clears the features and the segment map of its buffer. An inter frame without a primary reference
            // must code both the map and the data.
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

            // Both adaptive quantization modes refresh their segments on an intra or error resilient frame, an alternate reference, and a golden
            // frame that is not an overlay.
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

            // Cyclic refresh sets the segments of every real-time frame. The encoder codes these frames only once.
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

                // The block counts of the two boosted segments start at zero, so that each frame counts only its own boosted blocks.
                this.cyclicRefresh.FirstSegmentBlockCount = 0;
                this.cyclicRefresh.SecondSegmentBlockCount = 0;
            }

            if (segmentation.Enabled)
            {
                if (segmentation.SegmentationUpdateData != 1 && primary is not null)
                {
                    // A frame that does not code new segment data inherits the features of its primary reference. The copy includes the derived
                    // identifier bounds.
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
                // Disabled segmentation clears all its parameters, so that no stale flag or feature reaches the bitstream.
                segmentation.SegmentationUpdateMap = 0;
                segmentation.SegmentationUpdateData = 0;
                segmentation.SegmentationTemporalUpdate = 0;
                segmentation.SegmentIdPrecedesSkip = false;
                segmentation.LastActiveSegmentId = 0;
                segmentation.ClearFeatures();
            }

            // The buffer keeps the segmentation of the frame, so that later frames that use it as a primary reference can inherit it.
            current.Segmentation.CopyFeaturesFrom(segmentation);
            current.Segmentation.SegmentIdPrecedesSkip = segmentation.SegmentIdPrecedesSkip;
            current.Segmentation.LastActiveSegmentId = segmentation.LastActiveSegmentId;
            current.Segmentation.Enabled = segmentation.Enabled;

            // The segment quantizers set the lossless flag and the quantizer matrix levels of each segment.
            Av1QuantizationLookup.UpdateFrameQuantizationState(frameHeader);

            // The segment quantizers can change whether every segment codes losslessly. Only a frame where every segment is lossless fixes its
            // transforms at 4x4.
            frameHeader.TransformMode = frameHeader.CodedLossless
                ? Av1TransformMode.Only4x4
                : Av1TransformMode.Select;

            // The encoder map must hold no identifier above the last active segment, so the loop clamps each entry.
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
        /// Updates the noise estimate of a real-time frame. An intra frame or a frame of a new size first clears the still block counts.
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

            // An intra frame and a frame of a new size restart the still block counts.
            if (this.FrameHeader.IsIntra || this.IsResizePending)
            {
                this.noiseEstimate.ResetStillBlocks();
            }

            // The first coded frame of the sequence has no previous source.
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
        /// Sets the cyclic refresh segmentation of a real-time frame after the encoder chooses its quantizer. An intra frame first clears its
        /// segmentation. A sequence without cyclic refresh codes no segments.
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
        /// Keeps the segment map of a coded frame with segmentation in its buffer. A frame that updated its map keeps the map that the bitstream
        /// wrote. Any other frame keeps the map of its primary reference, if that reference has segmentation. The method then clears the update
        /// flags, which apply to one frame only.
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
