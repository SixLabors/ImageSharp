// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <content>
/// Measures the wavelet energy of a unit for the perceptual delta-quantizer mode.
/// </content>
internal sealed partial class Av1FirstPass<TSample, TOperator>
{
    /// <summary>
    /// Applies the lifting arithmetic of the 5/3 wavelet to one lane type. Each lane carries the same coefficient
    /// of a different 8x8 block, so every operation is element-wise and the transform needs no shuffles.
    /// </summary>
    /// <typeparam name="TLanes">The lane type: one block, or a register of blocks.</typeparam>
    private interface IWaveletOperator<TLanes>
        where TLanes : unmanaged
    {
        /// <summary>
        /// Gets the number of blocks the lane type carries.
        /// </summary>
        public static abstract int Count { get; }

        /// <summary>
        /// Loads one sample of consecutive blocks, scaled by four. Reference: the input scaling of
        /// dyadic_analyze_53_uint8_input().
        /// </summary>
        /// <param name="source">The source plane.</param>
        /// <param name="origins">The source index of each block origin, one per lane.</param>
        /// <param name="offset">The offset of the sample from each block origin.</param>
        /// <returns>The scaled samples.</returns>
        public static abstract TLanes Load(ReadOnlySpan<TSample> source, ReadOnlySpan<int> origins, int offset);

        /// <summary>
        /// Adds lanes. Reference: analysis_53_row().
        /// </summary>
        /// <param name="left">The first addend.</param>
        /// <param name="right">The second addend.</param>
        /// <returns>The sums.</returns>
        public static abstract TLanes Add(TLanes left, TLanes right);

        /// <summary>
        /// Subtracts lanes. Reference: analysis_53_row().
        /// </summary>
        /// <param name="left">The minuend.</param>
        /// <param name="right">The subtrahend.</param>
        /// <returns>The differences.</returns>
        public static abstract TLanes Subtract(TLanes left, TLanes right);

        /// <summary>
        /// Shifts lanes right with sign extension, which floors the division. Reference: analysis_53_row().
        /// </summary>
        /// <param name="value">The lanes.</param>
        /// <param name="count">The shift count.</param>
        /// <returns>The shifted lanes.</returns>
        public static abstract TLanes ShiftRight(TLanes value, int count);

        /// <summary>
        /// Adds a constant to every lane. Reference: analysis_53_row().
        /// </summary>
        /// <param name="value">The lanes.</param>
        /// <param name="constant">The constant.</param>
        /// <returns>The sums.</returns>
        public static abstract TLanes AddConstant(TLanes value, int constant);

        /// <summary>
        /// Sums the absolute values of every lane. Reference: haar_ac_sad().
        /// </summary>
        /// <param name="value">The lanes.</param>
        /// <returns>The sum over all lanes.</returns>
        public static abstract int SumAbsolute(TLanes value);
    }

    /// <summary>
    /// Sums the wavelet AC energy of the 8x8 blocks of a unit, walking the widest register that the remaining
    /// blocks fill before the scalar tail. Reference: av1_haar_ac_sad_mxn_uint8_input().
    /// </summary>
    /// <param name="source">The source plane.</param>
    /// <param name="index">The source index of the unit origin.</param>
    /// <param name="stride">The source row stride.</param>
    /// <param name="blocksPerSide">The number of 8x8 blocks along each side of the unit.</param>
    /// <returns>The summed energy.</returns>
    private static long GetWaveletEnergy(ReadOnlySpan<TSample> source, int index, int stride, int blocksPerSide)
    {
        // The unit holds at most 2x2 blocks; their origins become the lanes. The sum is exact, so the lane
        // grouping does not change it.
        Span<int> origins = stackalloc int[4];
        int count = 0;
        for (int row = 0; row < blocksPerSide; row++)
        {
            for (int column = 0; column < blocksPerSide; column++)
            {
                origins[count++] = index + (row * 8 * stride) + (column * 8);
            }
        }

        long energy = 0;
        int next = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            for (; next <= count - Vector128WaveletOperator.Count; next += Vector128WaveletOperator.Count)
            {
                energy += GetHaarAcSad<Vector128<int>, Vector128WaveletOperator>(source, origins[next..], stride);
            }
        }

        for (; next < count; next++)
        {
            energy += GetHaarAcSad<int, ScalarWaveletOperator>(source, origins[next..], stride);
        }

