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
        public static void PreparePrediction(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> prediction,
            Span<short> residual,
            Span<short> scratch,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth)
        {
            Av1TranslationalInterPredictor.Predict(
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
                scratch);

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
    }
}
