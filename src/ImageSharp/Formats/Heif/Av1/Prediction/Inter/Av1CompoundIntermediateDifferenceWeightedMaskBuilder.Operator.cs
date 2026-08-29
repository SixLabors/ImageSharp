// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1InterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines difference-weighted mask generation from compound intermediates.
/// </content>
internal static partial class Av1CompoundIntermediateDifferenceWeightedMaskBuilder
{
    /// <summary>
    /// Defines difference-weighted mask generation for scalar and SIMD lane groups.
    /// </summary>
    private interface IAv1CompoundIntermediateDifferenceWeightedMaskOperator
    {
        /// <summary>
        /// Creates one difference-weighted mask value from compound intermediate samples.
        /// </summary>
        /// <param name="first">The first compound intermediate.</param>
        /// <param name="second">The second compound intermediate.</param>
        /// <param name="differenceRound">The difference scaling shift.</param>
        /// <param name="invert">Whether to invert the selected predictor.</param>
        /// <returns>The AV1 mask value.</returns>
        public static abstract byte CreateMask(ushort first, ushort second, int differenceRound, bool invert);

        /// <summary>
        /// Creates 128 bits of difference-weighted mask values from compound intermediate samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor intermediates.</param>
        /// <param name="first1">The upper first-predictor intermediates.</param>
        /// <param name="second0">The lower second-predictor intermediates.</param>
        /// <param name="second1">The upper second-predictor intermediates.</param>
        /// <param name="differenceRound">The difference scaling shift.</param>
        /// <param name="invert">Whether to invert the selected predictor.</param>
        /// <returns>The packed AV1 mask values.</returns>
        public static abstract Vector128<byte> CreateMask(
            Vector128<ushort> first0,
            Vector128<ushort> first1,
            Vector128<ushort> second0,
            Vector128<ushort> second1,
            int differenceRound,
            bool invert);

        /// <summary>
        /// Creates 256 bits of difference-weighted mask values from compound intermediate samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor intermediates.</param>
        /// <param name="first1">The upper first-predictor intermediates.</param>
        /// <param name="second0">The lower second-predictor intermediates.</param>
        /// <param name="second1">The upper second-predictor intermediates.</param>
        /// <param name="differenceRound">The difference scaling shift.</param>
        /// <param name="invert">Whether to invert the selected predictor.</param>
        /// <returns>The packed AV1 mask values.</returns>
        public static abstract Vector256<byte> CreateMask(
            Vector256<ushort> first0,
            Vector256<ushort> first1,
            Vector256<ushort> second0,
            Vector256<ushort> second1,
            int differenceRound,
            bool invert);

        /// <summary>
        /// Creates 512 bits of difference-weighted mask values from compound intermediate samples.
        /// </summary>
        /// <param name="first0">The lower first-predictor intermediates.</param>
        /// <param name="first1">The upper first-predictor intermediates.</param>
        /// <param name="second0">The lower second-predictor intermediates.</param>
        /// <param name="second1">The upper second-predictor intermediates.</param>
        /// <param name="differenceRound">The difference scaling shift.</param>
        /// <param name="invert">Whether to invert the selected predictor.</param>
        /// <returns>The packed AV1 mask values.</returns>
        public static abstract Vector512<byte> CreateMask(
            Vector512<ushort> first0,
            Vector512<ushort> first1,
            Vector512<ushort> second0,
            Vector512<ushort> second1,
            int differenceRound,
            bool invert);
    }

