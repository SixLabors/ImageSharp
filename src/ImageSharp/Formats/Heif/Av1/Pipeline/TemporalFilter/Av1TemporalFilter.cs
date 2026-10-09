// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Memory;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Motion.Av1MotionSearchBase;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

/// <summary>
/// Filters a source frame with its motion-compensated neighbors in the look-ahead buffer before the frame is coded:
/// the key frames and alternate references of good-quality encoding with look-ahead.
/// </summary>
/// <remarks>
/// <para>
/// The filter divides the frame into 64x64 blocks. For each block and each neighboring frame it searches the luma
/// motion of the block and of its 32x32 and 16x16 sub-blocks, predicts all planes with the twelve-tap sharp filter,
/// weighs each predicted sample by a non-local-means weight that decays with the local window error, the block
/// search error, the motion distance, the noise level, the quantizer and the strength, and accumulates the weighted
/// samples. The frame to filter enters at the full weight. The filtered sample is the rounded weighted mean.
/// </para>
/// <para>
/// A caller uses three steps. <see cref="ShouldApplyFiltering"/> decides whether a frame is filtered. <see cref="Filter"/> writes
/// the filtered frame into a caller-owned buffer. <see cref="CheckShowFilteredFrame"/> decides between showing the filtered
/// alternate reference directly and coding an overlay.
/// </para>
/// </remarks>
internal static partial class Av1TemporalFilter
{
    /// <summary>
    /// The filter block size.
    /// </summary>
    internal const int BlockSize = 64;

    /// <summary>
    /// The gradient magnitude below which a sample counts as smooth in the noise estimate.
    /// </summary>
    internal const int NoiseEdgeThreshold = 50;

    /// <summary>
    /// The rate multiplier that makes the variance-domain motion cost of the shared searches zero: one error per bit.
    /// See <see cref="FillL1MotionCosts"/>.
    /// </summary>
    private const int MotionCostNoneRateMultiplier = 1 << 6;

    /// <summary>
    /// The look-ahead offset from which an intermediate alternate reference counts as the second alternate reference.
    /// </summary>
    private const int SecondAlternateReferenceOffset = 7;

    /// <summary>
    /// Returns whether the configuration enables temporal filtering.
    /// </summary>
    /// <param name="maximumFrames">The configured number of filter frames.</param>
    /// <param name="lagInFrames">The look-ahead depth in frames.</param>
    /// <returns><see langword="true"/> when frames are filtered.</returns>
    public static bool IsTemporalFilterOn(int maximumFrames, int lagInFrames)
        => maximumFrames > 0 && lagInFrames > 1;

    /// <summary>
    /// Returns whether an intermediate alternate reference is the second alternate reference of its group.
    /// </summary>
    /// <param name="updateType">The update type of the frame.</param>
    /// <param name="alternateReferenceOffset">The look-ahead offset of the source frame of the alternate reference.</param>
    /// <returns><see langword="true"/> for a second alternate reference.</returns>
    public static bool IsSecondAlternateReference(Av1FrameUpdateType updateType, int alternateReferenceOffset)
        => updateType == Av1FrameUpdateType.IntermediateAlternate && alternateReferenceOffset >= SecondAlternateReferenceOffset;

    /// <summary>
    /// Decides whether the filtered frame replaces the source of the frame that the encoder codes.
    /// </summary>
    /// <param name="temporalFilterOn">The result of <see cref="IsTemporalFilterOn"/>.</param>
    /// <param name="updateType">The update type of the coded frame.</param>
    /// <param name="isKeyFrame">Whether the coded frame is a key frame.</param>
    /// <param name="isSecondAlternateReference">Whether the coded frame is a second alternate reference.</param>
    /// <param name="showExistingFrame">Whether the coded frame only shows an existing frame.</param>
    /// <param name="losslessRequested">Whether the rate control requests lossless coding: both quantizer limits zero.</param>
    /// <param name="lumaNoiseLevel">For a key frame, the source luma noise level from <see cref="EstimateNoiseLevel"/>. Other frames do not use it.</param>
    /// <param name="settings">The filter settings.</param>
    /// <returns><see langword="true"/> when the filtered frame replaces the source.</returns>
    public static bool ShouldApplyFiltering(
        bool temporalFilterOn,
        Av1FrameUpdateType updateType,
        bool isKeyFrame,
        bool isSecondAlternateReference,
        bool showExistingFrame,
        bool losslessRequested,
        double lumaNoiseLevel,
        in Av1TemporalFilterSettings settings)
    {
        if (!temporalFilterOn ||
            (updateType != Av1FrameUpdateType.Key && updateType != Av1FrameUpdateType.Alternate && !isSecondAlternateReference))
        {
            return false;
        }

        if (isKeyFrame)
        {
            // A key frame is filtered only when its luma noise estimate is positive. A flat frame gives -1.
            return settings.KeyFrameFiltering != 0 && !showExistingFrame && !losslessRequested && lumaNoiseLevel > 0;
        }

        return !isSecondAlternateReference || settings.SecondAlternateReferenceFiltering;
    }

