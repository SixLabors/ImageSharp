// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <content>
/// Defines Q5 interpolation for AV1 directional intra prediction.
/// </content>
internal static partial class Av1DirectionalIntraPredictor
{
    /// <summary>
    /// The largest number of samples required to transpose a directional prediction block.
    /// </summary>
    public const int ScratchLength = 64 * 64;

    /// <summary>
    /// Defines scalar and SIMD interpolation for AV1 directional intra prediction.
    /// </summary>
    internal interface IDirectionalPredictionOperator
    {
        /// <summary>
        /// Interpolates one 8-bit pair.
        /// </summary>
        /// <param name="left">The first reference sample.</param>
        /// <param name="right">The second reference sample.</param>
        /// <param name="weight">The Q5 weight of <paramref name="right"/>.</param>
        /// <returns>The interpolated sample.</returns>
        public static abstract byte Interpolate(byte left, byte right, int weight);

        /// <summary>
        /// Interpolates one high-bit-depth pair.
        /// </summary>
        /// <param name="left">The first reference sample.</param>
        /// <param name="right">The second reference sample.</param>
        /// <param name="weight">The Q5 weight of <paramref name="right"/>.</param>
        /// <returns>The interpolated sample.</returns>
        public static abstract short Interpolate(short left, short right, int weight);

        /// <summary>
        /// Interpolates sixteen 8-bit pairs.
        /// </summary>
        /// <param name="left">The first reference samples.</param>
        /// <param name="right">The second reference samples.</param>
        /// <param name="weight">The Q5 weight of <paramref name="right"/>.</param>
        /// <returns>The interpolated samples.</returns>
        public static abstract Vector128<byte> Interpolate(Vector128<byte> left, Vector128<byte> right, int weight);

        /// <summary>
        /// Interpolates thirty-two 8-bit pairs.
        /// </summary>
        /// <param name="left">The first reference samples.</param>
        /// <param name="right">The second reference samples.</param>
        /// <param name="weight">The Q5 weight of <paramref name="right"/>.</param>
        /// <returns>The interpolated samples.</returns>
        public static abstract Vector256<byte> Interpolate(Vector256<byte> left, Vector256<byte> right, int weight);

        /// <summary>
        /// Interpolates sixty-four 8-bit pairs.
        /// </summary>
        /// <param name="left">The first reference samples.</param>
        /// <param name="right">The second reference samples.</param>
        /// <param name="weight">The Q5 weight of <paramref name="right"/>.</param>
        /// <returns>The interpolated samples.</returns>
        public static abstract Vector512<byte> Interpolate(Vector512<byte> left, Vector512<byte> right, int weight);

        /// <summary>
        /// Interpolates eight high-bit-depth pairs.
        /// </summary>
        /// <param name="left">The first reference samples.</param>
        /// <param name="right">The second reference samples.</param>
        /// <param name="weight">The Q5 weight of <paramref name="right"/>.</param>
        /// <returns>The interpolated samples.</returns>
        public static abstract Vector128<short> Interpolate(Vector128<short> left, Vector128<short> right, int weight);

        /// <summary>
        /// Interpolates sixteen high-bit-depth pairs.
        /// </summary>
        /// <param name="left">The first reference samples.</param>
        /// <param name="right">The second reference samples.</param>
        /// <param name="weight">The Q5 weight of <paramref name="right"/>.</param>
        /// <returns>The interpolated samples.</returns>
        public static abstract Vector256<short> Interpolate(Vector256<short> left, Vector256<short> right, int weight);

        /// <summary>
        /// Interpolates thirty-two high-bit-depth pairs.
        /// </summary>
        /// <param name="left">The first reference samples.</param>
        /// <param name="right">The second reference samples.</param>
        /// <param name="weight">The Q5 weight of <paramref name="right"/>.</param>
        /// <returns>The interpolated samples.</returns>
        public static abstract Vector512<short> Interpolate(Vector512<short> left, Vector512<short> right, int weight);

        /// <summary>
        /// Interpolates four widened pairs with independent weights.
        /// </summary>
        /// <param name="left">The first reference samples.</param>
        /// <param name="right">The second reference samples.</param>
        /// <param name="weights">The Q5 weights of <paramref name="right"/>.</param>
        /// <returns>The interpolated samples.</returns>
        public static abstract Vector128<int> Interpolate(Vector128<int> left, Vector128<int> right, Vector128<int> weights);
    }

    /// <summary>
    /// Gets the horizontal Q8 projection derivative for an adjusted angle.
    /// </summary>
    public static int GetDeltaX(int angle) => Predictor<DirectionalOperator>.GetDeltaX(angle);

    /// <summary>
    /// Gets the vertical Q8 projection derivative for an adjusted angle.
    /// </summary>
    public static int GetDeltaY(int angle) => Predictor<DirectionalOperator>.GetDeltaY(angle);

    /// <summary>
    /// Predicts an 8-bit directional block.
    /// </summary>
    public static void Predict(Span<byte> destination, int destinationStride, Av1TransformSize transformSize, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, bool upsampleAbove, bool upsampleLeft, int angle, Span<byte> scratch)
        => Predictor<DirectionalOperator>.Predict(destination, destinationStride, transformSize, above, left, upsampleAbove, upsampleLeft, angle, scratch);

    /// <summary>
    /// Predicts a high-bit-depth directional block.
    /// </summary>
    public static void Predict(Span<short> destination, int destinationStride, Av1TransformSize transformSize, ReadOnlySpan<short> above, ReadOnlySpan<short> left, bool upsampleAbove, bool upsampleLeft, int angle, Span<short> scratch)
        => Predictor<DirectionalOperator>.Predict(destination, destinationStride, transformSize, above, left, upsampleAbove, upsampleLeft, angle, scratch);

