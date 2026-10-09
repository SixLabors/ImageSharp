// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines final equal-average compound-intermediate reconstruction.
/// </content>
internal static partial class Av1CompoundIntermediateAveragePredictor
{
    /// <summary>
    /// Defines equal-average finalization for scalar and SIMD lane groups.
    /// </summary>
    private interface IAv1CompoundIntermediateAverageOperator
    {
        /// <summary>
        /// Equal-averages and finalizes one pair of compound intermediate samples.
        /// </summary>
        /// <param name="first">The first compound intermediate.</param>
        /// <param name="second">The second compound intermediate.</param>
        /// <param name="roundBits">The fractional bits to remove.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The reconstructed sample.</returns>
        public static abstract byte Average(ushort first, ushort second, int roundBits, int roundOffset);

        /// <summary>
        /// Equal-averages and finalizes one pair of high-bit-depth compound intermediate samples.
        /// </summary>
        /// <param name="first">The first compound intermediate.</param>
        /// <param name="second">The second compound intermediate.</param>
        /// <param name="roundBits">The fractional bits to remove.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <param name="maximum">The maximum reconstructed sample value.</param>
        /// <returns>The reconstructed sample.</returns>
        public static abstract ushort AverageHighBitDepth(
            ushort first,
            ushort second,
            int roundBits,
            int roundOffset,
            int maximum);

        /// <summary>
        /// Equal-averages and finalizes 128 bits of compound intermediate samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor intermediates.</param>
        /// <param name="first1">The upper first-predictor intermediates.</param>
        /// <param name="second0">The lower second-predictor intermediates.</param>
        /// <param name="second1">The upper second-predictor intermediates.</param>
        /// <param name="roundBits">The fractional bits to remove.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector128<byte> Average(
            Vector128<ushort> first0,
            Vector128<ushort> first1,
            Vector128<ushort> second0,
            Vector128<ushort> second1,
            int roundBits,
            int roundOffset);