    /// <summary>
    /// Decides whether a filtered alternate reference is shown directly, without an overlay: the filtered frame must
    /// be close to its source relative to the quantizer.
    /// </summary>
    /// <param name="frameWidth">The visible frame width.</param>
    /// <param name="frameHeight">The visible frame height.</param>
    /// <param name="result">The filter result with the source-to-filtered differences.</param>
    /// <param name="qIndex">The frame quantizer index from the rate control.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="enableOverlay">Whether overlays are enabled.</param>
    /// <param name="isSecondAlternateReference">Whether the frame is a second alternate reference.</param>
    /// <returns><see langword="true"/> to show the filtered frame, <see langword="false"/> to code an overlay.</returns>
    public static bool CheckShowFilteredFrame(
        int frameWidth,
        int frameHeight,
        in Av1TemporalFilterResult result,
        int qIndex,
        Av1BitDepth bitDepth,
        bool enableOverlay,
        bool isSecondAlternateReference)
    {
        if (!enableOverlay || isSecondAlternateReference)
        {
            return true;
        }

        int blockRows = (frameHeight + BlockSize - 1) / BlockSize;
        int blockColumns = (frameWidth + BlockSize - 1) / BlockSize;
        int blockCount = Math.Max(1, blockRows * blockColumns);

        // The mean, the variance and the threshold use single precision. The comparison of the deviation with 1.2 times the mean
        // uses double precision. This mix keeps the overlay decisions the same as other AV1 encoders.
        float mean = (float)result.DifferenceSum / blockCount;
        float deviation = (float)Math.Sqrt(((float)result.DifferenceSquares / blockCount) - (mean * mean));
        int step = Av1QuantizationLookup.GetAcQuant(qIndex, 0, bitDepth);
        float threshold = 0.7f * step * step;
        return mean < threshold && deviation < mean * 1.2;
    }

    /// <summary>
    /// Returns the quantizer factor of the filter from a quantizer index.
    /// </summary>
    /// <param name="qIndex">The quantizer index that sets the filter strength.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <returns>The real quantizer on the eight-bit scale, truncated.</returns>
    public static int GetQuantizationFactor(int qIndex, Av1BitDepth bitDepth)
    {
        int step = Av1QuantizationLookup.GetAcQuant(qIndex, 0, bitDepth);
        double quantizer = bitDepth switch
        {
            Av1BitDepth.EightBit => step / 4.0,
            Av1BitDepth.TenBit => step / 16.0,
            _ => step / 64.0
        };

        return (int)quantizer;
    }

    /// <summary>
    /// Estimates the noise level of one plane of a frame.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="frame">The frame.</param>
    /// <param name="plane">The plane.</param>
    /// <returns>The noise level, or -1 when the plane has fewer than sixteen smooth samples.</returns>
    public static double EstimateNoiseLevel<TSample, TOperator>(Av1EncoderFrame<TSample> frame, Av1Plane plane)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>
    {
        // Chroma uses the visible chroma dimensions: the visible luma dimensions divided by the subsampling, rounded up.
        int subsamplingX = plane == Av1Plane.Y ? 0 : frame.ChromaSubsamplingX;
        int subsamplingY = plane == Av1Plane.Y ? 0 : frame.ChromaSubsamplingY;
        Av1PlaneRegion<TSample> region = frame.CodedView.GetPlane(plane);
        ReadOnlySpan<TSample> samples = region.Samples;
        int stride = region.Stride;
        int origin = (region.Bounds.Y * stride) + region.Bounds.X;
        return EstimateNoise<TSample, TOperator>(
            samples[origin..],
            stride,
            (frame.Width + subsamplingX) >> subsamplingX,
            (frame.Height + subsamplingY) >> subsamplingY,
            frame.LumaBitDepth,
            NoiseEdgeThreshold);
    }

    /// <summary>
    /// Filters one frame of the look-ahead buffer with its neighbors and writes the filtered frame.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The temporal filter sample arithmetic.</typeparam>
    /// <typeparam name="TSearch">The motion search sample arithmetic.</typeparam>
    /// <param name="workspace">The prediction, error and accumulator buffers of the filter.</param>
    /// <param name="lookahead">The look-ahead frames in display order. Index <c>i</c> holds the frame at look-ahead offset <c>i</c>.
    /// Every frame shares one plane layout and is edge-extended from its visible size through at least
    /// <see cref="Av1TemporalFilterFrameParameters.BorderInPixels"/> samples.</param>
    /// <param name="filterIndex">The look-ahead index of the frame to filter.</param>
    /// <param name="settings">The filter settings.</param>
    /// <param name="parameters">The group-of-pictures and rate-control state of the filtered frame.</param>
    /// <param name="correlationCoefficients">The first-pass correlation coefficients of all available statistics entries, from the
    /// first entry up to, but excluding, the end.</param>
    /// <param name="statisticsPosition">The position of the statistics entry of the current frame in
    /// <paramref name="correlationCoefficients"/>.</param>
    /// <param name="arfBoost">The first-pass boost of alternate references. Key frames do not use it.</param>
    /// <param name="output">The caller-owned filtered frame. Every 64x64 block is written, including samples beyond
    /// the visible size, which the caller extends from the visible edges afterwards.</param>
    /// <returns>The frames used and the source-to-filtered difference for <see cref="CheckShowFilteredFrame"/>.</returns>
    public static Av1TemporalFilterResult Filter<TSample, TOperator, TSearch>(
        Av1TemporalFilterWorkspace<TSample> workspace,
        ReadOnlySpan<Av1EncoderFrame<TSample>> lookahead,
        int filterIndex,
        in Av1TemporalFilterSettings settings,
        in Av1TemporalFilterFrameParameters parameters,
        ReadOnlySpan<double> correlationCoefficients,
        int statisticsPosition,
        Av1SecondPass arfBoost,
        Av1EncoderFrame<TSample> output)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>, ISharpPredictionOperator<TSample>
        where TSearch : struct, IMotionSearchOperator<TSample>
    {
        Av1EncoderFrame<TSample> frameToFilter = lookahead[filterIndex];
        Av1BitDepth bitDepth = (Av1BitDepth)((frameToFilter.LumaBitDepth - 8) >> 1);
        int qFactor = GetQuantizationFactor(parameters.FilterQIndex, bitDepth);
        Span<double> noiseLevels = stackalloc double[3];
        (int framesBefore, int frameCount) = SetupFilteringBuffer<TSample, TOperator>(
            lookahead,
            filterIndex,
            in settings,
            in parameters,
            correlationCoefficients,
            statisticsPosition,
            arfBoost,
            qFactor,
            noiseLevels);

        TemporalFilterContext context = TemporalFilterContext.Create(frameToFilter, in settings, in parameters, qFactor);
        ReadOnlySpan<Av1EncoderFrame<TSample>> frames = lookahead.Slice(filterIndex - framesBefore, frameCount);
        Av1PlaneRegion<TSample> luma = frameToFilter.CodedView.GetPlane(Av1Plane.Y);
        Av1MotionSearchSites sites = new(workspace.SearchSites);
        sites.Configure(Av1MotionSearchSettings.FullPixelSearchMethod.NStep, luma.Stride);

        int blockRows = (frameToFilter.Height + BlockSize - 1) / BlockSize;
        int blockColumns = (frameToFilter.Width + BlockSize - 1) / BlockSize;
        long differenceSum = 0;
        long differenceSquares = 0;
        for (int blockRow = 0; blockRow < blockRows; blockRow++)
        {
            FilterRow<TSample, TOperator, TSearch>(
                workspace,
                frames,
                framesBefore,
                blockRow,
                blockColumns,
                in settings,
                in context,
                noiseLevels,
                output,
                ref differenceSum,
                ref differenceSquares);
        }

        return new Av1TemporalFilterResult(frameCount, framesBefore, differenceSum, differenceSquares);
    }

