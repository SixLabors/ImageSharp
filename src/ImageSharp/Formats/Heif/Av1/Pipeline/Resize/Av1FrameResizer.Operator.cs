// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Resize;

/// <content>
/// Defines the eight-tap filter that every resize pass applies, and the traversal that applies it.
/// </content>
internal static partial class Av1FrameResizer
{
    /// <summary>
    /// Loads, weights and stores the samples of one native sample type for the eight-tap resize filter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every resize pass computes, for each output row and each column, a sum of eight weighted input samples taken from
    /// the same column of eight chosen input rows, then rounds the sum by seven bits and clips it to the sample range.
    /// The traversal holds the sums in 32-bit lanes, one lane per column, because an eight-bit sample times a Q7
    /// coefficient summed over eight taps can reach about 46,000, beyond a 16-bit lane, and a 12-bit sample reaches
    /// about 740,000.
    /// </para>
    /// <para>
    /// Each overload performs the same lane-wise arithmetic at one register width, and the scalar overload performs
    /// it for one column. Every result is exact, so all widths give the same output.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    internal interface IAv1ResizeSampleOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Adds one weighted input sample to the sum of one column.
        /// </summary>
        /// <param name="sum">The sum so far.</param>
        /// <param name="source">The input sample.</param>
        /// <param name="coefficient">The signed Q7 coefficient of the input row.</param>
        /// <returns>The updated sum.</returns>
        public static abstract int MultiplyAdd(int sum, ref TSample source, int coefficient);

        /// <summary>
        /// Adds four weighted input samples, from four adjacent columns, to the sums of those columns.
        /// </summary>
        /// <param name="sum">The sums so far, one lane per column.</param>
        /// <param name="source">The input sample of the first column.</param>
        /// <param name="coefficient">The signed Q7 coefficient of the input row, in every lane.</param>
        /// <returns>The updated sums.</returns>
        public static abstract Vector128<int> MultiplyAdd(Vector128<int> sum, ref TSample source, Vector128<int> coefficient);

        /// <summary>
        /// Adds eight weighted input samples, from eight adjacent columns, to the sums of those columns.
        /// </summary>
        /// <param name="sum">The sums so far, one lane per column.</param>
        /// <param name="source">The input sample of the first column.</param>
        /// <param name="coefficient">The signed Q7 coefficient of the input row, in every lane.</param>
        /// <returns>The updated sums.</returns>
        public static abstract Vector256<int> MultiplyAdd(Vector256<int> sum, ref TSample source, Vector256<int> coefficient);

        /// <summary>
        /// Adds sixteen weighted input samples, from sixteen adjacent columns, to the sums of those columns.
        /// </summary>
        /// <param name="sum">The sums so far, one lane per column.</param>
        /// <param name="source">The input sample of the first column.</param>
        /// <param name="coefficient">The signed Q7 coefficient of the input row, in every lane.</param>
        /// <returns>The updated sums.</returns>
        public static abstract Vector512<int> MultiplyAdd(Vector512<int> sum, ref TSample source, Vector512<int> coefficient);

        /// <summary>
        /// Stores one finished sum as an output sample. The sum already holds the rounding offset.
        /// </summary>
        /// <param name="destination">The output sample.</param>
        /// <param name="sum">The sum with its rounding offset.</param>
        /// <param name="maximum">The largest sample value.</param>
        public static abstract void Store(ref TSample destination, int sum, int maximum);

        /// <summary>
        /// Stores four finished sums as the output samples of four adjacent columns.
        /// </summary>
        /// <param name="destination">The output sample of the first column.</param>
        /// <param name="sum">The sums with their rounding offset, one lane per column.</param>
        /// <param name="maximum">The largest sample value, in every lane.</param>
        public static abstract void Store(ref TSample destination, Vector128<int> sum, Vector128<int> maximum);

