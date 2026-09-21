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
            Span<short> scratch,
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
                scratch);

        /// <inheritdoc/>
        public static void PreparePrediction(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int referenceOrigin,
            Span<byte> prediction,
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
            BuildPrediction(
                reference,
                referenceStride,
                referenceOrigin,
                prediction,
                scratch,
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
        {
            // An absent mask selects equal weights. Keep this branch outside the pixel traversal.
            if (mask.IsEmpty)
            {
                int averageSum = 0;
                for (int y = 0; y < height; y += rowStep)
                {
                    int packedOffset = y * width;
                    ReadOnlySpan<byte> sourceRow = source.Slice(y * sourceStride, width);
                    ReadOnlySpan<byte> predictionRow = prediction.Slice(y * predictionStride, width);
                    for (int x = 0; x < width; x++)
                    {
                        int blended = (predictionRow[x] + secondPrediction[packedOffset + x] + 1) >> 1;
                        averageSum += Math.Abs(sourceRow[x] - blended);
                    }
                }

                return averageSum * rowStep;
            }

            int sum = 0;
            for (int y = 0; y < height; y += rowStep)
            {
                int packedOffset = y * width;
                ReadOnlySpan<byte> sourceRow = source.Slice(y * sourceStride, width);
                ReadOnlySpan<byte> predictionRow = prediction.Slice(y * predictionStride, width);
                for (int x = 0; x < width; x++)
                {
                    int weight = mask[packedOffset + x];
                    int blended = ((weight * predictionRow[x]) + ((64 - weight) * secondPrediction[packedOffset + x]) + 32) >> 6;
                    sum += Math.Abs(sourceRow[x] - blended);
                }
            }

            // Alternate-row sampling represents the full block. Normalize bit depth only after this scaling.
            return sum * rowStep;
        }

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
        {
            sum = 0;
            squares = 0;
            if (mask.IsEmpty)
            {
                for (int y = 0; y < height; y++)
                {
                    int packedOffset = y * width;
                    ReadOnlySpan<byte> sourceRow = source.Slice(y * sourceStride, width);
                    ReadOnlySpan<byte> predictionRow = prediction.Slice(y * predictionStride, width);
                    for (int x = 0; x < width; x++)
                    {
                        int blended = (predictionRow[x] + secondPrediction[packedOffset + x] + 1) >> 1;
                        int difference = sourceRow[x] - blended;
                        sum += difference;
                        squares += (long)difference * difference;
                    }
                }

                return;
            }

            for (int y = 0; y < height; y++)
            {
                int packedOffset = y * width;
                ReadOnlySpan<byte> sourceRow = source.Slice(y * sourceStride, width);
                ReadOnlySpan<byte> predictionRow = prediction.Slice(y * predictionStride, width);
                for (int x = 0; x < width; x++)
                {
                    // The mask weights sum to 64. Round the blend before subtraction; squaring an
                    // unrounded weighted residual would give a different motion-search objective.
                    int weight = mask[packedOffset + x];
                    int blended = ((weight * predictionRow[x]) + ((64 - weight) * secondPrediction[packedOffset + x]) + 32) >> 6;
                    int difference = sourceRow[x] - blended;
                    sum += difference;
                    squares += (long)difference * difference;
                }
            }
        }
    }
}
