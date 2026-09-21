// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Defines the threshold contract consumed by the shared corner traversal.
/// </content>
internal static partial class Av1CornerDetector
{
    /// <summary>
    /// Tests one direction of the corner threshold.
    /// </summary>
    /// <remarks>
    /// A pixel is a corner when nine adjacent samples of the circle around it are all brighter than
    /// the pixel by the barrier, or all darker than it by the barrier. The run test is the same for
    /// both directions, so the two directions are two operators over one traversal.
    /// </remarks>
    internal interface IAv1CornerThresholdOperator
    {
        /// <summary>
        /// Derives the comparison threshold of one centre sample.
        /// </summary>
        /// <param name="centre">The centre sample.</param>
        /// <param name="barrier">The barrier.</param>
        /// <returns>The threshold that a circle sample is compared against.</returns>
        public static abstract int Threshold(int centre, int barrier);

        /// <summary>
        /// Tests whether one circle sample passes the threshold.
        /// </summary>
        /// <param name="sample">The circle sample.</param>
        /// <param name="threshold">The threshold of the centre sample.</param>
        /// <returns>Whether the sample passes.</returns>
        public static abstract bool Exceeds(int sample, int threshold);

        /// <summary>
        /// Derives the comparison thresholds of sixteen centre samples.
        /// </summary>
        /// <param name="centre">The centre samples.</param>
        /// <param name="barrier">The barrier in every lane.</param>
        /// <returns>The thresholds that the circle samples are compared against.</returns>
        /// <remarks>
        /// A threshold is saturated to the range of a byte lane. The scalar overload does not
        /// saturate, because it computes in a wider type, and the two still agree on every result:
        /// a sample cannot leave the range zero through 255, so a saturated threshold decides the
        /// comparison the same way the unsaturated value decides it.
        /// </remarks>
        public static abstract Vector128<byte> Threshold(Vector128<byte> centre, Vector128<byte> barrier);

        /// <summary>
        /// Tests whether sixteen circle samples pass their thresholds.
        /// </summary>
        /// <param name="sample">The circle samples.</param>
        /// <param name="threshold">The thresholds of the centre samples.</param>
        /// <returns>All ones in each lane that passes and all zeros in each lane that does not.</returns>
        public static abstract Vector128<byte> Exceeds(Vector128<byte> sample, Vector128<byte> threshold);

        /// <summary>
        /// Derives the comparison thresholds of thirty-two centre samples.
        /// </summary>
        /// <param name="centre">The centre samples.</param>
        /// <param name="barrier">The barrier in every lane.</param>
        /// <returns>The thresholds that the circle samples are compared against.</returns>
        public static abstract Vector256<byte> Threshold(Vector256<byte> centre, Vector256<byte> barrier);

        /// <summary>
        /// Tests whether thirty-two circle samples pass their thresholds.
        /// </summary>
        /// <param name="sample">The circle samples.</param>
        /// <param name="threshold">The thresholds of the centre samples.</param>
        /// <returns>All ones in each lane that passes and all zeros in each lane that does not.</returns>
        public static abstract Vector256<byte> Exceeds(Vector256<byte> sample, Vector256<byte> threshold);

        /// <summary>
        /// Derives the comparison thresholds of sixty-four centre samples.
        /// </summary>
        /// <param name="centre">The centre samples.</param>
        /// <param name="barrier">The barrier in every lane.</param>
        /// <returns>The thresholds that the circle samples are compared against.</returns>
        public static abstract Vector512<byte> Threshold(Vector512<byte> centre, Vector512<byte> barrier);

        /// <summary>
        /// Tests whether sixty-four circle samples pass their thresholds.
        /// </summary>
        /// <param name="sample">The circle samples.</param>
        /// <param name="threshold">The thresholds of the centre samples.</param>
        /// <returns>All ones in each lane that passes and all zeros in each lane that does not.</returns>
        public static abstract Vector512<byte> Exceeds(Vector512<byte> sample, Vector512<byte> threshold);
    }