    /// <summary>
    /// Implements difference-weighted mask generation for scalar and SIMD lane groups.
    /// </summary>
    private readonly struct CompoundIntermediateDifferenceWeightedMaskOperator : IAv1CompoundIntermediateDifferenceWeightedMaskOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte CreateMask(ushort first, ushort second, int differenceRound, bool invert)
        {
            int difference = RoundPowerOfTwo(Math.Abs(first - second), differenceRound);
            int alpha = Math.Min(MaximumMaskAlpha, 38 + (difference >> 4));
            return (byte)(invert ? MaximumMaskAlpha - alpha : alpha);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> CreateMask(
            Vector128<ushort> first0,
            Vector128<ushort> first1,
            Vector128<ushort> second0,
            Vector128<ushort> second1,
            int differenceRound,
            bool invert)
            => Vector128.Narrow(
                CreateMask(first0, second0, differenceRound, invert),
                CreateMask(first1, second1, differenceRound, invert));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> CreateMask(
            Vector256<ushort> first0,
            Vector256<ushort> first1,
            Vector256<ushort> second0,
            Vector256<ushort> second1,
            int differenceRound,
            bool invert)
            => Vector256.Narrow(
                CreateMask(first0, second0, differenceRound, invert),
                CreateMask(first1, second1, differenceRound, invert));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> CreateMask(
            Vector512<ushort> first0,
            Vector512<ushort> first1,
            Vector512<ushort> second0,
            Vector512<ushort> second1,
            int differenceRound,
            bool invert)
            => Vector512.Narrow(
                CreateMask(first0, second0, differenceRound, invert),
                CreateMask(first1, second1, differenceRound, invert));

        /// <summary>
        /// Creates 128-bit unpacked mask values without losing the required pre-alpha rounding.
        /// </summary>
        private static Vector128<ushort> CreateMask(Vector128<ushort> first, Vector128<ushort> second, int differenceRound, bool invert)
        {
            Vector128<ushort> difference = Vector128.Max(first, second) - Vector128.Min(first, second);
            Vector128<int> lower = CreateMaskAlpha(Vector128.WidenLower(difference).AsInt32(), differenceRound, invert);
            Vector128<int> upper = CreateMaskAlpha(Vector128.WidenUpper(difference).AsInt32(), differenceRound, invert);
            return Vector128.Narrow(lower, upper).AsUInt16();
        }

        /// <summary>
        /// Creates 256-bit unpacked mask values without losing the required pre-alpha rounding.
        /// </summary>
        private static Vector256<ushort> CreateMask(Vector256<ushort> first, Vector256<ushort> second, int differenceRound, bool invert)
        {
            Vector256<ushort> difference = Vector256.Max(first, second) - Vector256.Min(first, second);
            Vector256<int> lower = CreateMaskAlpha(Vector256.WidenLower(difference).AsInt32(), differenceRound, invert);
            Vector256<int> upper = CreateMaskAlpha(Vector256.WidenUpper(difference).AsInt32(), differenceRound, invert);
            return Vector256.Narrow(lower, upper).AsUInt16();
        }

        /// <summary>
        /// Creates 512-bit unpacked mask values without losing the required pre-alpha rounding.
        /// </summary>
        private static Vector512<ushort> CreateMask(Vector512<ushort> first, Vector512<ushort> second, int differenceRound, bool invert)
        {
            Vector512<ushort> difference = Vector512.Max(first, second) - Vector512.Min(first, second);
            Vector512<int> lower = CreateMaskAlpha(Vector512.WidenLower(difference).AsInt32(), differenceRound, invert);
            Vector512<int> upper = CreateMaskAlpha(Vector512.WidenUpper(difference).AsInt32(), differenceRound, invert);
            return Vector512.Narrow(lower, upper).AsUInt16();
        }

        /// <summary>
        /// Converts 128-bit intermediate differences to the decoded type-38 mask range.
        /// </summary>
        private static Vector128<int> CreateMaskAlpha(Vector128<int> difference, int differenceRound, bool invert)
        {
            if (differenceRound != 0)
            {
                difference = (difference + Vector128.Create(1 << (differenceRound - 1))) >> differenceRound;
            }

            Vector128<int> maximum = Vector128.Create(MaximumMaskAlpha);
            Vector128<int> alpha = Vector128.Min(maximum, (difference >> 4) + Vector128.Create(38));
            return invert ? maximum - alpha : alpha;
        }

        /// <summary>
        /// Converts 256-bit intermediate differences to the decoded type-38 mask range.
        /// </summary>
        private static Vector256<int> CreateMaskAlpha(Vector256<int> difference, int differenceRound, bool invert)
        {
            if (differenceRound != 0)
            {
                difference = (difference + Vector256.Create(1 << (differenceRound - 1))) >> differenceRound;
            }

            Vector256<int> maximum = Vector256.Create(MaximumMaskAlpha);
            Vector256<int> alpha = Vector256.Min(maximum, (difference >> 4) + Vector256.Create(38));
            return invert ? maximum - alpha : alpha;
        }

        /// <summary>
        /// Converts 512-bit intermediate differences to the decoded type-38 mask range.
        /// </summary>
        private static Vector512<int> CreateMaskAlpha(Vector512<int> difference, int differenceRound, bool invert)
        {
            if (differenceRound != 0)
            {
                difference = (difference + Vector512.Create(1 << (differenceRound - 1))) >> differenceRound;
            }

            Vector512<int> maximum = Vector512.Create(MaximumMaskAlpha);
            Vector512<int> alpha = Vector512.Min(maximum, (difference >> 4) + Vector512.Create(38));
            return invert ? maximum - alpha : alpha;
        }
    }
}
