// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the arithmetic contract consumed by the shared assignment traversal.
/// </content>
internal static partial class Av1PaletteKMeans2D
{
    /// <summary>
    /// Finds the paired palette color nearest to a pair of samples.
    /// </summary>
    /// <remarks>
    /// Every overload describes the same lane-wise search. The distance is the squared distance in the plane of the two
    /// chroma components. It reaches about 33 million for twelve-bit samples, so the search runs on thirty-two-bit lanes.
    /// The samples arrive as sixteen-bit lanes, so each vector overload returns the distances of its two halves.
    /// </remarks>
    internal interface IAv1Palette2DNearestOperator
    {
        /// <summary>
        /// Finds the nearest paired color of one pair of samples.
        /// </summary>
        /// <param name="first">The first-plane sample.</param>
        /// <param name="second">The second-plane sample.</param>
        /// <param name="firstCentroids">The first-plane palette colors.</param>
        /// <param name="secondCentroids">The second-plane palette colors.</param>
        /// <param name="distance">Receives the squared distance to the nearest color.</param>
        /// <returns>The index of the nearest color.</returns>
        public static abstract int Nearest(
            short first,
            short second,
            ReadOnlySpan<short> firstCentroids,
            ReadOnlySpan<short> secondCentroids,
            out int distance);

        /// <summary>
        /// Finds the nearest paired color of eight pairs of samples.
        /// </summary>
        /// <param name="first">The first-plane samples.</param>
        /// <param name="second">The second-plane samples.</param>
        /// <param name="firstCentroids">The first-plane palette colors.</param>
        /// <param name="secondCentroids">The second-plane palette colors.</param>
        /// <param name="lower">Receives the squared distances of the first four pairs.</param>
        /// <param name="upper">Receives the squared distances of the second four pairs.</param>
        /// <returns>The index of the nearest color of each pair.</returns>
        public static abstract Vector128<short> Nearest(
            Vector128<short> first,
            Vector128<short> second,
            ReadOnlySpan<short> firstCentroids,
            ReadOnlySpan<short> secondCentroids,
            out Vector128<int> lower,
            out Vector128<int> upper);

        /// <summary>
        /// Finds the nearest paired color of sixteen pairs of samples.
        /// </summary>
        /// <param name="first">The first-plane samples.</param>
        /// <param name="second">The second-plane samples.</param>
        /// <param name="firstCentroids">The first-plane palette colors.</param>
        /// <param name="secondCentroids">The second-plane palette colors.</param>
        /// <param name="lower">Receives the squared distances of the first eight pairs.</param>
        /// <param name="upper">Receives the squared distances of the second eight pairs.</param>
        /// <returns>The index of the nearest color of each pair.</returns>
        public static abstract Vector256<short> Nearest(
            Vector256<short> first,
            Vector256<short> second,
            ReadOnlySpan<short> firstCentroids,
            ReadOnlySpan<short> secondCentroids,
            out Vector256<int> lower,
            out Vector256<int> upper);

        /// <summary>
        /// Finds the nearest paired color of thirty-two pairs of samples.
        /// </summary>
        /// <param name="first">The first-plane samples.</param>
        /// <param name="second">The second-plane samples.</param>
        /// <param name="firstCentroids">The first-plane palette colors.</param>
        /// <param name="secondCentroids">The second-plane palette colors.</param>
        /// <param name="lower">Receives the squared distances of the first sixteen pairs.</param>
        /// <param name="upper">Receives the squared distances of the second sixteen pairs.</param>
        /// <returns>The index of the nearest color of each pair.</returns>
        public static abstract Vector512<short> Nearest(
            Vector512<short> first,
            Vector512<short> second,
            ReadOnlySpan<short> firstCentroids,
            ReadOnlySpan<short> secondCentroids,
            out Vector512<int> lower,
            out Vector512<int> upper);
    }

