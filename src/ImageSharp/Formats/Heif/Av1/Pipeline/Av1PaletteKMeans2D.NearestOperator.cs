// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Implements the nearest-color search of two-dimensional palette clustering.
/// </content>
internal static partial class Av1PaletteKMeans2D
{
    /// <summary>
    /// Selects the paired palette color with the smallest squared distance from a chroma pair.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A squared distance reaches about 33 million for twelve-bit samples, so the comparison runs
    /// on thirty-two bit lanes. The samples arrive as sixteen-bit lanes, so each vector overload
    /// widens its difference into a lower half and an upper half and searches the two halves side
    /// by side. The index lanes stay sixteen bits wide, because a palette holds at most eight
    /// colors, and the traversal narrows them to bytes.
    /// </para>
    /// <para>
    /// The comparison is strict, so a pair equally close to two colors keeps the first of them,
    /// which is what the scalar reference does.
    /// </para>
    /// </remarks>
    private readonly struct NearestOperator : IAv1Palette2DNearestOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Nearest(
            short first,
            short second,
            ReadOnlySpan<short> firstCentroids,
            ReadOnlySpan<short> secondCentroids,
            out int distance)
        {
            int firstDifference = first - firstCentroids[0];
            int secondDifference = second - secondCentroids[0];
            distance = (firstDifference * firstDifference) + (secondDifference * secondDifference);
            int index = 0;
            for (int candidate = 1; candidate < firstCentroids.Length; candidate++)
            {
                firstDifference = first - firstCentroids[candidate];
                secondDifference = second - secondCentroids[candidate];
                int current = (firstDifference * firstDifference) + (secondDifference * secondDifference);
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
            Vector128<short> first,
            Vector128<short> second,
            ReadOnlySpan<short> firstCentroids,
            ReadOnlySpan<short> secondCentroids,
            out Vector128<int> lower,
            out Vector128<int> upper)
        {
            // Each lane is one independent chroma pair. A palette color is broadcast to every lane,
            // so the sweep is eight independent searches that share one sequence of comparisons.
            Distance(first, second, firstCentroids[0], secondCentroids[0], out lower, out upper);
            Vector128<int> indexLower = Vector128<int>.Zero;
            Vector128<int> indexUpper = Vector128<int>.Zero;
            for (int candidate = 1; candidate < firstCentroids.Length; candidate++)
            {
                Distance(
                    first,
                    second,
                    firstCentroids[candidate],
                    secondCentroids[candidate],
                    out Vector128<int> currentLower,
                    out Vector128<int> currentUpper);

                // The mask is all ones only where the new color is strictly nearer, so a tie keeps
                // the color already held and matches the scalar comparison.
                Vector128<int> replaceLower = Vector128.LessThan(currentLower, lower);
                Vector128<int> replaceUpper = Vector128.LessThan(currentUpper, upper);
                lower = Vector128.ConditionalSelect(replaceLower, currentLower, lower);
                upper = Vector128.ConditionalSelect(replaceUpper, currentUpper, upper);
                indexLower = Vector128.ConditionalSelect(replaceLower, Vector128.Create(candidate), indexLower);
                indexUpper = Vector128.ConditionalSelect(replaceUpper, Vector128.Create(candidate), indexUpper);
            }

            // The indices are below eight, so narrowing the two halves back into one vector of
            // sixteen-bit lanes restores the lane order of the samples exactly.
            return Vector128.Narrow(indexLower, indexUpper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Nearest(
            Vector256<short> first,
            Vector256<short> second,
            ReadOnlySpan<short> firstCentroids,
            ReadOnlySpan<short> secondCentroids,
            out Vector256<int> lower,
            out Vector256<int> upper)
        {
            // Sixteen independent chroma pairs, with the lane layout and the arithmetic of the
            // 128-bit overload.
            Distance(first, second, firstCentroids[0], secondCentroids[0], out lower, out upper);
            Vector256<int> indexLower = Vector256<int>.Zero;
            Vector256<int> indexUpper = Vector256<int>.Zero;
            for (int candidate = 1; candidate < firstCentroids.Length; candidate++)
            {
                Distance(
                    first,
                    second,
                    firstCentroids[candidate],
                    secondCentroids[candidate],
                    out Vector256<int> currentLower,
                    out Vector256<int> currentUpper);

                Vector256<int> replaceLower = Vector256.LessThan(currentLower, lower);
                Vector256<int> replaceUpper = Vector256.LessThan(currentUpper, upper);
                lower = Vector256.ConditionalSelect(replaceLower, currentLower, lower);
                upper = Vector256.ConditionalSelect(replaceUpper, currentUpper, upper);
                indexLower = Vector256.ConditionalSelect(replaceLower, Vector256.Create(candidate), indexLower);
                indexUpper = Vector256.ConditionalSelect(replaceUpper, Vector256.Create(candidate), indexUpper);
            }

            return Vector256.Narrow(indexLower, indexUpper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Nearest(
            Vector512<short> first,
            Vector512<short> second,
            ReadOnlySpan<short> firstCentroids,
            ReadOnlySpan<short> secondCentroids,
            out Vector512<int> lower,
            out Vector512<int> upper)
        {
            // Thirty-two independent chroma pairs. The comparison produces an all-ones or all-zero
            // lane mask on every supported path, including AVX-512, where the JIT lowers the mask
            // register back to a vector for the following select.
            Distance(first, second, firstCentroids[0], secondCentroids[0], out lower, out upper);
            Vector512<int> indexLower = Vector512<int>.Zero;
            Vector512<int> indexUpper = Vector512<int>.Zero;
            for (int candidate = 1; candidate < firstCentroids.Length; candidate++)
            {
                Distance(
                    first,
                    second,
                    firstCentroids[candidate],
                    secondCentroids[candidate],
                    out Vector512<int> currentLower,
                    out Vector512<int> currentUpper);

                Vector512<int> replaceLower = Vector512.LessThan(currentLower, lower);
                Vector512<int> replaceUpper = Vector512.LessThan(currentUpper, upper);
                lower = Vector512.ConditionalSelect(replaceLower, currentLower, lower);
                upper = Vector512.ConditionalSelect(replaceUpper, currentUpper, upper);
                indexLower = Vector512.ConditionalSelect(replaceLower, Vector512.Create(candidate), indexLower);
                indexUpper = Vector512.ConditionalSelect(replaceUpper, Vector512.Create(candidate), indexUpper);
            }

            return Vector512.Narrow(indexLower, indexUpper);
        }

        /// <summary>
        /// Computes the squared distance of eight chroma pairs from one paired color.
        /// </summary>
        /// <param name="first">The first-plane samples.</param>
        /// <param name="second">The second-plane samples.</param>
        /// <param name="firstCentroid">The first-plane color.</param>
        /// <param name="secondCentroid">The second-plane color.</param>
        /// <param name="lower">Receives the squared distances of the first four pairs.</param>
        /// <param name="upper">Receives the squared distances of the second four pairs.</param>
        /// <remarks>
        /// The difference of two twelve-bit samples fits a sixteen-bit lane, so it is taken before
        /// the widening and only the square needs the wider lanes.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Distance(
            Vector128<short> first,
            Vector128<short> second,
            short firstCentroid,
            short secondCentroid,
            out Vector128<int> lower,
            out Vector128<int> upper)
        {
            (Vector128<int> firstLower, Vector128<int> firstUpper) = Vector128.Widen(first - Vector128.Create(firstCentroid));
            (Vector128<int> secondLower, Vector128<int> secondUpper) = Vector128.Widen(second - Vector128.Create(secondCentroid));
            lower = (firstLower * firstLower) + (secondLower * secondLower);
            upper = (firstUpper * firstUpper) + (secondUpper * secondUpper);
        }

        /// <summary>
        /// Computes the squared distance of sixteen chroma pairs from one paired color.
        /// </summary>
        /// <param name="first">The first-plane samples.</param>
        /// <param name="second">The second-plane samples.</param>
        /// <param name="firstCentroid">The first-plane color.</param>
        /// <param name="secondCentroid">The second-plane color.</param>
        /// <param name="lower">Receives the squared distances of the first eight pairs.</param>
        /// <param name="upper">Receives the squared distances of the second eight pairs.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Distance(
            Vector256<short> first,
            Vector256<short> second,
            short firstCentroid,
            short secondCentroid,
            out Vector256<int> lower,
            out Vector256<int> upper)
        {
            (Vector256<int> firstLower, Vector256<int> firstUpper) = Vector256.Widen(first - Vector256.Create(firstCentroid));
            (Vector256<int> secondLower, Vector256<int> secondUpper) = Vector256.Widen(second - Vector256.Create(secondCentroid));
            lower = (firstLower * firstLower) + (secondLower * secondLower);
            upper = (firstUpper * firstUpper) + (secondUpper * secondUpper);
        }

        /// <summary>
        /// Computes the squared distance of thirty-two chroma pairs from one paired color.
        /// </summary>
        /// <param name="first">The first-plane samples.</param>
        /// <param name="second">The second-plane samples.</param>
        /// <param name="firstCentroid">The first-plane color.</param>
        /// <param name="secondCentroid">The second-plane color.</param>
        /// <param name="lower">Receives the squared distances of the first sixteen pairs.</param>
        /// <param name="upper">Receives the squared distances of the second sixteen pairs.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Distance(
            Vector512<short> first,
            Vector512<short> second,
            short firstCentroid,
            short secondCentroid,
            out Vector512<int> lower,
            out Vector512<int> upper)
        {
            (Vector512<int> firstLower, Vector512<int> firstUpper) = Vector512.Widen(first - Vector512.Create(firstCentroid));
            (Vector512<int> secondLower, Vector512<int> secondUpper) = Vector512.Widen(second - Vector512.Create(secondCentroid));
            lower = (firstLower * firstLower) + (secondLower * secondLower);
            upper = (firstUpper * firstUpper) + (secondUpper * secondUpper);
        }
    }
}