        /// <summary>
        /// Stores eight finished sums as the output samples of eight adjacent columns.
        /// </summary>
        /// <param name="destination">The output sample of the first column.</param>
        /// <param name="sum">The sums with their rounding offset, one lane per column.</param>
        /// <param name="maximum">The largest sample value, in every lane.</param>
        public static abstract void Store(ref TSample destination, Vector256<int> sum, Vector256<int> maximum);

        /// <summary>
        /// Stores sixteen finished sums as the output samples of sixteen adjacent columns.
        /// </summary>
        /// <param name="destination">The output sample of the first column.</param>
        /// <param name="sum">The sums with their rounding offset, one lane per column.</param>
        /// <param name="maximum">The largest sample value, in every lane.</param>
        public static abstract void Store(ref TSample destination, Vector512<int> sum, Vector512<int> maximum);
    }

    /// <summary>
    /// Applies one resize pass down the columns of a plane: each output row is the eight-tap filter of eight input
    /// rows. The plan names the input rows and the coefficients of each output row.
    /// </summary>
    /// <typeparam name="TSample">The native sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample arithmetic.</typeparam>
    /// <param name="input">The input rows.</param>
    /// <param name="inputOffset">The offset of the first input sample of input row 0.</param>
    /// <param name="inputStride">The distance between input rows.</param>
    /// <param name="output">The output rows.</param>
    /// <param name="outputOffset">The offset of the first output sample of output row 0.</param>
    /// <param name="outputStride">The distance between output rows.</param>
    /// <param name="plan">The input rows and coefficients of each output row.</param>
    /// <param name="width">The number of columns.</param>
    /// <param name="maximum">The largest sample value.</param>
    private static void ApplyPass<TSample, TOperator>(
        ReadOnlySpan<TSample> input,
        int inputOffset,
        int inputStride,
        Span<TSample> output,
        int outputOffset,
        int outputStride,
        in TapPlan plan,
        int width,
        int maximum)
        where TSample : unmanaged
        where TOperator : struct, IAv1ResizeSampleOperator<TSample>
    {
        ref TSample inputBase = ref Unsafe.AsRef(in input[0]);
        ref TSample outputBase = ref output[0];
        ReadOnlySpan<int> rows = plan.Rows;
        ReadOnlySpan<short> coefficients = plan.Coefficients;

        // The sums start at the rounding offset of the seven-bit shift. Reference: ROUND_POWER_OF_TWO(sum, FILTER_BITS).
        const int rounding = 1 << (FilterBits - 1);
        for (int row = 0; row < plan.Count; row++)
        {
            int tap = row * Taps;

            // The first input sample of each of the eight input rows.
            ref TSample row0 = ref Unsafe.Add(ref inputBase, inputOffset + (rows[tap] * inputStride));
            ref TSample row1 = ref Unsafe.Add(ref inputBase, inputOffset + (rows[tap + 1] * inputStride));
            ref TSample row2 = ref Unsafe.Add(ref inputBase, inputOffset + (rows[tap + 2] * inputStride));
            ref TSample row3 = ref Unsafe.Add(ref inputBase, inputOffset + (rows[tap + 3] * inputStride));
            ref TSample row4 = ref Unsafe.Add(ref inputBase, inputOffset + (rows[tap + 4] * inputStride));
            ref TSample row5 = ref Unsafe.Add(ref inputBase, inputOffset + (rows[tap + 5] * inputStride));
            ref TSample row6 = ref Unsafe.Add(ref inputBase, inputOffset + (rows[tap + 6] * inputStride));
            ref TSample row7 = ref Unsafe.Add(ref inputBase, inputOffset + (rows[tap + 7] * inputStride));
            ref TSample destination = ref Unsafe.Add(ref outputBase, outputOffset + (row * outputStride));
            int c0 = coefficients[tap];
            int c1 = coefficients[tap + 1];
            int c2 = coefficients[tap + 2];
            int c3 = coefficients[tap + 3];
            int c4 = coefficients[tap + 4];
            int c5 = coefficients[tap + 5];
            int c6 = coefficients[tap + 6];
            int c7 = coefficients[tap + 7];
            int x = 0;

            // The widest vectors take the columns first, and each narrower width takes what is left. A lane holds one
            // column, so the columns need no reduction.
            if (Vector512.IsHardwareAccelerated)
            {
                // The coefficients and the limits are broadcast once per output row.
                Vector512<int> maximum512 = Vector512.Create(maximum);
                Vector512<int> start512 = Vector512.Create(rounding);
                Vector512<int> k0 = Vector512.Create(c0);
                Vector512<int> k1 = Vector512.Create(c1);
                Vector512<int> k2 = Vector512.Create(c2);
                Vector512<int> k3 = Vector512.Create(c3);
                Vector512<int> k4 = Vector512.Create(c4);
                Vector512<int> k5 = Vector512.Create(c5);
                Vector512<int> k6 = Vector512.Create(c6);
                Vector512<int> k7 = Vector512.Create(c7);
                for (; x <= width - Vector512<int>.Count; x += Vector512<int>.Count)
                {
                    Vector512<int> sum = TOperator.MultiplyAdd(start512, ref Unsafe.Add(ref row0, x), k0);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row1, x), k1);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row2, x), k2);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row3, x), k3);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row4, x), k4);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row5, x), k5);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row6, x), k6);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row7, x), k7);
                    TOperator.Store(ref Unsafe.Add(ref destination, x), sum, maximum512);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<int> maximum256 = Vector256.Create(maximum);
                Vector256<int> start256 = Vector256.Create(rounding);
                Vector256<int> k0 = Vector256.Create(c0);
                Vector256<int> k1 = Vector256.Create(c1);
                Vector256<int> k2 = Vector256.Create(c2);
                Vector256<int> k3 = Vector256.Create(c3);
                Vector256<int> k4 = Vector256.Create(c4);
                Vector256<int> k5 = Vector256.Create(c5);
                Vector256<int> k6 = Vector256.Create(c6);
                Vector256<int> k7 = Vector256.Create(c7);
                for (; x <= width - Vector256<int>.Count; x += Vector256<int>.Count)
                {
                    Vector256<int> sum = TOperator.MultiplyAdd(start256, ref Unsafe.Add(ref row0, x), k0);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row1, x), k1);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row2, x), k2);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row3, x), k3);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row4, x), k4);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row5, x), k5);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row6, x), k6);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row7, x), k7);
                    TOperator.Store(ref Unsafe.Add(ref destination, x), sum, maximum256);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<int> maximum128 = Vector128.Create(maximum);
                Vector128<int> start128 = Vector128.Create(rounding);
                Vector128<int> k0 = Vector128.Create(c0);
                Vector128<int> k1 = Vector128.Create(c1);
                Vector128<int> k2 = Vector128.Create(c2);
                Vector128<int> k3 = Vector128.Create(c3);
                Vector128<int> k4 = Vector128.Create(c4);
                Vector128<int> k5 = Vector128.Create(c5);
                Vector128<int> k6 = Vector128.Create(c6);
                Vector128<int> k7 = Vector128.Create(c7);
                for (; x <= width - Vector128<int>.Count; x += Vector128<int>.Count)
                {
                    Vector128<int> sum = TOperator.MultiplyAdd(start128, ref Unsafe.Add(ref row0, x), k0);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row1, x), k1);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row2, x), k2);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row3, x), k3);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row4, x), k4);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row5, x), k5);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row6, x), k6);
                    sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row7, x), k7);
                    TOperator.Store(ref Unsafe.Add(ref destination, x), sum, maximum128);
                }
            }

            // The scalar overload finishes the columns that do not fill a vector.
            for (; x < width; x++)
            {
                int sum = rounding;
                sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row0, x), c0);
                sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row1, x), c1);
                sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row2, x), c2);
                sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row3, x), c3);
                sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row4, x), c4);
                sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row5, x), c5);
                sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row6, x), c6);
                sum = TOperator.MultiplyAdd(sum, ref Unsafe.Add(ref row7, x), c7);
                TOperator.Store(ref Unsafe.Add(ref destination, x), sum, maximum);
            }
        }
    }
}
