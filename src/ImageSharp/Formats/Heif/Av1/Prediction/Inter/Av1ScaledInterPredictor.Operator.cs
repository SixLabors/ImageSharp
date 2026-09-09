// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines variable-phase reference-scaled prediction arithmetic.
/// </content>
internal static partial class Av1ScaledInterPredictor
{
    /// <summary>
    /// Defines source and convolution arithmetic shared by variable-phase scaled prediction.
    /// </summary>
    private interface IAv1ScaledArithmeticOperator
    {
        /// <summary>
        /// Loads one native source sample as a signed accumulator value.
        /// </summary>
        /// <typeparam name="T">The native sample storage type.</typeparam>
        /// <param name="source">The first native source sample.</param>
        /// <param name="index">The source sample offset.</param>
        /// <returns>The widened sample.</returns>
        public static abstract int Load<T>(ref T source, int index)
            where T : unmanaged;

        /// <summary>
        /// Accumulates one sample-coefficient product.
        /// </summary>
        /// <param name="accumulator">The current convolution sum.</param>
        /// <param name="sample">The source sample.</param>
        /// <param name="coefficient">The signed Q7 coefficient.</param>
        /// <returns>The updated convolution sum.</returns>
        public static abstract int MultiplyAdd(int accumulator, int sample, int coefficient);

        /// <summary>
        /// Accumulates four independent sample-coefficient products.
        /// </summary>
        /// <param name="accumulator">The current convolution sums.</param>
        /// <param name="samples">The source samples.</param>
        /// <param name="coefficients">The signed Q7 coefficients.</param>
        /// <returns>The updated convolution sums.</returns>
        public static abstract Vector128<int> MultiplyAdd(Vector128<int> accumulator, Vector128<int> samples, Vector128<int> coefficients);

        /// <summary>
        /// Accumulates eight independent sample-coefficient products.
        /// </summary>
        /// <param name="accumulator">The current convolution sums.</param>
        /// <param name="samples">The source samples.</param>
        /// <param name="coefficients">The signed Q7 coefficients.</param>
        /// <returns>The updated convolution sums.</returns>
        public static abstract Vector256<int> MultiplyAdd(Vector256<int> accumulator, Vector256<int> samples, Vector256<int> coefficients);

        /// <summary>
        /// Accumulates sixteen independent sample-coefficient products.
        /// </summary>
        /// <param name="accumulator">The current convolution sums.</param>
        /// <param name="samples">The source samples.</param>
        /// <param name="coefficients">The signed Q7 coefficients.</param>
        /// <returns>The updated convolution sums.</returns>
        public static abstract Vector512<int> MultiplyAdd(Vector512<int> accumulator, Vector512<int> samples, Vector512<int> coefficients);

        /// <summary>
        /// Convolves one intermediate sample column without hardware intrinsics.
        /// </summary>
        /// <param name="source">The first intermediate sample.</param>
        /// <param name="sourceStride">The distance between intermediate rows.</param>
        /// <param name="coefficients">The first signed Q7 coefficient.</param>
        /// <param name="coefficientCount">The number of active coefficients.</param>
        /// <returns>The exact convolution sum.</returns>
        public static abstract int Convolve(ref short source, int sourceStride, ref short coefficients, int coefficientCount);

        /// <summary>
        /// Convolves eight adjacent intermediate samples through a 128-bit lane group.
        /// </summary>
        /// <param name="source">The first intermediate sample.</param>
        /// <param name="sourceStride">The distance between intermediate rows.</param>
        /// <param name="sourceOffset">The first column offset.</param>
        /// <param name="coefficients">The first signed Q7 coefficient.</param>
        /// <param name="coefficientCount">The number of active coefficients.</param>
        /// <param name="initial">The initial convolution bias.</param>
        /// <param name="result0">Receives the first four completed sums.</param>
        /// <param name="result1">Receives the next four completed sums.</param>
        public static abstract void Convolve(
            ref short source,
            int sourceStride,
            nuint sourceOffset,
            ref short coefficients,
            int coefficientCount,
            Vector128<int> initial,
            out Vector128<int> result0,
            out Vector128<int> result1);