    /// <summary>
    /// Chooses the frames before and after the filtered frame and estimates its noise levels.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The temporal filter sample arithmetic.</typeparam>
    /// <param name="lookahead">The look-ahead frames.</param>
    /// <param name="filterIndex">The look-ahead index of the frame to filter.</param>
    /// <param name="settings">The filter settings.</param>
    /// <param name="parameters">The group-of-pictures and rate-control state of the filtered frame.</param>
    /// <param name="correlationCoefficients">The first-pass correlation coefficients.</param>
    /// <param name="statisticsPosition">The position of the statistics entry of the current frame in <paramref name="correlationCoefficients"/>.</param>
    /// <param name="arfBoost">The first-pass boost of alternate references.</param>
    /// <param name="qFactor">The quantizer factor.</param>
    /// <param name="noiseLevels">Receives the noise level of every plane.</param>
    /// <returns>The number of frames before the filtered frame and the number of frames filtered together.</returns>
    private static (int FramesBefore, int FrameCount) SetupFilteringBuffer<TSample, TOperator>(
        ReadOnlySpan<Av1EncoderFrame<TSample>> lookahead,
        int filterIndex,
        in Av1TemporalFilterSettings settings,
        in Av1TemporalFilterFrameParameters parameters,
        ReadOnlySpan<double> correlationCoefficients,
        int statisticsPosition,
        Av1SecondPass arfBoost,
        int qFactor,
        Span<double> noiseLevels)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>
    {
        Av1EncoderFrame<TSample> frameToFilter = lookahead[filterIndex];
        int frameCount = Math.Max(settings.MaximumFrames, 1);
        int depth = lookahead.Length;

        // Filtering does not reach past key frames.
        int keyToCurrent = Math.Max(parameters.FramesSinceKey + filterIndex, 0);
        int currentToKey = Math.Max(parameters.FramesToKey - filterIndex - 1, 0);
        int maximumBefore = Math.Min(filterIndex, keyToCurrent);
        int maximumAfter = Math.Min(depth - filterIndex - 1, currentToKey);

        int planeCount = frameToFilter.IsMonochrome ? 1 : 3;
        for (int plane = 0; plane < planeCount; plane++)
        {
            noiseLevels[plane] = EstimateNoiseLevel<TSample, TOperator>(frameToFilter, (Av1Plane)plane);
        }

        // The statistics cursor steps back one entry right after a key frame. Entries at or past the end, and at or
        // before the start, count as unavailable and shorten the ranges.
        int statisticsBase = statisticsPosition - (parameters.FramesSinceKey == 0 ? 1 : 0);
        double afterCorrelation = 1.0;
        double beforeCorrelation = 1.0;
        for (int i = 1; i <= maximumAfter; i++)
        {
            int index = statisticsBase + filterIndex + i;
            if (index >= correlationCoefficients.Length)
            {
                maximumAfter = i - 1;
                break;
            }

            afterCorrelation *= Math.Max(correlationCoefficients[index], 0.001);
        }

        if (maximumAfter >= 1)
        {
            afterCorrelation = Math.Pow(afterCorrelation, 1.0 / maximumAfter);
        }

        for (int i = 1; i <= maximumBefore; i++)
        {
            int index = statisticsBase + filterIndex - i + 1;
            if (index <= 0)
            {
                maximumBefore = i - 1;
                break;
            }

            beforeCorrelation *= Math.Max(correlationCoefficients[index], 0.001);
        }

        if (maximumBefore >= 1)
        {
            beforeCorrelation = Math.Pow(beforeCorrelation, 1.0 / maximumBefore);
        }

        // A low noise level adds frames so that the filtered frame predicts later frames better. A nearly lossless
        // key frame keeps the configured count, and the first alternate reference after a key frame ignores the
        // adjustment because screen content detection has not run yet.
        int adjustment = 6;
        if (frameCount == 1)
        {
            adjustment = 0;
        }
        else if (parameters.UpdateType == Av1FrameUpdateType.Key && qFactor <= 10)
        {
            adjustment = 0;
        }
        else if (settings.FrameCountAdjustment > 0 && parameters.UpdateType != Av1FrameUpdateType.Key && parameters.FramesSinceKey > 0)
        {
            // Each level maps a luma noise below 0.5, below 1.0, and from 1.0 to the number of added frames.
            ReadOnlySpan<byte> adjustments = settings.FrameCountAdjustment == 1 ? [6, 4, 2] : [4, 2, 0];
            adjustment = noiseLevels[0] < 0.5 ? adjustments[0] : noiseLevels[0] < 1.0 ? adjustments[1] : adjustments[2];
        }

        frameCount = Math.Min(frameCount + adjustment, depth);
        int framesBefore;
        int framesAfter;
        if (parameters.IsKeyFrame)
        {
            framesBefore = Math.Min(parameters.IsForwardKeyFrame ? frameCount / 2 : 0, maximumBefore);
            framesAfter = Math.Min(frameCount - 1, maximumAfter);
        }
        else
        {
            // The earlier range goes to the forward count and the later range goes to the backward count. This swapped order keeps
            // the frame counts the same as other AV1 encoders.
            int boost = arfBoost.CalculateArfBoost(filterIndex, maximumBefore, maximumAfter);
            frameCount = Math.Min(frameCount, boost / 150);
            frameCount += (frameCount & 1) == 0 ? 1 : 0;

            // The second alternate reference uses at most two neighbors.
            if (parameters.UpdateType == Av1FrameUpdateType.IntermediateAlternate)
            {
                frameCount = Math.Min(frameCount, 3);
            }

            if (Math.Min(maximumAfter, maximumBefore) >= frameCount / 2)
            {
                framesBefore = frameCount / 2;
                framesAfter = frameCount / 2;
            }
            else
            {
                if (maximumAfter < frameCount / 2)
                {
                    framesAfter = maximumAfter;
                    framesBefore = Math.Min(frameCount - 1 - framesAfter, maximumBefore);
                }
                else
                {
                    framesBefore = maximumBefore;
                    framesAfter = Math.Min(frameCount - 1 - framesBefore, maximumAfter);
                }

                // The frame-level correlation limits how far the longer side can exceed the shorter side.
                if (maximumAfter > 0 && maximumBefore > 0)
                {
                    if (framesAfter < framesBefore)
                    {
                        int asymmetry = (int)(0.4 / Math.Max(1 - afterCorrelation, 0.01));
                        framesBefore = Math.Min(framesBefore, framesAfter + asymmetry);
                    }
                    else
                    {
                        int asymmetry = (int)(0.4 / Math.Max(1 - beforeCorrelation, 0.01));
                        framesAfter = Math.Min(framesAfter, framesBefore + asymmetry);
                    }
                }
            }
        }

        return (framesBefore, framesBefore + 1 + framesAfter);
    }

