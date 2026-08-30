// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1InterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines biased compound-prediction conversion arithmetic.
/// </content>
internal static partial class Av1CompoundInterPredictor
{
    /// <summary>
    /// Defines biased compound-prediction conversion for scalar and SIMD lane groups.
    /// </summary>
    private interface IAv1CompoundPredictionOperator
    {
        /// <summary>
        /// Converts one integer-position sample to the compound intermediate representation.
        /// </summary>
        /// <param name="sample">The source sample.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The biased compound intermediate.</returns>
        public static abstract ushort Copy(byte sample, int roundBits, int roundOffset);

        /// <summary>
        /// Converts one high-bit-depth integer-position sample to the compound intermediate representation.
        /// </summary>
        /// <param name="sample">The source sample.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The biased compound intermediate.</returns>
        public static abstract ushort CopyHighBitDepth(ushort sample, int roundBits, int roundOffset);

        /// <summary>
        /// Converts 128 bits of high-bit-depth integer-position samples to compound intermediates.
        /// </summary>
        /// <param name="samples">The source samples.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The biased compound intermediates.</returns>
        public static abstract Vector128<ushort> CopyHighBitDepth(
            Vector128<ushort> samples,
            int roundBits,
            int roundOffset);

        /// <summary>
        /// Converts 256 bits of high-bit-depth integer-position samples to compound intermediates.
        /// </summary>
        /// <param name="samples">The source samples.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The biased compound intermediates.</returns>
        public static abstract Vector256<ushort> CopyHighBitDepth(
            Vector256<ushort> samples,
            int roundBits,
            int roundOffset);

        /// <summary>
        /// Converts 512 bits of high-bit-depth integer-position samples to compound intermediates.
        /// </summary>
        /// <param name="samples">The source samples.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The biased compound intermediates.</returns>
        public static abstract Vector512<ushort> CopyHighBitDepth(
            Vector512<ushort> samples,
            int roundBits,
            int roundOffset);

        /// <summary>
        /// Converts 128 bits of integer-position samples to compound intermediates.
        /// </summary>
        /// <param name="samples">The source samples.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <param name="lower">Receives the lower widened intermediates.</param>
        /// <param name="upper">Receives the upper widened intermediates.</param>
        public static abstract void Copy(
            Vector128<byte> samples,
            int roundBits,
            int roundOffset,
            out Vector128<ushort> lower,
            out Vector128<ushort> upper);

        /// <summary>
        /// Converts 256 bits of integer-position samples to compound intermediates.
        /// </summary>
        /// <param name="samples">The source samples.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <param name="lower">Receives the lower widened intermediates.</param>
        /// <param name="upper">Receives the upper widened intermediates.</param>
        public static abstract void Copy(
            Vector256<byte> samples,
            int roundBits,
            int roundOffset,
            out Vector256<ushort> lower,
            out Vector256<ushort> upper);

        /// <summary>
        /// Converts 512 bits of integer-position samples to compound intermediates.
        /// </summary>
        /// <param name="samples">The source samples.</param>
        /// <param name="roundBits">The final reconstruction shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <param name="lower">Receives the lower widened intermediates.</param>
        /// <param name="upper">Receives the upper widened intermediates.</param>
        public static abstract void Copy(
            Vector512<byte> samples,
            int roundBits,
            int roundOffset,
            out Vector512<ushort> lower,
            out Vector512<ushort> upper);

        /// <summary>
        /// Applies direct-filter rounding and bias to one convolution result.
        /// </summary>
        /// <param name="result">The convolution result.</param>
        /// <param name="preShift">The shift applied before rounding.</param>
        /// <param name="round">The rounding shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The biased compound intermediate.</returns>
        public static abstract ushort PrepareDirect(int result, int preShift, int round, int roundOffset);

        /// <summary>
        /// Applies direct-filter rounding and bias to 128-bit widened convolution results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <param name="preShift">The shift applied before rounding.</param>
        /// <param name="round">The rounding shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The biased compound intermediates.</returns>
        public static abstract Vector128<ushort> PrepareDirect(
            Vector128<int> lower,
            Vector128<int> upper,
            int preShift,
            int round,
            int roundOffset);

        /// <summary>
        /// Applies direct-filter rounding and bias to 256-bit widened convolution results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <param name="preShift">The shift applied before rounding.</param>
        /// <param name="round">The rounding shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The biased compound intermediates.</returns>
        public static abstract Vector256<ushort> PrepareDirect(
            Vector256<int> lower,
            Vector256<int> upper,
            int preShift,
            int round,
            int roundOffset);

