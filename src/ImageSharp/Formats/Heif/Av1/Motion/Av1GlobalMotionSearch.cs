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
/// The fit produces real numbers. The bitstream codes each parameter at a fixed precision, so the encoder rounds the model before use. The rounded
/// model is not always the best model at that precision. Thus the search steps each parameter up and down and keeps the step that lowers the
/// prediction error.
/// </para>
/// </remarks>
internal static partial class Av1GlobalMotionSearch
{
    /// <summary>
    /// The base-two logarithm of the side of one error block. The error map is smaller than the frame by this many bits in each direction.
    /// </summary>
    public const int ErrorBlockLog = 5;

    /// <summary>
    /// The side of the square over which the search measures the error, in samples.
    /// </summary>
    public const int ErrorBlock = 1 << ErrorBlockLog;

    /// <summary>
    /// The number of agreeing points that one error block needs before the search measures it.
    /// </summary>
    private const int FeatureCountThreshold = 3;

    /// <summary>
    /// The number of marked error blocks that a model needs before the search measures the error over those blocks alone.
    /// </summary>
    private const int SegmentCountThreshold = 48;

    /// <summary>
    /// The largest product of the error share and the coding cost that is still worth coding.
    /// </summary>
    private const double ErrorAdvantageProductThreshold = 20000;

    /// <summary>
    /// The share of the unwarped error above which refinement stops at once.
    /// </summary>
    /// <remarks>
    /// This threshold is looser than the final threshold of a model. Refinement can still bring a model that starts slightly above the final
    /// threshold below it.
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
        /// <param name="width">The number of columns to write.</param>
        /// <param name="height">The number of rows to write.</param>
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
        /// <param name="width">The number of columns compared in each row.</param>
        /// <param name="height">The number of rows compared.</param>
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
    /// Gets the number of parameters that each model family leaves free, in family order.
    /// </summary>
    private static ReadOnlySpan<int> ParameterCounts => [0, 2, 4, 6];

    /// <summary>
    /// Rounds a fitted model to the precision the bitstream codes.
    /// </summary>
    /// <param name="model">The six fitted parameters.</param>
    /// <returns>The rounded model, with its type derived from the rounded values.</returns>
    /// <remarks>
    /// The method rounds each parameter at its own coded precision and clamps it to the range that the syntax allows. Then it scales the parameter
    /// back up to the precision of the warp filter. The method centers the two diagonal entries on one before the clamp, because the syntax codes
    /// their distance from one, not the value itself.
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
    /// A rotation with a zoom has only two free matrix parameters. Thus the method derives the other two from them after every change.
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
    /// <param name="width">The number of error blocks across the frame.</param>
    /// <param name="height">The number of error blocks down the frame.</param>
    /// <param name="inliers">The column and row of each agreeing point, interleaved.</param>
    /// <remarks>
    /// The measure covers only the area where the model was fitted. Thus parts of the frame that move on their own stay out of the comparison.
    /// When the marked area is too small, the method marks the whole frame, because a small marked area tells nothing about the frame.
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
    /// Gets the largest share of the unwarped error that a model can leave.
    /// </summary>
    /// <param name="level">The threshold level of the speed settings.</param>
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
    /// <param name="threshold">The largest share of the unwarped error that a model can leave.</param>
    /// <returns>Whether the model is worth coding.</returns>
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
    /// <param name="mapStride">The number of error blocks across the frame.</param>
    /// <returns>The sum of absolute sample differences over the marked blocks.</returns>
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

                // The code measures a block at the right or bottom edge only up to the edge of the coded frame. Thus no padding gets into the total.
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
    /// <param name="parameters">The warp model, whose shear parameters this method updates.</param>
    /// <param name="reference">The whole reference plane.</param>
    /// <param name="referenceStride">The row stride of the reference plane.</param>
    /// <param name="source">The source plane.</param>
    /// <param name="sourceStride">The row stride of the source plane.</param>
    /// <param name="width">The coded width of both planes.</param>
    /// <param name="height">The coded height of both planes.</param>
    /// <param name="map">The marked error blocks.</param>
    /// <param name="mapStride">The number of error blocks across the frame.</param>
    /// <param name="bestError">The total above which measuring stops.</param>
    /// <param name="bitDepth">The coded sample depth.</param>
    /// <param name="warped">The buffer that holds one warped block.</param>
    /// <param name="scratch">The intermediate storage of the warp filter.</param>
    /// <returns>The error, or <see cref="long.MaxValue"/> when the model cannot be used or is worse.</returns>
    /// <remarks>
    /// The measure stops when the total passes <paramref name="bestError"/>. A model that is already worse cannot become better over the remaining blocks.
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
    /// <param name="allocator">The allocator of the warp buffers.</param>
    /// <param name="parameters">The model to refine, in place.</param>
    /// <param name="type">The family that the model must keep.</param>
    /// <param name="reference">The whole reference plane.</param>
    /// <param name="referenceStride">The row stride of the reference plane.</param>
    /// <param name="source">The source plane.</param>
    /// <param name="sourceStride">The row stride of the source plane.</param>
    /// <param name="width">The coded width of both planes.</param>
    /// <param name="height">The coded height of both planes.</param>
    /// <param name="refinementCount">The number of step sizes to try. Each step size is half of the one before.</param>
    /// <param name="bitDepth">The coded sample depth.</param>
    /// <param name="referenceError">The error the unwarped frames already have.</param>
    /// <param name="map">The marked error blocks.</param>
    /// <param name="mapStride">The number of error blocks across the frame.</param>
    /// <param name="errorAdvantageThreshold">The largest share of the unwarped error that a model can leave.</param>
    /// <returns>The error of the refined model, or <see cref="long.MaxValue"/> when it is not usable.</returns>
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
            // Without refinement, the method measures the error of the model itself. The final threshold is the limit, so the measure stops when
            // it proves that the model fails.
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

                // Both directions start from the input value of the parameter. Thus the first direction cannot trap the search.
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

                // A direction that helped once usually helps again, so the search continues in it until the error stops falling.
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
    /// <param name="index">The index of the parameter to move.</param>
    /// <param name="value">The current value, at warp precision.</param>
    /// <param name="offset">The step, at coded precision.</param>
    /// <returns>The moved value, at warp precision.</returns>
    /// <remarks>
    /// The method first reduces the value to the coded precision. It centers the two diagonal entries on zero, because the syntax codes their
    /// distance from one. Then it adds the step, clamps the result to the range that the syntax allows, and returns the value to warp precision.
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