    /// <summary>
    /// Filters one row of 64x64 blocks and accumulates the source-to-filtered luma differences.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The temporal filter sample arithmetic.</typeparam>
    /// <typeparam name="TSearch">The motion search sample arithmetic.</typeparam>
    /// <param name="workspace">The prediction, error and accumulator buffers of the filter.</param>
    /// <param name="frames">The frames filtered together.</param>
    /// <param name="filterFrame">The index of the frame to filter in <paramref name="frames"/>.</param>
    /// <param name="blockRow">The block row.</param>
    /// <param name="blockColumns">The number of block columns.</param>
    /// <param name="settings">The filter settings.</param>
    /// <param name="context">The per-frame filter parameters.</param>
    /// <param name="noiseLevels">The noise level of every plane.</param>
    /// <param name="output">The filtered frame.</param>
    /// <param name="differenceSum">The running sum of the per-block luma squared differences.</param>
    /// <param name="differenceSquares">The running sum of the squares of the per-block luma squared differences.</param>
    private static void FilterRow<TSample, TOperator, TSearch>(
        Av1TemporalFilterWorkspace<TSample> workspace,
        ReadOnlySpan<Av1EncoderFrame<TSample>> frames,
        int filterFrame,
        int blockRow,
        int blockColumns,
        in Av1TemporalFilterSettings settings,
        in TemporalFilterContext context,
        ReadOnlySpan<double> noiseLevels,
        Av1EncoderFrame<TSample> output,
        ref long differenceSum,
        ref long differenceSquares)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>, ISharpPredictionOperator<TSample>
        where TSearch : struct, IMotionSearchOperator<TSample>
    {
        Av1EncoderFrame<TSample> frameToFilter = frames[filterFrame];
        Av1PlaneRegion<TSample> sourceLuma = frameToFilter.CodedView.GetPlane(Av1Plane.Y);
        ReadOnlySpan<TSample> sourceSamples = sourceLuma.Samples;
        int stride = sourceLuma.Stride;
        int sourceOrigin = (sourceLuma.Bounds.Y * stride) + sourceLuma.Bounds.X;
        ReadOnlySpan<TSample> frameToFilterLuma = frameToFilter.CodedView.GetPlane(Av1Plane.Y).Samples;
        ReadOnlySpan<TSample> frameToFilterBlue = frameToFilter.CodedView.GetPlane(Av1Plane.U).Samples;
        ReadOnlySpan<TSample> frameToFilterRed = frameToFilter.CodedView.GetPlane(Av1Plane.V).Samples;
        Span<TSample> outputLuma = output.CodedView.GetPlane(Av1Plane.Y).Samples;
        Span<TSample> outputBlue = output.CodedView.GetPlane(Av1Plane.U).Samples;
        Span<TSample> outputRed = output.CodedView.GetPlane(Av1Plane.V).Samples;
        Av1PlaneRegion<TSample> filteredLuma = output.CodedView.GetPlane(Av1Plane.Y);
        ReadOnlySpan<TSample> filteredSamples = outputLuma;
        Span<Av1MotionVector> subblockVectors = stackalloc Av1MotionVector[SubblockCount];
        Span<int> subblockErrors = stackalloc int[SubblockCount];
        Span<uint> accumulator = workspace.Accumulator[..context.BlockPixels];
        Span<ushort> count = workspace.Count[..context.BlockPixels];

        // The strength is reset for each row only: a block that lowers it lowers it for the rest of the row.
        int strength = settings.Strength;
        for (int blockColumn = 0; blockColumn < blockColumns; blockColumn++)
        {
            accumulator.Clear();
            count.Clear();
            Av1MotionVector referenceVector = default;

            // With pruning on, the block skips the sub-block motion search when its 4x4 log variances differ by 4.0 or less.
            // A high bit depth with sharpness 3 never searches the sub-blocks.
            bool highBitDepthSharpness = context.BitDepth > 8 && settings.Sharpness == 3;
            bool allowSubblockSearch = !highBitDepthSharpness;
            int blockOrigin = sourceOrigin + (blockRow * BlockSize * stride) + (blockColumn * BlockSize);
            if (!highBitDepthSharpness && settings.AllowSubblockMotionSearchPruning)
            {
                GetLogVarianceRange<TSample, TSearch>(
                    sourceSamples, stride, blockOrigin, workspace.Zeros, context.BitDepth, out double minimum, out double maximum);

                allowSubblockSearch = maximum - minimum > 4.0;
            }

            for (int frame = 0; frame < frames.Length; frame++)
            {
                bool isDcDifferenceLarge = false;
                bool isLowContrast = false;
                Av1PlaneRegion<TSample> referenceLuma = frames[frame].CodedView.GetPlane(Av1Plane.Y);
                if (frame == filterFrame)
                {
                    // Later frames continue the search from the mirrored vector.
                    referenceVector = new Av1MotionVector(-referenceVector.Row, -referenceVector.Column);
                }
                else
                {
                    BlockMotionSearch<TSample, TSearch> search = new(
                        sourceSamples,
                        sourceOrigin,
                        referenceLuma.Samples,
                        (referenceLuma.Bounds.Y * stride) + referenceLuma.Bounds.X,
                        stride,
                        workspace.FractionalPrediction,
                        workspace.Zeros,
                        workspace.SearchSites,
                        workspace.MotionCosts,
                        settings.MotionSearch,
                        in context);

                    search.Search(
                        blockRow,
                        blockColumn,
                        ref referenceVector,
                        allowSubblockSearch,
                        subblockVectors,
                        subblockErrors,
                        out isDcDifferenceLarge,
                        out isLowContrast);
                }

                if (settings.KeyFrameFiltering == 1 && context.CurrentFrameIsKeyFrame && isDcDifferenceLarge)
                {
                    strength = Math.Min(strength, 1);
                }

                // With sharpness 3, a low-contrast block limits the strength to 3. A high bit depth turns off the filter of this block.
                if (settings.Sharpness == 3 && isLowContrast)
                {
                    strength = Math.Min(strength, highBitDepthSharpness ? 0 : 3);
                }

                if (frame == filterFrame)
                {
                    AccumulateFrame<TSample, TOperator>(
                        frameToFilter,
                        frameToFilterLuma,
                        frameToFilterBlue,
                        frameToFilterRed,
                        blockRow,
                        blockColumn,
                        accumulator,
                        count);
                }
                else
                {
                    BuildPredictor<TSample, TOperator>(
                        frames[frame], blockRow, blockColumn, subblockVectors, workspace.Intermediate, workspace.Prediction);

                    ApplyFilter<TSample, TOperator>(
                        workspace,
                        frameToFilter,
                        frameToFilterLuma,
                        frameToFilterBlue,
                        frameToFilterRed,
                        blockRow,
                        blockColumn,
                        noiseLevels,
                        subblockVectors,
                        subblockErrors,
                        context.QFactor,
                        strength,
                        settings.WeightCalculationLevel);
                }
            }

            NormalizeBlock<TSample, TOperator>(output, outputLuma, outputBlue, outputRed, blockRow, blockColumn, accumulator, count);

            // Add the 64x64 luma squared difference between the source and the filtered block to the frame totals.
            int filteredStride = filteredLuma.Stride;
            int filteredOrigin = ((filteredLuma.Bounds.Y + (blockRow * BlockSize)) * filteredStride) + filteredLuma.Bounds.X + (blockColumn * BlockSize);
            GetVariance<TSample, TSearch>(
                sourceSamples[blockOrigin..],
                stride,
                filteredSamples[filteredOrigin..],
                filteredStride,
                BlockSize,
                BlockSize,
                context.BitDepth,
                out uint squaredError);

            differenceSum += squaredError;
            differenceSquares += squaredError * (long)squaredError;
        }
    }

