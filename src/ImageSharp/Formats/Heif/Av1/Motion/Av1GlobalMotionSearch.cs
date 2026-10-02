// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Turns a fitted warp model into the model the bitstream codes, and refines it against the frame.
/// </summary>
/// <remarks>
/// <para>
/// The fit produces real numbers. The bitstream codes each parameter at a fixed precision, so the
/// model has to be rounded before it can be used, and the rounded model is not always the best one
/// at that precision. Each parameter is therefore stepped up and down, and the step that lowers the
/// prediction error is kept.
/// </para>
/// <para>
/// Reference: av1_convert_model_to_params(), av1_refine_integerized_param(), get_warp_error(),
/// av1_segmented_frame_error(), av1_compute_feature_segmentation_map() and
/// av1_is_enough_erroradvantage().
/// </para>
/// </remarks>
internal static partial class Av1GlobalMotionSearch
{
    /// <summary>
    /// The bits by which the error map is smaller than the frame in each direction.
    /// </summary>
    /// <remarks>Reference: WARP_ERROR_BLOCK_LOG.</remarks>
    public const int ErrorBlockLog = 5;

    /// <summary>
    /// The side of the square over which the error is measured, in samples.
    /// </summary>
    /// <remarks>Reference: WARP_ERROR_BLOCK.</remarks>
    public const int ErrorBlock = 1 << ErrorBlockLog;

    /// <summary>
    /// The agreeing points one error block needs before the block is measured.
    /// </summary>
    /// <remarks>Reference: FEAT_COUNT_TR.</remarks>
    private const int FeatureCountThreshold = 3;

    /// <summary>
    /// The error blocks a model needs before the error is measured over those blocks alone.
    /// </summary>
    /// <remarks>Reference: SEG_COUNT_TR.</remarks>
    private const int SegmentCountThreshold = 48;

    /// <summary>
    /// The largest product of the error share and the coding cost that is still worth coding.
    /// </summary>
    /// <remarks>Reference: erroradv_prod_tr.</remarks>
    private const double ErrorAdvantageProductThreshold = 20000;

    /// <summary>
    /// The share of the unwarped error above which refinement gives up at once.
    /// </summary>
    /// <remarks>
    /// This is looser than the threshold a model must finally meet, because refinement can still
    /// bring a model that starts slightly above it under the final threshold.
    /// Reference: erroradv_early_tr.
    /// </remarks>
    private const double EarlyErrorAdvantageThreshold = 0.70;

    /// <summary>
    /// Defines the sample-specific parts of the warped error measure.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    internal interface IAv1GlobalMotionOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Warps one square of the reference frame with the model.
        /// </summary>
        /// <param name="source">The whole reference plane.</param>
        /// <param name="sourceStride">The row stride of the reference plane.</param>
        /// <param name="sourceWidth">The coded width of the reference plane.</param>
        /// <param name="sourceHeight">The coded height of the reference plane.</param>
        /// <param name="destination">The square that receives the warped samples.</param>
        /// <param name="destinationStride">The row stride of the destination.</param>
        /// <param name="position">The position of the square in the frame.</param>
        /// <param name="width">The columns to write.</param>
        /// <param name="height">The rows to write.</param>
        /// <param name="bitDepth">The coded sample depth, which only the high-bit-depth filter reads.</param>
        /// <param name="parameters">The warp model.</param>
        /// <param name="scratch">The intermediate storage of the two filter passes.</param>
        public static abstract void PredictWarped(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            int sourceWidth,
            int sourceHeight,
            Span<TSample> destination,
            int destinationStride,
            Point position,
            int width,
            int height,
            int bitDepth,
            Av1GlobalMotionParameters parameters,
            Span<short> scratch);

