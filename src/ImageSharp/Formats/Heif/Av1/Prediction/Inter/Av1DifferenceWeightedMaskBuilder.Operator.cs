// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1InterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines difference-weighted compound mask arithmetic.
/// </content>
internal static partial class Av1DifferenceWeightedMaskBuilder
{
    /// <summary>
    /// Defines difference-weighted compound mask generation for scalar and SIMD lane groups.
    /// </summary>
    private interface IAv1DifferenceWeightedMaskOperator
    {
        /// <summary>
        /// Creates one mask value from two 8-bit predictor samples.
        /// </summary>
        /// <param name="first">The first predictor sample.</param>
        /// <param name="second">The second predictor sample.</param>
        /// <param name="shift">The difference scaling shift.</param>
        /// <param name="invert">Whether to invert the selected predictor.</param>
        /// <returns>The AV1 mask value.</returns>
        public static abstract byte Create(byte first, byte second, int shift, bool invert);

        /// <summary>
        /// Creates one mask value from two high-bit-depth predictor samples.
        /// </summary>
        /// <param name="first">The first predictor sample.</param>
        /// <param name="second">The second predictor sample.</param>
        /// <param name="shift">The difference scaling shift.</param>
        /// <param name="invert">Whether to invert the selected predictor.</param>
        /// <returns>The AV1 mask value.</returns>
        public static abstract byte Create(ushort first, ushort second, int shift, bool invert);

        /// <summary>
        /// Creates 128 bits of mask values from 8-bit predictor samples.
        /// </summary>
        /// <param name="first">The first predictor samples.</param>
        /// <param name="second">The second predictor samples.</param>
        /// <param name="shift">The difference scaling shift.</param>
        /// <param name="invert">Whether to invert the selected predictor.</param>
        /// <returns>The AV1 mask values.</returns>
        public static abstract Vector128<byte> Create(Vector128<byte> first, Vector128<byte> second, int shift, bool invert);

        /// <summary>
        /// Creates 256 bits of mask values from 8-bit predictor samples.
        /// </summary>
        /// <param name="first">The first predictor samples.</param>
        /// <param name="second">The second predictor samples.</param>
        /// <param name="shift">The difference scaling shift.</param>
        /// <param name="invert">Whether to invert the selected predictor.</param>
        /// <returns>The AV1 mask values.</returns>
        public static abstract Vector256<byte> Create(Vector256<byte> first, Vector256<byte> second, int shift, bool invert);

        /// <summary>
        /// Creates 512 bits of mask values from 8-bit predictor samples.
        /// </summary>
        /// <param name="first">The first predictor samples.</param>
        /// <param name="second">The second predictor samples.</param>
        /// <param name="shift">The difference scaling shift.</param>
        /// <param name="invert">Whether to invert the selected predictor.</param>
        /// <returns>The AV1 mask values.</returns>
        public static abstract Vector512<byte> Create(Vector512<byte> first, Vector512<byte> second, int shift, bool invert);

        /// <summary>
        /// Creates 128 bits of packed mask values from high-bit-depth predictor samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor samples.</param>
        /// <param name="first1">The upper first-predictor samples.</param>
        /// <param name="second0">The lower second-predictor samples.</param>
        /// <param name="second1">The upper second-predictor samples.</param>
        /// <param name="shift">The difference scaling shift.</param>
        /// <param name="invert">Whether to invert the selected predictor.</param>
        /// <returns>The packed AV1 mask values.</returns>
        public static abstract Vector128<byte> Create(
            Vector128<ushort> first0,
            Vector128<ushort> first1,
            Vector128<ushort> second0,
            Vector128<ushort> second1,
            int shift,
            bool invert);

        /// <summary>
        /// Creates 256 bits of packed mask values from high-bit-depth predictor samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor samples.</param>
        /// <param name="first1">The upper first-predictor samples.</param>
        /// <param name="second0">The lower second-predictor samples.</param>
        /// <param name="second1">The upper second-predictor samples.</param>
        /// <param name="shift">The difference scaling shift.</param>
        /// <param name="invert">Whether to invert the selected predictor.</param>
        /// <returns>The packed AV1 mask values.</returns>
        public static abstract Vector256<byte> Create(
            Vector256<ushort> first0,
            Vector256<ushort> first1,
            Vector256<ushort> second0,
            Vector256<ushort> second1,
            int shift,
            bool invert);

        /// <summary>
        /// Creates 512 bits of packed mask values from high-bit-depth predictor samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor samples.</param>
        /// <param name="first1">The upper first-predictor samples.</param>
        /// <param name="second0">The lower second-predictor samples.</param>
        /// <param name="second1">The upper second-predictor samples.</param>
        /// <param name="shift">The difference scaling shift.</param>
        /// <param name="invert">Whether to invert the selected predictor.</param>
        /// <returns>The packed AV1 mask values.</returns>
        public static abstract Vector512<byte> Create(
            Vector512<ushort> first0,
            Vector512<ushort> first1,
            Vector512<ushort> second0,
            Vector512<ushort> second1,
            int shift,
            bool invert);
    }