    /// <summary>
    /// Adds one block of the frame to filter to the accumulators of every plane at the full weight.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The temporal filter sample arithmetic.</typeparam>
    /// <param name="frame">The frame to filter.</param>
    /// <param name="frameLuma">The luma samples of <paramref name="frame"/>, which the caller reads once outside its block loop.</param>
    /// <param name="frameBlue">The blue-difference samples of <paramref name="frame"/>, which the caller reads once outside its block loop.</param>
    /// <param name="frameRed">The red-difference samples of <paramref name="frame"/>, which the caller reads once outside its block loop.</param>
    /// <param name="blockRow">The block row.</param>
    /// <param name="blockColumn">The block column.</param>
    /// <param name="accumulator">The weighted sums of all planes.</param>
    /// <param name="count">The weight totals of all planes.</param>
    internal static void AccumulateFrame<TSample, TOperator>(
        Av1EncoderFrame<TSample> frame,
        ReadOnlySpan<TSample> frameLuma,
        ReadOnlySpan<TSample> frameBlue,
        ReadOnlySpan<TSample> frameRed,
        int blockRow,
        int blockColumn,
        Span<uint> accumulator,
        Span<ushort> count)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>
    {
        int planeOffset = 0;
        int planeCount = frame.IsMonochrome ? 1 : 3;
        for (int plane = 0; plane < planeCount; plane++)
        {
            GetPlaneBlock(
                frame,
                frameLuma,
                frameBlue,
                frameRed,
                (Av1Plane)plane,
                blockRow,
                blockColumn,
                out ReadOnlySpan<TSample> samples,
                out int stride,
                out int width,
                out int height);

            AccumulateSelf<TSample, TOperator>(
                samples, stride, accumulator.Slice(planeOffset, width * height), count.Slice(planeOffset, width * height), width, height);

            planeOffset += width * height;
        }
    }

