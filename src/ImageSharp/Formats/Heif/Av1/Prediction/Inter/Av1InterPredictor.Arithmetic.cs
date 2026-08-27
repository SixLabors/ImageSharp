// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Provides lane-wise convolution, rounding, clipping, and packing shared by every interpolation filter.
/// </content>
internal static partial class Av1InterPredictor
{
    /// <summary>
    /// Convolves sixteen adjacent 8-bit samples into four signed 32-bit accumulator vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Convolve(
        ref byte source,
        int tapStride,
        nuint column,
        ref short coefficients,
        int tapCount,
        Vector128<int> initial,
        out Vector128<int> result0,
        out Vector128<int> result1,
        out Vector128<int> result2,
        out Vector128<int> result3)
    {
        result0 = initial;
        result1 = initial;
        result2 = initial;
        result3 = initial;

        for (int tap = 0; tap < tapCount; tap++)
        {
            Vector128<byte> samples = Vector128.LoadUnsafe(ref Unsafe.Add(ref source, tap * tapStride), column);
            Av1IntraPredictorBase.Widen(samples, out Vector128<int> samples0, out Vector128<int> samples1, out Vector128<int> samples2, out Vector128<int> samples3);
            Vector128<int> coefficient = Vector128.Create((int)Unsafe.Add(ref coefficients, tap));

            // Each widened vector retains four consecutive source columns. Applying the same tap coefficient to all
            // lanes evaluates sixteen independent finite-impulse-response filters without a horizontal reduction.
            result0 += samples0 * coefficient;
            result1 += samples1 * coefficient;
            result2 += samples2 * coefficient;
            result3 += samples3 * coefficient;
        }
    }

    /// <summary>
    /// Convolves thirty-two adjacent 8-bit samples into four signed 32-bit accumulator vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Convolve(
        ref byte source,
        int tapStride,
        nuint column,
        ref short coefficients,
        int tapCount,
        Vector256<int> initial,
        out Vector256<int> result0,
        out Vector256<int> result1,
        out Vector256<int> result2,
        out Vector256<int> result3)
    {
        result0 = initial;
        result1 = initial;
        result2 = initial;
        result3 = initial;

        for (int tap = 0; tap < tapCount; tap++)
        {
            Vector256<byte> samples = Vector256.LoadUnsafe(ref Unsafe.Add(ref source, tap * tapStride), column);
            Av1IntraPredictorBase.Widen(samples, out Vector256<int> samples0, out Vector256<int> samples1, out Vector256<int> samples2, out Vector256<int> samples3);
            Vector256<int> coefficient = Vector256.Create((int)Unsafe.Add(ref coefficients, tap));

            result0 += samples0 * coefficient;
            result1 += samples1 * coefficient;
            result2 += samples2 * coefficient;
            result3 += samples3 * coefficient;
        }
    }

    /// <summary>
    /// Convolves sixty-four adjacent 8-bit samples into four signed 32-bit accumulator vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Convolve(
        ref byte source,
        int tapStride,
        nuint column,
        ref short coefficients,
        int tapCount,
        Vector512<int> initial,
        out Vector512<int> result0,
        out Vector512<int> result1,
        out Vector512<int> result2,
        out Vector512<int> result3)
    {
        result0 = initial;
        result1 = initial;
        result2 = initial;
        result3 = initial;

        for (int tap = 0; tap < tapCount; tap++)
        {
            Vector512<byte> samples = Vector512.LoadUnsafe(ref Unsafe.Add(ref source, tap * tapStride), column);
            Av1IntraPredictorBase.Widen(samples, out Vector512<int> samples0, out Vector512<int> samples1, out Vector512<int> samples2, out Vector512<int> samples3);
            Vector512<int> coefficient = Vector512.Create((int)Unsafe.Add(ref coefficients, tap));

            result0 += samples0 * coefficient;
            result1 += samples1 * coefficient;
            result2 += samples2 * coefficient;
            result3 += samples3 * coefficient;
        }
    }

    /// <summary>
    /// Convolves eight adjacent nonnegative 16-bit samples into two signed 32-bit accumulator vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Convolve(
        ref short source,
        int tapStride,
        nuint column,
        ref short coefficients,
        int tapCount,
        Vector128<int> initial,
        out Vector128<int> result0,
        out Vector128<int> result1)
    {
        result0 = initial;
        result1 = initial;

        for (int tap = 0; tap < tapCount; tap++)
        {
            Vector128<short> samples = Vector128.LoadUnsafe(ref Unsafe.Add(ref source, tap * tapStride), column);
            Av1IntraPredictorBase.Widen(samples, out Vector128<int> samples0, out Vector128<int> samples1);
            Vector128<int> coefficient = Vector128.Create((int)Unsafe.Add(ref coefficients, tap));

            // Reconstructed 10- and 12-bit samples and biased 2D intermediates are below short.MaxValue, so signed
            // widening preserves their values while allowing negative interpolation coefficients.
            result0 += samples0 * coefficient;
            result1 += samples1 * coefficient;
        }
    }

    /// <summary>
    /// Convolves sixteen adjacent nonnegative 16-bit samples into two signed 32-bit accumulator vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Convolve(
        ref short source,
        int tapStride,
        nuint column,
        ref short coefficients,
        int tapCount,
        Vector256<int> initial,
        out Vector256<int> result0,
        out Vector256<int> result1)
    {
        result0 = initial;
        result1 = initial;

        for (int tap = 0; tap < tapCount; tap++)
        {
            Vector256<short> samples = Vector256.LoadUnsafe(ref Unsafe.Add(ref source, tap * tapStride), column);
            Av1IntraPredictorBase.Widen(samples, out Vector256<int> samples0, out Vector256<int> samples1);
            Vector256<int> coefficient = Vector256.Create((int)Unsafe.Add(ref coefficients, tap));
            result0 += samples0 * coefficient;
            result1 += samples1 * coefficient;
        }
    }

    /// <summary>
    /// Convolves thirty-two adjacent nonnegative 16-bit samples into two signed 32-bit accumulator vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Convolve(
        ref short source,
        int tapStride,
        nuint column,
        ref short coefficients,
        int tapCount,
        Vector512<int> initial,
        out Vector512<int> result0,
        out Vector512<int> result1)
    {
        result0 = initial;
        result1 = initial;

        for (int tap = 0; tap < tapCount; tap++)
        {
            Vector512<short> samples = Vector512.LoadUnsafe(ref Unsafe.Add(ref source, tap * tapStride), column);
            Av1IntraPredictorBase.Widen(samples, out Vector512<int> samples0, out Vector512<int> samples1);
            Vector512<int> coefficient = Vector512.Create((int)Unsafe.Add(ref coefficients, tap));
            result0 += samples0 * coefficient;
            result1 += samples1 * coefficient;
        }
    }

    /// <summary>
    /// Applies AV1 power-of-two rounding to four-lane signed accumulators.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> RoundPowerOfTwo(Vector128<int> value, int bits)
        => bits == 0 ? value : (value + Vector128.Create(1 << (bits - 1))) >> bits;

    /// <summary>
    /// Applies AV1 power-of-two rounding to eight-lane signed accumulators.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> RoundPowerOfTwo(Vector256<int> value, int bits)
        => bits == 0 ? value : (value + Vector256.Create(1 << (bits - 1))) >> bits;

    /// <summary>
    /// Applies AV1 power-of-two rounding to sixteen-lane signed accumulators.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> RoundPowerOfTwo(Vector512<int> value, int bits)
        => bits == 0 ? value : (value + Vector512.Create(1 << (bits - 1))) >> bits;

    /// <summary>
    /// Clips and packs sixteen signed accumulators into 8-bit samples.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> PackBytes(Vector128<int> result0, Vector128<int> result1, Vector128<int> result2, Vector128<int> result3)
    {
        Vector128<int> maximum = Vector128.Create((int)byte.MaxValue);
        result0 = Vector128.Clamp(result0, Vector128<int>.Zero, maximum);
        result1 = Vector128.Clamp(result1, Vector128<int>.Zero, maximum);
        result2 = Vector128.Clamp(result2, Vector128<int>.Zero, maximum);
        result3 = Vector128.Clamp(result3, Vector128<int>.Zero, maximum);
        return Av1IntraPredictorBase.Narrow(result0, result1, result2, result3);
    }

    /// <summary>
    /// Clips and packs thirty-two signed accumulators into 8-bit samples.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> PackBytes(Vector256<int> result0, Vector256<int> result1, Vector256<int> result2, Vector256<int> result3)
    {
        Vector256<int> maximum = Vector256.Create((int)byte.MaxValue);
        result0 = Vector256.Clamp(result0, Vector256<int>.Zero, maximum);
        result1 = Vector256.Clamp(result1, Vector256<int>.Zero, maximum);
        result2 = Vector256.Clamp(result2, Vector256<int>.Zero, maximum);
        result3 = Vector256.Clamp(result3, Vector256<int>.Zero, maximum);
        return Av1IntraPredictorBase.Narrow(result0, result1, result2, result3);
    }

    /// <summary>
    /// Clips and packs sixty-four signed accumulators into 8-bit samples.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> PackBytes(Vector512<int> result0, Vector512<int> result1, Vector512<int> result2, Vector512<int> result3)
    {
        Vector512<int> maximum = Vector512.Create((int)byte.MaxValue);
        result0 = Vector512.Clamp(result0, Vector512<int>.Zero, maximum);
        result1 = Vector512.Clamp(result1, Vector512<int>.Zero, maximum);
        result2 = Vector512.Clamp(result2, Vector512<int>.Zero, maximum);
        result3 = Vector512.Clamp(result3, Vector512<int>.Zero, maximum);
        return Av1IntraPredictorBase.Narrow(result0, result1, result2, result3);
    }

    /// <summary>
    /// Clips and packs eight signed accumulators into high-bit-depth samples.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> PackHighBitDepth(Vector128<int> result0, Vector128<int> result1, int maximumValue)
    {
        Vector128<int> maximum = Vector128.Create(maximumValue);
        result0 = Vector128.Clamp(result0, Vector128<int>.Zero, maximum);
        result1 = Vector128.Clamp(result1, Vector128<int>.Zero, maximum);
        return Av1IntraPredictorBase.Narrow(result0, result1).AsUInt16();
    }

    /// <summary>
    /// Clips and packs sixteen signed accumulators into high-bit-depth samples.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ushort> PackHighBitDepth(Vector256<int> result0, Vector256<int> result1, int maximumValue)
    {
        Vector256<int> maximum = Vector256.Create(maximumValue);
        result0 = Vector256.Clamp(result0, Vector256<int>.Zero, maximum);
        result1 = Vector256.Clamp(result1, Vector256<int>.Zero, maximum);
        return Av1IntraPredictorBase.Narrow(result0, result1).AsUInt16();
    }

    /// <summary>
    /// Clips and packs thirty-two signed accumulators into high-bit-depth samples.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ushort> PackHighBitDepth(Vector512<int> result0, Vector512<int> result1, int maximumValue)
    {
        Vector512<int> maximum = Vector512.Create(maximumValue);
        result0 = Vector512.Clamp(result0, Vector512<int>.Zero, maximum);
        result1 = Vector512.Clamp(result1, Vector512<int>.Zero, maximum);
        return Av1IntraPredictorBase.Narrow(result0, result1).AsUInt16();
    }

    /// <summary>
    /// Computes one signed Q7 convolution sum from 8-bit samples.
    /// </summary>
    private static int ConvolveScalar(ref byte source, int sourceStride, ref short coefficients, int tapCount)
    {
        int sum = 0;
        for (int tap = 0; tap < tapCount; tap++)
        {
            sum += Unsafe.Add(ref coefficients, tap) * Unsafe.Add(ref source, tap * sourceStride);
        }

        return sum;
    }

    /// <summary>
    /// Computes one signed Q7 convolution sum from high-bit-depth samples.
    /// </summary>
    private static int ConvolveScalar(ref ushort source, int sourceStride, ref short coefficients, int tapCount)
    {
        int sum = 0;
        for (int tap = 0; tap < tapCount; tap++)
        {
            sum += Unsafe.Add(ref coefficients, tap) * Unsafe.Add(ref source, tap * sourceStride);
        }

        return sum;
    }

    /// <summary>
    /// Computes one signed Q7 convolution sum from biased intermediate samples.
    /// </summary>
    private static int ConvolveScalar(ref short source, int sourceStride, ref short coefficients, int tapCount)
    {
        int sum = 0;
        for (int tap = 0; tap < tapCount; tap++)
        {
            sum += Unsafe.Add(ref coefficients, tap) * Unsafe.Add(ref source, tap * sourceStride);
        }

        return sum;
    }

    /// <summary>
    /// Rounds an integer after division by a power of two using AV1's unsigned-bias rule.
    /// </summary>
    private static int RoundPowerOfTwo(int value, int bits) => bits == 0 ? value : (value + (1 << (bits - 1))) >> bits;
}
