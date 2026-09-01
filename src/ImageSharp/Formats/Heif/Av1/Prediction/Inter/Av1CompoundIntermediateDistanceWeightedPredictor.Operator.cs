// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines final distance-weighted compound-intermediate reconstruction.
/// </content>
internal static partial class Av1CompoundIntermediateDistanceWeightedPredictor
{
    /// <summary>
    /// Defines distance-weighted finalization for scalar and SIMD lane groups.
    /// </summary>
    private interface IAv1CompoundIntermediateDistanceWeightedOperator
    {
        /// <summary>
        /// Distance-weights and finalizes one pair of compound intermediate samples.
        /// </summary>
        /// <param name="first">The first compound intermediate.</param>
        /// <param name="second">The second compound intermediate.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The reconstructed sample.</returns>
        public static abstract byte DistanceWeighted(
            ushort first,
            ushort second,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset);

        /// <summary>
        /// Distance-weights and finalizes 128 bits of compound intermediate samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor intermediates.</param>
        /// <param name="first1">The upper first-predictor intermediates.</param>
        /// <param name="second0">The lower second-predictor intermediates.</param>
        /// <param name="second1">The upper second-predictor intermediates.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector128<byte> DistanceWeighted(
            Vector128<ushort> first0,
            Vector128<ushort> first1,
            Vector128<ushort> second0,
            Vector128<ushort> second1,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset);