    /// <summary>
    /// Weighs the prediction of one block from one reference frame and adds it to the accumulators of every plane.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The temporal filter sample arithmetic.</typeparam>
    /// <param name="workspace">The filter buffers holding the prediction and the accumulators.</param>
    /// <param name="frameToFilter">The frame to filter.</param>
    /// <param name="frameToFilterLuma">The luma samples of <paramref name="frameToFilter"/>, which the caller reads once outside its block loop.</param>
    /// <param name="frameToFilterBlue">
    /// The blue-difference samples of <paramref name="frameToFilter"/>, which the caller reads once outside its block loop.
    /// </param>
    /// <param name="frameToFilterRed">
    /// The red-difference samples of <paramref name="frameToFilter"/>, which the caller reads once outside its block loop.
    /// </param>
    /// <param name="blockRow">The block row.</param>
    /// <param name="blockColumn">The block column.</param>
    /// <param name="noiseLevels">The noise level of every plane.</param>
    /// <param name="subblockVectors">The sixteen sub-block motion vectors.</param>
    /// <param name="subblockErrors">The sixteen sub-block motion search errors.</param>
    /// <param name="qFactor">The quantizer factor.</param>
    /// <param name="strength">The filter strength, 0 to 6.</param>
    /// <param name="level">The weight calculation level.</param>
    internal static void ApplyFilter<TSample, TOperator>(
        Av1TemporalFilterWorkspace<TSample> workspace,
        Av1EncoderFrame<TSample> frameToFilter,
        ReadOnlySpan<TSample> frameToFilterLuma,
        ReadOnlySpan<TSample> frameToFilterBlue,
        ReadOnlySpan<TSample> frameToFilterRed,
        int blockRow,
        int blockColumn,
        ReadOnlySpan<double> noiseLevels,
        ReadOnlySpan<Av1MotionVector> subblockVectors,
        ReadOnlySpan<int> subblockErrors,
        int qFactor,
        int strength,
        int level)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>
    {
        int bitDepth = frameToFilter.LumaBitDepth;
        bool isMonochrome = frameToFilter.IsMonochrome;

        // A high-bit-depth frame with unequal subsampling (4:2:2) multiplies the combined error by the distance factor and the
        // decay factor separately. Other frames multiply by their product. The two orders round differently. Each frame type
        // keeps the order of other AV1 encoders, so the filtered samples stay the same.
        bool separateFactors = bitDepth > 8 && !isMonochrome && frameToFilter.ChromaSubsamplingX != frameToFilter.ChromaSubsamplingY;

        // A larger quantizer and a larger strength give larger weights. From a quantizer factor of 128, the quantizer decay
        // grows past one.
        double qDecay = Math.Pow((double)qFactor / 20, 2);
        qDecay = qDecay < 1e-5 ? 1e-5 : qDecay > 1 ? 1 : qDecay;
        if (qFactor >= 128)
        {
            qDecay = 0.5 * Math.Pow((double)qFactor / 64, 2);
        }

        double strengthDecay = Math.Pow((double)strength / 4, 2);
        strengthDecay = strengthDecay < 1e-5 ? 1e-5 : strengthDecay > 1 ? 1 : strengthDecay;

        // A longer motion vector gives a smaller weight, relative to a tenth of the shorter frame dimension.
        Span<double> distanceFactors = stackalloc double[SubblockCount];
        double distanceThreshold = Math.Max(Math.Min(frameToFilter.Height, frameToFilter.Width) * 0.1, 1);
        for (int i = 0; i < SubblockCount; i++)
        {
            Av1MotionVector vector = subblockVectors[i];
            double distance = Math.Sqrt(Math.Pow(vector.Row, 2) + Math.Pow(vector.Column, 2));
            distanceFactors[i] = Math.Max(distance / distanceThreshold, 1);
        }

        Span<double> blockErrors = stackalloc double[SubblockCount];
        Span<double> firstFactors = stackalloc double[SubblockCount];
        for (int i = 0; i < SubblockCount; i++)
        {
            blockErrors[i] = subblockErrors[i] * InverseErrorNormalization;
        }

        Span<TSample> prediction = workspace.Prediction;
        Span<uint> accumulator = workspace.Accumulator;
        Span<ushort> count = workspace.Count;
        Span<uint> lumaErrors = workspace.LumaErrors;
        int shift = 2 * (bitDepth - 8);
        int planeOffset = 0;
        int planeCount = isMonochrome ? 1 : 3;
        for (int plane = 0; plane < planeCount; plane++)
        {
            GetPlaneBlock(
                frameToFilter,
                frameToFilterLuma,
                frameToFilterBlue,
                frameToFilterRed,
                (Av1Plane)plane,
                blockRow,
                blockColumn,
                out ReadOnlySpan<TSample> samples,
                out int stride,
                out int width,
                out int height);

            int subsamplingX = plane == 0 ? 0 : frameToFilter.ChromaSubsamplingX;
            int subsamplingY = plane == 0 ? 0 : frameToFilter.ChromaSubsamplingY;
            int referenceCount = 25 + (plane == 0 ? 0 : 1 << (subsamplingX + subsamplingY));
            double inverseReferenceCount = 1.0 / referenceCount;

            // A higher noise level gives larger weights.
            double noiseDecay = 0.5 + Math.Log((2 * noiseLevels[plane]) + 5.0);
            double decay = 1 / (noiseDecay * qDecay * strengthDecay);
            for (int i = 0; i < SubblockCount; i++)
            {
                firstFactors[i] = separateFactors ? distanceFactors[i] : distanceFactors[i] * decay;
            }

            // The chroma planes add the luma errors of the samples they cover, because the search uses only the luma motion.
            // The luma errors are summed before the first chroma plane overwrites the squared differences. The second chroma
            // plane uses the same sums again.
            if (plane == (int)Av1Plane.U)
            {
                SumLumaErrors<TSample, TOperator>(workspace.SquaredErrors, subsamplingX, subsamplingY, width, height, lumaErrors);
            }

            ReadOnlySpan<TSample> planePrediction = prediction.Slice(planeOffset, width * height);
            BuildSquaredErrors<TSample, TOperator>(samples, stride, planePrediction, width, height, workspace.SquaredErrors);
            BuildWindowErrors<TSample, TOperator>(
                workspace.SquaredErrors,
                plane == 0 ? workspace.ZeroLumaErrors : lumaErrors,
                width,
                height,
                shift,
                workspace.Columns,
                workspace.WindowErrors);

            AccumulateWeights<TSample, TOperator>(
                workspace.WindowErrors,
                planePrediction,
                accumulator.Slice(planeOffset, width * height),
                count.Slice(planeOffset, width * height),
                width,
                height,
                inverseReferenceCount,
                blockErrors,
                firstFactors,
                separateFactors ? decay : 1.0,
                level);

            planeOffset += width * height;
        }
    }