        /// <summary>
        /// Applies direct-filter rounding and bias to 512-bit widened convolution results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <param name="preShift">The shift applied before rounding.</param>
        /// <param name="round">The rounding shift.</param>
        /// <param name="roundOffset">The compound intermediate bias.</param>
        /// <returns>The biased compound intermediates.</returns>
        public static abstract Vector512<ushort> PrepareDirect(
            Vector512<int> lower,
            Vector512<int> upper,
            int preShift,
            int round,
            int roundOffset);

        /// <summary>
        /// Applies first-pass compound rounding to one biased horizontal convolution result.
        /// </summary>
        /// <param name="result">The biased horizontal convolution result.</param>
        /// <returns>The rounded intermediate.</returns>
        public static abstract short PrepareHorizontal(int result);

        /// <summary>
        /// Applies first-pass compound rounding to 128-bit widened horizontal convolution results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <returns>The rounded intermediates.</returns>
        public static abstract Vector128<short> PrepareHorizontal(Vector128<int> lower, Vector128<int> upper);

        /// <summary>
        /// Applies first-pass compound rounding to 256-bit widened horizontal convolution results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <returns>The rounded intermediates.</returns>
        public static abstract Vector256<short> PrepareHorizontal(Vector256<int> lower, Vector256<int> upper);

        /// <summary>
        /// Applies first-pass compound rounding to 512-bit widened horizontal convolution results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <returns>The rounded intermediates.</returns>
        public static abstract Vector512<short> PrepareHorizontal(Vector512<int> lower, Vector512<int> upper);

        /// <summary>
        /// Applies first-pass compound rounding to one biased high-bit-depth horizontal convolution result.
        /// </summary>
        /// <param name="result">The horizontal convolution result.</param>
        /// <param name="bias">The bit-depth-dependent horizontal bias.</param>
        /// <param name="round">The bit-depth-dependent first-pass shift.</param>
        /// <returns>The rounded intermediate.</returns>
        public static abstract short PrepareHighBitDepthHorizontal(int result, int bias, int round);

        /// <summary>
        /// Applies first-pass compound rounding to 128-bit widened high-bit-depth horizontal results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <param name="bias">The bit-depth-dependent horizontal bias.</param>
        /// <param name="round">The bit-depth-dependent first-pass shift.</param>
        /// <returns>The rounded intermediates.</returns>
        public static abstract Vector128<short> PrepareHighBitDepthHorizontal(
            Vector128<int> lower,
            Vector128<int> upper,
            int bias,
            int round);

        /// <summary>
        /// Applies first-pass compound rounding to 256-bit widened high-bit-depth horizontal results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <param name="bias">The bit-depth-dependent horizontal bias.</param>
        /// <param name="round">The bit-depth-dependent first-pass shift.</param>
        /// <returns>The rounded intermediates.</returns>
        public static abstract Vector256<short> PrepareHighBitDepthHorizontal(
            Vector256<int> lower,
            Vector256<int> upper,
            int bias,
            int round);

        /// <summary>
        /// Applies first-pass compound rounding to 512-bit widened high-bit-depth horizontal results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <param name="bias">The bit-depth-dependent horizontal bias.</param>
        /// <param name="round">The bit-depth-dependent first-pass shift.</param>
        /// <returns>The rounded intermediates.</returns>
        public static abstract Vector512<short> PrepareHighBitDepthHorizontal(
            Vector512<int> lower,
            Vector512<int> upper,
            int bias,
            int round);

        /// <summary>
        /// Applies second-pass compound rounding to one biased vertical convolution result.
        /// </summary>
        /// <param name="result">The biased vertical convolution result.</param>
        /// <returns>The compound intermediate.</returns>
        public static abstract ushort PrepareVertical(int result);

        /// <summary>
        /// Applies second-pass compound rounding to 128-bit widened vertical convolution results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <returns>The compound intermediates.</returns>
        public static abstract Vector128<ushort> PrepareVertical(Vector128<int> lower, Vector128<int> upper);

        /// <summary>
        /// Applies second-pass compound rounding to 256-bit widened vertical convolution results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <returns>The compound intermediates.</returns>
        public static abstract Vector256<ushort> PrepareVertical(Vector256<int> lower, Vector256<int> upper);

        /// <summary>
        /// Applies second-pass compound rounding to 512-bit widened vertical convolution results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <returns>The compound intermediates.</returns>
        public static abstract Vector512<ushort> PrepareVertical(Vector512<int> lower, Vector512<int> upper);

