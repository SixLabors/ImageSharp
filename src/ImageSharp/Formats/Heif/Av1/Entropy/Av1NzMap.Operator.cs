// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <content>
/// Defines the arithmetic contract consumed by the shared context traversal.
/// </content>
internal static partial class Av1NzMap
{
    /// <summary>
    /// Reduces the levels of the forward neighbors of one coefficient to a magnitude band.
    /// </summary>
    /// <remarks>
    /// Every overload describes the same lane-wise reduction. A level is clipped to three, the five
    /// clipped levels are summed, the sum is halved with rounding, and the result is clipped to
    /// four. The traversal chooses which five neighbors to pass; the bands are the same for every
    /// transform class.
    /// </remarks>
    internal interface IAv1NzMapCountOperator
    {
        /// <summary>
        /// Reduces the neighbors of one coefficient.
        /// </summary>
        /// <param name="level0">The level of the first neighbor.</param>
        /// <param name="level1">The level of the second neighbor.</param>
        /// <param name="level2">The level of the third neighbor.</param>
        /// <param name="level3">The level of the fourth neighbor.</param>
        /// <param name="level4">The level of the fifth neighbor.</param>
        /// <returns>The magnitude band, from zero through four.</returns>
        public static abstract byte Count(int level0, int level1, int level2, int level3, int level4);

        /// <summary>
        /// Reduces the neighbors of sixteen coefficients.
        /// </summary>
        /// <param name="level0">The levels of the first neighbors.</param>
        /// <param name="level1">The levels of the second neighbors.</param>
        /// <param name="level2">The levels of the third neighbors.</param>
        /// <param name="level3">The levels of the fourth neighbors.</param>
        /// <param name="level4">The levels of the fifth neighbors.</param>
        /// <returns>The magnitude bands, each from zero through four.</returns>
        public static abstract Vector128<byte> Count(
            Vector128<byte> level0,
            Vector128<byte> level1,
            Vector128<byte> level2,
            Vector128<byte> level3,
            Vector128<byte> level4);

        /// <summary>
        /// Reduces the neighbors of thirty-two coefficients.
        /// </summary>
        /// <param name="level0">The levels of the first neighbors.</param>
        /// <param name="level1">The levels of the second neighbors.</param>
        /// <param name="level2">The levels of the third neighbors.</param>
        /// <param name="level3">The levels of the fourth neighbors.</param>
        /// <param name="level4">The levels of the fifth neighbors.</param>
        /// <returns>The magnitude bands, each from zero through four.</returns>
        public static abstract Vector256<byte> Count(
            Vector256<byte> level0,
            Vector256<byte> level1,
            Vector256<byte> level2,
            Vector256<byte> level3,
            Vector256<byte> level4);

        /// <summary>
        /// Reduces the neighbors of sixty-four coefficients.
        /// </summary>
        /// <param name="level0">The levels of the first neighbors.</param>
        /// <param name="level1">The levels of the second neighbors.</param>
        /// <param name="level2">The levels of the third neighbors.</param>
        /// <param name="level3">The levels of the fourth neighbors.</param>
        /// <param name="level4">The levels of the fifth neighbors.</param>
        /// <returns>The magnitude bands, each from zero through four.</returns>
        public static abstract Vector512<byte> Count(
            Vector512<byte> level0,
            Vector512<byte> level1,
            Vector512<byte> level2,
            Vector512<byte> level3,
            Vector512<byte> level4);
    }

    /// <summary>
    /// Sums five clipped neighbor levels, halves the sum with rounding, and clips it to four bands.
    /// </summary>
    /// <remarks>
    /// Reference: get_nz_mag() and get_nz_map_ctx_from_stats().
    /// </remarks>
    private readonly struct CountOperator : IAv1NzMapCountOperator
    {
        /// <summary>
        /// The largest level that one neighbor contributes.
        /// </summary>
        private const byte LevelLimit = 3;

        /// <summary>
        /// The largest magnitude band.
        /// </summary>
        private const byte BandLimit = 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Count(int level0, int level1, int level2, int level3, int level4)
        {
            int total = Math.Min(level0, LevelLimit) + Math.Min(level1, LevelLimit) +
                Math.Min(level2, LevelLimit) + Math.Min(level3, LevelLimit) + Math.Min(level4, LevelLimit);

            return (byte)Math.Min((total + 1) >> 1, BandLimit);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Count(
            Vector128<byte> level0,
            Vector128<byte> level1,
            Vector128<byte> level2,
            Vector128<byte> level3,
            Vector128<byte> level4)
        {
            // Each lane is one independent coefficient. Five clipped levels reach fifteen, so the
            // sum stays inside a byte lane and no widening is needed.
            Vector128<byte> limit = Vector128.Create(LevelLimit);
            Vector128<byte> total = Vector128.Min(level0, limit) + Vector128.Min(level1, limit) +
                Vector128.Min(level2, limit) + Vector128.Min(level3, limit) + Vector128.Min(level4, limit);

            return Vector128.Min(Halve(total), Vector128.Create(BandLimit));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Count(
            Vector256<byte> level0,
            Vector256<byte> level1,
            Vector256<byte> level2,
            Vector256<byte> level3,
            Vector256<byte> level4)
        {
            Vector256<byte> limit = Vector256.Create(LevelLimit);
            Vector256<byte> total = Vector256.Min(level0, limit) + Vector256.Min(level1, limit) +
                Vector256.Min(level2, limit) + Vector256.Min(level3, limit) + Vector256.Min(level4, limit);

            return Vector256.Min(Halve(total), Vector256.Create(BandLimit));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Count(
            Vector512<byte> level0,
            Vector512<byte> level1,
            Vector512<byte> level2,
            Vector512<byte> level3,
            Vector512<byte> level4)
        {
            Vector512<byte> limit = Vector512.Create(LevelLimit);
            Vector512<byte> total = Vector512.Min(level0, limit) + Vector512.Min(level1, limit) +
                Vector512.Min(level2, limit) + Vector512.Min(level3, limit) + Vector512.Min(level4, limit);

            return Vector512.Min(Halve(total), Vector512.Create(BandLimit));
        }

        /// <summary>
        /// Halves sixteen byte lanes with rounding.
        /// </summary>
        /// <param name="total">The sums, each at most fifteen.</param>
        /// <returns>The halved sums.</returns>
        /// <remarks>
        /// No instruction set shifts byte lanes, so the shift runs on sixteen-bit lanes instead.
        /// That carries the low bit of each odd byte into the top bit of the byte below it, and the
        /// mask clears those carried bits. Every other bit is the byte-lane result, because a sum of
        /// at most sixteen leaves the upper bits of its byte clear.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> Halve(Vector128<byte> total)
            => ((total + Vector128<byte>.One).AsUInt16() >>> 1).AsByte() & Vector128.Create((byte)0x7F);

        /// <summary>
        /// Halves thirty-two byte lanes with rounding.
        /// </summary>
        /// <param name="total">The sums, each at most fifteen.</param>
        /// <returns>The halved sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<byte> Halve(Vector256<byte> total)
            => ((total + Vector256<byte>.One).AsUInt16() >>> 1).AsByte() & Vector256.Create((byte)0x7F);

        /// <summary>
        /// Halves sixty-four byte lanes with rounding.
        /// </summary>
        /// <param name="total">The sums, each at most fifteen.</param>
        /// <returns>The halved sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<byte> Halve(Vector512<byte> total)
            => ((total + Vector512<byte>.One).AsUInt16() >>> 1).AsByte() & Vector512.Create((byte)0x7F);
    }
}