        /// <summary>
        /// Distance-weights and finalizes 256 bits of compound intermediate samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor intermediates.</param>
        /// <param name="first1">The upper first-predictor intermediates.</param>
        /// <param name="second0">The lower second-predictor intermediates.</param>
        /// <param name="second1">The upper second-predictor intermediates.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector256<byte> DistanceWeighted(
            Vector256<ushort> first0,
            Vector256<ushort> first1,
            Vector256<ushort> second0,
            Vector256<ushort> second1,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset);

        /// <summary>
        /// Distance-weights and finalizes 512 bits of compound intermediate samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor intermediates.</param>
        /// <param name="first1">The upper first-predictor intermediates.</param>
        /// <param name="second0">The lower second-predictor intermediates.</param>
        /// <param name="second1">The upper second-predictor intermediates.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector512<byte> DistanceWeighted(
            Vector512<ushort> first0,
            Vector512<ushort> first1,
            Vector512<ushort> second0,
            Vector512<ushort> second1,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset);

        /// <summary>
        /// Distance-weights and finalizes one pair of high-bit-depth compound intermediate samples.
        /// </summary>
        /// <param name="first">The first compound intermediate.</param>
        /// <param name="second">The second compound intermediate.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <param name="maximum">The maximum reconstructed sample value.</param>
        /// <returns>The reconstructed sample.</returns>
        public static abstract ushort DistanceWeightedHighBitDepth(
            ushort first,
            ushort second,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset,
            int maximum);

        /// <summary>
        /// Distance-weights and finalizes 128 bits of high-bit-depth compound intermediate samples.
        /// </summary>
        /// <param name="first">The first compound intermediates.</param>
        /// <param name="second">The second compound intermediates.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <param name="maximum">The maximum reconstructed sample value.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector128<ushort> DistanceWeightedHighBitDepth(
            Vector128<ushort> first,
            Vector128<ushort> second,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset,
            int maximum);

        /// <summary>
        /// Distance-weights and finalizes 256 bits of high-bit-depth compound intermediate samples.
        /// </summary>
        /// <param name="first">The first compound intermediates.</param>
        /// <param name="second">The second compound intermediates.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <param name="maximum">The maximum reconstructed sample value.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector256<ushort> DistanceWeightedHighBitDepth(
            Vector256<ushort> first,
            Vector256<ushort> second,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset,
            int maximum);

        /// <summary>
        /// Distance-weights and finalizes 512 bits of high-bit-depth compound intermediate samples.
        /// </summary>
        /// <param name="first">The first compound intermediates.</param>
        /// <param name="second">The second compound intermediates.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <param name="maximum">The maximum reconstructed sample value.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector512<ushort> DistanceWeightedHighBitDepth(
            Vector512<ushort> first,
            Vector512<ushort> second,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset,
            int maximum);
    }

    /// <summary>
    /// Implements distance-weighted finalization for scalar and SIMD lane groups.
    /// </summary>
    private readonly struct CompoundIntermediateDistanceWeightedOperator : IAv1CompoundIntermediateDistanceWeightedOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte DistanceWeighted(
            ushort first,
            ushort second,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset)
        {
            int result = ((first * firstWeight) + (second * secondWeight)) >> DistanceWeightBits;
            result -= roundOffset;
            return (byte)Math.Clamp(RoundPowerOfTwo(result, roundBits), 0, byte.MaxValue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort DistanceWeightedHighBitDepth(
            ushort first,
            ushort second,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset,
            int maximum)
        {
            int result = ((first * firstWeight) + (second * secondWeight)) >> DistanceWeightBits;
            result -= roundOffset;
            return (ushort)Math.Clamp(RoundPowerOfTwo(result, roundBits), 0, maximum);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> DistanceWeighted(
            Vector128<ushort> first0,
            Vector128<ushort> first1,
            Vector128<ushort> second0,
            Vector128<ushort> second1,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset)
            => Vector128.Narrow(
                DistanceWeighted(first0, second0, firstWeight, secondWeight, roundBits, roundOffset),
                DistanceWeighted(first1, second1, firstWeight, secondWeight, roundBits, roundOffset));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> DistanceWeighted(
            Vector256<ushort> first0,
            Vector256<ushort> first1,
            Vector256<ushort> second0,
            Vector256<ushort> second1,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset)
            => Vector256.Narrow(
                DistanceWeighted(first0, second0, firstWeight, secondWeight, roundBits, roundOffset),
                DistanceWeighted(first1, second1, firstWeight, secondWeight, roundBits, roundOffset));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> DistanceWeighted(
            Vector512<ushort> first0,
            Vector512<ushort> first1,
            Vector512<ushort> second0,
            Vector512<ushort> second1,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset)
            => Vector512.Narrow(
                DistanceWeighted(first0, second0, firstWeight, secondWeight, roundBits, roundOffset),
                DistanceWeighted(first1, second1, firstWeight, secondWeight, roundBits, roundOffset));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> DistanceWeightedHighBitDepth(
            Vector128<ushort> first,
            Vector128<ushort> second,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset,
            int maximum)
        {
            Vector128<uint> firstLower = Vector128.WidenLower(first);
            Vector128<uint> firstUpper = Vector128.WidenUpper(first);
            Vector128<uint> secondLower = Vector128.WidenLower(second);
            Vector128<uint> secondUpper = Vector128.WidenUpper(second);
            Vector128<uint> lower =
                ((firstLower * Vector128.Create((uint)firstWeight)) +
                 (secondLower * Vector128.Create((uint)secondWeight))) >> DistanceWeightBits;

            Vector128<uint> upper =
                ((firstUpper * Vector128.Create((uint)firstWeight)) +
                 (secondUpper * Vector128.Create((uint)secondWeight))) >> DistanceWeightBits;

            return FinalizeHighBitDepthIntermediate(
                Vector128.Narrow(lower, upper),
                roundBits,
                roundOffset,
                maximum);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> DistanceWeightedHighBitDepth(
            Vector256<ushort> first,
            Vector256<ushort> second,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset,
            int maximum)
        {
            Vector256<uint> firstLower = Vector256.WidenLower(first);
            Vector256<uint> firstUpper = Vector256.WidenUpper(first);
            Vector256<uint> secondLower = Vector256.WidenLower(second);
            Vector256<uint> secondUpper = Vector256.WidenUpper(second);
            Vector256<uint> lower =
                ((firstLower * Vector256.Create((uint)firstWeight)) +
                 (secondLower * Vector256.Create((uint)secondWeight))) >> DistanceWeightBits;

            Vector256<uint> upper =
                ((firstUpper * Vector256.Create((uint)firstWeight)) +
                 (secondUpper * Vector256.Create((uint)secondWeight))) >> DistanceWeightBits;

            return FinalizeHighBitDepthIntermediate(
                Vector256.Narrow(lower, upper),
                roundBits,
                roundOffset,
                maximum);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> DistanceWeightedHighBitDepth(
            Vector512<ushort> first,
            Vector512<ushort> second,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset,
            int maximum)
        {
            Vector512<uint> firstLower = Vector512.WidenLower(first);
            Vector512<uint> firstUpper = Vector512.WidenUpper(first);
            Vector512<uint> secondLower = Vector512.WidenLower(second);
            Vector512<uint> secondUpper = Vector512.WidenUpper(second);
            Vector512<uint> lower =
                ((firstLower * Vector512.Create((uint)firstWeight)) +
                 (secondLower * Vector512.Create((uint)secondWeight))) >> DistanceWeightBits;

            Vector512<uint> upper =
                ((firstUpper * Vector512.Create((uint)firstWeight)) +
                 (secondUpper * Vector512.Create((uint)secondWeight))) >> DistanceWeightBits;

            return FinalizeHighBitDepthIntermediate(
                Vector512.Narrow(lower, upper),
                roundBits,
                roundOffset,
                maximum);
        }

        /// <summary>
        /// Distance-weights 128-bit lanes without overflowing the unsigned intermediate range.
        /// </summary>
        private static Vector128<ushort> DistanceWeighted(
            Vector128<ushort> first,
            Vector128<ushort> second,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset)
        {
            Vector128<int> firstLower = Vector128.WidenLower(first).AsInt32();
            Vector128<int> firstUpper = Vector128.WidenUpper(first).AsInt32();
            Vector128<int> secondLower = Vector128.WidenLower(second).AsInt32();
            Vector128<int> secondUpper = Vector128.WidenUpper(second).AsInt32();
            Vector128<int> lower = ((firstLower * firstWeight) + (secondLower * secondWeight)) >> DistanceWeightBits;
            Vector128<int> upper = ((firstUpper * firstWeight) + (secondUpper * secondWeight)) >> DistanceWeightBits;
            return Vector128.Narrow(FinalizeIntermediate(lower, roundBits, roundOffset), FinalizeIntermediate(upper, roundBits, roundOffset)).AsUInt16();
        }

        /// <summary>
        /// Distance-weights 256-bit lanes without overflowing the unsigned intermediate range.
        /// </summary>
        private static Vector256<ushort> DistanceWeighted(
            Vector256<ushort> first,
            Vector256<ushort> second,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset)
        {
            Vector256<int> firstLower = Vector256.WidenLower(first).AsInt32();
            Vector256<int> firstUpper = Vector256.WidenUpper(first).AsInt32();
            Vector256<int> secondLower = Vector256.WidenLower(second).AsInt32();
            Vector256<int> secondUpper = Vector256.WidenUpper(second).AsInt32();
            Vector256<int> lower = ((firstLower * firstWeight) + (secondLower * secondWeight)) >> DistanceWeightBits;
            Vector256<int> upper = ((firstUpper * firstWeight) + (secondUpper * secondWeight)) >> DistanceWeightBits;
            return Vector256.Narrow(FinalizeIntermediate(lower, roundBits, roundOffset), FinalizeIntermediate(upper, roundBits, roundOffset)).AsUInt16();
        }

        /// <summary>
        /// Distance-weights 512-bit lanes without overflowing the unsigned intermediate range.
        /// </summary>
        private static Vector512<ushort> DistanceWeighted(
            Vector512<ushort> first,
            Vector512<ushort> second,
            int firstWeight,
            int secondWeight,
            int roundBits,
            int roundOffset)
        {
            Vector512<int> firstLower = Vector512.WidenLower(first).AsInt32();
            Vector512<int> firstUpper = Vector512.WidenUpper(first).AsInt32();
            Vector512<int> secondLower = Vector512.WidenLower(second).AsInt32();
            Vector512<int> secondUpper = Vector512.WidenUpper(second).AsInt32();
            Vector512<int> lower = ((firstLower * firstWeight) + (secondLower * secondWeight)) >> DistanceWeightBits;
            Vector512<int> upper = ((firstUpper * firstWeight) + (secondUpper * secondWeight)) >> DistanceWeightBits;
            return Vector512.Narrow(FinalizeIntermediate(lower, roundBits, roundOffset), FinalizeIntermediate(upper, roundBits, roundOffset)).AsUInt16();
        }
    }
}
