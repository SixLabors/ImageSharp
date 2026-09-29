// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Codes a good-quality sequence through the lookahead: the frames of each golden group leave display order, the
/// alternate references are coded hidden and shown later, and each temporal unit ends with a shown frame.
/// </content>
internal static partial class Av1FrameEncoder
{
    /// <summary>
    /// The time-stamp ticks per second. Reference: TICKS_PER_SEC.
    /// </summary>
    private const long TicksPerSecond = 10000000;

    internal abstract partial class SequenceEncoder
    {
        /// <summary>
        /// Whether each update type found a global motion model in the current golden group, or
        /// <see cref="int.MaxValue"/> before its first frame. Reference: ppi->valid_gm_model_found.
        /// </summary>
        private readonly int[] validGlobalMotionFound = new int[(int)Av1FrameUpdateType.Count];

        /// <summary>
        /// Whether the current frame skips the global motion search. Reference: disable_gm_search_based_on_stats().
        /// </summary>
        private bool globalMotionDisabledByStatistics;

        /// <summary>
        /// Gets the constant-quality index of the sequence. Reference: cq_level.
        /// </summary>
        private protected int LaggedQualityIndex => this.constantQualityIndex;

        /// <summary>
        /// Gets a value indicating whether the encoder replaces residuals outside the visible frame. Good-quality
        /// usage does so with the default objective delta-q mode and the temporal dependency model on, unless the
        /// sharpness is 3. Reference: the do_border_pad test of av1_encode().
        /// </summary>
        private protected bool UsesBorderPad =>
            !this.SequenceHeader.IsStillPicture &&
            this.Options.Speed < HeifEncodingSpeed.Level7 &&
            this.Options.DeltaQMode == Av1DeltaQMode.Objective &&
            this.Options.EnableTemporalModel &&
            this.Options.Sharpness != 3;