    /// <summary>
    /// Writes the filtered samples of one block of every plane.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The temporal filter sample arithmetic.</typeparam>
    /// <param name="output">The filtered frame.</param>
    /// <param name="outputLuma">The luma samples of <paramref name="output"/>, which the caller reads once outside its block loop.</param>
    /// <param name="outputBlue">The blue-difference samples of <paramref name="output"/>, which the caller reads once outside its block loop.</param>
    /// <param name="outputRed">The red-difference samples of <paramref name="output"/>, which the caller reads once outside its block loop.</param>
    /// <param name="blockRow">The block row.</param>
    /// <param name="blockColumn">The block column.</param>
    /// <param name="accumulator">The weighted sums of all planes.</param>
    /// <param name="count">The weight totals of all planes.</param>
    internal static void NormalizeBlock<TSample, TOperator>(
        Av1EncoderFrame<TSample> output,
        Span<TSample> outputLuma,
        Span<TSample> outputBlue,
        Span<TSample> outputRed,
        int blockRow,
        int blockColumn,
        ReadOnlySpan<uint> accumulator,
        ReadOnlySpan<ushort> count)
        where TSample : unmanaged
        where TOperator : struct, ITemporalFilterOperator<TSample>
    {
        int planeOffset = 0;
        int planeCount = output.IsMonochrome ? 1 : 3;
        for (int plane = 0; plane < planeCount; plane++)
        {
            int subsamplingX = plane == 0 ? 0 : output.ChromaSubsamplingX;
            int subsamplingY = plane == 0 ? 0 : output.ChromaSubsamplingY;
            int width = BlockSize >> subsamplingX;
            int height = BlockSize >> subsamplingY;
            Av1PlaneRegion<TSample> region = output.CodedView.GetPlane((Av1Plane)plane);
            int stride = region.Stride;
            int origin = ((region.Bounds.Y + (blockRow * height)) * stride) + region.Bounds.X + (blockColumn * width);
            Normalize<TSample, TOperator>(
                accumulator.Slice(planeOffset, width * height),
                count.Slice(planeOffset, width * height),
                width,
                height,
                ((Av1Plane)plane == Av1Plane.Y ? outputLuma : (Av1Plane)plane == Av1Plane.U ? outputBlue : outputRed)[origin..],
                stride);

            planeOffset += width * height;
        }
    }