    /// <summary>
    /// Marks the pixels of one row that carry a run of nine, in the arithmetic of a closed operator.
    /// </summary>
    /// <typeparam name="TOperator">The threshold direction.</typeparam>
    /// <remarks>
    /// <para>
    /// The circle around a pixel is sixteen samples, so the sixteen comparisons of one pixel form a
    /// sixteen-bit circular word, and the test is whether that word holds nine adjacent ones. The
    /// traversal never builds the word. It keeps the sixteen comparisons as sixteen lane masks and
    /// reduces them in steps, each of which doubles the length of the run it proves.
    /// </para>
    /// <para>
    /// With <c>m</c> the sixteen masks and every index taken around the circle,
    /// <c>pair[i] = m[i] &amp; m[i+1]</c> proves a run of two at <c>i</c>,
    /// <c>quad[i] = pair[i] &amp; pair[i+2]</c> a run of four,
    /// <c>oct[i] = quad[i] &amp; quad[i+4]</c> a run of eight, and
    /// <c>oct[i] &amp; m[i+8]</c> a run of nine. Because every step is shared by all sixteen
    /// starting positions, the whole test costs sixty-four lane operations rather than the one
    /// hundred and twenty-eight that testing each starting position on its own would cost.
    /// </para>
    /// <para>
    /// The result is the union over the starting positions, so one pass marks every pixel of the
    /// row segment.
    /// </para>
    /// </remarks>
    private static class Runs<TOperator>
        where TOperator : struct, IAv1CornerThresholdOperator
    {
        /// <summary>
        /// Marks every pixel of one row segment that carries a run of nine.
        /// </summary>
        /// <param name="centre">The first centre sample of the row segment.</param>
        /// <param name="offsets">The sample offsets of the circle, in circle order.</param>
        /// <param name="barrier">The barrier.</param>
        /// <param name="destination">The first mark of the row segment.</param>
        /// <param name="count">The number of pixels in the row segment.</param>
        /// <remarks>
        /// Both threshold directions contribute to one row of marks, so this method only ever sets
        /// a mark. The caller clears the row once, before the first direction runs.
        /// </remarks>
        public static void Mark(ref byte centre, ReadOnlySpan<int> offsets, int barrier, ref byte destination, int count)
        {
            int i = 0;

            // Descending widths share one offset, so a machine with AVX-512 still finishes a
            // thirty-two or sixteen pixel remainder in vectors rather than in the scalar loop. Each
            // stage owns its scratch, so the widths that do not run cost no stack.
            if (Vector512.IsHardwareAccelerated && count - i >= Vector512<byte>.Count)
            {
                i = MarkVector512(ref centre, offsets, barrier, ref destination, count, i);
            }

            if (Vector256.IsHardwareAccelerated && count - i >= Vector256<byte>.Count)
            {
                i = MarkVector256(ref centre, offsets, barrier, ref destination, count, i);
            }

            if (Vector128.IsHardwareAccelerated && count - i >= Vector128<byte>.Count)
            {
                i = MarkVector128(ref centre, offsets, barrier, ref destination, count, i);
            }

            // The scalar form tests the same runs, so the marks it sets are the marks that the
            // vector stages would have set for the same pixels.
            for (; i < count; i++)
            {
                if (Test(ref Unsafe.Add(ref centre, i), offsets, barrier))
                {
                    Unsafe.Add(ref destination, i) = byte.MaxValue;
                }
            }
        }

        /// <summary>
        /// Tests whether one pixel carries a run of nine.
        /// </summary>
        /// <param name="centre">The centre sample.</param>
        /// <param name="offsets">The sample offsets of the circle, in circle order.</param>
        /// <param name="barrier">The barrier.</param>
        /// <returns>Whether nine adjacent samples of the circle pass the threshold.</returns>
        /// <remarks>
        /// A run may wrap around the circle, so walking sixteen plus eight positions covers every
        /// starting position without a second loop.
        /// </remarks>
        public static bool Test(ref byte centre, ReadOnlySpan<int> offsets, int barrier)
        {
            int threshold = TOperator.Threshold(centre, barrier);
            int run = 0;
            for (int point = 0; point < CircleLength + RunLength - 1; point++)
            {
                if (TOperator.Exceeds(Unsafe.Add(ref centre, offsets[point & (CircleLength - 1)]), threshold))
                {
                    run++;
                    if (run >= RunLength)
                    {
                        return true;
                    }
                }
                else
                {
                    run = 0;
                }
            }

            return false;
        }

        /// <summary>
        /// Marks sixty-four pixels at a time.
        /// </summary>
        /// <param name="centre">The first centre sample of the row segment.</param>
        /// <param name="offsets">The sample offsets of the circle, in circle order.</param>
        /// <param name="barrier">The barrier.</param>
        /// <param name="destination">The first mark of the row segment.</param>
        /// <param name="count">The number of pixels in the row segment.</param>
        /// <param name="start">The first pixel this stage covers.</param>
        /// <returns>The first pixel the stage did not cover.</returns>
        /// <remarks>
        /// The three scratch arrays hold the sixteen masks and the two intermediate run lengths.
        /// They are allocated once for the whole stage, never inside the loop, because the stack of
        /// a method is not released until the method returns.
        /// </remarks>
        private static int MarkVector512(
            ref byte centre,
            ReadOnlySpan<int> offsets,
            int barrier,
            ref byte destination,
            int count,
            int start)
        {
            Span<Vector512<byte>> masks = stackalloc Vector512<byte>[CircleLength];
            Span<Vector512<byte>> first = stackalloc Vector512<byte>[CircleLength];
            Span<Vector512<byte>> second = stackalloc Vector512<byte>[CircleLength];

            Vector512<byte> barrierVector = Vector512.Create((byte)barrier);
            int i = start;
            int vectorEnd = count - Vector512<byte>.Count;
            for (; i <= vectorEnd; i += Vector512<byte>.Count)
            {
                // Sixty-four adjacent pixels are sixty-four independent lanes, and each sample of
                // the circle is one unaligned load at a constant offset from the centre.
                Vector512<byte> threshold = TOperator.Threshold(
                    Vector512.LoadUnsafe(ref centre, (nuint)i), barrierVector);

                for (int point = 0; point < CircleLength; point++)
                {
                    // A circle offset is signed, so it is applied to the reference itself.
                    // Casting it into the unsigned element offset would turn a negative offset
                    // into an enormous one.
                    masks[point] = TOperator.Exceeds(
                        Vector512.LoadUnsafe(ref Unsafe.Add(ref centre, offsets[point]), (nuint)i), threshold);
                }

                for (int point = 0; point < CircleLength; point++)
                {
                    first[point] = masks[point] & masks[(point + 1) & (CircleLength - 1)];
                }

                for (int point = 0; point < CircleLength; point++)
                {
                    second[point] = first[point] & first[(point + 2) & (CircleLength - 1)];
                }

                Vector512<byte> any = Vector512<byte>.Zero;
                for (int point = 0; point < CircleLength; point++)
                {
                    Vector512<byte> eight = second[point] & second[(point + 4) & (CircleLength - 1)];
                    any |= eight & masks[(point + 8) & (CircleLength - 1)];
                }

                (Vector512.LoadUnsafe(ref destination, (nuint)i) | any).StoreUnsafe(ref destination, (nuint)i);
            }

            return i;
        }

        /// <summary>
        /// Marks thirty-two pixels at a time.
        /// </summary>
        /// <param name="centre">The first centre sample of the row segment.</param>
        /// <param name="offsets">The sample offsets of the circle, in circle order.</param>
        /// <param name="barrier">The barrier.</param>
        /// <param name="destination">The first mark of the row segment.</param>
        /// <param name="count">The number of pixels in the row segment.</param>
        /// <param name="start">The first pixel this stage covers.</param>
        /// <returns>The first pixel the stage did not cover.</returns>
        private static int MarkVector256(
            ref byte centre,
            ReadOnlySpan<int> offsets,
            int barrier,
            ref byte destination,
            int count,
            int start)
        {
            Span<Vector256<byte>> masks = stackalloc Vector256<byte>[CircleLength];
            Span<Vector256<byte>> first = stackalloc Vector256<byte>[CircleLength];
            Span<Vector256<byte>> second = stackalloc Vector256<byte>[CircleLength];

            Vector256<byte> barrierVector = Vector256.Create((byte)barrier);
            int i = start;
            int vectorEnd = count - Vector256<byte>.Count;
            for (; i <= vectorEnd; i += Vector256<byte>.Count)
            {
                Vector256<byte> threshold = TOperator.Threshold(
                    Vector256.LoadUnsafe(ref centre, (nuint)i), barrierVector);

                for (int point = 0; point < CircleLength; point++)
                {
                    // A circle offset is signed, so it is applied to the reference itself.
                    // Casting it into the unsigned element offset would turn a negative offset
                    // into an enormous one.
                    masks[point] = TOperator.Exceeds(
                        Vector256.LoadUnsafe(ref Unsafe.Add(ref centre, offsets[point]), (nuint)i), threshold);
                }

                for (int point = 0; point < CircleLength; point++)
                {
                    first[point] = masks[point] & masks[(point + 1) & (CircleLength - 1)];
                }

                for (int point = 0; point < CircleLength; point++)
                {
                    second[point] = first[point] & first[(point + 2) & (CircleLength - 1)];
                }

                Vector256<byte> any = Vector256<byte>.Zero;
                for (int point = 0; point < CircleLength; point++)
                {
                    Vector256<byte> eight = second[point] & second[(point + 4) & (CircleLength - 1)];
                    any |= eight & masks[(point + 8) & (CircleLength - 1)];
                }

                (Vector256.LoadUnsafe(ref destination, (nuint)i) | any).StoreUnsafe(ref destination, (nuint)i);
            }

            return i;
        }

        /// <summary>
        /// Marks sixteen pixels at a time.
        /// </summary>
        /// <param name="centre">The first centre sample of the row segment.</param>
        /// <param name="offsets">The sample offsets of the circle, in circle order.</param>
        /// <param name="barrier">The barrier.</param>
        /// <param name="destination">The first mark of the row segment.</param>
        /// <param name="count">The number of pixels in the row segment.</param>
        /// <param name="start">The first pixel this stage covers.</param>
        /// <returns>The first pixel the stage did not cover.</returns>
        private static int MarkVector128(
            ref byte centre,
            ReadOnlySpan<int> offsets,
            int barrier,
            ref byte destination,
            int count,
            int start)
        {
            Span<Vector128<byte>> masks = stackalloc Vector128<byte>[CircleLength];
            Span<Vector128<byte>> first = stackalloc Vector128<byte>[CircleLength];
            Span<Vector128<byte>> second = stackalloc Vector128<byte>[CircleLength];

            Vector128<byte> barrierVector = Vector128.Create((byte)barrier);
            int i = start;
            int vectorEnd = count - Vector128<byte>.Count;
            for (; i <= vectorEnd; i += Vector128<byte>.Count)
            {
                Vector128<byte> threshold = TOperator.Threshold(
                    Vector128.LoadUnsafe(ref centre, (nuint)i), barrierVector);

                for (int point = 0; point < CircleLength; point++)
                {
                    // A circle offset is signed, so it is applied to the reference itself.
                    // Casting it into the unsigned element offset would turn a negative offset
                    // into an enormous one.
                    masks[point] = TOperator.Exceeds(
                        Vector128.LoadUnsafe(ref Unsafe.Add(ref centre, offsets[point]), (nuint)i), threshold);
                }

                for (int point = 0; point < CircleLength; point++)
                {
                    first[point] = masks[point] & masks[(point + 1) & (CircleLength - 1)];
                }

                for (int point = 0; point < CircleLength; point++)
                {
                    second[point] = first[point] & first[(point + 2) & (CircleLength - 1)];
                }

                Vector128<byte> any = Vector128<byte>.Zero;
                for (int point = 0; point < CircleLength; point++)
                {
                    Vector128<byte> eight = second[point] & second[(point + 4) & (CircleLength - 1)];
                    any |= eight & masks[(point + 8) & (CircleLength - 1)];
                }

                (Vector128.LoadUnsafe(ref destination, (nuint)i) | any).StoreUnsafe(ref destination, (nuint)i);
            }

            return i;
        }
    }
}
