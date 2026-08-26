// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <content>
/// Provides horizontal smooth intra prediction for scalar and SIMD sample representations.
/// </content>
internal abstract partial class Av1IntraPredictorBase
{
    /// <summary>
    /// Implements horizontal AV1 smooth intra prediction for scalar and SIMD lanes.
    /// </summary>
    /// <remarks>
    /// Each lane uses its Q8 column weight to interpolate between the current row's left sample and the top-right
    /// endpoint. Rewriting the complementary weight around 256 leaves one multiply per lane plus a shared endpoint and
    /// rounding bias.
    /// </remarks>
    internal readonly struct SmoothHorizontalOperator : IAv1IntraPredictionOperator
    {
        /// <summary>
        /// The Q8 scale used by the smooth surface.
        /// </summary>
        private const int WeightScale = 256;

        /// <inheritdoc/>
        public static Av1PredictionMode Mode => Av1PredictionMode.SmoothHorizontal;

        /// <inheritdoc/>
        public static Av1IntraPredictionInputs Inputs
            => Av1IntraPredictionInputs.Left | Av1IntraPredictionInputs.TopRight | Av1IntraPredictionInputs.ColumnWeight;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Predict(byte top, byte left, byte topLeft, byte topRight, byte bottomLeft, int columnWeight, int rowWeight)
            => (byte)(((left * columnWeight) + (topRight * (WeightScale - columnWeight)) + (WeightScale >> 1)) >> 8);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Predict(Vector128<byte> top, Vector128<byte> left, Vector128<byte> topLeft, Vector128<byte> topRight, Vector128<byte> bottomLeft, ref int columnWeights, int rowWeight)
        {
            int leftSample = left.GetElement(0);
            int topRightSample = topRight.GetElement(0);
            int horizontalDelta = leftSample - topRightSample;
            Vector128<int> endpointBias = Vector128.Create((topRightSample * WeightScale) + (WeightScale >> 1));

            return Narrow(
                Calculate(Vector128.LoadUnsafe(ref columnWeights), horizontalDelta, endpointBias),
                Calculate(Vector128.LoadUnsafe(ref columnWeights, (nuint)Vector128<int>.Count), horizontalDelta, endpointBias),
                Calculate(Vector128.LoadUnsafe(ref columnWeights, (nuint)(2 * Vector128<int>.Count)), horizontalDelta, endpointBias),
                Calculate(Vector128.LoadUnsafe(ref columnWeights, (nuint)(3 * Vector128<int>.Count)), horizontalDelta, endpointBias));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Predict(Vector256<byte> top, Vector256<byte> left, Vector256<byte> topLeft, Vector256<byte> topRight, Vector256<byte> bottomLeft, ref int columnWeights, int rowWeight)
        {
            int leftSample = left.GetElement(0);
            int topRightSample = topRight.GetElement(0);
            int horizontalDelta = leftSample - topRightSample;
            Vector256<int> endpointBias = Vector256.Create((topRightSample * WeightScale) + (WeightScale >> 1));

            return Narrow(
                Calculate(Vector256.LoadUnsafe(ref columnWeights), horizontalDelta, endpointBias),
                Calculate(Vector256.LoadUnsafe(ref columnWeights, (nuint)Vector256<int>.Count), horizontalDelta, endpointBias),
                Calculate(Vector256.LoadUnsafe(ref columnWeights, (nuint)(2 * Vector256<int>.Count)), horizontalDelta, endpointBias),
                Calculate(Vector256.LoadUnsafe(ref columnWeights, (nuint)(3 * Vector256<int>.Count)), horizontalDelta, endpointBias));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Predict(Vector512<byte> top, Vector512<byte> left, Vector512<byte> topLeft, Vector512<byte> topRight, Vector512<byte> bottomLeft, ref int columnWeights, int rowWeight)
        {
            int leftSample = left.GetElement(0);
            int topRightSample = topRight.GetElement(0);
            int horizontalDelta = leftSample - topRightSample;
            Vector512<int> endpointBias = Vector512.Create((topRightSample * WeightScale) + (WeightScale >> 1));

            return Narrow(
                Calculate(Vector512.LoadUnsafe(ref columnWeights), horizontalDelta, endpointBias),
                Calculate(Vector512.LoadUnsafe(ref columnWeights, (nuint)Vector512<int>.Count), horizontalDelta, endpointBias),
                Calculate(Vector512.LoadUnsafe(ref columnWeights, (nuint)(2 * Vector512<int>.Count)), horizontalDelta, endpointBias),
                Calculate(Vector512.LoadUnsafe(ref columnWeights, (nuint)(3 * Vector512<int>.Count)), horizontalDelta, endpointBias));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Predict(short top, short left, short topLeft, short topRight, short bottomLeft, int columnWeight, int rowWeight)
            => (short)(((left * columnWeight) + (topRight * (WeightScale - columnWeight)) + (WeightScale >> 1)) >> 8);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Predict(Vector128<short> top, Vector128<short> left, Vector128<short> topLeft, Vector128<short> topRight, Vector128<short> bottomLeft, ref int columnWeights, int rowWeight)
        {
            int leftSample = left.GetElement(0);
            int topRightSample = topRight.GetElement(0);
            int horizontalDelta = leftSample - topRightSample;
            Vector128<int> endpointBias = Vector128.Create((topRightSample * WeightScale) + (WeightScale >> 1));

            return Narrow(
                Calculate(Vector128.LoadUnsafe(ref columnWeights), horizontalDelta, endpointBias),
                Calculate(Vector128.LoadUnsafe(ref columnWeights, (nuint)Vector128<int>.Count), horizontalDelta, endpointBias));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Predict(Vector256<short> top, Vector256<short> left, Vector256<short> topLeft, Vector256<short> topRight, Vector256<short> bottomLeft, ref int columnWeights, int rowWeight)
        {
            int leftSample = left.GetElement(0);
            int topRightSample = topRight.GetElement(0);
            int horizontalDelta = leftSample - topRightSample;
            Vector256<int> endpointBias = Vector256.Create((topRightSample * WeightScale) + (WeightScale >> 1));

            return Narrow(
                Calculate(Vector256.LoadUnsafe(ref columnWeights), horizontalDelta, endpointBias),
                Calculate(Vector256.LoadUnsafe(ref columnWeights, (nuint)Vector256<int>.Count), horizontalDelta, endpointBias));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Predict(Vector512<short> top, Vector512<short> left, Vector512<short> topLeft, Vector512<short> topRight, Vector512<short> bottomLeft, ref int columnWeights, int rowWeight)
        {
            int leftSample = left.GetElement(0);
            int topRightSample = topRight.GetElement(0);
            int horizontalDelta = leftSample - topRightSample;
            Vector512<int> endpointBias = Vector512.Create((topRightSample * WeightScale) + (WeightScale >> 1));

            return Narrow(
                Calculate(Vector512.LoadUnsafe(ref columnWeights), horizontalDelta, endpointBias),
                Calculate(Vector512.LoadUnsafe(ref columnWeights, (nuint)Vector512<int>.Count), horizontalDelta, endpointBias));
        }

        /// <summary>
        /// Calculates four horizontal smooth predictions.
        /// </summary>
        /// <param name="columnWeights">The horizontal Q8 weights.</param>
        /// <param name="horizontalDelta">The difference between the left sample and top-right endpoint.</param>
        /// <param name="endpointBias">The top-right endpoint and rounding bias in every lane.</param>
        /// <returns>The rounded predictions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<int> Calculate(Vector128<int> columnWeights, int horizontalDelta, Vector128<int> endpointBias)
            => ((columnWeights * horizontalDelta) + endpointBias) >> 8;

        /// <summary>
        /// Calculates eight horizontal smooth predictions.
        /// </summary>
        /// <param name="columnWeights">The horizontal Q8 weights.</param>
        /// <param name="horizontalDelta">The difference between the left sample and top-right endpoint.</param>
        /// <param name="endpointBias">The top-right endpoint and rounding bias in every lane.</param>
        /// <returns>The rounded predictions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> Calculate(Vector256<int> columnWeights, int horizontalDelta, Vector256<int> endpointBias)
            => ((columnWeights * horizontalDelta) + endpointBias) >> 8;

        /// <summary>
        /// Calculates sixteen horizontal smooth predictions.
        /// </summary>
        /// <param name="columnWeights">The horizontal Q8 weights.</param>
        /// <param name="horizontalDelta">The difference between the left sample and top-right endpoint.</param>
        /// <param name="endpointBias">The top-right endpoint and rounding bias in every lane.</param>
        /// <returns>The rounded predictions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<int> Calculate(Vector512<int> columnWeights, int horizontalDelta, Vector512<int> endpointBias)
            => ((columnWeights * horizontalDelta) + endpointBias) >> 8;
    }
}