    /// <summary>
    /// Returns the samples of one filter block of one plane.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <param name="frame">The frame.</param>
    /// <param name="frameLuma">The luma samples of <paramref name="frame"/>, which the caller reads once outside its block loop.</param>
    /// <param name="frameBlue">The blue-difference samples of <paramref name="frame"/>, which the caller reads once outside its block loop.</param>
    /// <param name="frameRed">The red-difference samples of <paramref name="frame"/>, which the caller reads once outside its block loop.</param>
    /// <param name="plane">The plane.</param>
    /// <param name="blockRow">The block row.</param>
    /// <param name="blockColumn">The block column.</param>
    /// <param name="samples">The plane samples from the block origin.</param>
    /// <param name="stride">The plane row stride.</param>
    /// <param name="width">The block width in the plane.</param>
    /// <param name="height">The block height in the plane.</param>
    private static void GetPlaneBlock<TSample>(
        Av1EncoderFrame<TSample> frame,
        ReadOnlySpan<TSample> frameLuma,
        ReadOnlySpan<TSample> frameBlue,
        ReadOnlySpan<TSample> frameRed,
        Av1Plane plane,
        int blockRow,
        int blockColumn,
        out ReadOnlySpan<TSample> samples,
        out int stride,
        out int width,
        out int height)
        where TSample : unmanaged
    {
        int subsamplingX = plane == Av1Plane.Y ? 0 : frame.ChromaSubsamplingX;
        int subsamplingY = plane == Av1Plane.Y ? 0 : frame.ChromaSubsamplingY;
        width = BlockSize >> subsamplingX;
        height = BlockSize >> subsamplingY;
        Av1PlaneRegion<TSample> region = frame.CodedView.GetPlane(plane);
        stride = region.Stride;
        int origin = ((region.Bounds.Y + (blockRow * height)) * stride) + region.Bounds.X + (blockColumn * width);
        samples = (plane == Av1Plane.Y ? frameLuma : plane == Av1Plane.U ? frameBlue : frameRed)[origin..];
    }

    /// <summary>
    /// Holds the per-frame values that the block filter reads, copied from the frame, the settings and the
    /// group-of-pictures state.
    /// </summary>
    internal readonly struct TemporalFilterContext
    {
        /// <summary>
        /// Gets the visible frame width.
        /// </summary>
        public int FrameWidth { get; init; }

        /// <summary>
        /// Gets the visible frame height.
        /// </summary>
        public int FrameHeight { get; init; }

        /// <summary>
        /// Gets the frame width aligned to eight samples.
        /// </summary>
        public int CodedWidth { get; init; }

        /// <summary>
        /// Gets the frame height aligned to eight samples.
        /// </summary>
        public int CodedHeight { get; init; }

        /// <summary>
        /// Gets the sample bit depth.
        /// </summary>
        public int BitDepth { get; init; }

        /// <summary>
        /// Gets the sample bit depth as the coded precision.
        /// </summary>
        public Av1BitDepth BitDepthKind { get; init; }

        /// <summary>
        /// Gets the encoder frame border.
        /// </summary>
        public int BorderInPixels { get; init; }

        /// <summary>
        /// Gets the sharpness.
        /// </summary>
        public int Sharpness { get; init; }

        /// <summary>
        /// Gets a value indicating whether motion is restricted to whole samples.
        /// </summary>
        public bool ForceIntegerMotion { get; init; }

        /// <summary>
        /// Gets a value indicating whether eighth-sample motion is allowed.
        /// </summary>
        public bool AllowHighPrecisionMotion { get; init; }

        /// <summary>
        /// Gets a value indicating whether the frame the encoder is positioned at is a key frame.
        /// </summary>
        public bool CurrentFrameIsKeyFrame { get; init; }

        /// <summary>
        /// Gets a value indicating whether the frame the encoder is positioned at has the key frame update type.
        /// </summary>
        public bool CurrentFrameIsKeyFrameUpdate { get; init; }

        /// <summary>
        /// Gets the quantizer factor.
        /// </summary>
        public int QFactor { get; init; }

        /// <summary>
        /// Gets the number of samples of all planes of one filter block.
        /// </summary>
        public int BlockPixels { get; init; }

        /// <summary>
        /// Returns the per-frame filter values.
        /// </summary>
        /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
        /// <param name="frame">The frame to filter.</param>
        /// <param name="settings">The filter settings.</param>
        /// <param name="parameters">The group-of-pictures and rate-control state of the filtered frame.</param>
        /// <param name="qFactor">The quantizer factor.</param>
        /// <returns>The filter context.</returns>
        public static TemporalFilterContext Create<TSample>(
            Av1EncoderFrame<TSample> frame,
            in Av1TemporalFilterSettings settings,
            in Av1TemporalFilterFrameParameters parameters,
            int qFactor)
            where TSample : unmanaged
        {
            int chromaPixels = frame.IsMonochrome ? 0 : 2 * ((BlockSize * BlockSize) >> (frame.ChromaSubsamplingX + frame.ChromaSubsamplingY));
            return new TemporalFilterContext
            {
                FrameWidth = frame.Width,
                FrameHeight = frame.Height,
                CodedWidth = frame.CodedWidth,
                CodedHeight = frame.CodedHeight,
                BitDepth = frame.LumaBitDepth,
                BitDepthKind = (Av1BitDepth)((frame.LumaBitDepth - 8) >> 1),
                BorderInPixels = parameters.BorderInPixels,
                Sharpness = settings.Sharpness,
                ForceIntegerMotion = parameters.ForceIntegerMotion,
                AllowHighPrecisionMotion = parameters.AllowHighPrecisionMotion,
                CurrentFrameIsKeyFrame = parameters.CurrentFrameIsKeyFrame,
                CurrentFrameIsKeyFrameUpdate = parameters.CurrentFrameIsKeyFrameUpdate,
                QFactor = qFactor,
                BlockPixels = (BlockSize * BlockSize) + chromaPixels
            };
        }
    }
}
