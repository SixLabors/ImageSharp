// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1InterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines final masked compound-intermediate reconstruction.
/// </content>
internal static partial class Av1CompoundIntermediateMaskBlendPredictor
{
    /// <summary>
    /// Defines masked compound-intermediate finalization for scalar and SIMD lane groups.
    /// </summary>
    private interface IAv1CompoundIntermediateMaskBlendOperator
    {
        /// <summary>
        /// Alpha-blends and finalizes one pair of compound intermediate samples.
        /// </summary>
        /// <param name="first">The first compound intermediate.</param>
        /// <param name="second">The second compound intermediate.</param>
        /// <param name="alpha">The first-predictor weight in the AV1 mask range.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The reconstructed sample.</returns>
        public static abstract byte Blend(ushort first, ushort second, byte alpha, int roundBits, int roundOffset);

        /// <summary>
        /// Alpha-blends and finalizes 128 bits of compound intermediate samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor intermediates.</param>
        /// <param name="first1">The upper first-predictor intermediates.</param>
        /// <param name="second0">The lower second-predictor intermediates.</param>
        /// <param name="second1">The upper second-predictor intermediates.</param>
        /// <param name="alpha">The first-predictor weights in the AV1 mask range.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector128<byte> Blend(
            Vector128<ushort> first0,
            Vector128<ushort> first1,
            Vector128<ushort> second0,
            Vector128<ushort> second1,
            Vector128<byte> alpha,
            int roundBits,
            int roundOffset);