        /// <summary>
        /// Encodes the frames of a sequence with the lookahead of <see cref="Av1EncoderOptions.LagInFrames"/> and
        /// records where each sample ends. Reference: the encoder_encode() loop of the good-quality usage with a lag,
        /// which pushes one frame, runs the lookahead stage, and codes frames until one is shown.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="image">The image that holds the frames.</param>
        /// <param name="firstFrameIndex">The index of the first frame to encode.</param>
        /// <param name="frameCount">The number of frames to encode.</param>
        /// <param name="frameDurationTicks">The frame duration in time-stamp ticks. Reference: ts_duration.</param>
        /// <param name="stream">The destination stream.</param>
        /// <param name="sampleEnds">Receives the stream length after each sample, one per frame.</param>
        /// <param name="syncSamples">Receives whether each sample holds a shown key frame.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public abstract void EncodeWithLookahead<TPixel>(
            Image<TPixel> image,
            int firstFrameIndex,
            int frameCount,
            long frameDurationTicks,
            Stream stream,
            Span<long> sampleEnds,
            Span<bool> syncSamples,
            CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <summary>
        /// Returns whether every reference of the current frame precedes it in display order while the sequence
        /// codes with a lookahead. Reference: refs_are_one_sided() and the lag_in_frames test of
        /// check_skip_mode_enabled().
        /// </summary>
        /// <returns><see langword="true"/> when skip mode is off for one-sided references.</returns>
        private protected bool HasOnlyPastReferencesWithLag()
        {
            if (this.Options.LagInFrames <= 0 || this.FrameHeader.IsIntra)
            {
                return false;
            }

            ReadOnlySpan<uint> referenceFrameIndices = this.FrameHeader.GetReferenceFrameIndices();
            for (int index = 0; index < Av1Constants.ReferencesPerFrame; index++)
            {
                if (this.BlockWorkspace.ReferenceFrameNumbers[(int)referenceFrameIndices[index]] > this.BlockWorkspace.EncodedFrameCount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Creates the frame-level decisions of the lookahead for this sequence.
        /// </summary>
        /// <param name="frameDurationTicks">The frame duration in time-stamp ticks.</param>
        /// <returns>The decisions.</returns>
        private protected Av1SecondPass CreateSecondPass(long frameDurationTicks)
        {
            ObuFrameSize frameSize = this.FrameHeader.FrameSize;
            return new Av1SecondPass(
                frameSize.FrameWidth,
                frameSize.FrameHeight,
                this.SequenceHeader.ColorConfig.BitDepth,
                this.constantQualityIndex,
                Av1QuantizationLookup.GetQIndex(this.Options.MinimumQuantizer),
                Av1QuantizationLookup.GetQIndex(this.Options.MaximumQuantizer),
                (int)this.Options.Speed,
                this.Options.LagInFrames,
                (double)TicksPerSecond / frameDurationTicks,
                null);
        }

        /// <summary>
        /// Sets the frame header fields of a frame of a lookahead golden group before its source analysis: the frame
        /// type, the show flags, and the order hint of its display position. Reference: the frame parameters that
        /// av1_encode_strategy() passes to av1_encode(), with current_frame->order_hint.
        /// </summary>
        /// <param name="frame">The decisions of the frame.</param>
        private protected void ConfigureLaggedFrameHeader(in Av1SecondPassFrame frame)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            this.ConfigureFrameHeader(frame.IsKeyFrame ? ObuFrameType.KeyFrame : ObuFrameType.InterFrame);
            int orderHintBits = this.SequenceHeader.OrderHintInfo.OrderHintBits;
            frameHeader.OrderHint = orderHintBits == 0 ? 0 : (uint)frame.DisplayOrder & ((1U << orderHintBits) - 1);
            frameHeader.ShowFrame = frame.ShowFrame;
            frameHeader.ShowableFrame = frame.ShowableFrame;
        }

        /// <summary>
        /// Selects the reference slots, the refreshed slots, and the primary reference of a frame of a lookahead
        /// golden group. Reference: the reference setup of av1_encode_strategy(), and copy_frame_prob_info() at a key
        /// frame.
        /// </summary>
        /// <param name="parent">The frame state.</param>
        /// <param name="frame">The decisions of the frame.</param>
        private protected void ConfigureLaggedReferenceStructure(Av1PictureParentControlSet parent, in Av1SecondPassFrame frame)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            Av1GoodQualityReferenceStructure.GroupFrame groupFrame = new(
                frame.UpdateType,
                frame.LayerDepth,
                frame.MaxLayerDepth,
                frame.DisplayOrder,
                frame.ResetsReferences,
                isNonReference: false,
                showExisting: false);

            this.goodQualityStructure.ConfigureLagged(frameHeader, in groupFrame, []);
            parent.FrameUpdateType = frame.UpdateType;
            parent.StartsGoldenGroup = frame.UpdateType == Av1FrameUpdateType.Golden;

            // av1_configure_buffer_updates(): the golden refresh of the update role.
            parent.RefreshesGolden = frame.UpdateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or
                Av1FrameUpdateType.Overlay || (frame.UpdateType == Av1FrameUpdateType.Alternate && frame.ResetsReferences);

            if (frameHeader.FrameType == ObuFrameType.KeyFrame)
            {
                this.warpedProbabilities.AsSpan().Fill(64);
                DefaultObmcProbabilities.CopyTo(this.obmcProbabilities, 0);
            }
        }

        /// <summary>
        /// Decides whether the frame searches global motion from the models its golden group found so far. A group
        /// with an alternate reference stops searching once its alternate, intermediate alternate and leaf frames
        /// have all been coded without a model. Reference: the valid_gm_model_found reset of
        /// av1_compute_global_motion_facade() and disable_gm_search_based_on_stats().
        /// </summary>
        /// <param name="frame">The decisions of the frame.</param>
        /// <param name="group">The golden group of the frame.</param>
        private protected void BeginLaggedGlobalMotion(in Av1SecondPassFrame frame, Av1GopStructure group)
        {
            if (frame.GroupIndex == 0)
            {
                this.validGlobalMotionFound.AsSpan().Fill(int.MaxValue);
            }

            this.globalMotionDisabledByStatistics = group.ArfIndex > -1 &&
                this.validGlobalMotionFound[(int)Av1FrameUpdateType.Alternate] == 0 &&
                this.validGlobalMotionFound[(int)Av1FrameUpdateType.IntermediateAlternate] == 0 &&
                this.validGlobalMotionFound[(int)Av1FrameUpdateType.Last] == 0;
        }

        /// <summary>
        /// Records whether the coded frame found a global motion model for its update type. Reference: update_gm_stats().
        /// </summary>
        /// <param name="updateType">The update type of the frame.</param>
        private protected void CompleteLaggedGlobalMotion(Av1FrameUpdateType updateType)
        {
            int present = 0;
            foreach (Av1GlobalMotionParameters model in this.FrameHeader.GetGlobalMotionParameters())
            {
                if (model.Type != Av1GlobalMotionType.Identity)
                {
                    present = 1;
                    break;
                }
            }

            ref int found = ref this.validGlobalMotionFound[(int)updateType];
            found = found == int.MaxValue ? present : found | present;
            this.globalMotionDisabledByStatistics = false;
        }

        /// <summary>
        /// Applies the quantizer that the lookahead chose for the frame. Reference: av1_set_quantizer() with the q of
        /// av1_rc_pick_q_and_bounds().
        /// </summary>
        /// <param name="qIndex">The base quantizer index.</param>
        private protected void ApplyLaggedQuantizer(int qIndex)
        {
            this.QIndex = qIndex;
            ApplyFrameQuantizer(this.FrameHeader, this.SequenceHeader, qIndex, this.Options);
        }

        /// <summary>
        /// Writes a frame that shows the coded frame of a display position, and leaves the references unchanged.
        /// Reference: encode_show_existing_frame() and the OBU_FRAME_HEADER of av1_pack_bitstream().
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="frame">The decisions of the frame.</param>
        /// <param name="writeTemporalDelimiter">Whether the frame starts a temporal unit.</param>
        private protected void WriteShowExistingFrame(Stream stream, in Av1SecondPassFrame frame, bool writeTemporalDelimiter)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            int slot = this.goodQualityStructure.GetExistingFrameSlot(frame.DisplayOrder);
            if (slot < 0)
            {
                throw new InvalidOperationException("The frame to show is not in a reference slot.");
            }

            frameHeader.ShowExistingFrame = true;
            frameHeader.FrameToShowMapIdx = (uint)slot;
            this.ObuWriter.WriteShowExistingFrame(stream, this.SequenceHeader, frameHeader, writeTemporalDelimiter);
            frameHeader.ShowExistingFrame = false;
        }
    }