    /// <summary>
    /// Implements AV1 difference-weighted mask generation for scalar and SIMD lane groups.
    /// </summary>
    private readonly struct DifferenceWeightedMaskOperator : IAv1DifferenceWeightedMaskOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Create(byte first, byte second, int shift, bool invert)
            => Create((ushort)Math.Abs(first - second), shift, invert);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Create(ushort first, ushort second, int shift, bool invert)
            => Create((ushort)Math.Abs(first - second), shift, invert);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Create(Vector128<byte> first, Vector128<byte> second, int shift, bool invert)
        {
            Vector128<byte> difference = Vector128.Max(first, second) - Vector128.Min(first, second);
            Vector128<ushort> lower = CreateAlpha(Vector128.WidenLower(difference), shift, invert);
            Vector128<ushort> upper = CreateAlpha(Vector128.WidenUpper(difference), shift, invert);
            return Vector128.Narrow(lower, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Create(Vector256<byte> first, Vector256<byte> second, int shift, bool invert)
        {
            Vector256<byte> difference = Vector256.Max(first, second) - Vector256.Min(first, second);
            Vector256<ushort> lower = CreateAlpha(Vector256.WidenLower(difference), shift, invert);
            Vector256<ushort> upper = CreateAlpha(Vector256.WidenUpper(difference), shift, invert);
            return Vector256.Narrow(lower, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Create(Vector512<byte> first, Vector512<byte> second, int shift, bool invert)
        {
            Vector512<byte> difference = Vector512.Max(first, second) - Vector512.Min(first, second);
            Vector512<ushort> lower = CreateAlpha(Vector512.WidenLower(difference), shift, invert);
            Vector512<ushort> upper = CreateAlpha(Vector512.WidenUpper(difference), shift, invert);
            return Vector512.Narrow(lower, upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Create(
            Vector128<ushort> first0,
            Vector128<ushort> first1,
            Vector128<ushort> second0,
            Vector128<ushort> second1,
            int shift,
            bool invert)
            => Vector128.Narrow(
                CreateAlpha(Vector128.Max(first0, second0) - Vector128.Min(first0, second0), shift, invert),
                CreateAlpha(Vector128.Max(first1, second1) - Vector128.Min(first1, second1), shift, invert));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Create(
            Vector256<ushort> first0,
            Vector256<ushort> first1,
            Vector256<ushort> second0,
            Vector256<ushort> second1,
            int shift,
            bool invert)
            => Vector256.Narrow(
                CreateAlpha(Vector256.Max(first0, second0) - Vector256.Min(first0, second0), shift, invert),
                CreateAlpha(Vector256.Max(first1, second1) - Vector256.Min(first1, second1), shift, invert));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Create(
            Vector512<ushort> first0,
            Vector512<ushort> first1,
            Vector512<ushort> second0,
            Vector512<ushort> second1,
            int shift,
            bool invert)
            => Vector512.Narrow(
                CreateAlpha(Vector512.Max(first0, second0) - Vector512.Min(first0, second0), shift, invert),
                CreateAlpha(Vector512.Max(first1, second1) - Vector512.Min(first1, second1), shift, invert));

        /// <summary>
        /// Creates one mask value from an absolute predictor difference.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte Create(ushort difference, int shift, bool invert)
        {
            int alpha = Math.Min(MaximumMaskAlpha, 38 + (difference >> shift));
            return (byte)(invert ? MaximumMaskAlpha - alpha : alpha);
        }

        /// <summary>
        /// Creates 128-bit vectors of unpacked mask values from absolute predictor differences.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<ushort> CreateAlpha(Vector128<ushort> difference, int shift, bool invert)
        {
            Vector128<ushort> maximum = Vector128.Create((ushort)MaximumMaskAlpha);
            Vector128<ushort> alpha = Vector128.Min(maximum, (difference >> shift) + Vector128.Create((ushort)38));
            return invert ? maximum - alpha : alpha;
        }

        /// <summary>
        /// Creates 256-bit vectors of unpacked mask values from absolute predictor differences.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<ushort> CreateAlpha(Vector256<ushort> difference, int shift, bool invert)
        {
            Vector256<ushort> maximum = Vector256.Create((ushort)MaximumMaskAlpha);
            Vector256<ushort> alpha = Vector256.Min(maximum, (difference >> shift) + Vector256.Create((ushort)38));
            return invert ? maximum - alpha : alpha;
        }

        /// <summary>
        /// Creates 512-bit vectors of unpacked mask values from absolute predictor differences.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<ushort> CreateAlpha(Vector512<ushort> difference, int shift, bool invert)
        {
            Vector512<ushort> maximum = Vector512.Create((ushort)MaximumMaskAlpha);
            Vector512<ushort> alpha = Vector512.Min(maximum, (difference >> shift) + Vector512.Create((ushort)38));
            return invert ? maximum - alpha : alpha;
        }
    }
}
