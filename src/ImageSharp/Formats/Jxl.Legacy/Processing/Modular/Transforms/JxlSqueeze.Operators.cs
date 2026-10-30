// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

internal static partial class JxlSqueeze
{
    /// <summary>
    /// Bounds arguments only for the 32-bit SIMD calculation. Other arguments use the exact 64-bit scalar calculation.
    /// </summary>
    /// <remarks>
    /// With M = 2^29 - 1, differences have magnitude at most 2M and doubled slope limits are at most 4M + 1.
    /// The division shortcut adds at most 8M/3 + 2 before shifting. Residual addition and reconstructed pairs also fit Int32.
    /// This is not a sample limit: lanes outside this range are corrected before their results are stored or used for prediction.
    /// </remarks>
    private const int MaximumFastArgument = int.MaxValue / 4;

    /// <summary>
    /// The fixed-point multiplier ceil(2^32 / 3). The high word of the widened product gives floor(value / 3) in the fast range.
    /// </summary>
    private const int DivisionByThreeMultiplier = 0x55555556;

    /// <summary>
    /// Restores one pair from its average, residual, next average and preceding restored sample.
    /// </summary>
    private readonly struct InverseSqueezeOperator
    {
        /// <summary>
        /// Restores a pair with native 64-bit intermediate arithmetic and Int32 sample storage.
        /// </summary>
        /// <param name="average">The current pair average.</param>
        /// <param name="residual">The encoded difference after tendency prediction.</param>
        /// <param name="nextAverage">The following pair average, or the current average at an even edge.</param>
        /// <param name="previous">The preceding restored odd sample, or the average at the first pair.</param>
        /// <param name="even">The first restored sample.</param>
        /// <param name="odd">The second restored sample, used as the next pair's predecessor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(int average, int residual, int nextAverage, int previous, out int even, out int odd)
        {
            long difference = residual + SmoothTendency(previous, average, nextAverage);

            long first = average + (difference / 2);
            even = unchecked((int)first);
            odd = unchecked((int)(first - difference));
        }

        /// <inheritdoc cref="Invoke(int, int, int, int, out int, out int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector128<int> average,
            Vector128<int> residual,
            Vector128<int> nextAverage,
            Vector128<int> previous,
            out Vector128<int> even,
            out Vector128<int> odd)
        {
            Vector128<int> difference = residual + SmoothTendency(previous, average, nextAverage);

            // Logical sign extraction adds one only to a negative difference. The following arithmetic shift therefore truncates division toward zero.
            even = average + ((difference + (difference.AsUInt32() >> 31).AsInt32()) >> 1);
            odd = even - difference;
            uint wideLanes = GetWideArithmeticMask(average, residual, nextAverage, previous);

            while (wideLanes != 0)
            {
                // Only these lanes need wide arithmetic. Narrow each stored result before it becomes the next predecessor, as the scalar decoder does.
                int lane = BitOperations.TrailingZeroCount(wideLanes);

                this.Invoke(average.GetElement(lane), residual.GetElement(lane), nextAverage.GetElement(lane), previous.GetElement(lane), out int a, out int b);
                even = even.WithElement(lane, a);
                odd = odd.WithElement(lane, b);
                wideLanes &= wideLanes - 1;
            }
        }

