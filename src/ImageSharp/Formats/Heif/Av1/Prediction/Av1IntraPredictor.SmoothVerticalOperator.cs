// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <content>
/// Provides vertical smooth intra prediction for scalar and SIMD sample representations.
/// </content>
internal abstract partial class Av1IntraPredictorBase
{
    /// <summary>
    /// Implements vertical AV1 smooth intra prediction for scalar and SIMD lanes.
    /// </summary>
    /// <remarks>
    /// The row's Q8 weight is common to every lane, while top references vary by column. Rewriting the complementary
    /// weight around 256 leaves one product per lane plus a broadcast bottom-left endpoint and rounding bias.
    /// </remarks>
    internal readonly struct SmoothVerticalOperator : IAv1IntraPredictionOperator
    {
        /// <summary>
        /// The Q8 scale used by the smooth surface.
        /// </summary>
        private const int WeightScale = 256;

        /// <inheritdoc/>
        public static Av1PredictionMode Mode => Av1PredictionMode.SmoothVertical;

        /// <inheritdoc/>
        public static Av1IntraPredictionInputs Inputs
            => Av1IntraPredictionInputs.Top | Av1IntraPredictionInputs.BottomLeft | Av1IntraPredictionInputs.RowWeight;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Predict(byte top, byte left, byte topLeft, byte topRight, byte bottomLeft, int columnWeight, int rowWeight)
            => (byte)(((top * rowWeight) + (bottomLeft * (WeightScale - rowWeight)) + (WeightScale >> 1)) >> 8);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Predict(Vector128<byte> top, Vector128<byte> left, Vector128<byte> topLeft, Vector128<byte> topRight, Vector128<byte> bottomLeft, ref int columnWeights, int rowWeight)
        {
            Widen(top, out Vector128<int> top0, out Vector128<int> top1, out Vector128<int> top2, out Vector128<int> top3);
            int bottomLeftSample = bottomLeft.GetElement(0);
            Vector128<int> bottomLeftVector = Vector128.Create(bottomLeftSample);
            Vector128<int> endpointBias = Vector128.Create((bottomLeftSample * WeightScale) + (WeightScale >> 1));

            return Narrow(
                Calculate(top0, bottomLeftVector, endpointBias, rowWeight),
                Calculate(top1, bottomLeftVector, endpointBias, rowWeight),
                Calculate(top2, bottomLeftVector, endpointBias, rowWeight),
                Calculate(top3, bottomLeftVector, endpointBias, rowWeight));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Predict(Vector256<byte> top, Vector256<byte> left, Vector256<byte> topLeft, Vector256<byte> topRight, Vector256<byte> bottomLeft, ref int columnWeights, int rowWeight)
        {
            Widen(top, out Vector256<int> top0, out Vector256<int> top1, out Vector256<int> top2, out Vector256<int> top3);
            int bottomLeftSample = bottomLeft.GetElement(0);
            Vector256<int> bottomLeftVector = Vector256.Create(bottomLeftSample);
            Vector256<int> endpointBias = Vector256.Create((bottomLeftSample * WeightScale) + (WeightScale >> 1));

            return Narrow(
                Calculate(top0, bottomLeftVector, endpointBias, rowWeight),
                Calculate(top1, bottomLeftVector, endpointBias, rowWeight),
                Calculate(top2, bottomLeftVector, endpointBias, rowWeight),
                Calculate(top3, bottomLeftVector, endpointBias, rowWeight));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Predict(Vector512<byte> top, Vector512<byte> left, Vector512<byte> topLeft, Vector512<byte> topRight, Vector512<byte> bottomLeft, ref int columnWeights, int rowWeight)
        {
            Widen(top, out Vector512<int> top0, out Vector512<int> top1, out Vector512<int> top2, out Vector512<int> top3);
            int bottomLeftSample = bottomLeft.GetElement(0);
            Vector512<int> bottomLeftVector = Vector512.Create(bottomLeftSample);
            Vector512<int> endpointBias = Vector512.Create((bottomLeftSample * WeightScale) + (WeightScale >> 1));

            return Narrow(
                Calculate(top0, bottomLeftVector, endpointBias, rowWeight),
                Calculate(top1, bottomLeftVector, endpointBias, rowWeight),
                Calculate(top2, bottomLeftVector, endpointBias, rowWeight),
                Calculate(top3, bottomLeftVector, endpointBias, rowWeight));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Predict(short top, short left, short topLeft, short topRight, short bottomLeft, int columnWeight, int rowWeight)
            => (short)(((top * rowWeight) + (bottomLeft * (WeightScale - rowWeight)) + (WeightScale >> 1)) >> 8);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Predict(Vector128<short> top, Vector128<short> left, Vector128<short> topLeft, Vector128<short> topRight, Vector128<short> bottomLeft, ref int columnWeights, int rowWeight)
        {
            Widen(top, out Vector128<int> top0, out Vector128<int> top1);
            int bottomLeftSample = bottomLeft.GetElement(0);
            Vector128<int> bottomLeftVector = Vector128.Create(bottomLeftSample);
            Vector128<int> endpointBias = Vector128.Create((bottomLeftSample * WeightScale) + (WeightScale >> 1));

            return Narrow(
                Calculate(top0, bottomLeftVector, endpointBias, rowWeight),
                Calculate(top1, bottomLeftVector, endpointBias, rowWeight));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Predict(Vector256<short> top, Vector256<short> left, Vector256<short> topLeft, Vector256<short> topRight, Vector256<short> bottomLeft, ref int columnWeights, int rowWeight)
        {
            Widen(top, out Vector256<int> top0, out Vector256<int> top1);
            int bottomLeftSample = bottomLeft.GetElement(0);
            Vector256<int> bottomLeftVector = Vector256.Create(bottomLeftSample);
            Vector256<int> endpointBias = Vector256.Create((bottomLeftSample * WeightScale) + (WeightScale >> 1));

            return Narrow(
                Calculate(top0, bottomLeftVector, endpointBias, rowWeight),
                Calculate(top1, bottomLeftVector, endpointBias, rowWeight));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Predict(Vector512<short> top, Vector512<short> left, Vector512<short> topLeft, Vector512<short> topRight, Vector512<short> bottomLeft, ref int columnWeights, int rowWeight)
        {
            Widen(top, out Vector512<int> top0, out Vector512<int> top1);
            int bottomLeftSample = bottomLeft.GetElement(0);
            Vector512<int> bottomLeftVector = Vector512.Create(bottomLeftSample);
            Vector512<int> endpointBias = Vector512.Create((bottomLeftSample * WeightScale) + (WeightScale >> 1));

            return Narrow(
                Calculate(top0, bottomLeftVector, endpointBias, rowWeight),
                Calculate(top1, bottomLeftVector, endpointBias, rowWeight));
        }

        /// <summary>
        /// Calculates four vertical smooth predictions.
        /// </summary>
        /// <param name="top">The widened top samples.</param>
        /// <param name="bottomLeft">The bottom-left endpoint in every lane.</param>
        /// <param name="endpointBias">The bottom-left endpoint and rounding bias in every lane.</param>
        /// <param name="rowWeight">The vertical Q8 weight.</param>
        /// <returns>The rounded predictions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<int> Calculate(Vector128<int> top, Vector128<int> bottomLeft, Vector128<int> endpointBias, int rowWeight)
            => (((top - bottomLeft) * rowWeight) + endpointBias) >> 8;

        /// <summary>
        /// Calculates eight vertical smooth predictions.
        /// </summary>
        /// <param name="top">The widened top samples.</param>
        /// <param name="bottomLeft">The bottom-left endpoint in every lane.</param>
        /// <param name="endpointBias">The bottom-left endpoint and rounding bias in every lane.</param>
        /// <param name="rowWeight">The vertical Q8 weight.</param>
        /// <returns>The rounded predictions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> Calculate(Vector256<int> top, Vector256<int> bottomLeft, Vector256<int> endpointBias, int rowWeight)
            => (((top - bottomLeft) * rowWeight) + endpointBias) >> 8;

        /// <summary>
        /// Calculates sixteen vertical smooth predictions.
        /// </summary>
        /// <param name="top">The widened top samples.</param>
        /// <param name="bottomLeft">The bottom-left endpoint in every lane.</param>
        /// <param name="endpointBias">The bottom-left endpoint and rounding bias in every lane.</param>
        /// <param name="rowWeight">The vertical Q8 weight.</param>
        /// <returns>The rounded predictions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<int> Calculate(Vector512<int> top, Vector512<int> bottomLeft, Vector512<int> endpointBias, int rowWeight)
            => (((top - bottomLeft) * rowWeight) + endpointBias) >> 8;
    }
}
