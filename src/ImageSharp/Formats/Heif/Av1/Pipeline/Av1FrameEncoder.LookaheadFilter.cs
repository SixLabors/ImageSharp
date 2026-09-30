// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Filters the key frames and alternate references of a lookahead golden group and chooses the source each frame
/// codes from.
/// </content>
internal static partial class Av1FrameEncoder
{
    /// <summary>
    /// Holds the filtered frames of the current golden group. Reference: TEMPORAL_FILTER_INFO with its key-frame and
    /// alternate-reference buffers, and tf_buf_second_arf.
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    /// <typeparam name="TOperator">The filter arithmetic.</typeparam>
    /// <typeparam name="TSearch">The motion search arithmetic.</typeparam>
    private sealed class LookaheadTemporalFilter<TSample, TOperator, TSearch> : ILookaheadFilter<TSample>, IDisposable
        where TSample : unmanaged
        where TOperator : struct, Av1TemporalFilter.ITemporalFilterOperator<TSample>, Av1TemporalFilter.ISharpPredictionOperator<TSample>
        where TSearch : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
    {
        /// <summary>
        /// The buffer of a filtered key frame, of a filtered alternate reference, and of a filtered second
        /// alternate reference. Reference: tf_buf[0], tf_buf[1] and tf_buf_second_arf.
        /// </summary>
        private const int BufferCount = 3;

        private readonly Av1TemporalFilterWorkspace<TSample> workspace;
        private readonly Av1EncoderFrameBuffer<TSample>[] buffers = new Av1EncoderFrameBuffer<TSample>[BufferCount];
        private readonly int[] groupIndices = [-1, -1, -1];
        private readonly int[] lookaheadIndices = [-1, -1, -1];
        private readonly Av1TemporalFilterResult[] results = new Av1TemporalFilterResult[BufferCount];
        private readonly Av1EncoderOptions options;
        private readonly int constantQualityIndex;
        private readonly int border;
        private readonly Av1BitDepth bitDepth;
        private readonly int width;
        private readonly int height;
        private readonly int lagInFrames;

        public LookaheadTemporalFilter(
            Configuration configuration,
            int width,
            int height,
            int bitDepth,
            Av1ColorFormat colorFormat,
            int border,
            Av1EncoderOptions options,
            int constantQualityIndex)
        {
            this.workspace = new Av1TemporalFilterWorkspace<TSample>(configuration);
            for (int i = 0; i < BufferCount; i++)
            {
                this.buffers[i] = new Av1EncoderFrameBuffer<TSample>(
                    configuration,
                    width,
                    height,
                    bitDepth,
                    colorFormat,
                    CenteredChromaSamplePosition,
                    CenteredChromaSamplePosition,
                    border);
            }

            this.options = options;
            this.constantQualityIndex = constantQualityIndex;
            this.border = border;
            this.bitDepth = (Av1BitDepth)((bitDepth - 8) >> 1);
            this.width = width;
            this.height = height;
            this.lagInFrames = options.LagInFrames;
        }

        /// <summary>
        /// Discards the filtered frames of the previous group. Reference: av1_tf_info_reset().
        /// </summary>
        public void Reset()
        {
            this.groupIndices.AsSpan().Fill(-1);
            this.lookaheadIndices.AsSpan().Fill(-1);
        }

        /// <summary>
        /// Returns the filtered key frame or alternate reference of a group entry, or <see langword="null"/> when the
        /// entry has none. Reference: av1_tf_info_get_filtered_buf().
        /// </summary>
        /// <param name="groupIndex">The group index of the entry.</param>
        /// <returns>The filtered frame.</returns>
        public Av1EncoderFrame<TSample>? GetFilteredFrame(int groupIndex)
        {
            Av1EncoderFrame<TSample>? frame = null;
            for (int buffer = 0; buffer < 2; buffer++)
            {
                if (this.lookaheadIndices[buffer] >= 0 && this.groupIndices[buffer] == groupIndex)
                {
                    frame = this.buffers[buffer].Frame;
                }
            }

            return frame;
        }

        /// <summary>
        /// Filters the key frame and alternate reference of a golden group that are not filtered yet. A frame keeps
        /// the result of an earlier call for the same lookahead position, even from a trial group. Reference:
        /// av1_tf_info_filtering().
        /// </summary>
        /// <param name="lookahead">The lookahead.</param>
        /// <param name="secondPass">The frame-level decisions.</param>
        /// <param name="allowHighPrecisionMotion">The high precision flag the encoder holds.</param>
        /// <param name="allowScreenContentTools">The screen content flag the encoder holds.</param>
        /// <param name="motionSettings">The motion search settings the encoder holds.</param>
        public void FilterGroup(
            Av1LookaheadQueue<TSample> lookahead,
            Av1SecondPass secondPass,
            bool allowHighPrecisionMotion,
            bool allowScreenContentTools,
            Av1MotionSearchSettings motionSettings)
        {
            Av1GopStructure group = secondPass.Group;
            for (int index = 0; index < group.Size; index++)
            {
                Av1FrameUpdateType updateType = group.UpdateTypes[index];
                if (updateType is not (Av1FrameUpdateType.Key or Av1FrameUpdateType.Alternate))
                {
                    continue;
                }

                int buffer = group.KeyFrames[index] ? 0 : 1;
                int lookaheadIndex = group.ArfSourceOffsets[index] + group.CurrentFrameIndices[index];
                if (this.lookaheadIndices[buffer] == lookaheadIndex)
                {
                    continue;
                }

                this.lookaheadIndices[buffer] = lookaheadIndex;
                this.Filter(
                    buffer,
                    lookahead,
                    lookaheadIndex,
                    index,
                    currentIndex: 0,
                    secondPass,
                    allowHighPrecisionMotion,
                    allowScreenContentTools,
                    motionSettings);
            }
        }

        /// <summary>
        /// Chooses the source that a coded frame uses: its filtered frame for a key frame with visible noise, an
        /// alternate reference and a second alternate reference, and the lookahead frame otherwise. It also decides
        /// whether the frame can be shown later. Reference: denoise_and_encode().
        /// </summary>
        /// <param name="lookahead">The lookahead.</param>
        /// <param name="secondPass">The frame-level decisions.</param>
        /// <param name="frame">The decisions of the frame, whose showable flag the filter decides.</param>
        /// <param name="source">The lookahead source of the frame.</param>
        /// <param name="allowHighPrecisionMotion">The high precision flag the encoder holds.</param>
        /// <param name="allowScreenContentTools">The screen content flag the encoder holds.</param>
        /// <param name="motionSettings">The motion search settings the encoder holds.</param>
        /// <returns>The source to code.</returns>
        public Av1EncoderFrameBuffer<TSample> SelectSource(
            Av1LookaheadQueue<TSample> lookahead,
            Av1SecondPass secondPass,
            ref Av1SecondPassFrame frame,
            Av1EncoderFrameBuffer<TSample> source,
            bool allowHighPrecisionMotion,
            bool allowScreenContentTools,
            Av1MotionSearchSettings motionSettings)
        {
            Av1TemporalFilterSettings settings = new(this.options.Speed, this.width, this.height, allowScreenContentTools, motionSettings);
            bool secondAlternate = Av1TemporalFilter.IsSecondAlternateReference(frame.UpdateType, frame.SourceOffset);
            double noise = frame.IsKeyFrame
                ? Av1TemporalFilter.EstimateNoiseLevel<TSample, TOperator>(source.Frame, Av1Plane.Y)
                : 0;

            bool apply = Av1TemporalFilter.ShouldApplyFiltering(
                Av1TemporalFilter.IsTemporalFilterOn(settings.MaximumFrames, this.lagInFrames),
                frame.UpdateType,
                frame.IsKeyFrame,
                secondAlternate,
                frame.ShowExistingFrame,
                this.options.MinimumQuantizer == 0 && this.options.MaximumQuantizer == 0,
                noise,
                in settings);

            if (!apply)
            {
                return source;
            }

            // av1_rc_pick_q_and_bounds() before the filter decisions.
            int qIndex = secondPass.PickQIndex(frame.GroupIndex, false);
            if (frame.UpdateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Alternate)
            {
                int buffer = frame.IsKeyFrame ? 0 : 1;
                if (this.groupIndices[buffer] != frame.GroupIndex)
                {
                    return source;
                }

                bool showFiltered = Av1TemporalFilter.CheckShowFilteredFrame(
                    this.width, this.height, in this.results[buffer], qIndex, this.bitDepth, settings.EnableOverlay, false);

                frame = frame with { ShowableFrame = showFiltered };
                if (!frame.IsKeyFrame)
                {
                    secondPass.ShowExistingAlternateReference = showFiltered;
                }

                return this.buffers[buffer];
            }

            // The second alternate reference is filtered when it is coded, and it is always shown from its filtered
            // frame. Reference: the is_second_arf branch of denoise_and_encode().
            this.Filter(
                2,
                lookahead,
                frame.SourceOffset,
                frame.GroupIndex,
                frame.GroupIndex,
                secondPass,
                allowHighPrecisionMotion,
                allowScreenContentTools,
                motionSettings);

            frame = frame with { ShowableFrame = true };
            return this.buffers[2];
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.workspace.Dispose();
            foreach (Av1EncoderFrameBuffer<TSample> buffer in this.buffers)
            {
                buffer?.Dispose();
            }
        }

        private void Filter(
            int buffer,
            Av1LookaheadQueue<TSample> lookahead,
            int lookaheadIndex,
            int groupIndex,
            int currentIndex,
            Av1SecondPass secondPass,
            bool allowHighPrecisionMotion,
            bool allowScreenContentTools,
            Av1MotionSearchSettings motionSettings)
        {
            Av1GopStructure group = secondPass.Group;
            Av1EncoderFrame<TSample>[] frames = new Av1EncoderFrame<TSample>[lookahead.Count];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = lookahead.Peek(i)!.Frame;
            }

            double[] coefficients = new double[secondPass.StatisticsCount];
            secondPass.CopyCorrelationCoefficients(coefficients);
            Av1TemporalFilterSettings settings = new(this.options.Speed, this.width, this.height, allowScreenContentTools, motionSettings);
            Av1TemporalFilterFrameParameters parameters = new()
            {
                UpdateType = group.UpdateTypes[groupIndex],
                IsKeyFrame = group.KeyFrames[groupIndex],
                IsForwardKeyFrame = false,
                CurrentFrameIsKeyFrame = group.KeyFrames[currentIndex],
                CurrentFrameIsKeyFrameUpdate = group.UpdateTypes[currentIndex] == Av1FrameUpdateType.Key,
                FramesSinceKey = secondPass.FramesSinceKey,
                FramesToKey = secondPass.FramesToKey,
                FilterQIndex = this.constantQualityIndex,
                AllowHighPrecisionMotion = allowHighPrecisionMotion,
                ForceIntegerMotion = false,
                BorderInPixels = this.border
            };

            this.results[buffer] = Av1TemporalFilter.Filter<TSample, TOperator, TSearch>(
                this.workspace,
                frames,
                lookaheadIndex,
                in settings,
                in parameters,
                coefficients,
                secondPass.StatisticsPosition,
                secondPass,
                this.buffers[buffer].Frame);

            this.buffers[buffer].Frame.ExtendBorders();
            this.groupIndices[buffer] = groupIndex;
        }
    }
}