    private sealed partial class ByteSequenceEncoder
    {
        /// <inheritdoc/>
        public override void EncodeWithLookahead<TPixel>(
            Image<TPixel> image,
            int firstFrameIndex,
            int frameCount,
            long frameDurationTicks,
            Stream stream,
            Span<long> sampleEnds,
            Span<bool> syncSamples,
            CancellationToken cancellationToken)
        {
            Av1ColorFormat colorFormat = this.SequenceHeader.ColorConfig.GetColorFormat();
            int lumaBorder = (this.SequenceHeader.Use128x128Superblock ? 128 : 64) + 32;
            Av1SecondPass secondPass = this.CreateSecondPass(frameDurationTicks);
            using Av1LookaheadQueue<byte> lookahead = new(
                this.Configuration,
                image.Width,
                image.Height,
                ByteSampleBitDepth,
                colorFormat,
                CenteredChromaSamplePosition,
                CenteredChromaSamplePosition,
                lumaBorder,
                secondPass.LookaheadDepth);

            using Av1FirstPass<byte, Av1FirstPassOperator.ByteOperator> firstPass = new(
                this.Configuration,
                image.Width,
                image.Height,
                this.SequenceHeader.ColorConfig.BitDepth,
                this.Options.Speed,
                this.SequenceHeader.Use128x128Superblock ? 128 : 64,
                doBorderPad: this.UsesBorderPad,
                calculateWaveletEnergy: false,
                sharpness: this.Options.Sharpness,
                tuning: this.Options.Tuning,
                lookaheadStage: true);

            using LookaheadTemporalFilter<byte, Av1TemporalFilter.ByteOperator, Av1MotionSearchBase.ByteOperator>? filter =
                this.Options.EnableTemporalFilter
                    ? new(this.Configuration, image.Width, image.Height, ByteSampleBitDepth, colorFormat, lumaBorder, this.Options, this.LaggedQualityIndex)
                    : null;

            Rectangle sourceRectangle = new(0, 0, image.Width, image.Height);
            int pushed = 0;
            int sample = 0;
            int codedFrames = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pushed < frameCount)
                {
                    // av1_lookahead_push(): the source joins the lookahead with its border extended, and the lookahead
                    // stage measures it at once.
                    Av1EncoderFrameBuffer<byte> buffer = lookahead.BeginPush();
                    PrepareSource<TPixel, byte, HeifByteSampleConverter>(
                        this.Configuration,
                        image.Frames[firstFrameIndex + pushed],
                        sourceRectangle,
                        buffer.Frame,
                        this.ConversionWorkspace);

                    buffer.Frame.ExtendBorders();
                    lookahead.EndPush();
                    Av1EncoderFrameBuffer<byte>? previous = lookahead.Peek(lookahead.Count - 2);
                    Av1FirstPassStatistics statistics = firstPass.Process(
                        buffer.Frame,
                        (previous ?? buffer).Frame,
                        frameDurationTicks,
                        forceKeyFrame: false,
                        secondPass.Group.Size == 0 ? Av1FrameUpdateType.Key : secondPass.Group.UpdateTypes[0]);

                    secondPass.PushStatistics(in statistics);
                    pushed++;
                }

