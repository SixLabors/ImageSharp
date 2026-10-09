// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Filters the key frames and alternate references of a lookahead golden group and chooses the source each frame codes from.
/// </content>
internal static partial class Av1FrameEncoder
{
    /// <summary>
    /// Holds the filtered frames of the current golden group: a key frame, an alternate reference and a second alternate reference.
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
        /// The number of filtered frame buffers. Buffer 0 holds a filtered key frame, buffer 1 a filtered alternate reference, and buffer 2 a
        /// filtered second alternate reference.
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
        private readonly bool temporalFilterOn;

        /// <summary>
        /// Initializes a new instance of the <see cref="LookaheadTemporalFilter{TSample, TOperator, TSearch}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration that supplies the memory allocator.</param>
        /// <param name="width">The frame width in pixels.</param>
        /// <param name="height">The frame height in pixels.</param>
        /// <param name="bitDepth">The coded sample depth in bits.</param>
        /// <param name="colorFormat">The chroma format of the frames.</param>
        /// <param name="border">The border of each filtered frame buffer in pixels.</param>
        /// <param name="options">The encoder options.</param>
        /// <param name="constantQualityIndex">The constant quality quantizer index of the sequence.</param>
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
            this.temporalFilterOn = Av1TemporalFilter.IsTemporalFilterOn(
                Av1TemporalFilterSettings.DefaultMaximumFrames,
                options.LagInFrames);
        }

        /// <summary>
        /// Discards the filtered frames of the previous group.
        /// </summary>
        public void Reset()
        {
            this.groupIndices.AsSpan().Fill(-1);
            this.lookaheadIndices.AsSpan().Fill(-1);
        }

        /// <summary>
        /// Returns the filtered key frame or alternate reference of a group entry, or <see langword="null"/> when the entry has none.
        /// </summary>
        /// <param name="groupIndex">The group index of the entry.</param>
        /// <returns>The filtered frame.</returns>
        public Av1EncoderFrame<TSample>? GetFilteredFrame(int groupIndex)
        {
            int buffer = this.FindFilteredBuffer(groupIndex);
            return buffer < 0 ? null : this.buffers[buffer].Frame;
        }

        /// <summary>
        /// Filters the key frame and alternate reference of a golden group that are not filtered yet. A frame keeps the result of an earlier call
        /// for the same lookahead position, even when that call came from a trial group.
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
            Av1MotionSearchSettings motionSettings)
        {
            if (!this.temporalFilterOn)
            {
                return;
            }

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
                    forceIntegerMotion,
                    allowScreenContentTools,
                    motionSettings);
            }
        }

        /// <summary>
        /// Chooses the source that a coded frame uses. A key frame with visible noise, an alternate reference and a second alternate reference
        /// use their filtered frame. Other frames use the lookahead frame. The method also decides whether the frame can be shown later.
        /// </summary>
        /// <param name="lookahead">The lookahead.</param>
        /// <param name="secondPass">The frame-level decisions.</param>
        /// <param name="frame">The decisions of the frame, whose showable flag the filter decides.</param>
        /// <param name="source">The lookahead source of the frame.</param>
        /// <param name="allowHighPrecisionMotion">The high precision flag the encoder holds.</param>
        /// <param name="forceIntegerMotion">The integer motion flag of the last coded frame.</param>
        /// <param name="allowScreenContentTools">The screen content flag the encoder holds.</param>
        /// <param name="motionSettings">The motion search settings the encoder holds.</param>
        /// <param name="screenContent">Whether the frames are classified as screen content.</param>
        /// <returns>The source to code.</returns>
        public Av1EncoderFrameBuffer<TSample> SelectSource(
            Av1LookaheadQueue<TSample> lookahead,
            Av1SecondPass secondPass,
            ref Av1SecondPassFrame frame,
            Av1EncoderFrameBuffer<TSample> source,
            bool allowHighPrecisionMotion,
            bool forceIntegerMotion,
            bool allowScreenContentTools,
            Av1MotionSearchSettings motionSettings,
            bool screenContent)
        {
            Av1TemporalFilterSettings settings = new(
                this.options.Speed, this.options.Sharpness, this.width, this.height, allowScreenContentTools, motionSettings);

            bool secondAlternate = Av1TemporalFilter.IsSecondAlternateReference(frame.UpdateType, frame.SourceOffset);
            double noise = frame.IsKeyFrame
                ? Av1TemporalFilter.EstimateNoiseLevel<TSample, TOperator>(source.Frame, Av1Plane.Y)
                : 0;

            bool apply = Av1TemporalFilter.ShouldApplyFiltering(
                this.temporalFilterOn,
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

            // The rate control picks the quantizer of the frame first, because the decision to show the filtered frame reads it.
            int qIndex = secondPass.PickQIndex(frame.GroupIndex, screenContent);
            if (frame.UpdateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Alternate)
            {
                int buffer = this.FindFilteredBuffer(frame.GroupIndex);
                if (buffer < 0)
                {
                    // Without a filtered frame, the encoder does not show an alternate reference again.
                    if (!frame.IsKeyFrame)
                    {
                        secondPass.ShowExistingAlternateReference = false;
                    }

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

            // The filter processes the second alternate reference when the encoder codes it. The encoder always shows this frame from its filtered
            // frame.
            this.Filter(
                2,
                lookahead,
                frame.SourceOffset,
                frame.GroupIndex,
                frame.GroupIndex,
                secondPass,
                allowHighPrecisionMotion,
                forceIntegerMotion,
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

        /// <summary>
        /// Returns the key-frame or alternate-reference buffer that holds the filtered frame of a group entry. When both buffers hold it, the
        /// method returns the alternate-reference buffer. When the filter is off or neither buffer holds it, the method returns -1.
        /// </summary>
        /// <param name="groupIndex">The group index of the entry.</param>
        /// <returns>The buffer index.</returns>
        private int FindFilteredBuffer(int groupIndex)
        {
            int found = -1;
            if (!this.temporalFilterOn)
            {
                return found;
            }

            for (int buffer = 0; buffer < 2; buffer++)
            {
                if (this.lookaheadIndices[buffer] >= 0 && this.groupIndices[buffer] == groupIndex)
                {
                    found = buffer;
                }
            }

            return found;
        }

        /// <summary>
        /// Filters one lookahead frame into a buffer and records the group entry that the buffer now holds.
        /// </summary>
        /// <param name="buffer">The buffer that receives the filtered frame.</param>
        /// <param name="lookahead">The lookahead.</param>
        /// <param name="lookaheadIndex">The lookahead position of the frame to filter.</param>
        /// <param name="groupIndex">The group index of the filtered frame.</param>
        /// <param name="currentIndex">The group index of the frame that the encoder codes at the time of the call.</param>
        /// <param name="secondPass">The frame-level decisions.</param>
        /// <param name="allowHighPrecisionMotion">The high precision flag the encoder holds.</param>
        /// <param name="forceIntegerMotion">The integer motion flag of the last coded frame.</param>
        /// <param name="allowScreenContentTools">The screen content flag the encoder holds.</param>
        /// <param name="motionSettings">The motion search settings the encoder holds.</param>
        private void Filter(
            int buffer,
            Av1LookaheadQueue<TSample> lookahead,
            int lookaheadIndex,
            int groupIndex,
            int currentIndex,
            Av1SecondPass secondPass,
            bool allowHighPrecisionMotion,
            bool forceIntegerMotion,
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
            Av1TemporalFilterSettings settings = new(
                this.options.Speed, this.options.Sharpness, this.width, this.height, allowScreenContentTools, motionSettings);

            Av1TemporalFilterFrameParameters parameters = new()
            {
                UpdateType = group.UpdateTypes[groupIndex],
                IsKeyFrame = group.KeyFrames[groupIndex],
                IsForwardKeyFrame = false,
                CurrentFrameIsKeyFrame = group.KeyFrames[currentIndex],
                CurrentFrameIsKeyFrameUpdate = group.UpdateTypes[currentIndex] == Av1FrameUpdateType.Key,
                FramesSinceKey = secondPass.FramesSinceKey,
                FramesToKey = secondPass.FramesToKey,
                FilterQIndex = secondPass.GetTemporalFilterQIndex(currentIndex),
                AllowHighPrecisionMotion = allowHighPrecisionMotion,
                ForceIntegerMotion = forceIntegerMotion,
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
