// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Implements the normative symmetric-even halving kernel for scalar and SIMD lanes.
/// </content>
internal static partial class Av1PlaneDownsampler
{
    /// <summary>
    /// Applies the kernel <c>{-1, -3, 12, 56, 56, 12, -3, -1}</c> in Q7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The kernel is the four half taps <c>{56, 12, -3, -1}</c>, mirrored.
    /// Thus an output is the weighted sum of the four samples on each side of the sampling position.
    /// The half taps total 64, which is half of the Q7 unit, because each tap weights two samples.
    /// </para>
    /// <para>
    /// The weighted sum reaches 34680, which a signed sixteen-bit lane cannot hold. Thus the two signs accumulate separately.
    /// The positive terms reach 34680 and the negative terms reach 2040, and both fit in an unsigned sixteen-bit lane.
    /// The lanes thus stay sixteen bits wide, so each register holds twice as many outputs as an accumulation widened to thirty-two bits.
    /// </para>
    /// </remarks>
    private readonly struct SymmetricEvenOperator : IAv1HalfFilterOperator
    {
        /// <summary>
        /// The magnitude of the innermost tap pair.
        /// </summary>
        private const int Tap0 = 56;

        /// <summary>
        /// The magnitude of the second tap pair.
        /// </summary>
        private const int Tap1 = 12;

        /// <summary>
        /// The magnitude of the third tap pair, whose sign is negative.
        /// </summary>
        private const int Tap2 = 3;

        /// <summary>
        /// The magnitude of the outermost tap pair, whose sign is negative.
        /// </summary>
        private const int Tap3 = 1;

        /// <summary>
        /// The rounding term added before the Q7 shift.
        /// </summary>
        private const int Rounding = 1 << (FilterBits - 1);

        /// <summary>
        /// The largest eight-bit sample.
        /// </summary>
        private const int Maximum = 255;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Filter(int s0, int s1, int s2, int s3, int s4, int s5, int s6, int s7)
        {
            // The two inner tap pairs carry the positive weights, and the two outer pairs carry the negative weights.
            // The split keeps the scalar form the same as the vector form, which must split them to stay inside an unsigned sixteen-bit lane.
            int positive = (Tap0 * (s3 + s4)) + (Tap1 * (s2 + s5));
            int negative = (Tap2 * (s1 + s6)) + (Tap3 * (s0 + s7));

            return (byte)Math.Clamp((positive + Rounding - negative) >> FilterBits, 0, Maximum);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> Filter(
            Vector128<ushort> s0,
            Vector128<ushort> s1,
            Vector128<ushort> s2,
            Vector128<ushort> s3,
            Vector128<ushort> s4,
            Vector128<ushort> s5,
            Vector128<ushort> s6,
            Vector128<ushort> s7)
        {
            // Each lane is one independent output. The code sums a tap pair before it applies the weight.
            // This halves the multiplications and is exact, because a pair reaches only 510.
            Vector128<ushort> positive = ((s3 + s4) * Vector128.Create((ushort)Tap0)) +
                ((s2 + s5) * Vector128.Create((ushort)Tap1));

            // The outermost tap is 1, so its pair needs no multiplication.
            Vector128<ushort> negative = ((s1 + s6) * Vector128.Create((ushort)Tap2)) + (s0 + s7);

            // The rounding term joins the positive side.
            // Thus the comparison below decides the sign of the full expression, not the sign of the weighted sum alone.
            Vector128<ushort> biased = positive + Vector128.Create((ushort)Rounding);

            // An unsigned lane cannot represent a negative difference, so the mask sets the negative lanes to zero.
            // This gives the same result as the scalar clamp, because a negative sum always clamps to zero after the arithmetic shift.
            Vector128<ushort> mask = Vector128.GreaterThanOrEqual(biased, negative);
            Vector128<ushort> difference = (biased - negative) & mask;

            return Vector128.Min(difference >>> FilterBits, Vector128.Create((ushort)Maximum));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> Filter(
            Vector256<ushort> s0,
            Vector256<ushort> s1,
            Vector256<ushort> s2,
            Vector256<ushort> s3,
            Vector256<ushort> s4,
            Vector256<ushort> s5,
            Vector256<ushort> s6,
            Vector256<ushort> s7)
        {
            // This overload computes sixteen independent outputs with the lane layout and the arithmetic of the 128-bit overload.
            // The explicit overload lets the JIT emit native YMM operations without a width test or a split of the vector into halves.
            Vector256<ushort> positive = ((s3 + s4) * Vector256.Create((ushort)Tap0)) +
                ((s2 + s5) * Vector256.Create((ushort)Tap1));

            Vector256<ushort> negative = ((s1 + s6) * Vector256.Create((ushort)Tap2)) + (s0 + s7);
            Vector256<ushort> biased = positive + Vector256.Create((ushort)Rounding);
            Vector256<ushort> mask = Vector256.GreaterThanOrEqual(biased, negative);
            Vector256<ushort> difference = (biased - negative) & mask;

            return Vector256.Min(difference >>> FilterBits, Vector256.Create((ushort)Maximum));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> Filter(
            Vector512<ushort> s0,
            Vector512<ushort> s1,
            Vector512<ushort> s2,
            Vector512<ushort> s3,
            Vector512<ushort> s4,
            Vector512<ushort> s5,
            Vector512<ushort> s6,
            Vector512<ushort> s7)
        {
            // This overload computes thirty-two independent outputs. The comparison produces an all-ones or all-zero lane mask on every supported path.
            // On AVX-512, the JIT changes the mask register back to a vector for the following AND.
            Vector512<ushort> positive = ((s3 + s4) * Vector512.Create((ushort)Tap0)) +
                ((s2 + s5) * Vector512.Create((ushort)Tap1));

            Vector512<ushort> negative = ((s1 + s6) * Vector512.Create((ushort)Tap2)) + (s0 + s7);
            Vector512<ushort> biased = positive + Vector512.Create((ushort)Rounding);
            Vector512<ushort> mask = Vector512.GreaterThanOrEqual(biased, negative);
            Vector512<ushort> difference = (biased - negative) & mask;

            return Vector512.Min(difference >>> FilterBits, Vector512.Create((ushort)Maximum));
        }
    }
}