                bool flush = pushed == frameCount;
                bool temporalUnitStarted = false;
                while (secondPass.TryBeginFrame(flush, out Av1SecondPassFrame frame))
                {
                    if (frame.ShowExistingFrame)
                    {
                        this.WriteShowExistingFrame(stream, in frame, !temporalUnitStarted);
                        secondPass.CompleteFrame(0);
                    }
                    else
                    {
                        // The filter reads the motion settings and frame flags the encoder holds from the previous
                        // frame; before the first frame they are those of a key frame. High precision vectors are
                        // always allowed. Reference: the av1_set_high_precision_mv(cpi, 1, 0) call that starts
                        // av1_get_compressed_data().
                        Av1MotionSearchSettings motionSettings = codedFrames == 0
                            ? new(this.Options.Speed, false, image.Size, this.QIndex, true, false)
                            : this.PictureBuffer.Picture.Parent.MotionSearchSettings;

                        if (filter is not null && frame.GroupIndex == 0)
                        {
                            filter.FilterGroup(
                                lookahead,
                                secondPass,
                                true,
                                this.FrameHeader.AllowScreenContentTools,
                                motionSettings);
                        }

                        Av1EncoderFrameBuffer<byte> source = lookahead.Peek(frame.SourceOffset)!;
                        if (filter is not null)
                        {
                            source = filter.SelectSource(
                                lookahead,
                                secondPass,
                                ref frame,
                                source,
                                true,
                                this.FrameHeader.AllowScreenContentTools,
                                motionSettings);
                        }

                        this.EncodeLaggedFrame(source, in frame, secondPass, stream, !temporalUnitStarted);
                        codedFrames++;
                    }

                    temporalUnitStarted = true;
                    if (frame.ShowFrame)
                    {
                        lookahead.Pop();
                        sampleEnds[sample] = stream.Length;
                        syncSamples[sample] = frame.IsKeyFrame;
                        sample++;
                        break;
                    }
                }

                if (flush && secondPass.PendingFrameCount == 0)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Codes one frame of a lookahead golden group from its lookahead source. Reference: av1_encode() through
        /// encode_frame_to_data_rate() for a frame that is not a repeat.
        /// </summary>
        /// <param name="source">The lookahead source of the frame.</param>
        /// <param name="frame">The decisions of the frame.</param>
        /// <param name="secondPass">The frame-level decisions of the lookahead.</param>
        /// <param name="stream">The destination stream.</param>
        /// <param name="writeTemporalDelimiter">Whether the frame starts a temporal unit.</param>
        private void EncodeLaggedFrame(
            Av1EncoderFrameBuffer<byte> source,
            in Av1SecondPassFrame frame,
            Av1SecondPass secondPass,
            Stream stream,
            bool writeTemporalDelimiter)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            this.ConfigureLaggedFrameHeader(in frame);

