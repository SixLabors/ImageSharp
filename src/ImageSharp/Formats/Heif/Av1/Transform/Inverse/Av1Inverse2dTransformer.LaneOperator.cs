// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Defines the lane arithmetic of the inverse-transform stage networks. One lane is one transform axis, so every stage network is written once over a lane type
/// and each width supplies only its own arithmetic.
/// </content>
internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Supplies the fixed-point arithmetic of one register width, or of one axis for the scalar path.
    /// </summary>
    /// <remarks>
    /// Each member works on every lane independently and never mixes axes. Addition, subtraction and negation wrap in 32 bits. The scalar members widen each
    /// product to 64 bits before the rounding shift. The SIMD members keep products in 32-bit lanes, because the stage ranges bound conformant input.
    /// </remarks>
    /// <typeparam name="TLanes">The lane type.</typeparam>
    internal interface IAv1TransformLaneOperator<TLanes>
        where TLanes : unmanaged
    {
        /// <summary>
        /// Adds lanes and clamps each sum to a signed stage range.
        /// </summary>
        /// <param name="left">The first addend.</param>
        /// <param name="right">The second addend.</param>
        /// <param name="bitCount">The width of the signed stage range.</param>
        /// <returns>The clamped sums.</returns>
        public static abstract TLanes AddClamp(TLanes left, TLanes right, byte bitCount);

        /// <summary>
        /// Subtracts lanes and clamps each difference to a signed stage range.
        /// </summary>
        /// <param name="left">The minuend.</param>
        /// <param name="right">The subtrahend.</param>
        /// <param name="bitCount">The width of the signed stage range.</param>
        /// <returns>The clamped differences.</returns>
        public static abstract TLanes SubtractClamp(TLanes left, TLanes right, byte bitCount);

        /// <summary>
        /// Clamps lanes to a signed stage range.
        /// </summary>
        /// <param name="value">The stage values.</param>
        /// <param name="bitCount">The width of the signed stage range.</param>
        /// <returns>The clamped values.</returns>
        public static abstract TLanes Clamp(TLanes value, byte bitCount);

        /// <summary>
        /// Negates lanes.
        /// </summary>
        /// <param name="value">The stage values.</param>
        /// <returns>The negated values.</returns>
        public static abstract TLanes Negate(TLanes value);

        /// <summary>
        /// Calculates one rounded output of a weighted two-input butterfly.
        /// </summary>
        /// <param name="weight0">The first fixed-point weight.</param>
        /// <param name="input0">The first input values.</param>
        /// <param name="weight1">The second fixed-point weight.</param>
        /// <param name="input1">The second input values.</param>
        /// <param name="cosBit">The number of fractional bits in each weight.</param>
        /// <returns>The rounded fixed-point results.</returns>
        public static abstract TLanes HalfButterfly(int weight0, TLanes input0, int weight1, TLanes input1, int cosBit);

        /// <summary>
        /// Multiplies lanes by a fixed-point weight and rounds the products.
        /// </summary>
        /// <remarks>
        /// A sparse network uses this member where the other butterfly input is known to be zero.
        /// </remarks>
        /// <param name="value">The input values.</param>
        /// <param name="multiplier">The fixed-point weight.</param>
        /// <param name="cosBit">The number of fractional bits in the weight.</param>
        /// <returns>The rounded fixed-point results.</returns>
        public static abstract TLanes MultiplyRound(TLanes value, int multiplier, int cosBit);
    }

    /// <summary>
    /// Reinterprets scalar transform storage as transform positions, so the scalar path runs the shared stage networks.
    /// </summary>
    /// <remarks>
    /// Field <c>Vn</c> of the result is element <c>n</c> of <paramref name="values"/>. The span must hold every position the operator reads or writes. The
    /// two-dimensional traversal and the operator contract guarantee that, so the stage networks never touch storage past the span.
    /// </remarks>
    /// <param name="values">The scalar transform storage.</param>
    /// <returns>A reference to the transform positions that start at the first element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Av1TransformVector<int> AsPositions(ReadOnlySpan<int> values)
        => ref Unsafe.As<int, Av1TransformVector<int>>(ref MemoryMarshal.GetReference(values));

    /// <summary>
    /// Transforms one axis at a time.
    /// </summary>
    internal readonly struct ScalarLaneOperator : IAv1TransformLaneOperator<int>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AddClamp(int left, int right, byte bitCount) => Av1Transform1dMath.Clamp(left + right, bitCount);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SubtractClamp(int left, int right, byte bitCount) => Av1Transform1dMath.Clamp(left - right, bitCount);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clamp(int value, byte bitCount) => Av1Transform1dMath.Clamp(value, bitCount);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Negate(int value) => -value;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HalfButterfly(int weight0, int input0, int weight1, int input1, int cosBit)
            => Av1Transform1dMath.HalfButterfly(weight0, input0, weight1, input1, cosBit);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MultiplyRound(int value, int multiplier, int cosBit) => Av1Math.RoundShift((long)value * multiplier, cosBit);
    }

    /// <summary>
    /// Transforms four axes at a time.
    /// </summary>
    internal readonly struct Vector128LaneOperator : IAv1TransformLaneOperator<Vector128<int>>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> AddClamp(Vector128<int> left, Vector128<int> right, byte bitCount)
            => Av1Transform1dMath.Clamp(left + right, bitCount);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> SubtractClamp(Vector128<int> left, Vector128<int> right, byte bitCount)
            => Av1Transform1dMath.Clamp(left - right, bitCount);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Clamp(Vector128<int> value, byte bitCount) => Av1Transform1dMath.Clamp(value, bitCount);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Negate(Vector128<int> value) => -value;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> HalfButterfly(int weight0, Vector128<int> input0, int weight1, Vector128<int> input1, int cosBit)
            => Av1Transform1dMath.HalfButterfly(weight0, input0, weight1, input1, cosBit);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> MultiplyRound(Vector128<int> value, int multiplier, int cosBit)
            => Av1Transform1dMath.MultiplyRound(value, multiplier, cosBit);
    }

    /// <summary>
    /// Transforms eight axes at a time.
    /// </summary>
    internal readonly struct Vector256LaneOperator : IAv1TransformLaneOperator<Vector256<int>>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> AddClamp(Vector256<int> left, Vector256<int> right, byte bitCount)
            => Av1Transform1dMath.Clamp(left + right, bitCount);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> SubtractClamp(Vector256<int> left, Vector256<int> right, byte bitCount)
            => Av1Transform1dMath.Clamp(left - right, bitCount);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Clamp(Vector256<int> value, byte bitCount) => Av1Transform1dMath.Clamp(value, bitCount);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Negate(Vector256<int> value) => -value;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> HalfButterfly(int weight0, Vector256<int> input0, int weight1, Vector256<int> input1, int cosBit)
            => Av1Transform1dMath.HalfButterfly(weight0, input0, weight1, input1, cosBit);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> MultiplyRound(Vector256<int> value, int multiplier, int cosBit)
            => Av1Transform1dMath.MultiplyRound(value, multiplier, cosBit);
    }
}