        /// <summary>
        /// Convolves sixteen adjacent intermediate samples through a 256-bit lane group.
        /// </summary>
        /// <param name="source">The first intermediate sample.</param>
        /// <param name="sourceStride">The distance between intermediate rows.</param>
        /// <param name="sourceOffset">The first column offset.</param>
        /// <param name="coefficients">The first signed Q7 coefficient.</param>
        /// <param name="coefficientCount">The number of active coefficients.</param>
        /// <param name="initial">The initial convolution bias.</param>
        /// <param name="result0">Receives the first eight completed sums.</param>
        /// <param name="result1">Receives the next eight completed sums.</param>
        public static abstract void Convolve(
            ref short source,
            int sourceStride,
            nuint sourceOffset,
            ref short coefficients,
            int coefficientCount,
            Vector256<int> initial,
            out Vector256<int> result0,
            out Vector256<int> result1);

        /// <summary>
        /// Convolves thirty-two adjacent intermediate samples through a 512-bit lane group.
        /// </summary>
        /// <param name="source">The first intermediate sample.</param>
        /// <param name="sourceStride">The distance between intermediate rows.</param>
        /// <param name="sourceOffset">The first column offset.</param>
        /// <param name="coefficients">The first signed Q7 coefficient.</param>
        /// <param name="coefficientCount">The number of active coefficients.</param>
        /// <param name="initial">The initial convolution bias.</param>
        /// <param name="result0">Receives the first sixteen completed sums.</param>
        /// <param name="result1">Receives the next sixteen completed sums.</param>
        public static abstract void Convolve(
            ref short source,
            int sourceStride,
            nuint sourceOffset,
            ref short coefficients,
            int coefficientCount,
            Vector512<int> initial,
            out Vector512<int> result0,
            out Vector512<int> result1);
    }

    /// <summary>
    /// Defines the output domain produced by variable-phase scaled prediction.
    /// </summary>
    private interface IAv1ScaledPredictionOperator
    {
        /// <summary>
        /// Gets the vertical convolution shift for the selected output domain.
        /// </summary>
        /// <param name="horizontalRound">The horizontal convolution shift.</param>
        /// <returns>The vertical convolution shift.</returns>
        public static abstract int GetVerticalRound(int horizontalRound);

        /// <summary>
        /// Gets the bias removed after vertical convolution for the selected output domain.
        /// </summary>
        /// <param name="offsetBits">The biased intermediate precision.</param>
        /// <param name="verticalRound">The vertical convolution shift.</param>
        /// <returns>The bias removed before storing the result.</returns>
        public static abstract int GetRoundOffset(int offsetBits, int verticalRound);

        /// <summary>
        /// Stores one completed prediction in the selected output domain.
        /// </summary>
        /// <typeparam name="T">The native sample storage type.</typeparam>
        /// <param name="destination">The first destination sample.</param>
        /// <param name="index">The destination offset.</param>
        /// <param name="value">The completed prediction.</param>
        /// <param name="bitDepth">The decoded sample precision.</param>
        public static abstract void Store<T>(ref T destination, int index, int value, int bitDepth)
            where T : unmanaged;

        /// <summary>
        /// Stores eight completed predictions in the selected output domain.
        /// </summary>
        /// <typeparam name="T">The native sample storage type.</typeparam>
        /// <param name="destination">The first destination sample.</param>
        /// <param name="index">The destination offset.</param>
        /// <param name="result0">The first four completed predictions.</param>
        /// <param name="result1">The next four completed predictions.</param>
        /// <param name="bitDepth">The decoded sample precision.</param>
        public static abstract void Store<T>(ref T destination, int index, Vector128<int> result0, Vector128<int> result1, int bitDepth)
            where T : unmanaged;