        /// <summary>
        /// Applies second-pass compound rounding to one biased high-bit-depth vertical convolution result.
        /// </summary>
        /// <param name="result">The vertical convolution result.</param>
        /// <param name="bias">The bit-depth-dependent vertical bias.</param>
        /// <returns>The compound intermediate.</returns>
        public static abstract ushort PrepareHighBitDepthVertical(int result, int bias);

        /// <summary>
        /// Applies second-pass compound rounding to 128-bit widened high-bit-depth vertical results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <param name="bias">The bit-depth-dependent vertical bias.</param>
        /// <returns>The compound intermediates.</returns>
        public static abstract Vector128<ushort> PrepareHighBitDepthVertical(
            Vector128<int> lower,
            Vector128<int> upper,
            int bias);

        /// <summary>
        /// Applies second-pass compound rounding to 256-bit widened high-bit-depth vertical results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <param name="bias">The bit-depth-dependent vertical bias.</param>
        /// <returns>The compound intermediates.</returns>
        public static abstract Vector256<ushort> PrepareHighBitDepthVertical(
            Vector256<int> lower,
            Vector256<int> upper,
            int bias);

        /// <summary>
        /// Applies second-pass compound rounding to 512-bit widened high-bit-depth vertical results.
        /// </summary>
        /// <param name="lower">The lower convolution results.</param>
        /// <param name="upper">The upper convolution results.</param>
        /// <param name="bias">The bit-depth-dependent vertical bias.</param>
        /// <returns>The compound intermediates.</returns>
        public static abstract Vector512<ushort> PrepareHighBitDepthVertical(
            Vector512<int> lower,
            Vector512<int> upper,
            int bias);
    }

