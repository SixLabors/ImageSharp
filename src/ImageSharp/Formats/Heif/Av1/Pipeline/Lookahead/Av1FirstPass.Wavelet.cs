// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

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
        /// Loads one row of horizontally adjacent blocks, scaled by four, with the lanes of each column holding
        /// the blocks. Reference: the input scaling of dyadic_analyze_53_uint8_input().
        /// </summary>
        /// <param name="source">The source plane.</param>
        /// <param name="index">The source index of the row in the first block.</param>
        /// <param name="row">Receives the eight columns of the row.</param>
        public static abstract void LoadRow(ReadOnlySpan<TSample> source, int index, Span<TLanes> row);

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
        /// Takes the absolute value of every lane. Reference: haar_ac_sad().
        /// </summary>
        /// <param name="value">The lanes.</param>
        /// <returns>The magnitudes.</returns>
        public static abstract TLanes Abs(TLanes value);

        /// <summary>
        /// Stores every lane, one block per element.
        /// </summary>
        /// <param name="value">The lanes.</param>
        /// <param name="destination">Receives the lanes.</param>
        public static abstract void Store(TLanes value, Span<int> destination);
    }

    /// <summary>
    /// Measures the wavelet AC energy of horizontally adjacent 8x8 blocks, walking the widest register that the
    /// remaining blocks fill before the scalar tail. Reference: haar_ac_sad_8x8_uint8_input() for each block of
    /// av1_haar_ac_sad_mxn_uint8_input().
    /// </summary>
    /// <param name="source">The source plane.</param>
    /// <param name="index">The source index of the first block origin.</param>
    /// <param name="stride">The source row stride.</param>
    /// <param name="energies">Receives the energy of each block.</param>
    internal static void GetWaveletEnergies(ReadOnlySpan<TSample> source, int index, int stride, Span<int> energies)
    {
        int count = energies.Length;
        int block = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; block <= count - Vector512WaveletOperator.Count; block += Vector512WaveletOperator.Count)
            {
                GetHaarAcSad<Vector512<int>, Vector512WaveletOperator>(source, index + (block * 8), stride, energies[block..]);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; block <= count - Vector256WaveletOperator.Count; block += Vector256WaveletOperator.Count)
            {
                GetHaarAcSad<Vector256<int>, Vector256WaveletOperator>(source, index + (block * 8), stride, energies[block..]);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; block <= count - Vector128WaveletOperator.Count; block += Vector128WaveletOperator.Count)
            {
                GetHaarAcSad<Vector128<int>, Vector128WaveletOperator>(source, index + (block * 8), stride, energies[block..]);
            }
        }

        for (; block < count; block++)
        {
            GetHaarAcSad<int, ScalarWaveletOperator>(source, index + (block * 8), stride, energies[block..]);
        }
    }

    /// <summary>
    /// Transforms 8x8 blocks with three levels of the 5/3 wavelet, rows before columns at each level, and sums
    /// the magnitudes outside the 4x4 low band. Reference: haar_ac_sad_8x8_uint8_input() with
    /// av1_fdwt8x8_uint8_input_c() and haar_ac_sad().
    /// </summary>
    /// <typeparam name="TLanes">The lane type.</typeparam>
    /// <typeparam name="TWaveletOperator">The lane arithmetic.</typeparam>
    /// <param name="source">The source plane.</param>
    /// <param name="index">The source index of the first block origin.</param>
    /// <param name="stride">The source row stride.</param>
    /// <param name="energies">Receives the energy of each block, one per lane.</param>
    private static void GetHaarAcSad<TLanes, TWaveletOperator>(ReadOnlySpan<TSample> source, int index, int stride, Span<int> energies)
        where TLanes : unmanaged
        where TWaveletOperator : struct, IWaveletOperator<TLanes>
    {
        Span<TLanes> coefficients = stackalloc TLanes[64];
        Span<TLanes> buffer = stackalloc TLanes[16];
        for (int row = 0; row < 8; row++)
        {
            TWaveletOperator.LoadRow(source, index + (row * stride), coefficients.Slice(row * 8, 8));
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

        // Each block's sum is exact in its own lane, so the lanes add in any order.
        TLanes energy = default;
        for (int row = 0; row < 8; row++)
        {
            for (int column = row < 4 ? 4 : 0; column < 8; column++)
            {
                energy = TWaveletOperator.Add(energy, TWaveletOperator.Abs(coefficients[(row * 8) + column]));
            }
        }

        TWaveletOperator.Store(energy, energies);
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
    /// Adds the wavelet AC energy of every unit of one row to its record. The energy covers the whole unit even
    /// where the measured block is smaller. Reference: the av1_haar_ac_sad_mxn_uint8_input() call of
    /// firstpass_intra_prediction().
    /// </summary>
    /// <param name="frame">The frame being measured.</param>
    /// <param name="unitRow">The unit row.</param>
    /// <param name="unitColumnStart">The first unit column of the tile.</param>
    /// <param name="unitCount">The number of units in the row of the tile.</param>
    /// <param name="records">The records of the row's units in the tile.</param>
    private void AddWaveletEnergies(ref FrameContext frame, int unitRow, int unitColumnStart, int unitCount, Span<FrameStatistics> records)
    {
        // The units of a row lie side by side, so each row of 8x8 blocks runs across all of them.
        int blocksPerSide = frame.FirstPassBlockSize.GetWidth() / 8;
        int unitIndex = frame.SourceOrigin +
            (((unitRow << frame.UnitLog2) << 2) * frame.SourceStride) +
            ((unitColumnStart << frame.UnitLog2) << 2);

        Span<int> energies = this.waveletEnergies.AsSpan(0, unitCount * blocksPerSide);
        for (int blockRow = 0; blockRow < blocksPerSide; blockRow++)
        {
            GetWaveletEnergies(frame.Source, unitIndex + (blockRow * 8 * frame.SourceStride), frame.SourceStride, energies);
            for (int block = 0; block < energies.Length; block++)
            {
                records[block / blocksPerSide].FrameAverageWaveletEnergy += energies[block];
            }
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
        public static void LoadRow(ReadOnlySpan<TSample> source, int index, Span<int> row)
        {
            for (int column = 0; column < 8; column++)
            {
                row[column] = TOperator.ToInt32(source[index + column]) << 2;
            }
        }

        /// <inheritdoc/>
        public static int Add(int left, int right) => left + right;

        /// <inheritdoc/>
        public static int Subtract(int left, int right) => left - right;

        /// <inheritdoc/>
        public static int ShiftRight(int value, int count) => value >> count;

        /// <inheritdoc/>
        public static int AddConstant(int value, int constant) => value + constant;

        /// <inheritdoc/>
        public static int Abs(int value) => Math.Abs(value);

        /// <inheritdoc/>
        public static void Store(int value, Span<int> destination) => destination[0] = value;
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
        public static void LoadRow(ReadOnlySpan<TSample> source, int index, Span<Vector128<int>> row)
        {
            // Each block row loads as two halves of four samples; transposing each set of halves turns the
            // blocks into lanes.
            for (int half = 0; half < 2; half++)
            {
                int start = index + (half * 4);
                Vector128<int> block0 = TOperator.LoadWidened(source, start, default(Vector128<int>));
                Vector128<int> block1 = TOperator.LoadWidened(source, start + 8, default(Vector128<int>));
                Vector128<int> block2 = TOperator.LoadWidened(source, start + 16, default(Vector128<int>));
                Vector128<int> block3 = TOperator.LoadWidened(source, start + 24, default(Vector128<int>));
                Av1Transform2dOperations.Transpose(ref block0, ref block1, ref block2, ref block3);
                row[half * 4] = block0 << 2;
                row[(half * 4) + 1] = block1 << 2;
                row[(half * 4) + 2] = block2 << 2;
                row[(half * 4) + 3] = block3 << 2;
            }
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
        public static Vector128<int> Abs(Vector128<int> value) => Vector128.Abs(value);

        /// <inheritdoc/>
        public static void Store(Vector128<int> value, Span<int> destination) => value.CopyTo(destination);
    }

    /// <summary>
    /// Transforms eight blocks at once, one per 32-bit lane.
    /// </summary>
    private readonly struct Vector256WaveletOperator : IWaveletOperator<Vector256<int>>
    {
        /// <inheritdoc/>
        public static int Count => Vector256<int>.Count;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LoadRow(ReadOnlySpan<TSample> source, int index, Span<Vector256<int>> row)
        {
            // Transposing the eight block rows turns the blocks into lanes.
            Vector256<int> block0 = TOperator.LoadWidened(source, index, default(Vector256<int>));
            Vector256<int> block1 = TOperator.LoadWidened(source, index + 8, default(Vector256<int>));
            Vector256<int> block2 = TOperator.LoadWidened(source, index + 16, default(Vector256<int>));
            Vector256<int> block3 = TOperator.LoadWidened(source, index + 24, default(Vector256<int>));
            Vector256<int> block4 = TOperator.LoadWidened(source, index + 32, default(Vector256<int>));
            Vector256<int> block5 = TOperator.LoadWidened(source, index + 40, default(Vector256<int>));
            Vector256<int> block6 = TOperator.LoadWidened(source, index + 48, default(Vector256<int>));
            Vector256<int> block7 = TOperator.LoadWidened(source, index + 56, default(Vector256<int>));
            Av1Transform2dOperations.Transpose(ref block0, ref block1, ref block2, ref block3, ref block4, ref block5, ref block6, ref block7);
            row[0] = block0 << 2;
            row[1] = block1 << 2;
            row[2] = block2 << 2;
            row[3] = block3 << 2;
            row[4] = block4 << 2;
            row[5] = block5 << 2;
            row[6] = block6 << 2;
            row[7] = block7 << 2;
        }

        /// <inheritdoc/>
        public static Vector256<int> Add(Vector256<int> left, Vector256<int> right) => left + right;

        /// <inheritdoc/>
        public static Vector256<int> Subtract(Vector256<int> left, Vector256<int> right) => left - right;

        /// <inheritdoc/>
        public static Vector256<int> ShiftRight(Vector256<int> value, int count) => Vector256.ShiftRightArithmetic(value, count);

        /// <inheritdoc/>
        public static Vector256<int> AddConstant(Vector256<int> value, int constant) => value + Vector256.Create(constant);

        /// <inheritdoc/>
        public static Vector256<int> Abs(Vector256<int> value) => Vector256.Abs(value);

        /// <inheritdoc/>
        public static void Store(Vector256<int> value, Span<int> destination) => value.CopyTo(destination);
    }

    /// <summary>
    /// Transforms sixteen blocks at once, one per 32-bit lane.
    /// </summary>
    private readonly struct Vector512WaveletOperator : IWaveletOperator<Vector512<int>>
    {
        /// <inheritdoc/>
        public static int Count => Vector512<int>.Count;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LoadRow(ReadOnlySpan<TSample> source, int index, Span<Vector512<int>> row)
        {
            // The first and last eight blocks transpose separately into the lower and upper halves of the lanes.
            InlineArray8<Vector256<int>> lower = default;
            InlineArray8<Vector256<int>> upper = default;
            Vector256WaveletOperator.LoadRow(source, index, lower);
            Vector256WaveletOperator.LoadRow(source, index + 64, upper);
            for (int column = 0; column < 8; column++)
            {
                row[column] = Vector512.Create(lower[column], upper[column]);
            }
        }

        /// <inheritdoc/>
        public static Vector512<int> Add(Vector512<int> left, Vector512<int> right) => left + right;

        /// <inheritdoc/>
        public static Vector512<int> Subtract(Vector512<int> left, Vector512<int> right) => left - right;

        /// <inheritdoc/>
        public static Vector512<int> ShiftRight(Vector512<int> value, int count) => Vector512.ShiftRightArithmetic(value, count);

        /// <inheritdoc/>
        public static Vector512<int> AddConstant(Vector512<int> value, int constant) => value + Vector512.Create(constant);

        /// <inheritdoc/>
        public static Vector512<int> Abs(Vector512<int> value) => Vector512.Abs(value);

        /// <inheritdoc/>
        public static void Store(Vector512<int> value, Span<int> destination) => value.CopyTo(destination);
    }
}
