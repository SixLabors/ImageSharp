// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Runs the temporal dependency model of a lookahead sequence and hands its statistics to the frames it measured.
/// </content>
internal static partial class Av1FrameEncoder
{
    /// <summary>
    /// The filtered key frames and alternate references of the current golden group.
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    internal interface ILookaheadFilter<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Discards the filtered frames of the previous group. Reference: av1_tf_info_reset().
        /// </summary>
        public void Reset();

        /// <summary>
        /// Filters the key frame and alternate reference of a golden group that are not filtered yet.
        /// Reference: av1_tf_info_filtering().
        /// </summary>
        /// <param name="lookahead">The lookahead.</param>
        /// <param name="secondPass">The frame-level decisions.</param>
        /// <param name="allowHighPrecisionMotion">The high precision flag the encoder holds.</param>
        /// <param name="forceIntegerMotion">The integer motion flag of the last coded frame.</param>
        /// <param name="allowScreenContentTools">The screen content flag the encoder holds.</param>
        /// <param name="motionSettings">The motion search settings the encoder holds.</param>
        public void FilterGroup(
            Av1LookaheadQueue<TSample> lookahead,
            Av1SecondPass secondPass,
            bool allowHighPrecisionMotion,
            bool forceIntegerMotion,
            bool allowScreenContentTools,
            Av1MotionSearchSettings motionSettings);