    /// <summary>
    /// Implements AV1 compound-prediction conversion for scalar and SIMD lane groups.
    /// </summary>
    private readonly struct CompoundPredictionOperator : IAv1CompoundPredictionOperator
    {
        private const int HorizontalBias = 1 << (8 + FilterBits - 1);
        private const int VerticalBias = 1 << (8 + (2 * FilterBits) - Round0Bits);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Copy(byte sample, int roundBits, int roundOffset)
            => (ushort)((sample << roundBits) + roundOffset);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort CopyHighBitDepth(ushort sample, int roundBits, int roundOffset)
            => (ushort)((sample << roundBits) + roundOffset);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> CopyHighBitDepth(
            Vector128<ushort> samples,
            int roundBits,
            int roundOffset)
            => (samples << roundBits) + Vector128.Create((ushort)roundOffset);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> CopyHighBitDepth(
            Vector256<ushort> samples,
            int roundBits,
            int roundOffset)
            => (samples << roundBits) + Vector256.Create((ushort)roundOffset);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> CopyHighBitDepth(
            Vector512<ushort> samples,
            int roundBits,
            int roundOffset)
            => (samples << roundBits) + Vector512.Create((ushort)roundOffset);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy(
            Vector128<byte> samples,
            int roundBits,
            int roundOffset,
            out Vector128<ushort> lower,
            out Vector128<ushort> upper)
        {
            Vector128<ushort> offset = Vector128.Create((ushort)roundOffset);
            lower = (Vector128.WidenLower(samples) << roundBits) + offset;
            upper = (Vector128.WidenUpper(samples) << roundBits) + offset;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy(
            Vector256<byte> samples,
            int roundBits,
            int roundOffset,
            out Vector256<ushort> lower,
            out Vector256<ushort> upper)
        {
            Vector256<ushort> offset = Vector256.Create((ushort)roundOffset);
            lower = (Vector256.WidenLower(samples) << roundBits) + offset;
            upper = (Vector256.WidenUpper(samples) << roundBits) + offset;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy(
            Vector512<byte> samples,
            int roundBits,
            int roundOffset,
            out Vector512<ushort> lower,
            out Vector512<ushort> upper)
        {
            Vector512<ushort> offset = Vector512.Create((ushort)roundOffset);
            lower = (Vector512.WidenLower(samples) << roundBits) + offset;
            upper = (Vector512.WidenUpper(samples) << roundBits) + offset;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort PrepareDirect(int result, int preShift, int round, int roundOffset)
            => (ushort)(RoundPowerOfTwo(result << preShift, round) + roundOffset);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> PrepareDirect(
            Vector128<int> lower,
            Vector128<int> upper,
            int preShift,
            int round,
            int roundOffset)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower << preShift, round) + Vector128.Create(roundOffset),
                RoundPowerOfTwo(upper << preShift, round) + Vector128.Create(roundOffset)).AsUInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> PrepareDirect(
            Vector256<int> lower,
            Vector256<int> upper,
            int preShift,
            int round,
            int roundOffset)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower << preShift, round) + Vector256.Create(roundOffset),
                RoundPowerOfTwo(upper << preShift, round) + Vector256.Create(roundOffset)).AsUInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> PrepareDirect(
            Vector512<int> lower,
            Vector512<int> upper,
            int preShift,
            int round,
            int roundOffset)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower << preShift, round) + Vector512.Create(roundOffset),
                RoundPowerOfTwo(upper << preShift, round) + Vector512.Create(roundOffset)).AsUInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short PrepareHorizontal(int result)
            => (short)RoundPowerOfTwo(HorizontalBias + result, Round0Bits);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> PrepareHorizontal(Vector128<int> lower, Vector128<int> upper)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower + Vector128.Create(HorizontalBias), Round0Bits),
                RoundPowerOfTwo(upper + Vector128.Create(HorizontalBias), Round0Bits));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> PrepareHorizontal(Vector256<int> lower, Vector256<int> upper)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower + Vector256.Create(HorizontalBias), Round0Bits),
                RoundPowerOfTwo(upper + Vector256.Create(HorizontalBias), Round0Bits));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> PrepareHorizontal(Vector512<int> lower, Vector512<int> upper)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower + Vector512.Create(HorizontalBias), Round0Bits),
                RoundPowerOfTwo(upper + Vector512.Create(HorizontalBias), Round0Bits));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short PrepareHighBitDepthHorizontal(int result, int bias, int round)
            => (short)RoundPowerOfTwo(bias + result, round);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> PrepareHighBitDepthHorizontal(
            Vector128<int> lower,
            Vector128<int> upper,
            int bias,
            int round)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower + Vector128.Create(bias), round),
                RoundPowerOfTwo(upper + Vector128.Create(bias), round));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> PrepareHighBitDepthHorizontal(
            Vector256<int> lower,
            Vector256<int> upper,
            int bias,
            int round)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower + Vector256.Create(bias), round),
                RoundPowerOfTwo(upper + Vector256.Create(bias), round));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> PrepareHighBitDepthHorizontal(
            Vector512<int> lower,
            Vector512<int> upper,
            int bias,
            int round)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower + Vector512.Create(bias), round),
                RoundPowerOfTwo(upper + Vector512.Create(bias), round));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort PrepareVertical(int result)
            => (ushort)RoundPowerOfTwo(VerticalBias + result, CompoundRound1Bits);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> PrepareVertical(Vector128<int> lower, Vector128<int> upper)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower + Vector128.Create(VerticalBias), CompoundRound1Bits),
                RoundPowerOfTwo(upper + Vector128.Create(VerticalBias), CompoundRound1Bits)).AsUInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> PrepareVertical(Vector256<int> lower, Vector256<int> upper)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower + Vector256.Create(VerticalBias), CompoundRound1Bits),
                RoundPowerOfTwo(upper + Vector256.Create(VerticalBias), CompoundRound1Bits)).AsUInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> PrepareVertical(Vector512<int> lower, Vector512<int> upper)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower + Vector512.Create(VerticalBias), CompoundRound1Bits),
                RoundPowerOfTwo(upper + Vector512.Create(VerticalBias), CompoundRound1Bits)).AsUInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort PrepareHighBitDepthVertical(int result, int bias)
            => (ushort)RoundPowerOfTwo(bias + result, CompoundRound1Bits);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> PrepareHighBitDepthVertical(
            Vector128<int> lower,
            Vector128<int> upper,
            int bias)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower + Vector128.Create(bias), CompoundRound1Bits),
                RoundPowerOfTwo(upper + Vector128.Create(bias), CompoundRound1Bits)).AsUInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> PrepareHighBitDepthVertical(
            Vector256<int> lower,
            Vector256<int> upper,
            int bias)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower + Vector256.Create(bias), CompoundRound1Bits),
                RoundPowerOfTwo(upper + Vector256.Create(bias), CompoundRound1Bits)).AsUInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> PrepareHighBitDepthVertical(
            Vector512<int> lower,
            Vector512<int> upper,
            int bias)
            => Av1IntraPredictorBase.Narrow(
                RoundPowerOfTwo(lower + Vector512.Create(bias), CompoundRound1Bits),
                RoundPowerOfTwo(upper + Vector512.Create(bias), CompoundRound1Bits)).AsUInt16();
    }
}