        /// <summary>
        /// Equal-averages and finalizes 256 bits of compound intermediate samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor intermediates.</param>
        /// <param name="first1">The upper first-predictor intermediates.</param>
        /// <param name="second0">The lower second-predictor intermediates.</param>
        /// <param name="second1">The upper second-predictor intermediates.</param>
        /// <param name="roundBits">The fractional bits to remove.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector256<byte> Average(
            Vector256<ushort> first0,
            Vector256<ushort> first1,
            Vector256<ushort> second0,
            Vector256<ushort> second1,
            int roundBits,
            int roundOffset);

        /// <summary>
        /// Equal-averages and finalizes 512 bits of compound intermediate samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor intermediates.</param>
        /// <param name="first1">The upper first-predictor intermediates.</param>
        /// <param name="second0">The lower second-predictor intermediates.</param>
        /// <param name="second1">The upper second-predictor intermediates.</param>
        /// <param name="roundBits">The fractional bits to remove.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector512<byte> Average(
            Vector512<ushort> first0,
            Vector512<ushort> first1,
            Vector512<ushort> second0,
            Vector512<ushort> second1,
            int roundBits,
            int roundOffset);

        /// <summary>
        /// Equal-averages and finalizes 128 bits of high-bit-depth compound intermediate samples.
        /// </summary>
        /// <param name="first">The first compound intermediates.</param>
        /// <param name="second">The second compound intermediates.</param>
        /// <param name="roundBits">The fractional bits to remove.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <param name="maximum">The maximum reconstructed sample value.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector128<ushort> AverageHighBitDepth(
            Vector128<ushort> first,
            Vector128<ushort> second,
            int roundBits,
            int roundOffset,
            int maximum);

        /// <summary>
        /// Equal-averages and finalizes 256 bits of high-bit-depth compound intermediate samples.
        /// </summary>
        /// <param name="first">The first compound intermediates.</param>
        /// <param name="second">The second compound intermediates.</param>
        /// <param name="roundBits">The fractional bits to remove.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <param name="maximum">The maximum reconstructed sample value.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector256<ushort> AverageHighBitDepth(
            Vector256<ushort> first,
            Vector256<ushort> second,
            int roundBits,
            int roundOffset,
            int maximum);

        /// <summary>
        /// Equal-averages and finalizes 512 bits of high-bit-depth compound intermediate samples.
        /// </summary>
        /// <param name="first">The first compound intermediates.</param>
        /// <param name="second">The second compound intermediates.</param>
        /// <param name="roundBits">The fractional bits to remove.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <param name="maximum">The maximum reconstructed sample value.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector512<ushort> AverageHighBitDepth(
            Vector512<ushort> first,
            Vector512<ushort> second,
            int roundBits,
            int roundOffset,
            int maximum);
    }

    /// <summary>
    /// Implements equal-average finalization for scalar and SIMD lane groups.
    /// The vector forms use <c>(a &amp; b) + ((a ^ b) &gt;&gt; 1)</c>. This identity equals <c>(a + b) &gt;&gt; 1</c> and cannot overflow an unsigned lane.
    /// The 8-bit forms clamp to 0 through 255 before they narrow, so the narrowing keeps every value.
    /// </summary>
    private readonly struct CompoundIntermediateAverageOperator : IAv1CompoundIntermediateAverageOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Average(ushort first, ushort second, int roundBits, int roundOffset)
        {
            // The AV1 average truncates here. The final shift does the only rounding.
            int result = ((first + second) >> 1) - roundOffset;
            return (byte)Math.Clamp(RoundPowerOfTwo(result, roundBits), 0, byte.MaxValue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort AverageHighBitDepth(
            ushort first,
            ushort second,
            int roundBits,
            int roundOffset,
            int maximum)
        {
            int result = ((first + second) >> 1) - roundOffset;
            return (ushort)Math.Clamp(RoundPowerOfTwo(result, roundBits), 0, maximum);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Average(
            Vector128<ushort> first0,
            Vector128<ushort> first1,
            Vector128<ushort> second0,
            Vector128<ushort> second1,
            int roundBits,
            int roundOffset)
            => Vector128.Narrow(
                FinalizeIntermediate((first0 & second0) + ((first0 ^ second0) >> 1), roundBits, roundOffset),
                FinalizeIntermediate((first1 & second1) + ((first1 ^ second1) >> 1), roundBits, roundOffset));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Average(
            Vector256<ushort> first0,
            Vector256<ushort> first1,
            Vector256<ushort> second0,
            Vector256<ushort> second1,
            int roundBits,
            int roundOffset)
            => Vector256.Narrow(
                FinalizeIntermediate((first0 & second0) + ((first0 ^ second0) >> 1), roundBits, roundOffset),
                FinalizeIntermediate((first1 & second1) + ((first1 ^ second1) >> 1), roundBits, roundOffset));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Average(
            Vector512<ushort> first0,
            Vector512<ushort> first1,
            Vector512<ushort> second0,
            Vector512<ushort> second1,
            int roundBits,
            int roundOffset)
            => Vector512.Narrow(
                FinalizeIntermediate((first0 & second0) + ((first0 ^ second0) >> 1), roundBits, roundOffset),
                FinalizeIntermediate((first1 & second1) + ((first1 ^ second1) >> 1), roundBits, roundOffset));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> AverageHighBitDepth(
            Vector128<ushort> first,
            Vector128<ushort> second,
            int roundBits,
            int roundOffset,
            int maximum)
            => FinalizeHighBitDepthIntermediate(
                (first & second) + ((first ^ second) >> 1),
                roundBits,
                roundOffset,
                maximum);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> AverageHighBitDepth(
            Vector256<ushort> first,
            Vector256<ushort> second,
            int roundBits,
            int roundOffset,
            int maximum)
            => FinalizeHighBitDepthIntermediate(
                (first & second) + ((first ^ second) >> 1),
                roundBits,
                roundOffset,
                maximum);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> AverageHighBitDepth(
            Vector512<ushort> first,
            Vector512<ushort> second,
            int roundBits,
            int roundOffset,
            int maximum)
            => FinalizeHighBitDepthIntermediate(
                (first & second) + ((first ^ second) >> 1),
                roundBits,
                roundOffset,
                maximum);
    }
}
