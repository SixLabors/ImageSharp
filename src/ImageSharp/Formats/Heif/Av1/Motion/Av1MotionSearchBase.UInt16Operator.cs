// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

internal static partial class Av1MotionSearchBase
{
    /// <summary>
    /// Measures high-bit-depth sample errors with the shared vector-width residual traversal.
    /// </summary>
    public readonly struct UInt16Operator : IMotionSearchOperator<ushort>
    {
        /// <inheritdoc/>
        public static void BuildPrediction(
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> prediction,
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
                bitDepth,
                intermediateRows);

        /// <inheritdoc/>
        public static void PreparePrediction(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> prediction,
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
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> buffer,
            int width,
            int height,
            int horizontalPhase,
            int verticalPhase,
            int taps,
            int bitDepth)
            => Av1TranslationalInterPredictor.PredictForSearch(
                reference, referenceStride, referenceOrigin, buffer, width, height, horizontalPhase, verticalPhase, taps, bitDepth);

        /// <inheritdoc/>
        public static void Subtract(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            Span<short> residual,
            int width,
            int height)
            => Av1ResidualBuilder.Subtract(source, sourceStride, prediction, width, residual, width, width, height);

        /// <inheritdoc/>
        public static void PredictScaled(
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> buffer,
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
                bitDepth,
                intermediateRows);

        /// <inheritdoc/>
        public static int SumAbsoluteDifferences(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            int width,
            int height,
            int rowStep)
            => Av1ResidualBuilder.SumAbsoluteDifferences(source, sourceStride, prediction, predictionStride, width, height, rowStep);

        /// <inheritdoc/>
        public static void GetMoments(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            int width,
            int height,
            out int sum,
            out long squares)
            => Av1ResidualBuilder.GetMoments(source, sourceStride, prediction, predictionStride, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static int SumCompoundAbsoluteDifferences(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            ReadOnlySpan<ushort> secondPrediction,
            ReadOnlySpan<byte> mask,
            int width,
            int height,
            int rowStep)
            => Av1ResidualBuilder.SumCompoundAbsoluteDifferences(
                source, sourceStride, prediction, predictionStride, secondPrediction, mask, width, height, rowStep);

        /// <inheritdoc/>
        public static void GetCompoundMoments(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            ReadOnlySpan<ushort> secondPrediction,
            ReadOnlySpan<byte> mask,
            int width,
            int height,
            out int sum,
            out long squares)
            => Av1ResidualBuilder.GetCompoundMoments(
                source, sourceStride, prediction, predictionStride, secondPrediction, mask, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static int SumObmcAbsoluteDifferences(
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height)
            => Av1ObmcSearch.SumAbsoluteDifferences<ushort, Av1ObmcSearch.UInt16Operator>(prediction, predictionStride, weightedSource, mask, width, height);

        /// <inheritdoc/>
        public static void GetObmcMoments(
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height,
            out int sum,
            out ulong squares)
            => Av1ObmcSearch.GetMoments<ushort, Av1ObmcSearch.UInt16Operator>(prediction, predictionStride, weightedSource, mask, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static void WeightObmcAbove(ReadOnlySpan<ushort> prediction, int weight, Span<int> weightedSource, Span<int> mask, int width)
            => Av1ObmcSearch.WeightAbove<ushort, Av1ObmcSearch.UInt16Operator>(prediction, weight, weightedSource, mask, width);

        /// <inheritdoc/>
        public static void WeightObmcLeft(ReadOnlySpan<ushort> prediction, ReadOnlySpan<byte> weights, Span<int> weightedSource, Span<int> mask)
            => Av1ObmcSearch.WeightLeft<ushort, Av1ObmcSearch.UInt16Operator>(prediction, weights, weightedSource, mask);

        /// <inheritdoc/>
        public static void ScaleObmcTarget(Span<int> weightedSource, Span<int> mask)
            => Av1ObmcSearch.Scale<ushort, Av1ObmcSearch.UInt16Operator>(weightedSource, mask);

        /// <inheritdoc/>
        public static void SubtractObmcSource(ReadOnlySpan<ushort> source, Span<int> weightedSource)
            => Av1ObmcSearch.SubtractFromSource<ushort, Av1ObmcSearch.UInt16Operator>(source, weightedSource);
    }
}