        /// <summary>
        /// Stores sixteen completed predictions in the selected output domain.
        /// </summary>
        /// <typeparam name="T">The native sample storage type.</typeparam>
        /// <param name="destination">The first destination sample.</param>
        /// <param name="index">The destination offset.</param>
        /// <param name="result0">The first eight completed predictions.</param>
        /// <param name="result1">The next eight completed predictions.</param>
        /// <param name="bitDepth">The decoded sample precision.</param>
        public static abstract void Store<T>(ref T destination, int index, Vector256<int> result0, Vector256<int> result1, int bitDepth)
            where T : unmanaged;

        /// <summary>
        /// Stores thirty-two completed predictions in the selected output domain.
        /// </summary>
        /// <typeparam name="T">The native sample storage type.</typeparam>
        /// <param name="destination">The first destination sample.</param>
        /// <param name="index">The destination offset.</param>
        /// <param name="result0">The first sixteen completed predictions.</param>
        /// <param name="result1">The next sixteen completed predictions.</param>
        /// <param name="bitDepth">The decoded sample precision.</param>
        public static abstract void Store<T>(ref T destination, int index, Vector512<int> result0, Vector512<int> result1, int bitDepth)
            where T : unmanaged;
    }

    /// <summary>
    /// Produces native-pixel scaled prediction for scalar and SIMD lane groups.
    /// </summary>
    private readonly struct NativeOperator : IAv1ScaledArithmeticOperator, IAv1ScaledPredictionOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetVerticalRound(int horizontalRound)
            => (2 * FilterBits) - horizontalRound;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetRoundOffset(int offsetBits, int verticalRound)
            => (1 << (offsetBits - verticalRound)) + (1 << (offsetBits - verticalRound - 1));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Load<T>(ref T source, int index)
            where T : unmanaged
        {
            // The only closed forms are byte and ushort. The JIT removes this storage choice from each specialization,
            // leaving the shared variable-phase traversal free of duplicate 8-bit and high-bit-depth implementations.
            if (typeof(T) == typeof(byte))
            {
                return Unsafe.Add(ref Unsafe.As<T, byte>(ref source), index);
            }

            return Unsafe.Add(ref Unsafe.As<T, ushort>(ref source), index);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MultiplyAdd(int accumulator, int sample, int coefficient)
            => accumulator + (sample * coefficient);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> MultiplyAdd(Vector128<int> accumulator, Vector128<int> samples, Vector128<int> coefficients)
            => accumulator + (samples * coefficients);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> MultiplyAdd(Vector256<int> accumulator, Vector256<int> samples, Vector256<int> coefficients)
            => accumulator + (samples * coefficients);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> MultiplyAdd(Vector512<int> accumulator, Vector512<int> samples, Vector512<int> coefficients)
            => accumulator + (samples * coefficients);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Convolve(ref short source, int sourceStride, ref short coefficients, int coefficientCount)
            => ConvolveScalar(ref source, sourceStride, ref coefficients, coefficientCount);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convolve(
            ref short source,
            int sourceStride,
            nuint sourceOffset,
            ref short coefficients,
            int coefficientCount,
            Vector128<int> initial,
            out Vector128<int> result0,
            out Vector128<int> result1)
            => Av1TranslationalInterPredictor.Convolve(
                ref source,
                sourceStride,
                sourceOffset,
                ref coefficients,
                coefficientCount,
                initial,
                out result0,
                out result1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convolve(
            ref short source,
            int sourceStride,
            nuint sourceOffset,
            ref short coefficients,
            int coefficientCount,
            Vector256<int> initial,
            out Vector256<int> result0,
            out Vector256<int> result1)
            => Av1TranslationalInterPredictor.Convolve(
                ref source,
                sourceStride,
                sourceOffset,
                ref coefficients,
                coefficientCount,
                initial,
                out result0,
                out result1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convolve(
            ref short source,
            int sourceStride,
            nuint sourceOffset,
            ref short coefficients,
            int coefficientCount,
            Vector512<int> initial,
            out Vector512<int> result0,
            out Vector512<int> result1)
            => Av1TranslationalInterPredictor.Convolve(
                ref source,
                sourceStride,
                sourceOffset,
                ref coefficients,
                coefficientCount,
                initial,
                out result0,
                out result1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<T>(ref T destination, int index, int value, int bitDepth)
            where T : unmanaged
        {
            if (typeof(T) == typeof(byte))
            {
                Unsafe.Add(ref Unsafe.As<T, byte>(ref destination), index) = (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
                return;
            }

            Unsafe.Add(ref Unsafe.As<T, ushort>(ref destination), index) = (ushort)Math.Clamp(value, 0, (1 << bitDepth) - 1);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<T>(ref T destination, int index, Vector128<int> result0, Vector128<int> result1, int bitDepth)
            where T : unmanaged
        {
            if (typeof(T) == typeof(byte))
            {
                PackBytes(result0, result1, Vector128<int>.Zero, Vector128<int>.Zero)
                    .GetLower()
                    .StoreUnsafe(ref Unsafe.As<T, byte>(ref destination), (nuint)index);

                return;
            }

            PackHighBitDepth(result0, result1, (1 << bitDepth) - 1)
                .StoreUnsafe(ref Unsafe.As<T, ushort>(ref destination), (nuint)index);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<T>(ref T destination, int index, Vector256<int> result0, Vector256<int> result1, int bitDepth)
            where T : unmanaged
        {
            if (typeof(T) == typeof(byte))
            {
                PackBytes(result0, result1, Vector256<int>.Zero, Vector256<int>.Zero)
                    .GetLower()
                    .StoreUnsafe(ref Unsafe.As<T, byte>(ref destination), (nuint)index);

                return;
            }

            PackHighBitDepth(result0, result1, (1 << bitDepth) - 1)
                .StoreUnsafe(ref Unsafe.As<T, ushort>(ref destination), (nuint)index);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<T>(ref T destination, int index, Vector512<int> result0, Vector512<int> result1, int bitDepth)
            where T : unmanaged
        {
            if (typeof(T) == typeof(byte))
            {
                PackBytes(result0, result1, Vector512<int>.Zero, Vector512<int>.Zero)
                    .GetLower()
                    .StoreUnsafe(ref Unsafe.As<T, byte>(ref destination), (nuint)index);

                return;
            }

            PackHighBitDepth(result0, result1, (1 << bitDepth) - 1)
                .StoreUnsafe(ref Unsafe.As<T, ushort>(ref destination), (nuint)index);
        }
    }

    /// <summary>
    /// Produces no-round compound intermediates for scalar and SIMD lane groups.
    /// </summary>
    private readonly struct CompoundOperator : IAv1ScaledPredictionOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetVerticalRound(int horizontalRound)
            => Av1CompoundInterPredictor.CompoundRound1Bits;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetRoundOffset(int offsetBits, int verticalRound) => 0;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<T>(ref T destination, int index, int value, int bitDepth)
            where T : unmanaged
        {
            // Compound entry points close T as ushort. Their no-round values retain the positive convolution bias,
            // so storing the normative unsigned intermediate needs neither pixel clipping nor a storage-type branch.
            Unsafe.Add(ref Unsafe.As<T, ushort>(ref destination), index) = (ushort)value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<T>(
            ref T destination,
            int index,
            Vector128<int> result0,
            Vector128<int> result1,
            int bitDepth)
            where T : unmanaged
            => Av1NonDirectionalIntraPredictorBase.Narrow(result0, result1)
                .AsUInt16()
                .StoreUnsafe(ref Unsafe.As<T, ushort>(ref destination), (nuint)index);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<T>(
            ref T destination,
            int index,
            Vector256<int> result0,
            Vector256<int> result1,
            int bitDepth)
            where T : unmanaged
            => Av1NonDirectionalIntraPredictorBase.Narrow(result0, result1)
                .AsUInt16()
                .StoreUnsafe(ref Unsafe.As<T, ushort>(ref destination), (nuint)index);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<T>(
            ref T destination,
            int index,
            Vector512<int> result0,
            Vector512<int> result1,
            int bitDepth)
            where T : unmanaged
            => Av1NonDirectionalIntraPredictorBase.Narrow(result0, result1)
                .AsUInt16()
                .StoreUnsafe(ref Unsafe.As<T, ushort>(ref destination), (nuint)index);
    }
}