        /// <summary>
        /// Measures the absolute error between two equally sized regions.
        /// </summary>
        /// <param name="source">The first sample of the source region.</param>
        /// <param name="sourceStride">The row stride of the source region.</param>
        /// <param name="prediction">The first sample of the prediction region.</param>
        /// <param name="predictionStride">The row stride of the prediction region.</param>
        /// <param name="width">The columns compared in each row.</param>
        /// <param name="height">The rows compared.</param>
        /// <returns>The sum of absolute sample differences.</returns>
        public static abstract int SumAbsoluteDifferences(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            int width,
            int height);
    }

    /// <summary>
    /// Gets the parameters that each model family leaves free, in family order.
    /// </summary>
    /// <remarks>Reference: max_trans_model_params.</remarks>
    private static ReadOnlySpan<int> ParameterCounts => [0, 2, 4, 6];

    /// <summary>
    /// Rounds a fitted model to the precision the bitstream codes.
    /// </summary>
    /// <param name="model">The six fitted parameters.</param>
    /// <returns>The rounded model, with its type derived from the rounded values.</returns>
    /// <remarks>
    /// Each parameter is rounded at its own coded precision, clamped to the range the syntax allows,
    /// and then scaled back up to the precision the warp filter works at. The two diagonal entries
    /// are centered on one before they are clamped, because the syntax codes their distance from
    /// one rather than the value itself. Reference: convert_to_params().
    /// </remarks>
    public static Av1GlobalMotionParameters ConvertModelToParameters(ReadOnlySpan<double> model)
    {
        const int TranslationScale = 1 << Av1GlobalMotionParameters.TranslationPrecisionBits;
        const int TranslationMaximum = 1 << Av1GlobalMotionParameters.AbsoluteTranslationBits;
        const int TranslationDecodeFactor =
            1 << (Av1GlobalMotionParameters.ModelPrecisionBits - Av1GlobalMotionParameters.TranslationPrecisionBits);

        const int AlphaScale = 1 << Av1GlobalMotionParameters.AlphaPrecisionBits;
        const int AlphaMaximum = Av1GlobalMotionParameters.AlphaValueMagnitude - 1;

        Av1GlobalMotionParameters parameters = default;
        for (int i = 0; i < 2; i++)
        {
            int value = (int)Math.Floor((model[i] * TranslationScale) + 0.5);
            parameters[i] = Math.Clamp(value, -TranslationMaximum, TranslationMaximum) * TranslationDecodeFactor;
        }

        for (int i = 2; i < 6; i++)
        {
            int oneCentered = (i == 2 || i == 5) ? AlphaScale : 0;
            int value = (int)Math.Floor((model[i] * AlphaScale) + 0.5);
            value = Math.Clamp(value - oneCentered, -AlphaMaximum, AlphaMaximum);
            parameters[i] = (value + oneCentered) * Av1GlobalMotionParameters.AlphaDecodeFactor;
        }

        parameters.Type = GetModelType(parameters);
        return parameters;
    }

    /// <summary>
    /// Names the simplest model family that describes one set of parameters.
    /// </summary>
    /// <param name="parameters">The model to classify.</param>
    /// <returns>The family.</returns>
    /// <remarks>Reference: get_wmtype().</remarks>
    public static Av1GlobalMotionType GetModelType(Av1GlobalMotionParameters parameters)
    {
        if (parameters[5] == Av1GlobalMotionParameters.ModelScale && parameters[4] == 0 &&
            parameters[2] == Av1GlobalMotionParameters.ModelScale && parameters[3] == 0)
        {
            return parameters[1] == 0 && parameters[0] == 0
                ? Av1GlobalMotionType.Identity
                : Av1GlobalMotionType.Translation;
        }

        return parameters[2] == parameters[5] && parameters[3] == -parameters[4]
            ? Av1GlobalMotionType.RotationZoom
            : Av1GlobalMotionType.Affine;
    }

    /// <summary>
    /// Forces the parameters that one model family holds fixed.
    /// </summary>
    /// <param name="parameters">The model to constrain.</param>
    /// <param name="type">The family to constrain it to.</param>
    /// <remarks>
    /// A rotation with a zoom has only two free matrix parameters, so the other two are derived from
    /// them after every change. Reference: force_wmtype().
    /// </remarks>
    public static void ForceModelType(ref Av1GlobalMotionParameters parameters, Av1GlobalMotionType type)
    {
        if (type <= Av1GlobalMotionType.Identity)
        {
            parameters[0] = 0;
            parameters[1] = 0;
        }

        if (type <= Av1GlobalMotionType.Translation)
        {
            parameters[2] = Av1GlobalMotionParameters.ModelScale;
            parameters[3] = 0;
        }

        if (type <= Av1GlobalMotionType.RotationZoom)
        {
            parameters[4] = -parameters[3];
            parameters[5] = parameters[2];
        }

        parameters.Type = type;
    }

    /// <summary>
    /// Marks the error blocks that hold enough agreeing points to be worth measuring.
    /// </summary>
    /// <param name="map">The one byte per error block that receives the marks.</param>
    /// <param name="width">The error blocks across the frame.</param>
    /// <param name="height">The error blocks down the frame.</param>
    /// <param name="inliers">The column and row of each agreeing point, interleaved.</param>
    /// <remarks>
    /// Measuring only where the model was fitted keeps parts of the frame that move on their own out
    /// of the comparison. When too little of the frame is marked, the whole frame is measured
    /// instead, because a mark that small says nothing about the frame.
    /// Reference: av1_compute_feature_segmentation_map().
    /// </remarks>
    public static void ComputeFeatureSegmentationMap(Span<byte> map, int width, int height, ReadOnlySpan<int> inliers)
    {
        map[..(width * height)].Clear();
        for (int i = 0; i < inliers.Length / 2; i++)
        {
            int blockX = inliers[2 * i] >> ErrorBlockLog;
            int blockY = inliers[(2 * i) + 1] >> ErrorBlockLog;
            map[(blockY * width) + blockX]++;
        }

        int marked = 0;
        for (int i = 0; i < width * height; i++)
        {
            map[i] = map[i] >= FeatureCountThreshold ? (byte)1 : (byte)0;
            marked += map[i];
        }

        if (marked < SegmentCountThreshold)
        {
            map[..(width * height)].Fill(1);
        }
    }

    /// <summary>
    /// Gets the largest share of the unwarped error that a model may leave. Reference: erroradv_tr.
    /// </summary>
    /// <param name="level">The threshold level of the speed settings. Reference: gm_erroradv_tr_level.</param>
    /// <returns>The threshold.</returns>
    public static double GetErrorAdvantageThreshold(int level) => level switch
    {
        0 => 0.65,
        1 => 0.3,
        _ => 0.2,
    };

    /// <summary>
    /// Gets whether the error a model saves is worth what the model costs to code.
    /// </summary>
    /// <param name="errorAdvantage">The warped error as a share of the unwarped error.</param>
    /// <param name="parametersCost">The cost of coding the model.</param>
    /// <param name="threshold">The largest share of the unwarped error that a model may leave.</param>
    /// <returns>Whether the model is worth coding.</returns>
    /// <remarks>Reference: av1_is_enough_erroradvantage().</remarks>
    public static bool IsEnoughErrorAdvantage(double errorAdvantage, int parametersCost, double threshold)
        => errorAdvantage < threshold &&
           errorAdvantage * parametersCost < ErrorAdvantageProductThreshold;

    /// <summary>
    /// Measures the error of one frame pair over the marked blocks, with no model applied.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    /// <typeparam name="TOperator">The sample-specific measures.</typeparam>
    /// <param name="reference">The reference plane.</param>
    /// <param name="referenceStride">The row stride of the reference plane.</param>
    /// <param name="source">The source plane.</param>
    /// <param name="sourceStride">The row stride of the source plane.</param>
    /// <param name="width">The coded width of both planes.</param>
    /// <param name="height">The coded height of both planes.</param>
    /// <param name="map">The marked error blocks.</param>
    /// <param name="mapStride">The error blocks across the frame.</param>
    /// <returns>The sum of absolute sample differences over the marked blocks.</returns>
    /// <remarks>Reference: segmented_frame_error().</remarks>
    public static long GetSegmentedFrameError<TSample, TOperator>(
        ReadOnlySpan<TSample> reference,
        int referenceStride,
        ReadOnlySpan<TSample> source,
        int sourceStride,
        int width,
        int height,
        ReadOnlySpan<byte> map,
        int mapStride)
        where TSample : unmanaged
        where TOperator : struct, IAv1GlobalMotionOperator<TSample>
    {
        long total = 0;
        for (int row = 0; row < height; row += ErrorBlock)
        {
            for (int column = 0; column < width; column += ErrorBlock)
            {
                if (map[((row >> ErrorBlockLog) * mapStride) + (column >> ErrorBlockLog)] == 0)
                {
                    continue;
                }

                // A block at the right or bottom edge is measured only as far as the coded frame
                // reaches, so no padding enters the total.
                int blockWidth = Math.Min(ErrorBlock, width - column);
                int blockHeight = Math.Min(ErrorBlock, height - row);
                total += TOperator.SumAbsoluteDifferences(
                    reference[((row * referenceStride) + column)..],
                    referenceStride,
                    source[((row * sourceStride) + column)..],
                    sourceStride,
                    blockWidth,
                    blockHeight);
            }
        }

        return total;
    }

    /// <summary>
    /// Measures the error of one frame pair over the marked blocks, with the model applied.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    /// <typeparam name="TOperator">The sample-specific measures.</typeparam>
    /// <param name="parameters">The warp model, whose shear parameters this updates.</param>
    /// <param name="reference">The whole reference plane.</param>
    /// <param name="referenceStride">The row stride of the reference plane.</param>
    /// <param name="source">The source plane.</param>
    /// <param name="sourceStride">The row stride of the source plane.</param>
    /// <param name="width">The coded width of both planes.</param>
    /// <param name="height">The coded height of both planes.</param>
    /// <param name="map">The marked error blocks.</param>
    /// <param name="mapStride">The error blocks across the frame.</param>
    /// <param name="bestError">The total above which measuring stops.</param>
    /// <param name="bitDepth">The coded sample depth.</param>
    /// <param name="warped">The scratch that holds one warped block.</param>
    /// <param name="scratch">The intermediate storage of the warp filter.</param>
    /// <returns>The error, or <see cref="long.MaxValue"/> when the model cannot be used or is worse.</returns>
    /// <remarks>
    /// Measuring stops as soon as the total passes the best total so far, because a model that is
    /// already worse cannot become better over the blocks that remain.
    /// Reference: get_warp_error() and warp_error().
    /// </remarks>
    public static long GetWarpError<TSample, TOperator>(
        ref Av1GlobalMotionParameters parameters,
        ReadOnlySpan<TSample> reference,
        int referenceStride,
        ReadOnlySpan<TSample> source,
        int sourceStride,
        int width,
        int height,
        ReadOnlySpan<byte> map,
        int mapStride,
        long bestError,
        int bitDepth,
        Span<TSample> warped,
        Span<short> scratch)
        where TSample : unmanaged
        where TOperator : struct, IAv1GlobalMotionOperator<TSample>
    {
        parameters.UpdateShearParameters();
        if (parameters.IsInvalid)
        {
            return long.MaxValue;
        }

        long total = 0;
        for (int row = 0; row < height; row += ErrorBlock)
        {
            for (int column = 0; column < width; column += ErrorBlock)
            {
                if (map[((row >> ErrorBlockLog) * mapStride) + (column >> ErrorBlockLog)] == 0)
                {
                    continue;
                }

                int blockWidth = Math.Min(ErrorBlock, width - column);
                int blockHeight = Math.Min(ErrorBlock, height - row);
                TOperator.PredictWarped(
                    reference,
                    referenceStride,
                    width,
                    height,
                    warped,
                    ErrorBlock,
                    new Point(column, row),
                    blockWidth,
                    blockHeight,
                    bitDepth,
                    parameters,
                    scratch);

                total += TOperator.SumAbsoluteDifferences(
                    warped,
                    ErrorBlock,
                    source[((row * sourceStride) + column)..],
                    sourceStride,
                    blockWidth,
                    blockHeight);

                if (total > bestError)
                {
                    return long.MaxValue;
                }
            }
        }

        return total;
    }

    /// <summary>
    /// Steps each free parameter of a model until the error stops falling.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    /// <typeparam name="TOperator">The sample-specific measures.</typeparam>
    /// <param name="allocator">The allocator of the warp scratch.</param>
    /// <param name="parameters">The model to refine, in place.</param>
    /// <param name="type">The family that the model must keep.</param>
    /// <param name="reference">The whole reference plane.</param>
    /// <param name="referenceStride">The row stride of the reference plane.</param>
    /// <param name="source">The source plane.</param>
    /// <param name="sourceStride">The row stride of the source plane.</param>
    /// <param name="width">The coded width of both planes.</param>
    /// <param name="height">The coded height of both planes.</param>
    /// <param name="refinementCount">The step sizes tried, each half of the one before.</param>
    /// <param name="bitDepth">The coded sample depth.</param>
    /// <param name="referenceError">The error the unwarped frames already have.</param>
    /// <param name="map">The marked error blocks.</param>
    /// <param name="mapStride">The error blocks across the frame.</param>
    /// <param name="errorAdvantageThreshold">The largest share of the unwarped error that a model may leave.</param>
    /// <returns>The error of the refined model, or <see cref="long.MaxValue"/> when it is not usable.</returns>
    /// <remarks>Reference: av1_refine_integerized_param().</remarks>
    public static long RefineIntegerizedParameters<TSample, TOperator>(
        MemoryAllocator allocator,
        ref Av1GlobalMotionParameters parameters,
        Av1GlobalMotionType type,
        ReadOnlySpan<TSample> reference,
        int referenceStride,
        ReadOnlySpan<TSample> source,
        int sourceStride,
        int width,
        int height,
        int refinementCount,
        int bitDepth,
        long referenceError,
        ReadOnlySpan<byte> map,
        int mapStride,
        double errorAdvantageThreshold)
        where TSample : unmanaged
        where TOperator : struct, IAv1GlobalMotionOperator<TSample>
    {
        int parameterCount = ParameterCounts[(int)type];
        ForceModelType(ref parameters, type);
        parameters.Type = GetModelType(parameters);

        using IMemoryOwner<TSample> warpedOwner = allocator.Allocate<TSample>(ErrorBlock * ErrorBlock);
        using IMemoryOwner<short> scratchOwner = allocator.Allocate<short>(Av1WarpedInterPredictor.WarpedScratchLength);
        Span<TSample> warped = warpedOwner.Memory.Span;
        Span<short> scratch = scratchOwner.Memory.Span;

        if (refinementCount == 0)
        {
            // The error of the model itself, measured only as far as the final threshold needs, so that the
            // measure can stop once it proves that the model is not taken.
            long selectionThreshold = (long)Math.Round(referenceError * errorAdvantageThreshold, MidpointRounding.ToEven);
            return GetWarpError<TSample, TOperator>(
                ref parameters, reference, referenceStride, source, sourceStride, width, height, map, mapStride, selectionThreshold, bitDepth, warped, scratch);
        }

        long threshold = (long)Math.Round(referenceError * EarlyErrorAdvantageThreshold, MidpointRounding.ToEven);
        long bestError = GetWarpError<TSample, TOperator>(
            ref parameters, reference, referenceStride, source, sourceStride, width, height, map, mapStride, threshold, bitDepth, warped, scratch);

        if (bestError > threshold)
        {
            return long.MaxValue;
        }

        int step = 1 << (refinementCount - 1);
        for (int refinement = 0; refinement < refinementCount; refinement++, step >>= 1)
        {
            for (int index = 0; index < parameterCount; index++)
            {
                int direction = 0;
                int current = parameters[index];
                int best = current;

                // Both directions are tried from the value the parameter came in with, so the search
                // cannot be trapped by the first direction it tries.
                for (int trial = -1; trial <= 1; trial += 2)
                {
                    parameters[index] = AddParameterOffset(index, current, step * trial);
                    ForceModelType(ref parameters, type);
                    long stepError = GetWarpError<TSample, TOperator>(
                        ref parameters, reference, referenceStride, source, sourceStride, width, height, map, mapStride, bestError, bitDepth, warped, scratch);

                    if (stepError < bestError)
                    {
                        bestError = stepError;
                        best = parameters[index];
                        direction = trial;
                    }
                }

                // A direction that helped once usually helps again, so it is followed until it stops.
                while (direction != 0)
                {
                    parameters[index] = AddParameterOffset(index, best, step * direction);
                    ForceModelType(ref parameters, type);
                    long stepError = GetWarpError<TSample, TOperator>(
                        ref parameters, reference, referenceStride, source, sourceStride, width, height, map, mapStride, bestError, bitDepth, warped, scratch);

                    if (stepError >= bestError)
                    {
                        break;
                    }

                    bestError = stepError;
                    best = parameters[index];
                }

                parameters[index] = best;
                ForceModelType(ref parameters, type);
            }
        }

        parameters.Type = GetModelType(parameters);
        return bestError;
    }

    /// <summary>
    /// Moves one model parameter by a step taken at its own coded precision.
    /// </summary>
    /// <param name="index">The parameter to move.</param>
    /// <param name="value">The current value, at warp precision.</param>
    /// <param name="offset">The step, at coded precision.</param>
    /// <returns>The moved value, at warp precision.</returns>
    /// <remarks>
    /// The value is first brought down to the precision the syntax codes and, for the two diagonal
    /// entries, centered on zero, because the syntax codes their distance from one. The step is then
    /// added, the result clamped to the range the syntax allows, and the value returned to warp
    /// precision. Reference: add_param_offset().
    /// </remarks>
    public static int AddParameterOffset(int index, int value, int offset)
    {
        bool isTranslation = index < 2;
        int precisionDifference = isTranslation
            ? Av1GlobalMotionParameters.ModelPrecisionBits - Av1GlobalMotionParameters.TranslationPrecisionBits
            : Av1GlobalMotionParameters.AlphaPrecisionDifference;

        int limit = isTranslation
            ? 1 << Av1GlobalMotionParameters.AbsoluteTranslationBits
            : Av1GlobalMotionParameters.AlphaValueMagnitude - 1;

        int oneCentered = (index == 2 || index == 5) ? 1 << Av1GlobalMotionParameters.ModelPrecisionBits : 0;
        int centered = (value - oneCentered) >> precisionDifference;
        centered = Math.Clamp(centered + offset, -limit, limit);
        return (centered << precisionDifference) + oneCentered;
    }

    /// <summary>
    /// Measures one warped block of 8-bit samples.
    /// </summary>
    internal readonly struct ByteOperator : IAv1GlobalMotionOperator<byte>
    {
        /// <inheritdoc/>
        public static void PredictWarped(
            ReadOnlySpan<byte> source,
            int sourceStride,
            int sourceWidth,
            int sourceHeight,
            Span<byte> destination,
            int destinationStride,
            Point position,
            int width,
            int height,
            int bitDepth,
            Av1GlobalMotionParameters parameters,
            Span<short> scratch)
            => Av1WarpedInterPredictor.PredictWarped(
                source,
                sourceStride,
                Point.Empty,
                sourceWidth,
                sourceHeight,
                destination,
                destinationStride,
                position,
                width,
                height,
                0,
                0,
                parameters,
                scratch);

        /// <inheritdoc/>
        public static int SumAbsoluteDifferences(
            ReadOnlySpan<byte> source, int sourceStride, ReadOnlySpan<byte> prediction, int predictionStride, int width, int height)
            => Av1ResidualBuilder.SumAbsoluteDifferences(source, sourceStride, prediction, predictionStride, width, height, 1);
    }

    /// <summary>
    /// Measures one warped block of high-bit-depth samples.
    /// </summary>
    internal readonly struct UInt16Operator : IAv1GlobalMotionOperator<ushort>
    {
        /// <inheritdoc/>
        public static void PredictWarped(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            int sourceWidth,
            int sourceHeight,
            Span<ushort> destination,
            int destinationStride,
            Point position,
            int width,
            int height,
            int bitDepth,
            Av1GlobalMotionParameters parameters,
            Span<short> scratch)
            => Av1WarpedInterPredictor.PredictWarped(
                source,
                sourceStride,
                Point.Empty,
                sourceWidth,
                sourceHeight,
                destination,
                destinationStride,
                position,
                width,
                height,
                0,
                0,
                bitDepth,
                parameters,
                scratch);

        /// <inheritdoc/>
        public static int SumAbsoluteDifferences(
            ReadOnlySpan<ushort> source, int sourceStride, ReadOnlySpan<ushort> prediction, int predictionStride, int width, int height)
            => Av1ResidualBuilder.SumAbsoluteDifferences(source, sourceStride, prediction, predictionStride, width, height, 1);
    }
}