        return energy;
    }

    /// <summary>
    /// Transforms 8x8 blocks with three levels of the 5/3 wavelet, rows before columns at each level, and sums
    /// the magnitudes outside the 4x4 low band. Reference: haar_ac_sad_8x8_uint8_input() with
    /// av1_fdwt8x8_uint8_input_c() and haar_ac_sad().
    /// </summary>
    /// <typeparam name="TLanes">The lane type.</typeparam>
    /// <typeparam name="TWaveletOperator">The lane arithmetic.</typeparam>
    /// <param name="source">The source plane.</param>
    /// <param name="origins">The block origins, one per lane.</param>
    /// <param name="stride">The source row stride.</param>
    /// <returns>The energy summed over the lanes.</returns>
    private static int GetHaarAcSad<TLanes, TWaveletOperator>(ReadOnlySpan<TSample> source, ReadOnlySpan<int> origins, int stride)
        where TLanes : unmanaged
        where TWaveletOperator : struct, IWaveletOperator<TLanes>
    {
        Span<TLanes> coefficients = stackalloc TLanes[64];
        Span<TLanes> buffer = stackalloc TLanes[16];
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                coefficients[(row * 8) + column] = TWaveletOperator.Load(source, origins, (row * stride) + column);
            }
        }

        // Each level halves the band it transforms; the level that would leave a single sample stops.
        int height = 8;
        int width = 8;
        while (height >= 2 && width >= 2)
        {
            int lowHeight = (height + 1) >> 1;
            int lowWidth = (width + 1) >> 1;
            for (int row = 0; row < height; row++)
            {
                Span<TLanes> line = coefficients.Slice(row * 8, 8);
                line[..width].CopyTo(buffer);
                AnalyzeRow<TLanes, TWaveletOperator>(width, buffer, line, lowWidth);
            }

            for (int column = 0; column < width; column++)
            {
                for (int row = 0; row < height; row++)
                {
                    buffer[row + height] = coefficients[(row * 8) + column];
                }

                AnalyzeColumn<TLanes, TWaveletOperator>(height, buffer, lowHeight);
                for (int row = 0; row < height; row++)
                {
                    coefficients[(row * 8) + column] = buffer[row];
                }
            }

            height = lowHeight;
            width = lowWidth;
        }

        int energy = 0;
        for (int row = 0; row < 8; row++)
        {
            for (int column = row < 4 ? 4 : 0; column < 8; column++)
            {
                energy += TWaveletOperator.SumAbsolute(coefficients[(row * 8) + column]);
            }
        }

        return energy;
    }

    /// <summary>
    /// Splits one row into a doubled low band and a high band by lifting. Reference: analysis_53_row().
    /// </summary>
    /// <typeparam name="TLanes">The lane type.</typeparam>
    /// <typeparam name="TWaveletOperator">The lane arithmetic.</typeparam>
    /// <param name="length">The even number of samples.</param>
    /// <param name="input">The samples.</param>
    /// <param name="output">Receives the low band followed by the high band.</param>
    /// <param name="half">The length of each band.</param>
    private static void AnalyzeRow<TLanes, TWaveletOperator>(int length, ReadOnlySpan<TLanes> input, Span<TLanes> output, int half)
        where TLanes : unmanaged
        where TWaveletOperator : struct, IWaveletOperator<TLanes>
    {
        int pairs = length >> 1;
        for (int k = 0; k < pairs - 1; k++)
        {
            TLanes even = input[2 * k];
            output[k] = TWaveletOperator.Add(even, even);

            // The high band predicts each odd sample from the rounded mean of its even neighbors.
            TLanes mean = TWaveletOperator.ShiftRight(TWaveletOperator.AddConstant(TWaveletOperator.Add(even, input[(2 * k) + 2]), 1), 1);
            output[half + k] = TWaveletOperator.Subtract(input[(2 * k) + 1], mean);
        }

        TLanes last = input[length - 2];
        output[pairs - 1] = TWaveletOperator.Add(last, last);
        output[half + pairs - 1] = TWaveletOperator.Subtract(input[length - 1], last);
        UpdateLowBand<TLanes, TWaveletOperator>(output, half, pairs);
    }

    /// <summary>
    /// Splits one column into a low band and a high band by lifting, with the high band halved and rounded.
    /// The column is read from the second half of the buffer and written to its first half.
    /// Reference: analysis_53_col().
    /// </summary>
    /// <typeparam name="TLanes">The lane type.</typeparam>
    /// <typeparam name="TWaveletOperator">The lane arithmetic.</typeparam>
    /// <param name="length">The even number of samples.</param>
    /// <param name="buffer">The buffer holding the column after its first <paramref name="length"/> entries.</param>
    /// <param name="half">The length of each band.</param>
    private static void AnalyzeColumn<TLanes, TWaveletOperator>(int length, Span<TLanes> buffer, int half)
        where TLanes : unmanaged
        where TWaveletOperator : struct, IWaveletOperator<TLanes>
    {
        int pairs = length >> 1;
        for (int k = 0; k < pairs - 1; k++)
        {
            TLanes even = buffer[length + (2 * k)];
            TLanes odd = buffer[length + (2 * k) + 1];
            buffer[k] = even;
            TLanes doubled = TWaveletOperator.Add(odd, odd);
            TLanes neighbors = TWaveletOperator.Add(even, buffer[length + (2 * k) + 2]);
            buffer[half + k] = TWaveletOperator.ShiftRight(TWaveletOperator.AddConstant(TWaveletOperator.Subtract(doubled, neighbors), 2), 2);
        }

        TLanes last = buffer[(2 * length) - 2];
        buffer[pairs - 1] = last;
        buffer[half + pairs - 1] = TWaveletOperator.ShiftRight(
            TWaveletOperator.AddConstant(TWaveletOperator.Subtract(buffer[(2 * length) - 1], last), 1), 1);

        UpdateLowBand<TLanes, TWaveletOperator>(buffer, half, pairs);
    }

    /// <summary>
    /// Adds the rounded mean of the neighboring high-band values to each low-band value, repeating the first
    /// high-band value before the band. Reference: the update loop of analysis_53_row() and analysis_53_col().
    /// </summary>
    /// <typeparam name="TLanes">The lane type.</typeparam>
    /// <typeparam name="TWaveletOperator">The lane arithmetic.</typeparam>
    /// <param name="bands">The low band followed by the high band.</param>
    /// <param name="half">The offset of the high band.</param>
    /// <param name="pairs">The length of each band.</param>
    private static void UpdateLowBand<TLanes, TWaveletOperator>(Span<TLanes> bands, int half, int pairs)
        where TLanes : unmanaged
        where TWaveletOperator : struct, IWaveletOperator<TLanes>
    {
        TLanes previous = bands[half];
        for (int k = 0; k < pairs; k++)
        {
            TLanes high = bands[half + k];
            TLanes update = TWaveletOperator.ShiftRight(TWaveletOperator.AddConstant(TWaveletOperator.Add(previous, high), 1), 1);
            bands[k] = TWaveletOperator.Add(bands[k], update);
            previous = high;
        }
    }

    /// <summary>
    /// Transforms one block at a time.
    /// </summary>
    private readonly struct ScalarWaveletOperator : IWaveletOperator<int>
    {
        /// <inheritdoc/>
        public static int Count => 1;

        /// <inheritdoc/>
        public static int Load(ReadOnlySpan<TSample> source, ReadOnlySpan<int> origins, int offset)
            => TOperator.ToInt32(source[origins[0] + offset]) << 2;

        /// <inheritdoc/>
        public static int Add(int left, int right) => left + right;

        /// <inheritdoc/>
        public static int Subtract(int left, int right) => left - right;

        /// <inheritdoc/>
        public static int ShiftRight(int value, int count) => value >> count;

        /// <inheritdoc/>
        public static int AddConstant(int value, int constant) => value + constant;

        /// <inheritdoc/>
        public static int SumAbsolute(int value) => Math.Abs(value);
    }

    /// <summary>
    /// Transforms four blocks at once, one per 32-bit lane.
    /// </summary>
    private readonly struct Vector128WaveletOperator : IWaveletOperator<Vector128<int>>
    {
        /// <inheritdoc/>
        public static int Count => Vector128<int>.Count;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Load(ReadOnlySpan<TSample> source, ReadOnlySpan<int> origins, int offset)
        {
            // The blocks lie on different rows or columns, so each lane gathers its own sample.
            Vector128<int> samples = Vector128.Create(
                TOperator.ToInt32(source[origins[0] + offset]),
                TOperator.ToInt32(source[origins[1] + offset]),
                TOperator.ToInt32(source[origins[2] + offset]),
                TOperator.ToInt32(source[origins[3] + offset]));

            return Vector128.ShiftLeft(samples, 2);
        }

        /// <inheritdoc/>
        public static Vector128<int> Add(Vector128<int> left, Vector128<int> right) => left + right;

        /// <inheritdoc/>
        public static Vector128<int> Subtract(Vector128<int> left, Vector128<int> right) => left - right;

        /// <inheritdoc/>
        public static Vector128<int> ShiftRight(Vector128<int> value, int count) => Vector128.ShiftRightArithmetic(value, count);

        /// <inheritdoc/>
        public static Vector128<int> AddConstant(Vector128<int> value, int constant) => value + Vector128.Create(constant);

        /// <inheritdoc/>
        public static int SumAbsolute(Vector128<int> value) => Vector128.Sum(Vector128.Abs(value));
    }
}