    /// <summary>
    /// Predicts an 8-bit directional block without hardware intrinsics.
    /// </summary>
    public static void PredictScalar(Span<byte> destination, int destinationStride, Av1TransformSize transformSize, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, bool upsampleAbove, bool upsampleLeft, int angle)
        => Predictor<DirectionalOperator>.PredictScalar(destination, destinationStride, transformSize, above, left, upsampleAbove, upsampleLeft, angle);

    /// <summary>
    /// Predicts a high-bit-depth directional block without hardware intrinsics.
    /// </summary>
    public static void PredictScalar(Span<short> destination, int destinationStride, Av1TransformSize transformSize, ReadOnlySpan<short> above, ReadOnlySpan<short> left, bool upsampleAbove, bool upsampleLeft, int angle)
        => Predictor<DirectionalOperator>.PredictScalar(destination, destinationStride, transformSize, above, left, upsampleAbove, upsampleLeft, angle);

    /// <summary>
    /// Interpolates projected neighboring samples for all directional prediction zones.
    /// </summary>
    internal readonly struct DirectionalOperator : IDirectionalPredictionOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Interpolate(byte left, byte right, int weight)
            => (byte)(((left * (32 - weight)) + (right * weight) + 16) >> 5);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Interpolate(short left, short right, int weight)
            => (short)(((left * (32 - weight)) + (right * weight) + 16) >> 5);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Interpolate(Vector128<byte> left, Vector128<byte> right, int weight)
        {
            (Vector128<ushort> leftLow, Vector128<ushort> leftHigh) = Vector128.Widen(left);
            (Vector128<ushort> rightLow, Vector128<ushort> rightHigh) = Vector128.Widen(right);
            Vector128<ushort> rounding = Vector128.Create((ushort)16);
            Vector128<ushort> low = ((leftLow * (ushort)(32 - weight)) + (rightLow * (ushort)weight) + rounding) >> 5;
            Vector128<ushort> high = ((leftHigh * (ushort)(32 - weight)) + (rightHigh * (ushort)weight) + rounding) >> 5;

            return Vector128.Narrow(low, high);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Interpolate(Vector256<byte> left, Vector256<byte> right, int weight)
        {
            (Vector256<ushort> leftLow, Vector256<ushort> leftHigh) = Vector256.Widen(left);
            (Vector256<ushort> rightLow, Vector256<ushort> rightHigh) = Vector256.Widen(right);
            Vector256<ushort> rounding = Vector256.Create((ushort)16);
            Vector256<ushort> low = ((leftLow * (ushort)(32 - weight)) + (rightLow * (ushort)weight) + rounding) >> 5;
            Vector256<ushort> high = ((leftHigh * (ushort)(32 - weight)) + (rightHigh * (ushort)weight) + rounding) >> 5;

            return Vector256.Narrow(low, high);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Interpolate(Vector512<byte> left, Vector512<byte> right, int weight)
        {
            (Vector512<ushort> leftLow, Vector512<ushort> leftHigh) = Vector512.Widen(left);
            (Vector512<ushort> rightLow, Vector512<ushort> rightHigh) = Vector512.Widen(right);
            Vector512<ushort> rounding = Vector512.Create((ushort)16);
            Vector512<ushort> low = ((leftLow * (ushort)(32 - weight)) + (rightLow * (ushort)weight) + rounding) >> 5;
            Vector512<ushort> high = ((leftHigh * (ushort)(32 - weight)) + (rightHigh * (ushort)weight) + rounding) >> 5;

            return Vector512.Narrow(low, high);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Interpolate(Vector128<short> left, Vector128<short> right, int weight)
        {
            (Vector128<int> leftLow, Vector128<int> leftHigh) = Vector128.Widen(left);
            (Vector128<int> rightLow, Vector128<int> rightHigh) = Vector128.Widen(right);
            Vector128<int> rounding = Vector128.Create(16);
            Vector128<int> low = ((leftLow * (32 - weight)) + (rightLow * weight) + rounding) >> 5;
            Vector128<int> high = ((leftHigh * (32 - weight)) + (rightHigh * weight) + rounding) >> 5;

            return Vector128.Narrow(low, high);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Interpolate(Vector256<short> left, Vector256<short> right, int weight)
        {
            (Vector256<int> leftLow, Vector256<int> leftHigh) = Vector256.Widen(left);
            (Vector256<int> rightLow, Vector256<int> rightHigh) = Vector256.Widen(right);
            Vector256<int> rounding = Vector256.Create(16);
            Vector256<int> low = ((leftLow * (32 - weight)) + (rightLow * weight) + rounding) >> 5;
            Vector256<int> high = ((leftHigh * (32 - weight)) + (rightHigh * weight) + rounding) >> 5;

            return Vector256.Narrow(low, high);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Interpolate(Vector512<short> left, Vector512<short> right, int weight)
        {
            (Vector512<int> leftLow, Vector512<int> leftHigh) = Vector512.Widen(left);
            (Vector512<int> rightLow, Vector512<int> rightHigh) = Vector512.Widen(right);
            Vector512<int> rounding = Vector512.Create(16);
            Vector512<int> low = ((leftLow * (32 - weight)) + (rightLow * weight) + rounding) >> 5;
            Vector512<int> high = ((leftHigh * (32 - weight)) + (rightHigh * weight) + rounding) >> 5;

            return Vector512.Narrow(low, high);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Interpolate(Vector128<int> left, Vector128<int> right, Vector128<int> weights)
            => ((left * (Vector128.Create(32) - weights)) + (right * weights) + Vector128.Create(16)) >> 5;
    }
}