            Av1EncoderReferencePool<byte>.Entry current = this.referencePool.Acquire();
            Av1EncoderReferencePool<byte>.Entry? last = frameHeader.IsIntra ? null : this.referencePool.GetSlot(this.GetLastSlot());
            bool isScreenContent = ConfigureFrameTools(
                this.Configuration,
                source.Frame,
                (last ?? current).Buffer.Frame,
                this.SequenceHeader,
                frameHeader,
                this.Options);

            this.PictureBuffer.Reset(frameHeader);
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            parent.PreviousSource = default;
            parent.SourceBlockSad = default;
            parent.HighSourceSad = false;
            parent.FrameSourceSad = 0;
            parent.SourceMotionPercentage = 0;
            if (frameHeader.IsIntra)
            {
                this.framesSinceKey = 0;
            }

            parent.FramesSinceKey = this.framesSinceKey;
            parent.FramesSinceGolden = this.FramesSinceGolden;
            parent.IsScreenContent = isScreenContent;

            this.ConfigureLaggedReferenceStructure(parent, in frame);

            // Reference distances follow display order, and a hidden frame keeps the count of the frames shown
            // before it. Reference: current_frame->display_order_hint and current_frame->frame_number.
            this.BlockWorkspace.EncodedFrameCount = frame.DisplayOrder;
            this.BlockWorkspace.FrameNumber = frame.DisplayOrder - frame.SourceOffset;
            int qIndex = secondPass.ChooseBaseQIndex(isScreenContent, tplValid: false, tplR0: 0, tplQStepRatio: 0);
            this.ApplyLaggedQuantizer(qIndex);

            // The lookahead makes this a statistics-consuming stage, so inter frames scale the rate multiplier by
            // their layer depth and the golden boost. Reference: is_stat_consumption_stage() in av1_compute_rd_mult().
            parent.IsStatConsumptionStage = true;
            parent.LayerDepth = frame.LayerDepth;
            parent.GoldenBoost = secondPass.GoldenBoost;
            parent.TplFrame = null;
            parent.TplStatisticsReady = false;

            parent.EncoderOptions = this.Options;
            parent.ConstantQualityIndex = this.QIndex;
            parent.SpeedSettings = new(
                this.Options.Speed,
                this.SequenceHeader.IsStillPicture,
                frameHeader.IsIntra,
                parent.FrameUpdateType,
                this.QIndex,
                new Size(source.Frame.Width, source.Frame.Height));

            parent.BorderPad = this.UsesBorderPad;

            this.ConfigureReferenceTools(parent);
            this.SymbolEncoder.BeginFrame(this.BindReferences(parent), frameHeader.QuantizationParameters.BaseQIndex);
            this.BeginLaggedGlobalMotion(in frame, secondPass.Group);
            this.SearchGlobalMotion<byte, ByteGlobalMotionSearchOperator>(source.Frame, this.references, parent);

            Encode(
                this.ObuWriter,
                stream,
                this.SequenceHeader,
                frameHeader,
                this.PictureBuffer.Picture,
                source,
                this.references,
                current.Buffer,
                this.Coefficients,
                this.TileWorkspace,
                this.BlockWorkspace,
                this.SymbolEncoder,
                writeSequenceHeader: frameHeader.FrameType == ObuFrameType.KeyFrame && frameHeader.ShowFrame,
                writeTemporalDelimiter);

            this.CompleteLaggedGlobalMotion(frame.UpdateType);
            this.SymbolEncoder.SnapshotTo(current.Context);
            this.MotionField.SaveFrameMotionVectors(this.PictureBuffer.Picture, current.MotionField);
            this.CompleteFrameHeader();
            this.CompleteReferenceStructure();
            secondPass.CompleteFrame(qIndex);

