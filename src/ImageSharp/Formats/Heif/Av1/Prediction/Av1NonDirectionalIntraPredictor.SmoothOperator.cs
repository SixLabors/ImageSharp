// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <content>
/// Provides two-dimensional smooth intra prediction for scalar and SIMD sample representations.
/// </content>
internal abstract partial class Av1NonDirectionalIntraPredictorBase
{
    /// <summary>
    /// Implements two-dimensional AV1 smooth intra prediction for scalar and SIMD lanes.
    /// </summary>
    /// <remarks>
    /// Horizontal and vertical Q8 interpolations are accumulated before one Q9 rounding shift. Expanding each
    /// complementary weight around 256 reduces the lane equation to two products and a shared endpoint bias while
    /// preserving the normative result exactly.
    /// </remarks>
    internal readonly struct SmoothOperator : IAv1IntraPredictionOperator
    {
        /// <summary>
        /// The Q8 scale used by each smooth surface.
        /// </summary>
        private const int WeightScale = 256;

        /// <inheritdoc/>
        public static Av1PredictionMode Mode => Av1PredictionMode.Smooth;

        /// <inheritdoc/>
        public static Av1IntraPredictionInputs Inputs
            => Av1IntraPredictionInputs.Top
            | Av1IntraPredictionInputs.Left
            | Av1IntraPredictionInputs.TopRight
            | Av1IntraPredictionInputs.BottomLeft
            | Av1IntraPredictionInputs.ColumnWeight
            | Av1IntraPredictionInputs.RowWeight;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Predict(byte top, byte left, byte topLeft, byte topRight, byte bottomLeft, int columnWeight, int rowWeight)
        {
            int prediction = (top * rowWeight) + (bottomLeft * (WeightScale - rowWeight));
            prediction += (left * columnWeight) + (topRight * (WeightScale - columnWeight));
            return (byte)((prediction + WeightScale) >> 9);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Predict(Vector128<byte> top, Vector128<byte> left, Vector128<byte> topLeft, Vector128<byte> topRight, Vector128<byte> bottomLeft, ref int columnWeights, int rowWeight)
        {
            Widen(top, out Vector128<int> top0, out Vector128<int> top1, out Vector128<int> top2, out Vector128<int> top3);
            Vector128<int> weight0 = Vector128.LoadUnsafe(ref columnWeights);
            Vector128<int> weight1 = Vector128.LoadUnsafe(ref columnWeights, (nuint)Vector128<int>.Count);
            Vector128<int> weight2 = Vector128.LoadUnsafe(ref columnWeights, (nuint)(2 * Vector128<int>.Count));
            Vector128<int> weight3 = Vector128.LoadUnsafe(ref columnWeights, (nuint)(3 * Vector128<int>.Count));
            int leftSample = left.GetElement(0);
            int topRightSample = topRight.GetElement(0);
            int bottomLeftSample = bottomLeft.GetElement(0);
            int horizontalDelta = leftSample - topRightSample;
            Vector128<int> bottomLeftVector = Vector128.Create(bottomLeftSample);
            Vector128<int> endpointBias = Vector128.Create(((bottomLeftSample + topRightSample) * WeightScale) + WeightScale);

            return Narrow(
                Calculate(top0, weight0, bottomLeftVector, horizontalDelta, endpointBias, rowWeight),
                Calculate(top1, weight1, bottomLeftVector, horizontalDelta, endpointBias, rowWeight),
                Calculate(top2, weight2, bottomLeftVector, horizontalDelta, endpointBias, rowWeight),
                Calculate(top3, weight3, bottomLeftVector, horizontalDelta, endpointBias, rowWeight));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Predict(Vector256<byte> top, Vector256<byte> left, Vector256<byte> topLeft, Vector256<byte> topRight, Vector256<byte> bottomLeft, ref int columnWeights, int rowWeight)
        {
            Widen(top, out Vector256<int> top0, out Vector256<int> top1, out Vector256<int> top2, out Vector256<int> top3);
            Vector256<int> weight0 = Vector256.LoadUnsafe(ref columnWeights);
            Vector256<int> weight1 = Vector256.LoadUnsafe(ref columnWeights, (nuint)Vector256<int>.Count);
            Vector256<int> weight2 = Vector256.LoadUnsafe(ref columnWeights, (nuint)(2 * Vector256<int>.Count));
            Vector256<int> weight3 = Vector256.LoadUnsafe(ref columnWeights, (nuint)(3 * Vector256<int>.Count));
            int leftSample = left.GetElement(0);
            int topRightSample = topRight.GetElement(0);
            int bottomLeftSample = bottomLeft.GetElement(0);
            int horizontalDelta = leftSample - topRightSample;
            Vector256<int> bottomLeftVector = Vector256.Create(bottomLeftSample);
            Vector256<int> endpointBias = Vector256.Create(((bottomLeftSample + topRightSample) * WeightScale) + WeightScale);

            return Narrow(
                Calculate(top0, weight0, bottomLeftVector, horizontalDelta, endpointBias, rowWeight),
                Calculate(top1, weight1, bottomLeftVector, horizontalDelta, endpointBias, rowWeight),
                Calculate(top2, weight2, bottomLeftVector, horizontalDelta, endpointBias, rowWeight),
                Calculate(top3, weight3, bottomLeftVector, horizontalDelta, endpointBias, rowWeight));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Predict(Vector512<byte> top, Vector512<byte> left, Vector512<byte> topLeft, Vector512<byte> topRight, Vector512<byte> bottomLeft, ref int columnWeights, int rowWeight)
        {
            Widen(top, out Vector512<int> top0, out Vector512<int> top1, out Vector512<int> top2, out Vector512<int> top3);
            Vector512<int> weight0 = Vector512.LoadUnsafe(ref columnWeights);
            Vector512<int> weight1 = Vector512.LoadUnsafe(ref columnWeights, (nuint)Vector512<int>.Count);
            Vector512<int> weight2 = Vector512.LoadUnsafe(ref columnWeights, (nuint)(2 * Vector512<int>.Count));
            Vector512<int> weight3 = Vector512.LoadUnsafe(ref columnWeights, (nuint)(3 * Vector512<int>.Count));
            int leftSample = left.GetElement(0);
            int topRightSample = topRight.GetElement(0);
            int bottomLeftSample = bottomLeft.GetElement(0);
            int horizontalDelta = leftSample - topRightSample;
            Vector512<int> bottomLeftVector = Vector512.Create(bottomLeftSample);
            Vector512<int> endpointBias = Vector512.Create(((bottomLeftSample + topRightSample) * WeightScale) + WeightScale);

            return Narrow(
                Calculate(top0, weight0, bottomLeftVector, horizontalDelta, endpointBias, rowWeight),
                Calculate(top1, weight1, bottomLeftVector, horizontalDelta, endpointBias, rowWeight),
                Calculate(top2, weight2, bottomLeftVector, horizontalDelta, endpointBias, rowWeight),
                Calculate(top3, weight3, bottomLeftVector, horizontalDelta, endpointBias, rowWeight));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Predict(short top, short left, short topLeft, short topRight, short bottomLeft, int columnWeight, int rowWeight)
        {
            int prediction = (top * rowWeight) + (bottomLeft * (WeightScale - rowWeight));
            prediction += (left * columnWeight) + (topRight * (WeightScale - columnWeight));
            return (short)((prediction + WeightScale) >> 9);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Predict(Vector128<short> top, Vector128<short> left, Vector128<short> topLeft, Vector128<short> topRight, Vector128<short> bottomLeft, ref int columnWeights, int rowWeight)
        {
            Widen(top, out Vector128<int> top0, out Vector128<int> top1);
            Vector128<int> weight0 = Vector128.LoadUnsafe(ref columnWeights);
            Vector128<int> weight1 = Vector128.LoadUnsafe(ref columnWeights, (nuint)Vector128<int>.Count);
            int leftSample = left.GetElement(0);
            int topRightSample = topRight.GetElement(0);
            int bottomLeftSample = bottomLeft.GetElement(0);
            int horizontalDelta = leftSample - topRightSample;
            Vector128<int> bottomLeftVector = Vector128.Create(bottomLeftSample);
            Vector128<int> endpointBias = Vector128.Create(((bottomLeftSample + topRightSample) * WeightScale) + WeightScale);

            return Narrow(
                Calculate(top0, weight0, bottomLeftVector, horizontalDelta, endpointBias, rowWeight),
                Calculate(top1, weight1, bottomLeftVector, horizontalDelta, endpointBias, rowWeight));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Predict(Vector256<short> top, Vector256<short> left, Vector256<short> topLeft, Vector256<short> topRight, Vector256<short> bottomLeft, ref int columnWeights, int rowWeight)
        {
            Widen(top, out Vector256<int> top0, out Vector256<int> top1);
            Vector256<int> weight0 = Vector256.LoadUnsafe(ref columnWeights);
            Vector256<int> weight1 = Vector256.LoadUnsafe(ref columnWeights, (nuint)Vector256<int>.Count);
            int leftSample = left.GetElement(0);
            int topRightSample = topRight.GetElement(0);
            int bottomLeftSample = bottomLeft.GetElement(0);
            int horizontalDelta = leftSample - topRightSample;
            Vector256<int> bottomLeftVector = Vector256.Create(bottomLeftSample);
            Vector256<int> endpointBias = Vector256.Create(((bottomLeftSample + topRightSample) * WeightScale) + WeightScale);

            return Narrow(
                Calculate(top0, weight0, bottomLeftVector, horizontalDelta, endpointBias, rowWeight),
                Calculate(top1, weight1, bottomLeftVector, horizontalDelta, endpointBias, rowWeight));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Predict(Vector512<short> top, Vector512<short> left, Vector512<short> topLeft, Vector512<short> topRight, Vector512<short> bottomLeft, ref int columnWeights, int rowWeight)
        {
            Widen(top, out Vector512<int> top0, out Vector512<int> top1);
            Vector512<int> weight0 = Vector512.LoadUnsafe(ref columnWeights);
            Vector512<int> weight1 = Vector512.LoadUnsafe(ref columnWeights, (nuint)Vector512<int>.Count);
            int leftSample = left.GetElement(0);
            int topRightSample = topRight.GetElement(0);
            int bottomLeftSample = bottomLeft.GetElement(0);
            int horizontalDelta = leftSample - topRightSample;
            Vector512<int> bottomLeftVector = Vector512.Create(bottomLeftSample);
            Vector512<int> endpointBias = Vector512.Create(((bottomLeftSample + topRightSample) * WeightScale) + WeightScale);

            return Narrow(
                Calculate(top0, weight0, bottomLeftVector, horizontalDelta, endpointBias, rowWeight),
                Calculate(top1, weight1, bottomLeftVector, horizontalDelta, endpointBias, rowWeight));
        }

        /// <summary>
        /// Calculates four two-dimensional smooth predictions.
        /// </summary>
        /// <param name="top">The widened top samples.</param>
        /// <param name="columnWeights">The horizontal Q8 weights.</param>
        /// <param name="bottomLeft">The bottom-left endpoint in every lane.</param>
        /// <param name="horizontalDelta">The difference between the left sample and top-right endpoint.</param>
        /// <param name="endpointBias">The combined endpoint and rounding bias in every lane.</param>
        /// <param name="rowWeight">The vertical Q8 weight.</param>
        /// <returns>The rounded predictions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<int> Calculate(
            Vector128<int> top,
            Vector128<int> columnWeights,
            Vector128<int> bottomLeft,
            int horizontalDelta,
            Vector128<int> endpointBias,
            int rowWeight)
        {
            // Expanding the complementary weights gives the exact normative sum while reducing it to two vector
            // multiplications: (top - bottom) * rowWeight + (left - right) * columnWeight + the endpoint bias.
            return (((top - bottomLeft) * rowWeight) + (columnWeights * horizontalDelta) + endpointBias) >> 9;
        }

        /// <summary>
        /// Calculates eight two-dimensional smooth predictions.
        /// </summary>
        /// <param name="top">The widened top samples.</param>
        /// <param name="columnWeights">The horizontal Q8 weights.</param>
        /// <param name="bottomLeft">The bottom-left endpoint in every lane.</param>
        /// <param name="horizontalDelta">The difference between the left sample and top-right endpoint.</param>
        /// <param name="endpointBias">The combined endpoint and rounding bias in every lane.</param>
        /// <param name="rowWeight">The vertical Q8 weight.</param>
        /// <returns>The rounded predictions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> Calculate(
            Vector256<int> top,
            Vector256<int> columnWeights,
            Vector256<int> bottomLeft,
            int horizontalDelta,
            Vector256<int> endpointBias,
            int rowWeight)
            => (((top - bottomLeft) * rowWeight) + (columnWeights * horizontalDelta) + endpointBias) >> 9;

        /// <summary>
        /// Calculates sixteen two-dimensional smooth predictions.
        /// </summary>
        /// <param name="top">The widened top samples.</param>
        /// <param name="columnWeights">The horizontal Q8 weights.</param>
        /// <param name="bottomLeft">The bottom-left endpoint in every lane.</param>
        /// <param name="horizontalDelta">The difference between the left sample and top-right endpoint.</param>
        /// <param name="endpointBias">The combined endpoint and rounding bias in every lane.</param>
        /// <param name="rowWeight">The vertical Q8 weight.</param>
        /// <returns>The rounded predictions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<int> Calculate(
            Vector512<int> top,
            Vector512<int> columnWeights,
            Vector512<int> bottomLeft,
            int horizontalDelta,
            Vector512<int> endpointBias,
            int rowWeight)
            => (((top - bottomLeft) * rowWeight) + (columnWeights * horizontalDelta) + endpointBias) >> 9;
    }
}
