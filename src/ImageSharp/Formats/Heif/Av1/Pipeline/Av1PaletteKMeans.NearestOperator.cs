// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Implements the nearest-color search of one-dimensional palette clustering.
/// </content>
internal static partial class Av1PaletteKMeans
{
    /// <summary>
    /// Selects the palette color with the smallest absolute difference from a sample.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The distance is the absolute difference, not its square, which is what the reference
    /// compares. Squaring is left to the caller, which needs the squares only for the total
    /// distortion and not for the comparison.
    /// </para>
    /// <para>
    /// The comparison is strict, so a sample equally close to two colors keeps the first of them.
    /// The reference reaches the same choice, because its scalar loop replaces the best color only
    /// on a strictly smaller distance.
    /// </para>
    /// </remarks>
    private readonly struct NearestOperator : IAv1PaletteNearestOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Nearest(short sample, ReadOnlySpan<short> centroids, out int distance)
        {
            distance = Math.Abs(sample - centroids[0]);
            int index = 0;
            for (int candidate = 1; candidate < centroids.Length; candidate++)
            {
                int current = Math.Abs(sample - centroids[candidate]);
                if (current < distance)
                {
                    distance = current;
                    index = candidate;
                }
            }

            return index;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Nearest(
            Vector128<short> sample,
            ReadOnlySpan<short> centroids,
            out Vector128<short> distance)
        {
            // Each lane is one independent sample. A palette color is broadcast to every lane, so
            // the sweep is eight independent searches that share one sequence of comparisons.
            distance = Vector128.Abs(sample - Vector128.Create(centroids[0]));
            Vector128<short> index = Vector128<short>.Zero;
            for (int candidate = 1; candidate < centroids.Length; candidate++)
            {
                Vector128<short> current = Vector128.Abs(sample - Vector128.Create(centroids[candidate]));

                // The mask is all ones only where the new color is strictly nearer, so a tie keeps
                // the color already held and matches the scalar comparison.
                Vector128<short> replace = Vector128.LessThan(current, distance);
                distance = Vector128.ConditionalSelect(replace, current, distance);
                index = Vector128.ConditionalSelect(replace, Vector128.Create((short)candidate), index);
            }

            return index;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Nearest(
            Vector256<short> sample,
            ReadOnlySpan<short> centroids,
            out Vector256<short> distance)
        {
            // Sixteen independent samples, with the lane layout and the arithmetic of the 128-bit
            // overload.
            distance = Vector256.Abs(sample - Vector256.Create(centroids[0]));
            Vector256<short> index = Vector256<short>.Zero;
            for (int candidate = 1; candidate < centroids.Length; candidate++)
            {
                Vector256<short> current = Vector256.Abs(sample - Vector256.Create(centroids[candidate]));
                Vector256<short> replace = Vector256.LessThan(current, distance);
                distance = Vector256.ConditionalSelect(replace, current, distance);
                index = Vector256.ConditionalSelect(replace, Vector256.Create((short)candidate), index);
            }

            return index;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Nearest(
            Vector512<short> sample,
            ReadOnlySpan<short> centroids,
            out Vector512<short> distance)
        {
            // Thirty-two independent samples. The comparison produces an all-ones or all-zero lane
            // mask on every supported path, including AVX-512, where the JIT lowers the mask
            // register back to a vector for the following select.
            distance = Vector512.Abs(sample - Vector512.Create(centroids[0]));
            Vector512<short> index = Vector512<short>.Zero;
            for (int candidate = 1; candidate < centroids.Length; candidate++)
            {
                Vector512<short> current = Vector512.Abs(sample - Vector512.Create(centroids[candidate]));
                Vector512<short> replace = Vector512.LessThan(current, distance);
                distance = Vector512.ConditionalSelect(replace, current, distance);
                index = Vector512.ConditionalSelect(replace, Vector512.Create((short)candidate), index);
            }

            return index;
        }
    }
}