            this.framesSinceKey++;
            current.Buffer.Frame.ExtendBorders();
            this.referencePool.Refresh(current, frameHeader.RefreshFrameFlags);
        }
    }

    private sealed partial class HighBitDepthSequenceEncoder
    {
        /// <inheritdoc/>
        public override void EncodeWithLookahead<TPixel>(
            Image<TPixel> image,
            int firstFrameIndex,
            int frameCount,
            long frameDurationTicks,
            Stream stream,
            Span<long> sampleEnds,
            Span<bool> syncSamples,
            CancellationToken cancellationToken)
        {
            Av1ColorFormat colorFormat = this.SequenceHeader.ColorConfig.GetColorFormat();
            int lumaBorder = (this.SequenceHeader.Use128x128Superblock ? 128 : 64) + 32;
            Av1SecondPass secondPass = this.CreateSecondPass(frameDurationTicks);
            using Av1LookaheadQueue<ushort> lookahead = new(
                this.Configuration,
                image.Width,
                image.Height,
                this.SequenceHeader.ColorConfig.BitDepth.GetBitCount(),
                colorFormat,
                CenteredChromaSamplePosition,
                CenteredChromaSamplePosition,
                lumaBorder,
                secondPass.LookaheadDepth);

            using Av1FirstPass<ushort, Av1FirstPassOperator.UInt16Operator> firstPass = new(
                this.Configuration,
                image.Width,
                image.Height,
                this.SequenceHeader.ColorConfig.BitDepth,
                this.Options.Speed,
                this.SequenceHeader.Use128x128Superblock ? 128 : 64,
                doBorderPad: this.UsesBorderPad,
                calculateWaveletEnergy: false,
                sharpness: this.Options.Sharpness,
                tuning: this.Options.Tuning,
                lookaheadStage: true);

            Rectangle sourceRectangle = new(0, 0, image.Width, image.Height);
            int pushed = 0;
            int sample = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pushed < frameCount)
                {
                    // av1_lookahead_push(): the source joins the lookahead with its border extended, and the lookahead
                    // stage measures it at once.
                    Av1EncoderFrameBuffer<ushort> buffer = lookahead.BeginPush();
                    PrepareSource<TPixel, ushort, HeifUShortSampleConverter>(
                        this.Configuration,
                        image.Frames[firstFrameIndex + pushed],
                        sourceRectangle,
                        buffer.Frame,
                        this.ConversionWorkspace);

                    buffer.Frame.ExtendBorders();
                    lookahead.EndPush();
                    Av1EncoderFrameBuffer<ushort>? previous = lookahead.Peek(lookahead.Count - 2);
                    Av1FirstPassStatistics statistics = firstPass.Process(
                        buffer.Frame,
                        (previous ?? buffer).Frame,
                        frameDurationTicks,
                        forceKeyFrame: false,
                        secondPass.Group.Size == 0 ? Av1FrameUpdateType.Key : secondPass.Group.UpdateTypes[0]);

                    secondPass.PushStatistics(in statistics);
                    pushed++;
                }

                bool flush = pushed == frameCount;
                bool temporalUnitStarted = false;
                while (secondPass.TryBeginFrame(flush, out Av1SecondPassFrame frame))
                {
                    if (frame.ShowExistingFrame)
                    {
                        this.WriteShowExistingFrame(stream, in frame, !temporalUnitStarted);
                        secondPass.CompleteFrame(0);
                    }
                    else
                    {
                        Av1EncoderFrameBuffer<ushort> source = lookahead.Peek(frame.SourceOffset)!;
                        this.EncodeLaggedFrame(source, in frame, secondPass, stream, !temporalUnitStarted);
                    }

                    temporalUnitStarted = true;
                    if (frame.ShowFrame)
                    {
                        lookahead.Pop();
                        sampleEnds[sample] = stream.Length;
                        syncSamples[sample] = frame.IsKeyFrame;
                        sample++;
                        break;
                    }
                }

                if (flush && secondPass.PendingFrameCount == 0)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Codes one frame of a lookahead golden group from its lookahead source. Reference: av1_encode() through
        /// encode_frame_to_data_rate() for a frame that is not a repeat.
        /// </summary>
        /// <param name="source">The lookahead source of the frame.</param>
        /// <param name="frame">The decisions of the frame.</param>
        /// <param name="secondPass">The frame-level decisions of the lookahead.</param>
        /// <param name="stream">The destination stream.</param>
        /// <param name="writeTemporalDelimiter">Whether the frame starts a temporal unit.</param>
        private void EncodeLaggedFrame(
            Av1EncoderFrameBuffer<ushort> source,
            in Av1SecondPassFrame frame,
            Av1SecondPass secondPass,
            Stream stream,
            bool writeTemporalDelimiter)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            this.ConfigureLaggedFrameHeader(in frame);

            Av1EncoderReferencePool<ushort>.Entry current = this.referencePool.Acquire();
            Av1EncoderReferencePool<ushort>.Entry? last = frameHeader.IsIntra ? null : this.referencePool.GetSlot(this.GetLastSlot());
            bool isScreenContent = ConfigureFrameTools(
                this.Configuration,
                source.Frame,
                (last ?? current).Buffer.Frame,
                this.SequenceHeader,
                frameHeader,
                this.Options);

            this.PictureBuffer.Reset(frameHeader);
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            parent.PreviousSource = default;
            parent.SourceBlockSad = default;
            parent.HighSourceSad = false;
            parent.FrameSourceSad = 0;
            parent.SourceMotionPercentage = 0;
            if (frameHeader.IsIntra)
            {
                this.framesSinceKey = 0;
            }

            parent.FramesSinceKey = this.framesSinceKey;
            parent.FramesSinceGolden = this.FramesSinceGolden;
            parent.IsScreenContent = isScreenContent;

            this.ConfigureLaggedReferenceStructure(parent, in frame);

            // Reference distances follow display order, and a hidden frame keeps the count of the frames shown
            // before it. Reference: current_frame->display_order_hint and current_frame->frame_number.
            this.BlockWorkspace.EncodedFrameCount = frame.DisplayOrder;
            this.BlockWorkspace.FrameNumber = frame.DisplayOrder - frame.SourceOffset;
            int qIndex = secondPass.ChooseBaseQIndex(isScreenContent, tplValid: false, tplR0: 0, tplQStepRatio: 0);
            this.ApplyLaggedQuantizer(qIndex);

            // The lookahead makes this a statistics-consuming stage, so inter frames scale the rate multiplier by
            // their layer depth and the golden boost. Reference: is_stat_consumption_stage() in av1_compute_rd_mult().
            parent.IsStatConsumptionStage = true;
            parent.LayerDepth = frame.LayerDepth;
            parent.GoldenBoost = secondPass.GoldenBoost;
            parent.TplFrame = null;
            parent.TplStatisticsReady = false;

            parent.EncoderOptions = this.Options;
            parent.ConstantQualityIndex = this.QIndex;
            parent.SpeedSettings = new(
                this.Options.Speed,
                this.SequenceHeader.IsStillPicture,
                frameHeader.IsIntra,
                parent.FrameUpdateType,
                this.QIndex,
                new Size(source.Frame.Width, source.Frame.Height));

            parent.BorderPad = this.UsesBorderPad;

            this.ConfigureReferenceTools(parent);
            this.SymbolEncoder.BeginFrame(this.BindReferences(parent), frameHeader.QuantizationParameters.BaseQIndex);
            this.BeginLaggedGlobalMotion(in frame, secondPass.Group);
            this.SearchGlobalMotion<ushort, UInt16GlobalMotionSearchOperator>(source.Frame, this.references, parent);

            Encode(
                this.ObuWriter,
                stream,
                this.SequenceHeader,
                frameHeader,
                this.PictureBuffer.Picture,
                source,
                this.references,
                current.Buffer,
                this.Coefficients,
                this.TileWorkspace,
                this.BlockWorkspace,
                this.SymbolEncoder,
                writeSequenceHeader: frameHeader.FrameType == ObuFrameType.KeyFrame && frameHeader.ShowFrame,
                writeTemporalDelimiter);

            this.CompleteLaggedGlobalMotion(frame.UpdateType);
            this.SymbolEncoder.SnapshotTo(current.Context);
            this.MotionField.SaveFrameMotionVectors(this.PictureBuffer.Picture, current.MotionField);
            this.CompleteFrameHeader();
            this.CompleteReferenceStructure();
            secondPass.CompleteFrame(qIndex);

            this.framesSinceKey++;
            current.Buffer.Frame.ExtendBorders();
            this.referencePool.Refresh(current, frameHeader.RefreshFrameFlags);
        }
    }
}