    /// <summary>
    /// Assigns every chroma pair of a block with the arithmetic of a closed search operator.
    /// </summary>
    /// <typeparam name="TOperator">The nearest-color search.</typeparam>
    /// <remarks>
    /// One lane is one chroma pair, and the operator broadcasts each palette color. Thus the traversal is a plain walk of
    /// the samples at descending register widths with a scalar tail. The index of a color is less than the palette limit
    /// of eight, so the narrowing of the index lanes to bytes is exact.
    /// </remarks>
    private static class Assign<TOperator>
        where TOperator : struct, IAv1Palette2DNearestOperator
    {
        /// <summary>
        /// Assigns every chroma pair to its nearest palette color.
        /// </summary>
        /// <param name="firstSamples">The active first-plane samples.</param>
        /// <param name="secondSamples">The active second-plane samples.</param>
        /// <param name="firstCentroids">The first-plane palette colors.</param>
        /// <param name="secondCentroids">The second-plane palette colors.</param>
        /// <param name="indices">The destination palette indices.</param>
        /// <returns>The sum of the squared two-plane distances.</returns>
        public static long Apply(
            ReadOnlySpan<short> firstSamples,
            ReadOnlySpan<short> secondSamples,
            ReadOnlySpan<short> firstCentroids,
            ReadOnlySpan<short> secondCentroids,
            Span<byte> indices)
        {
            // The lower and the upper squared distances add in 32-bit lanes. The sums widen into 64-bit lane totals,
            // and one reduction at the end gives the distortion. A squared two-plane distance is less than 2^26,
            // so the sum of two distances fits in 32 bits. The total of a whole block needs 64 bits.
            ref short firstBase = ref MemoryMarshal.GetReference(firstSamples);
            ref short secondBase = ref MemoryMarshal.GetReference(secondSamples);
            ref byte indexBase = ref MemoryMarshal.GetReference(indices);

            int offset = 0;
            long distortion = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<long> total = Vector512<long>.Zero;
                int vectorEnd = firstSamples.Length - Vector512<short>.Count;
                for (; offset <= vectorEnd; offset += Vector512<short>.Count)
                {
                    Vector512<short> index = TOperator.Nearest(
                        Vector512.LoadUnsafe(ref firstBase, (nuint)offset),
                        Vector512.LoadUnsafe(ref secondBase, (nuint)offset),
                        firstCentroids,
                        secondCentroids,
                        out Vector512<int> lower,
                        out Vector512<int> upper);

                    Vector512.Narrow(index.AsUInt16(), Vector512<ushort>.Zero).GetLower()
                        .StoreUnsafe(ref indexBase, (nuint)offset);

                    (Vector512<long> lowerTotal, Vector512<long> upperTotal) = Vector512.Widen(lower + upper);
                    total += lowerTotal + upperTotal;
                }

                distortion += Vector512.Sum(total);
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<long> total = Vector256<long>.Zero;
                int vectorEnd = firstSamples.Length - Vector256<short>.Count;
                for (; offset <= vectorEnd; offset += Vector256<short>.Count)
                {
                    Vector256<short> index = TOperator.Nearest(
                        Vector256.LoadUnsafe(ref firstBase, (nuint)offset),
                        Vector256.LoadUnsafe(ref secondBase, (nuint)offset),
                        firstCentroids,
                        secondCentroids,
                        out Vector256<int> lower,
                        out Vector256<int> upper);

                    Vector256.Narrow(index.AsUInt16(), Vector256<ushort>.Zero).GetLower()
                        .StoreUnsafe(ref indexBase, (nuint)offset);

                    (Vector256<long> lowerTotal, Vector256<long> upperTotal) = Vector256.Widen(lower + upper);
                    total += lowerTotal + upperTotal;
                }

                distortion += Vector256.Sum(total);
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<long> total = Vector128<long>.Zero;
                int vectorEnd = firstSamples.Length - Vector128<short>.Count;
                for (; offset <= vectorEnd; offset += Vector128<short>.Count)
                {
                    Vector128<short> index = TOperator.Nearest(
                        Vector128.LoadUnsafe(ref firstBase, (nuint)offset),
                        Vector128.LoadUnsafe(ref secondBase, (nuint)offset),
                        firstCentroids,
                        secondCentroids,
                        out Vector128<int> lower,
                        out Vector128<int> upper);

                    Vector128.Narrow(index.AsUInt16(), Vector128<ushort>.Zero).GetLower()
                        .StoreUnsafe(ref indexBase, (nuint)offset);

                    (Vector128<long> lowerTotal, Vector128<long> upperTotal) = Vector128.Widen(lower + upper);
                    total += lowerTotal + upperTotal;
                }

                distortion += Vector128.Sum(total);
            }

            for (; offset < firstSamples.Length; offset++)
            {
                indices[offset] = (byte)TOperator.Nearest(
                    firstSamples[offset], secondSamples[offset], firstCentroids, secondCentroids, out int distance);

                distortion += distance;
            }

            return distortion;
        }
    }
}
