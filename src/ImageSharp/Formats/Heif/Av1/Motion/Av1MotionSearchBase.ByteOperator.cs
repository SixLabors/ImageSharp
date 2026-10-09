// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

internal static partial class Av1MotionSearchBase
{
    /// <summary>
    /// Measures eight-bit sample errors with the shared vector-width residual traversal.
    /// </summary>
    public readonly struct ByteOperator : IMotionSearchOperator<byte>
    {
        /// <inheritdoc/>
        public static void BuildPrediction(
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int referenceOrigin,
            Span<byte> prediction,
            Span<short> intermediateRows,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth)
            => Av1TranslationalInterPredictor.Predict(
                reference,
                referenceStride,
                referenceOrigin,
                prediction,
                width,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                intermediateRows);

        /// <inheritdoc/>
        public static void PreparePrediction(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int referenceOrigin,
            Span<byte> prediction,
            Span<short> residual,
            Span<short> intermediateRows,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth)
        {
            BuildPrediction(
                reference,
                referenceStride,
                referenceOrigin,
                prediction,
                intermediateRows,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                bitDepth);

            Av1ResidualBuilder.Subtract(source, sourceStride, prediction, width, residual, width, width, height);
        }

        /// <inheritdoc/>
        public static void Predict(
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int referenceOrigin,
            Span<byte> buffer,
            int width,
            int height,
            int horizontalPhase,
            int verticalPhase,
            int taps,
            int bitDepth)
            => Av1TranslationalInterPredictor.PredictForSearch(
                reference, referenceStride, referenceOrigin, buffer, width, height, horizontalPhase, verticalPhase, taps);

        /// <inheritdoc/>
        public static void Subtract(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            Span<short> residual,
            int width,
            int height)
            => Av1ResidualBuilder.Subtract(source, sourceStride, prediction, width, residual, width, width, height);

        /// <inheritdoc/>
        public static void PredictScaled(
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int referenceOrigin,
            Span<byte> buffer,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int horizontalStep,
            int verticalPhase,
            int verticalStep,
            Span<short> intermediateRows,
            int bitDepth)
            => Av1ScaledInterPredictor.PredictScaled(
                reference,
                referenceStride,
                referenceOrigin,
                buffer,
                width,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                horizontalStep,
                verticalPhase,
                verticalStep,
                intermediateRows);

        /// <inheritdoc/>
        public static int SumAbsoluteDifferences(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            int width,
            int height,
            int rowStep)
            => Av1ResidualBuilder.SumAbsoluteDifferences(source, sourceStride, prediction, predictionStride, width, height, rowStep);

        /// <inheritdoc/>
        public static void GetMoments(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            int width,
            int height,
            out int sum,
            out long squares)
            => Av1ResidualBuilder.GetMoments(source, sourceStride, prediction, predictionStride, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static int SumCompoundAbsoluteDifferences(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            ReadOnlySpan<byte> secondPrediction,
            ReadOnlySpan<byte> mask,
            int width,
            int height,
            int rowStep)
            => Av1ResidualBuilder.SumCompoundAbsoluteDifferences(
                source, sourceStride, prediction, predictionStride, secondPrediction, mask, width, height, rowStep);

        /// <inheritdoc/>
        public static void GetCompoundMoments(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            ReadOnlySpan<byte> secondPrediction,
            ReadOnlySpan<byte> mask,
            int width,
            int height,
            out int sum,
            out long squares)
            => Av1ResidualBuilder.GetCompoundMoments(
                source, sourceStride, prediction, predictionStride, secondPrediction, mask, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static int SumObmcAbsoluteDifferences(
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height)
            => Av1ObmcSearch.SumAbsoluteDifferences<byte, Av1ObmcSearch.ByteOperator>(prediction, predictionStride, weightedSource, mask, width, height);

        /// <inheritdoc/>
        public static void GetObmcMoments(
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height,
            out int sum,
            out ulong squares)
            => Av1ObmcSearch.GetMoments<byte, Av1ObmcSearch.ByteOperator>(prediction, predictionStride, weightedSource, mask, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static void WeightObmcAbove(ReadOnlySpan<byte> prediction, int weight, Span<int> weightedSource, Span<int> mask, int width)
            => Av1ObmcSearch.WeightAbove<byte, Av1ObmcSearch.ByteOperator>(prediction, weight, weightedSource, mask, width);

        /// <inheritdoc/>
        public static void WeightObmcLeft(ReadOnlySpan<byte> prediction, ReadOnlySpan<byte> weights, Span<int> weightedSource, Span<int> mask)
            => Av1ObmcSearch.WeightLeft<byte, Av1ObmcSearch.ByteOperator>(prediction, weights, weightedSource, mask);

        /// <inheritdoc/>
        public static void ScaleObmcTarget(Span<int> weightedSource, Span<int> mask)
            => Av1ObmcSearch.Scale<byte, Av1ObmcSearch.ByteOperator>(weightedSource, mask);

        /// <inheritdoc/>
        public static void SubtractObmcSource(ReadOnlySpan<byte> source, Span<int> weightedSource)
            => Av1ObmcSearch.SubtractFromSource<byte, Av1ObmcSearch.ByteOperator>(source, weightedSource);
    }
}