        /// <summary>
        /// Alpha-blends and finalizes 256 bits of compound intermediate samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor intermediates.</param>
        /// <param name="first1">The upper first-predictor intermediates.</param>
        /// <param name="second0">The lower second-predictor intermediates.</param>
        /// <param name="second1">The upper second-predictor intermediates.</param>
        /// <param name="alpha">The first-predictor weights in the AV1 mask range.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector256<byte> Blend(
            Vector256<ushort> first0,
            Vector256<ushort> first1,
            Vector256<ushort> second0,
            Vector256<ushort> second1,
            Vector256<byte> alpha,
            int roundBits,
            int roundOffset);

        /// <summary>
        /// Alpha-blends and finalizes 512 bits of compound intermediate samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor intermediates.</param>
        /// <param name="first1">The upper first-predictor intermediates.</param>
        /// <param name="second0">The lower second-predictor intermediates.</param>
        /// <param name="second1">The upper second-predictor intermediates.</param>
        /// <param name="alpha">The first-predictor weights in the AV1 mask range.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The reconstructed samples.</returns>
        public static abstract Vector512<byte> Blend(
            Vector512<ushort> first0,
            Vector512<ushort> first1,
            Vector512<ushort> second0,
            Vector512<ushort> second1,
            Vector512<byte> alpha,
            int roundBits,
            int roundOffset);
    }

    /// <summary>
    /// Implements masked compound-intermediate finalization for scalar and SIMD lane groups.
    /// </summary>
    private readonly struct CompoundIntermediateMaskBlendOperator : IAv1CompoundIntermediateMaskBlendOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Blend(ushort first, ushort second, byte alpha, int roundBits, int roundOffset)
        {
            // The Q6 blend truncates because final pixel rounding is still pending after bias removal.
            int result = ((alpha * first) + ((MaximumMaskAlpha - alpha) * second)) >> MaskWeightBits;
            result -= roundOffset;
            return (byte)Math.Clamp(RoundPowerOfTwo(result, roundBits), 0, byte.MaxValue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Blend(
            Vector128<ushort> first0,
            Vector128<ushort> first1,
            Vector128<ushort> second0,
            Vector128<ushort> second1,
            Vector128<byte> alpha,
            int roundBits,
            int roundOffset)
            => Vector128.Narrow(
                Blend(first0, second0, Vector128.WidenLower(alpha), roundBits, roundOffset),
                Blend(first1, second1, Vector128.WidenUpper(alpha), roundBits, roundOffset));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Blend(
            Vector256<ushort> first0,
            Vector256<ushort> first1,
            Vector256<ushort> second0,
            Vector256<ushort> second1,
            Vector256<byte> alpha,
            int roundBits,
            int roundOffset)
            => Vector256.Narrow(
                Blend(first0, second0, Vector256.WidenLower(alpha), roundBits, roundOffset),
                Blend(first1, second1, Vector256.WidenUpper(alpha), roundBits, roundOffset));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Blend(
            Vector512<ushort> first0,
            Vector512<ushort> first1,
            Vector512<ushort> second0,
            Vector512<ushort> second1,
            Vector512<byte> alpha,
            int roundBits,
            int roundOffset)
            => Vector512.Narrow(
                Blend(first0, second0, Vector512.WidenLower(alpha), roundBits, roundOffset),
                Blend(first1, second1, Vector512.WidenUpper(alpha), roundBits, roundOffset));

        /// <summary>
        /// Alpha-blends 128-bit lanes after widening every product to signed 32-bit precision.
        /// </summary>
        private static Vector128<ushort> Blend(
            Vector128<ushort> first,
            Vector128<ushort> second,
            Vector128<ushort> alpha,
            int roundBits,
            int roundOffset)
        {
            Vector128<int> firstLower = Vector128.WidenLower(first).AsInt32();
            Vector128<int> firstUpper = Vector128.WidenUpper(first).AsInt32();
            Vector128<int> secondLower = Vector128.WidenLower(second).AsInt32();
            Vector128<int> secondUpper = Vector128.WidenUpper(second).AsInt32();
            Vector128<int> alphaLower = Vector128.WidenLower(alpha).AsInt32();
            Vector128<int> alphaUpper = Vector128.WidenUpper(alpha).AsInt32();
            Vector128<int> maximum = Vector128.Create(MaximumMaskAlpha);
            Vector128<int> lower = ((alphaLower * firstLower) + ((maximum - alphaLower) * secondLower)) >> MaskWeightBits;
            Vector128<int> upper = ((alphaUpper * firstUpper) + ((maximum - alphaUpper) * secondUpper)) >> MaskWeightBits;
            return Vector128.Narrow(FinalizeIntermediate(lower, roundBits, roundOffset), FinalizeIntermediate(upper, roundBits, roundOffset)).AsUInt16();
        }

        /// <summary>
        /// Alpha-blends 256-bit lanes after widening every product to signed 32-bit precision.
        /// </summary>
        private static Vector256<ushort> Blend(
            Vector256<ushort> first,
            Vector256<ushort> second,
            Vector256<ushort> alpha,
            int roundBits,
            int roundOffset)
        {
            Vector256<int> firstLower = Vector256.WidenLower(first).AsInt32();
            Vector256<int> firstUpper = Vector256.WidenUpper(first).AsInt32();
            Vector256<int> secondLower = Vector256.WidenLower(second).AsInt32();
            Vector256<int> secondUpper = Vector256.WidenUpper(second).AsInt32();
            Vector256<int> alphaLower = Vector256.WidenLower(alpha).AsInt32();
            Vector256<int> alphaUpper = Vector256.WidenUpper(alpha).AsInt32();
            Vector256<int> maximum = Vector256.Create(MaximumMaskAlpha);
            Vector256<int> lower = ((alphaLower * firstLower) + ((maximum - alphaLower) * secondLower)) >> MaskWeightBits;
            Vector256<int> upper = ((alphaUpper * firstUpper) + ((maximum - alphaUpper) * secondUpper)) >> MaskWeightBits;
            return Vector256.Narrow(FinalizeIntermediate(lower, roundBits, roundOffset), FinalizeIntermediate(upper, roundBits, roundOffset)).AsUInt16();
        }

        /// <summary>
        /// Alpha-blends 512-bit lanes after widening every product to signed 32-bit precision.
        /// </summary>
        private static Vector512<ushort> Blend(
            Vector512<ushort> first,
            Vector512<ushort> second,
            Vector512<ushort> alpha,
            int roundBits,
            int roundOffset)
        {
            Vector512<int> firstLower = Vector512.WidenLower(first).AsInt32();
            Vector512<int> firstUpper = Vector512.WidenUpper(first).AsInt32();
            Vector512<int> secondLower = Vector512.WidenLower(second).AsInt32();
            Vector512<int> secondUpper = Vector512.WidenUpper(second).AsInt32();
            Vector512<int> alphaLower = Vector512.WidenLower(alpha).AsInt32();
            Vector512<int> alphaUpper = Vector512.WidenUpper(alpha).AsInt32();
            Vector512<int> maximum = Vector512.Create(MaximumMaskAlpha);
            Vector512<int> lower = ((alphaLower * firstLower) + ((maximum - alphaLower) * secondLower)) >> MaskWeightBits;
            Vector512<int> upper = ((alphaUpper * firstUpper) + ((maximum - alphaUpper) * secondUpper)) >> MaskWeightBits;
            return Vector512.Narrow(FinalizeIntermediate(lower, roundBits, roundOffset), FinalizeIntermediate(upper, roundBits, roundOffset)).AsUInt16();
        }
    }
}
