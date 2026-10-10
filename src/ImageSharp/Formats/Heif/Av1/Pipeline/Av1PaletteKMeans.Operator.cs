// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the arithmetic contract consumed by the shared assignment traversal.
/// </content>
internal static partial class Av1PaletteKMeans
{
    /// <summary>
    /// Finds the palette color nearest to a sample.
    /// </summary>
    /// <remarks>
    /// Every overload describes the same lane-wise search. The palette holds at most eight colors, so the search is a
    /// short sweep over the colors. The comparison is strict, so the first color wins a tie.
    /// </remarks>
    internal interface IAv1PaletteNearestOperator
    {
        /// <summary>
        /// Finds the nearest color of one sample.
        /// </summary>
        /// <param name="sample">The sample.</param>
        /// <param name="centroids">The palette colors.</param>
        /// <param name="distance">Receives the distance to the nearest color.</param>
        /// <returns>The index of the nearest color.</returns>
        public static abstract int Nearest(short sample, ReadOnlySpan<short> centroids, out int distance);

        /// <summary>
        /// Finds the nearest color of eight samples.
        /// </summary>
        /// <param name="sample">The samples.</param>
        /// <param name="centroids">The palette colors.</param>
        /// <param name="distance">Receives the distance to the nearest color of each sample.</param>
        /// <returns>The index of the nearest color of each sample.</returns>
        public static abstract Vector128<short> Nearest(
            Vector128<short> sample,
            ReadOnlySpan<short> centroids,
            out Vector128<short> distance);

        /// <summary>
        /// Finds the nearest color of sixteen samples.
        /// </summary>
        /// <param name="sample">The samples.</param>
        /// <param name="centroids">The palette colors.</param>
        /// <param name="distance">Receives the distance to the nearest color of each sample.</param>
        /// <returns>The index of the nearest color of each sample.</returns>
        public static abstract Vector256<short> Nearest(
            Vector256<short> sample,
            ReadOnlySpan<short> centroids,
            out Vector256<short> distance);

        /// <summary>
        /// Finds the nearest color of thirty-two samples.
        /// </summary>
        /// <param name="sample">The samples.</param>
        /// <param name="centroids">The palette colors.</param>
        /// <param name="distance">Receives the distance to the nearest color of each sample.</param>
        /// <returns>The index of the nearest color of each sample.</returns>
        public static abstract Vector512<short> Nearest(
            Vector512<short> sample,
            ReadOnlySpan<short> centroids,
            out Vector512<short> distance);
    }

    /// <summary>
    /// Assigns every sample of a block with the arithmetic of a closed search operator.
    /// </summary>
    /// <typeparam name="TOperator">The nearest-color search.</typeparam>
    /// <remarks>
    /// One lane is one sample, and the operator broadcasts each palette color. Thus the traversal is a plain walk of the
    /// samples at descending register widths with a scalar tail. The index of a color is less than the palette limit of
    /// eight, so the narrowing of the index lanes to bytes is exact.
    /// </remarks>
    private static class Assign<TOperator>
        where TOperator : struct, IAv1PaletteNearestOperator
    {
        /// <summary>
        /// Assigns every sample to its nearest palette color.
        /// </summary>
        /// <param name="samples">The active block samples.</param>
        /// <param name="centroids">The candidate palette colors.</param>
        /// <param name="indices">The destination palette indices.</param>
        /// <returns>The sum of the squared sample-to-color distances.</returns>
        public static long Apply(ReadOnlySpan<short> samples, ReadOnlySpan<short> centroids, Span<byte> indices)
        {
            // A multiply-add of the distances with themselves adds the squares of two adjacent lanes into one 32-bit lane.
            // These sums widen into 64-bit lane totals, and one reduction at the end gives the distortion.
            // The sum of two squared twelve-bit distances fits in 32 bits. The total of a whole block needs 64 bits.
            ref short sampleBase = ref MemoryMarshal.GetReference(samples);
            ref byte indexBase = ref MemoryMarshal.GetReference(indices);

            int offset = 0;
            long distortion = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<long> total = Vector512<long>.Zero;
                int vectorEnd = samples.Length - Vector512<short>.Count;
                for (; offset <= vectorEnd; offset += Vector512<short>.Count)
                {
                    Vector512<short> index = TOperator.Nearest(
                        Vector512.LoadUnsafe(ref sampleBase, (nuint)offset),
                        centroids,
                        out Vector512<short> distance);

                    Vector512.Narrow(index.AsUInt16(), Vector512<ushort>.Zero).GetLower()
                        .StoreUnsafe(ref indexBase, (nuint)offset);

                    (Vector512<long> lower, Vector512<long> upper) = Vector512.Widen(Vector512_.MultiplyAddAdjacent(distance, distance));
                    total += lower + upper;
                }

                distortion += Vector512.Sum(total);
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<long> total = Vector256<long>.Zero;
                int vectorEnd = samples.Length - Vector256<short>.Count;
                for (; offset <= vectorEnd; offset += Vector256<short>.Count)
                {
                    Vector256<short> index = TOperator.Nearest(
                        Vector256.LoadUnsafe(ref sampleBase, (nuint)offset),
                        centroids,
                        out Vector256<short> distance);

                    Vector256.Narrow(index.AsUInt16(), Vector256<ushort>.Zero).GetLower()
                        .StoreUnsafe(ref indexBase, (nuint)offset);

                    (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(Vector256_.MultiplyAddAdjacent(distance, distance));
                    total += lower + upper;
                }

                distortion += Vector256.Sum(total);
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<long> total = Vector128<long>.Zero;
                int vectorEnd = samples.Length - Vector128<short>.Count;
                for (; offset <= vectorEnd; offset += Vector128<short>.Count)
                {
                    Vector128<short> index = TOperator.Nearest(
                        Vector128.LoadUnsafe(ref sampleBase, (nuint)offset),
                        centroids,
                        out Vector128<short> distance);

                    Vector128.Narrow(index.AsUInt16(), Vector128<ushort>.Zero).GetLower()
                        .StoreUnsafe(ref indexBase, (nuint)offset);

                    (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(Vector128_.MultiplyAddAdjacent(distance, distance));
                    total += lower + upper;
                }

                distortion += Vector128.Sum(total);
            }

            for (; offset < samples.Length; offset++)
            {
                indices[offset] = (byte)TOperator.Nearest(samples[offset], centroids, out int distance);
                distortion += (long)distance * distance;
            }

            return distortion;
        }
    }
}