        /// <summary>
        /// Returns the filtered frame of a group entry, or <see langword="null"/>. Reference:
        /// av1_tf_info_get_filtered_buf().
        /// </summary>
        /// <param name="groupIndex">The group index of the entry.</param>
        /// <returns>The filtered frame.</returns>
        public Av1EncoderFrame<TSample>? GetFilteredFrame(int groupIndex);
    }

    internal abstract partial class SequenceEncoder
    {
        /// <summary>
        /// Hands a coded frame of a lookahead sequence the statistics of the temporal dependency model.
        /// </summary>
        /// <typeparam name="TSample">The sample type.</typeparam>
        internal interface ILaggedTemporalModel<TSample>
            where TSample : unmanaged
        {
            /// <summary>
            /// Hands a frame the statistics of its group entry and returns the inputs of its quantizer choice.
            /// </summary>
            /// <param name="parent">The frame state.</param>
            /// <param name="groupIndex">The group index of the frame.</param>
            /// <param name="qStepRatio">Receives the quantizer step ratio of the frame.</param>
            /// <param name="frameValid">
            /// Receives whether the frame's statistics entry is valid, ready or not, which gates the quantizer
            /// replacement. Reference: tpl_frame[gf_frame_index].is_valid in av1_set_size_dependent_vars().
            /// </param>
            /// <returns>Whether the frame has ready and valid statistics. Reference: av1_tpl_stats_ready().</returns>
            public bool ApplyToFrame(Av1PictureParentControlSet parent, int groupIndex, out double qStepRatio, out bool frameValid);

            /// <summary>
            /// Records the state a coded frame leaves for the next model run.
            /// </summary>
            /// <param name="frame">The decisions of the coded frame.</param>
            /// <param name="source">The source the frame was coded from.</param>
            /// <param name="picture">The coded picture.</param>
            /// <param name="context">The entropy context the frame ended with.</param>
            /// <param name="qIndex">The base quantizer of the frame.</param>
            public void CompleteFrame(
                in Av1SecondPassFrame frame,
                Av1EncoderFrame<TSample> source,
                Av1PictureControlSet picture,
                Av1FrameEntropyContext context,
                int qIndex);
        }

        /// <summary>
        /// Codes one frame of a lookahead golden group at one sample type.
        /// </summary>
        /// <typeparam name="TSample">The sample type.</typeparam>
        internal interface ILaggedFrameCoder<TSample>
            where TSample : unmanaged
        {
            /// <summary>
            /// Gets the reconstructions in the reference slots.
            /// </summary>
            public Av1EncoderReferencePool<TSample> ReferencePool { get; }

            /// <summary>
            /// Classifies an intra frame as screen content from its unfiltered source, before the frame's quantizer,
            /// filtering and temporal dependency model read the decision. An inter frame keeps the decision of the
            /// last intra frame. Reference: the av1_set_screen_content_options() call of av1_encode_strategy() before
            /// denoise_and_encode().
            /// </summary>
            /// <param name="unfilteredSource">The lookahead source of the frame.</param>
            /// <param name="isKeyFrame">Whether the frame is a key frame.</param>
            /// <returns>Whether the frames are classified as screen content.</returns>
            public bool DecideLaggedScreenContent(Av1EncoderFrameBuffer<TSample> unfilteredSource, bool isKeyFrame);

            /// <summary>
            /// Codes one frame of a lookahead golden group from its source. Reference: av1_encode() through
            /// encode_frame_to_data_rate() for a frame that is not a repeat.
            /// </summary>
            /// <param name="source">The source of the frame.</param>
            /// <param name="frame">The decisions of the frame.</param>
            /// <param name="secondPass">The frame-level decisions of the lookahead.</param>
            /// <param name="stream">The destination stream.</param>
            /// <param name="writeTemporalDelimiter">Whether the frame starts a temporal unit.</param>
            /// <param name="temporalModel">The temporal dependency model, or <see langword="null"/> without it.</param>
            /// <param name="lastSource">
            /// The source of the frame shown before a shown frame, or <see langword="null"/> for the first frame and a
            /// hidden frame. Reference: the last_source of choose_frame_source().
            /// </param>
            public void EncodeLaggedFrame(
                Av1EncoderFrameBuffer<TSample> source,
                in Av1SecondPassFrame frame,
                Av1SecondPass secondPass,
                Stream stream,
                bool writeTemporalDelimiter,
                ILaggedTemporalModel<TSample>? temporalModel,
                Av1EncoderFrameBuffer<TSample>? lastSource);
        }

        /// <summary>
        /// Holds the temporal dependency model of a lookahead sequence: it measures each golden group, answers the group
        /// length test of the lookahead decisions, and hands each frame its statistics. Reference: the TPL parts of
        /// av1_get_second_pass_params(), av1_encode_strategy(), set_size_dependent_vars() and
        /// encode_frame_to_data_rate().
        /// </summary>
        /// <typeparam name="TSample">The sample type.</typeparam>
        /// <typeparam name="TSearchOperator">The motion search arithmetic.</typeparam>
        /// <typeparam name="TTplOperator">The model sample arithmetic.</typeparam>
        private protected sealed class LookaheadTemporalModel<TSample, TSearchOperator, TTplOperator>
            : Av1SecondPass.IGopLengthEvaluator, IAv1TplReferenceMapper, ILaggedTemporalModel<TSample>, IDisposable
            where TSample : unmanaged
            where TSearchOperator : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
            where TTplOperator : struct, IAv1TplSampleOperator<TSample>
        {
            private readonly SequenceEncoder owner;
            private readonly Av1TplModel<TSample, TSearchOperator, TTplOperator> model;
            private readonly Av1TplSetupInput<TSample> input = new();
            private readonly Av1LookaheadQueue<TSample> lookahead;
            private readonly Av1EncoderReferencePool<TSample> referencePool;
            private readonly ILookaheadFilter<TSample>? filter;
            private readonly int width;
            private readonly int height;
            private Av1GopStructure group = new();
            private Av1FrameEntropyContext? lastContext;
            private int lastQIndex = -1;
            private int lastTrialQIndex = -1;
            private double importance;

            /// <summary>
            /// Whether the statistics of the coded frame were ready before the frame processed them, which decides the
            /// copy of the group's last frame. Reference: the av1_tpl_stats_ready() test of av1_encode(), which runs
            /// before process_tpl_stats_frame().
            /// </summary>
            private bool statisticsReadyBeforeCoding;

            /// <summary>
            /// Initializes a new instance of the <see cref="LookaheadTemporalModel{TSample, TSearchOperator, TTplOperator}"/>
            /// class.
            /// </summary>
            /// <param name="owner">The sequence encoder that codes the frames.</param>
            /// <param name="lookahead">The lookahead.</param>
            /// <param name="referencePool">The reconstructions in the reference slots.</param>
            /// <param name="filter">The filtered frames, or <see langword="null"/> without temporal filtering.</param>
            /// <param name="width">The frame width.</param>
            /// <param name="height">The frame height.</param>
            /// <param name="colorFormat">The sampling layout.</param>
            /// <param name="lagInFrames">The effective look-ahead depth. Reference: gf_cfg->lag_in_frames.</param>
            public LookaheadTemporalModel(
                SequenceEncoder owner,
                Av1LookaheadQueue<TSample> lookahead,
                Av1EncoderReferencePool<TSample> referencePool,
                ILookaheadFilter<TSample>? filter,
                int width,
                int height,
                Av1ColorFormat colorFormat,
                int lagInFrames)
            {
                this.owner = owner;
                this.lookahead = lookahead;
                this.referencePool = referencePool;
                this.filter = filter;
                this.width = width;
                this.height = height;
                this.model = new(
                    owner.Configuration,
                    width,
                    height,
                    owner.SequenceHeader.ColorConfig.BitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    lagInFrames);

                this.model.ModeInfo.Reset();
            }

            /// <summary>
            /// Gets or sets the motion search settings the encoder holds from the previous frame, which the model and the
            /// filter reuse.
            /// </summary>
            public Av1MotionSearchSettings MotionSettings { get; set; }

            /// <inheritdoc/>
            public void BeginGroup() => this.filter?.Reset();

            /// <inheritdoc/>
            public void BeginKeyFrameInterval() => this.model.ForgetPreviousGroupAlternate();

            /// <inheritdoc/>
            public void FilterGroup(Av1SecondPass secondPass)
                => this.filter?.FilterGroup(
                    this.lookahead,
                    secondPass,
                    true,
                    this.owner.FrameHeader.ForceIntegerMotionVector,
                    this.owner.SpeedFeatureScreenContentTools,
                    this.MotionSettings);

            /// <inheritdoc/>
            public int SetupTplStatistics(Av1SecondPass secondPass, int gopEvaluation)
            {
                this.BuildInput(secondPass, secondPass.Group.KeyFrames[0], secondPass.FrameNumber);
                int evaluation = this.model.SetupStatistics(this.input, gopEvaluation);
                this.RecordModelQuantizer();
                return evaluation;
            }

            /// <summary>
            /// Leaves the encoder's base quantizer index at the model's leaf quantizer when the run measured a frame.
            /// Reference: the cm->quant_params.base_qindex assignment of init_mc_flow_dispenser().
            /// </summary>
            private void RecordModelQuantizer()
            {
                if (this.model.MeasuredFrame)
                {
                    this.owner.CommonBaseQIndex = this.model.BaseLayerQIndex;
                }
            }

            /// <summary>
            /// Measures a new golden group before its first frame is coded, unless the group length test already
            /// measured it; a group the model does not cover loses its statistics. Reference: the TPL block of
            /// av1_encode_strategy() for gf_frame_index 0.
            /// </summary>
            /// <param name="secondPass">The frame-level decisions.</param>
            /// <param name="frame">The decisions of the first frame of the group.</param>
            public void BeginFrame(Av1SecondPass secondPass, in Av1SecondPassFrame frame)
            {
                if (frame.GroupIndex != 0)
                {
                    return;
                }

                Av1GopStructure group = secondPass.Group;
                bool allow = this.owner.Options.LagInFrames > 1 &&
                    this.owner.Options.EnableTemporalModel &&
                    group.Size <= Av1TplModelConstants.FrameStatisticsLength &&
                    (frame.IsKeyFrame || frame.UpdateType is Av1FrameUpdateType.Alternate or Av1FrameUpdateType.Golden);

                if (!allow)
                {
                    this.model.InitializeStatistics();
                    return;
                }

                if (!frame.ReusesTplStatistics)
                {
                    secondPass.PreloadTplQuantizers();
                    this.BuildInput(secondPass, frame.IsKeyFrame, frame.FrameNumber);
                    _ = this.model.SetupStatistics(this.input, 0);
                    this.RecordModelQuantizer();
                }
            }

            /// <summary>
            /// Hands a frame the statistics of its group entry, measures its importance, and returns the inputs of its
            /// quantizer choice. A frame whose statistics show no dependency loses them. Reference: the
            /// process_tpl_stats_frame() call of set_size_dependent_vars(), av1_tpl_stats_ready() and
            /// av1_tpl_get_qstep_ratio().
            /// </summary>
            /// <param name="parent">The frame state.</param>
            /// <param name="groupIndex">The group index of the frame.</param>
            /// <param name="qStepRatio">Receives the quantizer step ratio of the frame.</param>
            /// <param name="frameValid">Receives whether the frame's statistics entry is valid, ready or not.</param>
            /// <returns>Whether the frame has ready and valid statistics.</returns>
            public bool ApplyToFrame(Av1PictureParentControlSet parent, int groupIndex, out double qStepRatio, out bool frameValid)
            {
                parent.TplFrame = groupIndex < Av1TplModelConstants.MaximumFrameIndex ? this.model.GetFrame(groupIndex) : null;
                this.statisticsReadyBeforeCoding = this.model.IsStatisticsReady(groupIndex);
                if (this.model.IsStatisticsReady(groupIndex))
                {
                    // The golden boost blend of process_tpl_stats_frame() is part of the quantizer choice, so only
                    // the importance is measured here.
                    int boost = 0;
                    Av1TplDecisions.ProcessFrame(this.model.GetFrame(groupIndex), false, 0, 0, 0, ref this.importance, ref boost);
                }

                bool ready = this.model.IsStatisticsReady(groupIndex);
                parent.TplStatisticsReady = ready;
                parent.TplImportance = this.importance;
                qStepRatio = ready ? Math.Sqrt(1 / Av1TplDecisions.GetFrameImportance(this.model.GetFrame(groupIndex))) : 1;
                frameValid = groupIndex < Av1TplModelConstants.FrameStatisticsLength && this.model.GetFrame(groupIndex).IsValid;
                return ready;
            }

            /// <summary>
            /// Records the state a coded frame leaves for the next model run: the source and model reconstruction of the
            /// last displayed frame of a group, the frame's mode information, entropy context, quantizer and motion
            /// search settings. Reference: the prev_gop_arf copy of encode_frame_to_data_rate(), and the mi_alloc,
            /// cm->fc and speed features the next av1_tpl_setup_stats() reads.
            /// </summary>
            /// <param name="frame">The decisions of the coded frame.</param>
            /// <param name="source">The source the frame was coded from.</param>
            /// <param name="picture">The coded picture.</param>
            /// <param name="context">The entropy context the frame ended with.</param>
            /// <param name="qIndex">The base quantizer of the frame.</param>
            public void CompleteFrame(
                in Av1SecondPassFrame frame,
                Av1EncoderFrame<TSample> source,
                Av1PictureControlSet picture,
                Av1FrameEntropyContext context,
                int qIndex)
            {
                this.model.SavePreviousGroupAlternate(
                    this.input.Group, frame.GroupIndex, this.statisticsReadyBeforeCoding, source, frame.DisplayOrder);

                this.CaptureModeInfo(picture);
                this.lastContext = context;
                this.lastQIndex = qIndex;
                this.lastTrialQIndex = picture.Parent.ScreenContentTrialQIndex;
                this.MotionSettings = picture.Parent.MotionSearchSettings;
            }

            /// <inheritdoc/>
            public void GetReferenceFrames(
                ReadOnlySpan<int> pairDisplayOrders,
                ReadOnlySpan<int> pairPyramidLevels,
                int displayOrder,
                int groupIndex,
                Span<int> remappedSlots)
                => Av1GoodQualityReferenceStructure.MapReferences(pairDisplayOrders, pairPyramidLevels, displayOrder, remappedSlots);

            /// <inheritdoc/>
            public int GetRefreshFrameFlags(
                int groupIndex,
                Av1FrameUpdateType updateType,
                bool showExistingFrame,
                int displayOrder,
                ReadOnlySpan<int> pairDisplayOrders,
                ReadOnlySpan<int> pairPyramidLevels)
            {
                // Entries past the group read what the group arrays hold there, as the reference does.
                Av1GoodQualityReferenceStructure.GroupFrame frame = new(
                    updateType,
                    this.group.LayerDepths[groupIndex],
                    this.group.MaxLayerDepth,
                    displayOrder,
                    this.group.ReferenceResets[groupIndex],
                    isNonReference: false,
                    showExistingFrame);

                return Av1GoodQualityReferenceStructure.GetRefreshFlags(pairDisplayOrders, pairPyramidLevels, in frame);
            }

            /// <inheritdoc/>
            public void Dispose() => this.model.Dispose();

            /// <summary>
            /// Fills the model input from the lookahead, the filtered frames, the reference slots and the lookahead
            /// decisions of the group. Reference: the state av1_tpl_setup_stats() and init_gop_frames_for_tpl() read.
            /// </summary>
            /// <param name="secondPass">The frame-level decisions.</param>
            /// <param name="keyFrame">Whether the frame about to be coded is a key frame. Reference: frame_params->frame_type.</param>
            /// <param name="frameNumber">The number of frames shown since the last key frame. Reference: frame_number.</param>
            private void BuildInput(Av1SecondPass secondPass, bool keyFrame, int frameNumber)
            {
                Av1GopStructure group = secondPass.Group;
                this.group = group;
                Av1TplGroup tplGroup = this.input.Group;
                tplGroup.Size = group.Size;
                tplGroup.MaximumLayerDepth = group.MaxLayerDepth;
                tplGroup.MaximumLayerDepthAllowed = group.MaxLayerDepthAllowed;
                tplGroup.ArfIndex = group.ArfIndex;

                // The model reads the group's arrays past its size for the look-ahead frames it adds, so the whole
                // arrays are copied, with what earlier groups left there. Reference: the gf_group entries that
                // init_gop_frames_for_tpl() reads past gop_length.
                int count = Math.Min(group.Size, tplGroup.UpdateType.Length);
                for (int index = 0; index < tplGroup.UpdateType.Length; index++)
                {
                    tplGroup.UpdateType[index] = group.UpdateTypes[index];
                    tplGroup.IsKeyFrame[index] = group.KeyFrames[index];
                    tplGroup.LayerDepth[index] = group.LayerDepths[index];
                    tplGroup.ArfSourceOffset[index] = group.ArfSourceOffsets[index];
                    tplGroup.CurrentFrameIndex[index] = group.CurrentFrameIndices[index];
                    tplGroup.QIndex[index] = group.QValues[index];
                    tplGroup.IsNonReference[index] = false;
                    tplGroup.DisplayIndex[index] = group.DisplayIndices[index];
                    Av1EncoderFrame<TSample>? filtered = index < count ? this.filter?.GetFilteredFrame(index) : null;
                    this.input.HasFilteredFrame[index] = filtered is not null;
                    if (filtered is not null)
                    {
                        this.input.FilteredFrames[index] = filtered.Value;
                    }
                }

                this.input.IsKeyFrame = keyFrame;
                this.input.FrameNumber = frameNumber;
                this.input.ReferenceMapper = this;

                int lookaheadCount = Math.Min(this.lookahead.Count, this.input.Lookahead.Length);
                this.input.LookaheadCount = lookaheadCount;
                for (int index = 0; index < lookaheadCount; index++)
                {
                    this.input.Lookahead[index] = this.lookahead.Peek(index)!.Frame;
                }

                Av1GoodQualityReferenceStructure structure = this.owner.goodQualityStructure;
                for (int slot = 0; slot < Av1TplModelConstants.ReferenceFrameSlotCount; slot++)
                {
                    Av1EncoderReferencePool<TSample>.Entry? entry = this.referencePool.GetSlot(slot);
                    if (entry is not null)
                    {
                        this.input.SlotFrames[slot] = entry.Buffer.Frame;
                        this.input.SlotBufferIds[slot] = entry.Id;
                    }

                    this.input.SlotDisplayOrderHints[slot] = structure.GetSlotDisplayOrder(slot);
                }

                structure.GetReferenceMapPairs(keyFrame, this.input.PairDisplayOrders, this.input.PairPyramidLevels);

                Av1EncoderOptions options = this.owner.Options;
                this.input.LagInFrames = secondPass.LagInFrames;
                this.input.FramesToKey = secondPass.FramesToKey;
                this.input.BaselineGoldenInterval = secondPass.BaselineGoldenInterval;
                this.input.GoldenBoost = secondPass.GoldenBoost;
                this.input.BestQuality = secondPass.BestQuality;
                this.input.WorstQuality = secondPass.WorstQuality;
                this.input.AdjustLeafQuantizer = options.RateControlMode is Av1RateControlMode.Quality or Av1RateControlMode.VariableBitRate;

                // Every frame starts with eighth-sample vectors allowed; the forced integer flag is the one the previous
                // frame left. Reference: av1_set_high_precision_mv(cpi, 1, 0) in av1_get_compressed_data().
                this.input.AllowHighPrecisionMotionVector = true;
                this.input.ForceIntegerMotionVector = this.owner.FrameHeader.ForceIntegerMotionVector;
                this.input.QuantizerDeltaQIndex = this.owner.FrameHeader.DeltaQParameters.IsPresent
                    ? this.owner.PictureBuffer.Picture.Parent.SuperblockDeltaQIndex
                    : 0;

                Av1TileInfo firstTile = new(0, 0, this.owner.FrameHeader);
                this.input.TileModeInfoRowEnd = firstTile.ModeInfoRowEnd;
                this.input.TileModeInfoColumnEnd = firstTile.ModeInfoColumnEnd;
                this.input.Tuning = options.Tuning;

                // The quantizer tables are built when the compressor is created, before libavif sets the sharpness,
                // and rebuilt with it only when a frame is coded. So the model of the first group rounds as sharpness
                // zero. Reference: the av1_init_quantizer() calls of av1_create_compressor() and
                // encode_without_recode().
                this.input.Sharpness = options.Sharpness;
                this.input.QuantizerSharpness = this.lastContext is null ? 0 : options.Sharpness;
                this.input.SuperblockSize = this.owner.SequenceHeader.Use128x128Superblock ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;
                this.input.SpeedFeatures = new Av1TplSpeedFeatures(
                    options.Speed, this.width, this.height, this.lastQIndex, this.lastTrialQIndex);

                this.input.MotionSettings = this.MotionSettings;
                if (this.lastContext is not null)
                {
                    this.input.MotionVectorContext.CopyFrom(this.lastContext.MotionVector);
                }
            }

            /// <summary>
            /// Takes the mode-information grid and records a coded frame left, which the model borrows. Reference: the
            /// mi_grid_base and mi_alloc that set_mode_info_offsets() lends to mode_estimation().
            /// </summary>
            /// <param name="picture">The coded picture.</param>
            private void CaptureModeInfo(Av1PictureControlSet picture) => this.model.ModeInfo.Capture(picture);
        }
    }
}