        /// <inheritdoc cref="Invoke(int, int, int, int, out int, out int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector256<int> average,
            Vector256<int> residual,
            Vector256<int> nextAverage,
            Vector256<int> previous,
            out Vector256<int> even,
            out Vector256<int> odd)
        {
            Vector256<int> difference = residual + SmoothTendency(previous, average, nextAverage);

            // Logical sign extraction adds one only to a negative difference. The following arithmetic shift therefore truncates division toward zero.
            even = average + ((difference + (difference.AsUInt32() >> 31).AsInt32()) >> 1);
            odd = even - difference;
            uint wideLanes = GetWideArithmeticMask(average, residual, nextAverage, previous);

            while (wideLanes != 0)
            {
                // Only these lanes need wide arithmetic. Narrow each stored result before it becomes the next predecessor, as the scalar decoder does.
                int lane = BitOperations.TrailingZeroCount(wideLanes);

                this.Invoke(average.GetElement(lane), residual.GetElement(lane), nextAverage.GetElement(lane), previous.GetElement(lane), out int a, out int b);
                even = even.WithElement(lane, a);
                odd = odd.WithElement(lane, b);
                wideLanes &= wideLanes - 1;
            }
        }

        /// <inheritdoc cref="Invoke(int, int, int, int, out int, out int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector512<int> average,
            Vector512<int> residual,
            Vector512<int> nextAverage,
            Vector512<int> previous,
            out Vector512<int> even,
            out Vector512<int> odd)
        {
            Vector512<int> difference = residual + SmoothTendency(previous, average, nextAverage);

            // Logical sign extraction adds one only to a negative difference. The following arithmetic shift therefore truncates division toward zero.
            even = average + ((difference + (difference.AsUInt32() >> 31).AsInt32()) >> 1);
            odd = even - difference;
            uint wideLanes = GetWideArithmeticMask(average, residual, nextAverage, previous);

            while (wideLanes != 0)
            {
                // Only these lanes need wide arithmetic. Narrow each stored result before it becomes the next predecessor, as the scalar decoder does.
                int lane = BitOperations.TrailingZeroCount(wideLanes);

                this.Invoke(average.GetElement(lane), residual.GetElement(lane), nextAverage.GetElement(lane), previous.GetElement(lane), out int a, out int b);
                even = even.WithElement(lane, a);
                odd = odd.WithElement(lane, b);
                wideLanes &= wideLanes - 1;
            }
        }
    }

    /// <summary>
    /// Calculates the encoded residual from original pair samples and neighboring averages.
    /// </summary>
    private readonly struct ForwardSqueezeOperator
    {
        /// <summary>
        /// Calculates one residual with 64-bit intermediates, then stores its Int32 representation.
        /// </summary>
        /// <param name="even">The first original sample.</param>
        /// <param name="odd">The second original sample.</param>
        /// <param name="average">The already calculated pair average.</param>
        /// <param name="nextAverage">The following pair average, or the current average at an even edge.</param>
        /// <param name="previous">The preceding original odd sample, or the average at the first pair.</param>
        /// <returns>The encoded difference minus the predicted tendency.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Invoke(int even, int odd, int average, int nextAverage, int previous)
            => unchecked((int)(((long)even - odd) - SmoothTendency(previous, average, nextAverage)));

        /// <inheritdoc cref="Invoke(int, int, int, int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<int> Invoke(Vector128<int> even, Vector128<int> odd, Vector128<int> average, Vector128<int> nextAverage, Vector128<int> previous)
        {
            Vector128<int> result = even - odd - SmoothTendency(previous, average, nextAverage);

            uint wideLanes = GetWideArithmeticMask(even, odd, nextAverage, previous);

            while (wideLanes != 0)
            {
                // Average lies between even and odd, so the four checked arguments also bound it. Other lanes use the same wide equation as scalar traversal.
                int lane = BitOperations.TrailingZeroCount(wideLanes);

                int residual = this.Invoke(
                    even.GetElement(lane),
                    odd.GetElement(lane),
                    average.GetElement(lane),
                    nextAverage.GetElement(lane),
                    previous.GetElement(lane));
                result = result.WithElement(lane, residual);

                wideLanes &= wideLanes - 1;
            }

            return result;
        }

        /// <inheritdoc cref="Invoke(int, int, int, int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<int> Invoke(Vector256<int> even, Vector256<int> odd, Vector256<int> average, Vector256<int> nextAverage, Vector256<int> previous)
        {
            Vector256<int> result = even - odd - SmoothTendency(previous, average, nextAverage);

            uint wideLanes = GetWideArithmeticMask(even, odd, nextAverage, previous);

            while (wideLanes != 0)
            {
                // Average lies between even and odd, so the four checked arguments also bound it. Other lanes use the same wide equation as scalar traversal.
                int lane = BitOperations.TrailingZeroCount(wideLanes);

                int residual = this.Invoke(
                    even.GetElement(lane),
                    odd.GetElement(lane),
                    average.GetElement(lane),
                    nextAverage.GetElement(lane),
                    previous.GetElement(lane));
                result = result.WithElement(lane, residual);

                wideLanes &= wideLanes - 1;
            }

            return result;
        }

        /// <inheritdoc cref="Invoke(int, int, int, int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<int> Invoke(Vector512<int> even, Vector512<int> odd, Vector512<int> average, Vector512<int> nextAverage, Vector512<int> previous)
        {
            Vector512<int> result = even - odd - SmoothTendency(previous, average, nextAverage);

            uint wideLanes = GetWideArithmeticMask(even, odd, nextAverage, previous);

            while (wideLanes != 0)
            {
                // Average lies between even and odd, so the four checked arguments also bound it. Other lanes use the same wide equation as scalar traversal.
                int lane = BitOperations.TrailingZeroCount(wideLanes);

                int residual = this.Invoke(
                    even.GetElement(lane),
                    odd.GetElement(lane),
                    average.GetElement(lane),
                    nextAverage.GetElement(lane),
                    previous.GetElement(lane));
                result = result.WithElement(lane, residual);

                wideLanes &= wideLanes - 1;
            }

            return result;
        }
    }

    /// <summary>
    /// Finds lanes whose four arguments require 64-bit arithmetic.
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <param name="third">The third argument.</param>
    /// <param name="fourth">The fourth argument.</param>
    /// <returns>One set bit per lane outside the proved fast range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetWideArithmeticMask(Vector128<int> first, Vector128<int> second, Vector128<int> third, Vector128<int> fourth)
    {
        // M consists of 29 low set bits. OR of unsigned magnitudes is at most M exactly when every argument is in range.
        // Abs(Int32.MinValue) retains its high bit; interpreting it as unsigned correctly selects the wide calculation.
        Vector128<uint> magnitudes = Vector128.Abs(first).AsUInt32() | Vector128.Abs(second).AsUInt32()
            | Vector128.Abs(third).AsUInt32() | Vector128.Abs(fourth).AsUInt32();

        return Vector128.ExtractMostSignificantBits(Vector128.GreaterThan(magnitudes, Vector128.Create((uint)MaximumFastArgument)));
    }

    /// <summary>
    /// Calculates pair averages without overflowing the sum of two Int32 samples.
    /// </summary>
    /// <param name="first">The first samples.</param>
    /// <param name="second">The second samples.</param>
    /// <returns>The same biased integer averages as the scalar 64-bit equation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Average(Vector128<int> first, Vector128<int> second)
    {
        // (a & b) + ((a ^ b) >> 1) gives floor((a+b)/2) without forming the possibly overflowing sum.
        // An odd sum rounds up only when the first sample is greater, matching (a+b+(a>b))/2 with an arithmetic shift.
        Vector128<int> different = first ^ second;
        return (first & second) + (different >> 1) + (Vector128.GreaterThan(first, second) & different & Vector128<int>.One);
    }

    /// <summary>
    /// Calculates the native integer tendency for independent sample lanes.
    /// </summary>
    /// <param name="previous">The preceding original or restored samples.</param>
    /// <param name="average">The current pair averages.</param>
    /// <param name="nextAverage">The following pair averages.</param>
    /// <returns>The predicted difference. Callers correct lanes outside MaximumFastArgument with the 64-bit scalar equation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> SmoothTendency(Vector128<int> previous, Vector128<int> average, Vector128<int> nextAverage)
    {
        Vector128<int> before = previous - average;
        Vector128<int> after = average - nextAverage;
        Vector128<int> absoluteBefore = Vector128.Abs(before);

        Vector128<int> absoluteAfter = Vector128.Abs(after);
        Vector128<int> multiplier = Vector128.Create(DivisionByThreeMultiplier);

        Vector128<long> evenProducts = Vector128_.MultiplyWideningEven(absoluteBefore, multiplier);

        // Move each odd word into its preceding even word before widening. Endianness changes which half of a 64-bit lane holds that word.
        Vector128<int> oddWords = BitConverter.IsLittleEndian
            ? (absoluteBefore.AsUInt64() >> 32).AsInt32()
            : (absoluteBefore.AsUInt64() << 32).AsInt32();

        Vector128<long> oddProducts = Vector128_.MultiplyWideningEven(oddWords, multiplier);

        // The high product words give floor(abs(before)/3) within the fast range. Narrow yields all even quotients, then all odd quotients;
        // a complete-register shuffle restores their original sample order. This avoids multiplying Int32 values before widening.
        Vector128<int> quotient = Vector128.Narrow(evenProducts >> 32, oddProducts >> 32);
        quotient = Vector128.ShuffleNative(quotient, Vector128.Create(0, 2, 1, 3));
        Vector128<int> magnitude = (quotient + Vector128.Abs(previous - nextAverage) + Vector128.Create(2)) >> 2;
        Vector128<int> twiceBefore = absoluteBefore << 1;
        magnitude = Vector128.ConditionalSelect(
            Vector128.GreaterThan(magnitude, twiceBefore + (magnitude & Vector128<int>.One)),
            twiceBefore + Vector128<int>.One,
            magnitude);

        Vector128<int> twiceAfter = absoluteAfter << 1;
        magnitude = Vector128.ConditionalSelect(Vector128.GreaterThan(magnitude + (magnitude & Vector128<int>.One), twiceAfter), twiceAfter, magnitude);

        // Only a strict change in slope direction suppresses the tendency. Equal neighbors still use the native parity-sensitive limits above.
        Vector128<int> nonmonotonic = ~Vector128.Equals(before, Vector128<int>.Zero) & ~Vector128.Equals(after, Vector128<int>.Zero)
            & Vector128.LessThan(before ^ after, Vector128<int>.Zero);

        Vector128<int> signed = Vector128.ConditionalSelect(Vector128.LessThan(previous, nextAverage), -magnitude, magnitude);

        return signed & ~nonmonotonic;
    }

    /// <summary>
    /// Finds lanes whose four arguments require 64-bit arithmetic.
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <param name="third">The third argument.</param>
    /// <param name="fourth">The fourth argument.</param>
    /// <returns>One set bit per lane outside the proved fast range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetWideArithmeticMask(Vector256<int> first, Vector256<int> second, Vector256<int> third, Vector256<int> fourth)
    {
        // M consists of 29 low set bits. OR of unsigned magnitudes is at most M exactly when every argument is in range.
        // Abs(Int32.MinValue) retains its high bit; interpreting it as unsigned correctly selects the wide calculation.
        Vector256<uint> magnitudes = Vector256.Abs(first).AsUInt32() | Vector256.Abs(second).AsUInt32()
            | Vector256.Abs(third).AsUInt32() | Vector256.Abs(fourth).AsUInt32();

        return Vector256.ExtractMostSignificantBits(Vector256.GreaterThan(magnitudes, Vector256.Create((uint)MaximumFastArgument)));
    }

    /// <summary>
    /// Calculates pair averages without overflowing the sum of two Int32 samples.
    /// </summary>
    /// <param name="first">The first samples.</param>
    /// <param name="second">The second samples.</param>
    /// <returns>The same biased integer averages as the scalar 64-bit equation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> Average(Vector256<int> first, Vector256<int> second)
    {
        // (a & b) + ((a ^ b) >> 1) gives floor((a+b)/2) without forming the possibly overflowing sum.
        // An odd sum rounds up only when the first sample is greater, matching (a+b+(a>b))/2 with an arithmetic shift.
        Vector256<int> different = first ^ second;
        return (first & second) + (different >> 1) + (Vector256.GreaterThan(first, second) & different & Vector256<int>.One);
    }

    /// <summary>
    /// Calculates the native integer tendency for independent sample lanes.
    /// </summary>
    /// <param name="previous">The preceding original or restored samples.</param>
    /// <param name="average">The current pair averages.</param>
    /// <param name="nextAverage">The following pair averages.</param>
    /// <returns>The predicted difference. Callers correct lanes outside MaximumFastArgument with the 64-bit scalar equation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> SmoothTendency(Vector256<int> previous, Vector256<int> average, Vector256<int> nextAverage)
    {
        Vector256<int> before = previous - average;
        Vector256<int> after = average - nextAverage;
        Vector256<int> absoluteBefore = Vector256.Abs(before);

        Vector256<int> absoluteAfter = Vector256.Abs(after);
        Vector256<int> multiplier = Vector256.Create(DivisionByThreeMultiplier);

        Vector256<long> evenProducts = Vector256_.MultiplyWideningEven(absoluteBefore, multiplier);

        // Move each odd word into its preceding even word before widening. Endianness changes which half of a 64-bit lane holds that word.
        Vector256<int> oddWords = BitConverter.IsLittleEndian
            ? (absoluteBefore.AsUInt64() >> 32).AsInt32()
            : (absoluteBefore.AsUInt64() << 32).AsInt32();

        Vector256<long> oddProducts = Vector256_.MultiplyWideningEven(oddWords, multiplier);

        // The high product words give floor(abs(before)/3) within the fast range. Narrow yields all even quotients, then all odd quotients;
        // a complete-register shuffle restores their original sample order. This avoids multiplying Int32 values before widening.
        Vector256<int> quotient = Vector256.Narrow(evenProducts >> 32, oddProducts >> 32);
        quotient = Vector256.ShuffleNative(quotient, Vector256.Create(0, 4, 1, 5, 2, 6, 3, 7));
        Vector256<int> magnitude = (quotient + Vector256.Abs(previous - nextAverage) + Vector256.Create(2)) >> 2;
        Vector256<int> twiceBefore = absoluteBefore << 1;
        magnitude = Vector256.ConditionalSelect(
            Vector256.GreaterThan(magnitude, twiceBefore + (magnitude & Vector256<int>.One)),
            twiceBefore + Vector256<int>.One,
            magnitude);

        Vector256<int> twiceAfter = absoluteAfter << 1;
        magnitude = Vector256.ConditionalSelect(Vector256.GreaterThan(magnitude + (magnitude & Vector256<int>.One), twiceAfter), twiceAfter, magnitude);

        // Only a strict change in slope direction suppresses the tendency. Equal neighbors still use the native parity-sensitive limits above.
        Vector256<int> nonmonotonic = ~Vector256.Equals(before, Vector256<int>.Zero) & ~Vector256.Equals(after, Vector256<int>.Zero)
            & Vector256.LessThan(before ^ after, Vector256<int>.Zero);

        Vector256<int> signed = Vector256.ConditionalSelect(Vector256.LessThan(previous, nextAverage), -magnitude, magnitude);

        return signed & ~nonmonotonic;
    }

    /// <summary>
    /// Finds lanes whose four arguments require 64-bit arithmetic.
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <param name="third">The third argument.</param>
    /// <param name="fourth">The fourth argument.</param>
    /// <returns>One set bit per lane outside the proved fast range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetWideArithmeticMask(Vector512<int> first, Vector512<int> second, Vector512<int> third, Vector512<int> fourth)
    {
        // M consists of 29 low set bits. OR of unsigned magnitudes is at most M exactly when every argument is in range.
        // Abs(Int32.MinValue) retains its high bit; interpreting it as unsigned correctly selects the wide calculation.
        Vector512<uint> magnitudes = Vector512.Abs(first).AsUInt32() | Vector512.Abs(second).AsUInt32()
            | Vector512.Abs(third).AsUInt32() | Vector512.Abs(fourth).AsUInt32();

        return (uint)Vector512.ExtractMostSignificantBits(Vector512.GreaterThan(magnitudes, Vector512.Create((uint)MaximumFastArgument)));
    }

    /// <summary>
    /// Calculates pair averages without overflowing the sum of two Int32 samples.
    /// </summary>
    /// <param name="first">The first samples.</param>
    /// <param name="second">The second samples.</param>
    /// <returns>The same biased integer averages as the scalar 64-bit equation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> Average(Vector512<int> first, Vector512<int> second)
    {
        // (a & b) + ((a ^ b) >> 1) gives floor((a+b)/2) without forming the possibly overflowing sum.
        // An odd sum rounds up only when the first sample is greater, matching (a+b+(a>b))/2 with an arithmetic shift.
        Vector512<int> different = first ^ second;
        return (first & second) + (different >> 1) + (Vector512.GreaterThan(first, second) & different & Vector512<int>.One);
    }

    /// <summary>
    /// Calculates the native integer tendency for independent sample lanes.
    /// </summary>
    /// <param name="previous">The preceding original or restored samples.</param>
    /// <param name="average">The current pair averages.</param>
    /// <param name="nextAverage">The following pair averages.</param>
    /// <returns>The predicted difference. Callers correct lanes outside MaximumFastArgument with the 64-bit scalar equation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> SmoothTendency(Vector512<int> previous, Vector512<int> average, Vector512<int> nextAverage)
    {
        Vector512<int> before = previous - average;
        Vector512<int> after = average - nextAverage;
        Vector512<int> absoluteBefore = Vector512.Abs(before);

        Vector512<int> absoluteAfter = Vector512.Abs(after);
        Vector512<int> multiplier = Vector512.Create(DivisionByThreeMultiplier);

        Vector512<long> evenProducts = Vector512_.MultiplyWideningEven(absoluteBefore, multiplier);

        // Move each odd word into its preceding even word before widening. Endianness changes which half of a 64-bit lane holds that word.
        Vector512<int> oddWords = BitConverter.IsLittleEndian
            ? (absoluteBefore.AsUInt64() >> 32).AsInt32()
            : (absoluteBefore.AsUInt64() << 32).AsInt32();

        Vector512<long> oddProducts = Vector512_.MultiplyWideningEven(oddWords, multiplier);

        // The high product words give floor(abs(before)/3) within the fast range. Narrow yields all even quotients, then all odd quotients;
        // a complete-register shuffle restores their original sample order. This avoids multiplying Int32 values before widening.
        Vector512<int> quotient = Vector512.Narrow(evenProducts >> 32, oddProducts >> 32);
        quotient = Vector512.ShuffleNative(quotient, Vector512.Create(0, 8, 1, 9, 2, 10, 3, 11, 4, 12, 5, 13, 6, 14, 7, 15));
        Vector512<int> magnitude = (quotient + Vector512.Abs(previous - nextAverage) + Vector512.Create(2)) >> 2;
        Vector512<int> twiceBefore = absoluteBefore << 1;
        magnitude = Vector512.ConditionalSelect(
            Vector512.GreaterThan(magnitude, twiceBefore + (magnitude & Vector512<int>.One)),
            twiceBefore + Vector512<int>.One,
            magnitude);

        Vector512<int> twiceAfter = absoluteAfter << 1;
        magnitude = Vector512.ConditionalSelect(Vector512.GreaterThan(magnitude + (magnitude & Vector512<int>.One), twiceAfter), twiceAfter, magnitude);

        // Only a strict change in slope direction suppresses the tendency. Equal neighbors still use the native parity-sensitive limits above.
        Vector512<int> nonmonotonic = ~Vector512.Equals(before, Vector512<int>.Zero) & ~Vector512.Equals(after, Vector512<int>.Zero)
            & Vector512.LessThan(before ^ after, Vector512<int>.Zero);

        Vector512<int> signed = Vector512.ConditionalSelect(Vector512.LessThan(previous, nextAverage), -magnitude, magnitude);

        return signed & ~nonmonotonic;
    }
}
