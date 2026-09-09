// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Implements symmetric Wiener arithmetic at scalar and portable SIMD widths.
/// </content>
internal static partial class Av1WienerFilter
{
    /// <summary>
    /// Evaluates the seven-tap kernel independently for every destination sample.
    /// </summary>
    internal readonly struct WienerOperator : IAv1WienerOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Filter(
            int sample0,
            int sample1,
            int sample2,
            int sample3,
            int sample4,
            int sample5,
            int sample6,
            int coefficient0,
            int coefficient1,
            int coefficient2,
            int coefficient3,
            int bias,
            int roundBits,
            int maximum)
        {
            int sum = ((sample0 + sample6) * coefficient0) + ((sample1 + sample5) * coefficient1) +
                ((sample2 + sample4) * coefficient2) + (sample3 * coefficient3) + bias;

            return (ushort)Math.Clamp(sum >> roundBits, 0, maximum);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> Filter(
            Vector128<ushort> sample0,
            Vector128<ushort> sample1,
            Vector128<ushort> sample2,
            Vector128<ushort> sample3,
            Vector128<ushort> sample4,
            Vector128<ushort> sample5,
            Vector128<ushort> sample6,
            Vector128<int> coefficient0,
            Vector128<int> coefficient1,
            Vector128<int> coefficient2,
            Vector128<int> coefficient3,
            Vector128<int> bias,
            int roundBits,
            Vector128<int> maximum)
        {
            // Each 16-bit lane denotes one output column. Widen before summing symmetric pairs:
            // a vertical intermediate can reach 32767, so its pair must remain unsigned until widened.
            // Lower and upper halves stay in increasing column order throughout all 32-bit arithmetic.
            Vector128<int> lower =
                ((Vector128.WidenLower(sample0).AsInt32() + Vector128.WidenLower(sample6).AsInt32()) * coefficient0) +
                ((Vector128.WidenLower(sample1).AsInt32() + Vector128.WidenLower(sample5).AsInt32()) * coefficient1) +
                ((Vector128.WidenLower(sample2).AsInt32() + Vector128.WidenLower(sample4).AsInt32()) * coefficient2) +
                (Vector128.WidenLower(sample3).AsInt32() * coefficient3) + bias;

            Vector128<int> upper =
                ((Vector128.WidenUpper(sample0).AsInt32() + Vector128.WidenUpper(sample6).AsInt32()) * coefficient0) +
                ((Vector128.WidenUpper(sample1).AsInt32() + Vector128.WidenUpper(sample5).AsInt32()) * coefficient1) +
                ((Vector128.WidenUpper(sample2).AsInt32() + Vector128.WidenUpper(sample4).AsInt32()) * coefficient2) +
                (Vector128.WidenUpper(sample3).AsInt32() * coefficient3) + bias;

            // Bias already contains the pass offset and half-unit rounding term. Arithmetic shifting
            // preserves negative convolution results until clipping; narrowing then cannot wrap.
            lower = Vector128.Clamp(Vector128.ShiftRightArithmetic(lower, roundBits), Vector128<int>.Zero, maximum);
            upper = Vector128.Clamp(Vector128.ShiftRightArithmetic(upper, roundBits), Vector128<int>.Zero, maximum);
            return Vector128.Narrow(lower.AsUInt32(), upper.AsUInt32());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> Filter(
            Vector256<ushort> sample0,
            Vector256<ushort> sample1,
            Vector256<ushort> sample2,
            Vector256<ushort> sample3,
            Vector256<ushort> sample4,
            Vector256<ushort> sample5,
            Vector256<ushort> sample6,
            Vector256<int> coefficient0,
            Vector256<int> coefficient1,
            Vector256<int> coefficient2,
            Vector256<int> coefficient3,
            Vector256<int> bias,
            int roundBits,
            Vector256<int> maximum)
        {
            // Each 16-bit lane denotes one output column. Widen before summing symmetric pairs:
            // a vertical intermediate can reach 32767, so its pair must remain unsigned until widened.
            // Lower and upper halves stay in increasing column order throughout all 32-bit arithmetic.
            Vector256<int> lower =
                ((Vector256.WidenLower(sample0).AsInt32() + Vector256.WidenLower(sample6).AsInt32()) * coefficient0) +
                ((Vector256.WidenLower(sample1).AsInt32() + Vector256.WidenLower(sample5).AsInt32()) * coefficient1) +
                ((Vector256.WidenLower(sample2).AsInt32() + Vector256.WidenLower(sample4).AsInt32()) * coefficient2) +
                (Vector256.WidenLower(sample3).AsInt32() * coefficient3) + bias;

            Vector256<int> upper =
                ((Vector256.WidenUpper(sample0).AsInt32() + Vector256.WidenUpper(sample6).AsInt32()) * coefficient0) +
                ((Vector256.WidenUpper(sample1).AsInt32() + Vector256.WidenUpper(sample5).AsInt32()) * coefficient1) +
                ((Vector256.WidenUpper(sample2).AsInt32() + Vector256.WidenUpper(sample4).AsInt32()) * coefficient2) +
                (Vector256.WidenUpper(sample3).AsInt32() * coefficient3) + bias;

            // Bias already contains the pass offset and half-unit rounding term. Arithmetic shifting
            // preserves negative convolution results until clipping; narrowing then cannot wrap.
            lower = Vector256.Clamp(Vector256.ShiftRightArithmetic(lower, roundBits), Vector256<int>.Zero, maximum);
            upper = Vector256.Clamp(Vector256.ShiftRightArithmetic(upper, roundBits), Vector256<int>.Zero, maximum);
            return Vector256.Narrow(lower.AsUInt32(), upper.AsUInt32());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> Filter(
            Vector512<ushort> sample0,
            Vector512<ushort> sample1,
            Vector512<ushort> sample2,
            Vector512<ushort> sample3,
            Vector512<ushort> sample4,
            Vector512<ushort> sample5,
            Vector512<ushort> sample6,
            Vector512<int> coefficient0,
            Vector512<int> coefficient1,
            Vector512<int> coefficient2,
            Vector512<int> coefficient3,
            Vector512<int> bias,
            int roundBits,
            Vector512<int> maximum)
        {
            // Each 16-bit lane denotes one output column. Widen before summing symmetric pairs:
            // a vertical intermediate can reach 32767, so its pair must remain unsigned until widened.
            // Lower and upper halves stay in increasing column order throughout all 32-bit arithmetic.
            Vector512<int> lower =
                ((Vector512.WidenLower(sample0).AsInt32() + Vector512.WidenLower(sample6).AsInt32()) * coefficient0) +
                ((Vector512.WidenLower(sample1).AsInt32() + Vector512.WidenLower(sample5).AsInt32()) * coefficient1) +
                ((Vector512.WidenLower(sample2).AsInt32() + Vector512.WidenLower(sample4).AsInt32()) * coefficient2) +
                (Vector512.WidenLower(sample3).AsInt32() * coefficient3) + bias;

            Vector512<int> upper =
                ((Vector512.WidenUpper(sample0).AsInt32() + Vector512.WidenUpper(sample6).AsInt32()) * coefficient0) +
                ((Vector512.WidenUpper(sample1).AsInt32() + Vector512.WidenUpper(sample5).AsInt32()) * coefficient1) +
                ((Vector512.WidenUpper(sample2).AsInt32() + Vector512.WidenUpper(sample4).AsInt32()) * coefficient2) +
                (Vector512.WidenUpper(sample3).AsInt32() * coefficient3) + bias;

            // Bias already contains the pass offset and half-unit rounding term. Arithmetic shifting
            // preserves negative convolution results until clipping; narrowing then cannot wrap.
            lower = Vector512.Clamp(Vector512.ShiftRightArithmetic(lower, roundBits), Vector512<int>.Zero, maximum);
            upper = Vector512.Clamp(Vector512.ShiftRightArithmetic(upper, roundBits), Vector512<int>.Zero, maximum);
            return Vector512.Narrow(lower.AsUInt32(), upper.AsUInt32());
        }
    }
}
