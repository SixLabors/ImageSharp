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
            int predictionStride,
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
                predictionStride,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                bitDepth,
                intermediateRows);

        /// <inheritdoc/>
        public static void SubtractPrediction(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            Span<short> residual,
            int width,
            int height)
            => Av1ResidualBuilder.Subtract<ushort, Av1ResidualBuilder.UInt16Operator>(
                source, sourceStride, prediction, predictionStride, residual, width, width, height);

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
        public static void PredictScaled(
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> buffer,
            int bufferStride,
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
                bufferStride,
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
            => Av1ResidualBuilder.SumAbsoluteDifferences<ushort, Av1ResidualBuilder.UInt16Operator>(
                source, sourceStride, prediction, predictionStride, width, height, rowStep);

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
            => Av1ResidualBuilder.GetMoments<ushort, Av1ResidualBuilder.UInt16Operator>(
                source, sourceStride, prediction, predictionStride, width, height, out sum, out squares);

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
            => Av1ResidualBuilder.SumCompoundAbsoluteDifferences<ushort, Av1ResidualBuilder.UInt16Operator>(
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
            => Av1ResidualBuilder.GetCompoundMoments<ushort, Av1ResidualBuilder.UInt16Operator>(
                source, sourceStride, prediction, predictionStride, secondPrediction, mask, width, height, out sum, out squares);
    }
}
