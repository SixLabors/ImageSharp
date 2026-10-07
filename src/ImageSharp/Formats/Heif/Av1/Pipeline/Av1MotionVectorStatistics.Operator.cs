// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the source texture measure of the motion vector statistics.
/// </content>
internal sealed partial class Av1MotionVectorStatistics
{
    /// <summary>
    /// Loads source samples widened to sixteen-bit lanes across hardware widths.
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    internal interface ITextureOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Loads eight consecutive samples.
        /// </summary>
        /// <param name="source">The first sample of the row.</param>
        /// <param name="index">The index of the first loaded sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The samples.</returns>
        public static abstract Vector128<ushort> Load(ref TSample source, nuint index, Vector128<ushort> lanes);

        /// <summary>
        /// Loads sixteen consecutive samples.
        /// </summary>
        /// <param name="source">The first sample of the row.</param>
        /// <param name="index">The index of the first loaded sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The samples.</returns>
        public static abstract Vector256<ushort> Load(ref TSample source, nuint index, Vector256<ushort> lanes);

        /// <summary>
        /// Loads thirty-two consecutive samples.
        /// </summary>
        /// <param name="source">The first sample of the row.</param>
        /// <param name="index">The index of the first loaded sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The samples.</returns>
        public static abstract Vector512<ushort> Load(ref TSample source, nuint index, Vector512<ushort> lanes);

        /// <summary>
        /// Loads one sample.
        /// </summary>
        /// <param name="source">The first sample of the row.</param>
        /// <param name="index">The index of the sample.</param>
        /// <returns>The sample.</returns>
        public static abstract int Load(ref TSample source, nuint index);
    }

    /// <summary>
    /// Adds the source texture of one block: over every sample but the last row and column, the absolute horizontal
    /// and vertical differences to the next sample, shifted to eight bits, and their product. The block may extend
    /// past the frame, where the source border repeats the last row and column: a column from the last one on has no
    /// horizontal difference and the vertical difference of the last column, and a row from the last one on has no
    /// vertical difference. The totals wrap like the reference's integer sums. Reference: the texture loops of
    /// collect_mv_stats_b().
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    /// <typeparam name="TOperator">The texture operator of the sample type.</typeparam>
    /// <param name="luma">The visible luma plane.</param>
    /// <param name="lumaSamples">The samples of <paramref name="luma"/>, which the caller reads once outside its block loop.</param>
    /// <param name="origin">The block origin.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="shift">The shift that reduces a difference to eight bits.</param>
    /// <param name="horizontal">The horizontal total.</param>
    /// <param name="vertical">The vertical total.</param>
    /// <param name="diagonal">The product total.</param>
    internal static void AccumulateTexture<TSample, TOperator>(
        Av1PlaneRegion<TSample> luma,
        ReadOnlySpan<TSample> lumaSamples,
        Point origin,
        int width,
        int height,
        int shift,
        ref int horizontal,
        ref int vertical,
        ref int diagonal)
        where TSample : unmanaged
        where TOperator : struct, ITextureOperator<TSample>
    {
        int lastColumn = luma.Width - 1;
        int lastRow = luma.Height - 1;
        int columns = width - 1;
        int inside = Math.Clamp(lastColumn - origin.X, 0, columns);
        int outside = columns - inside;
        Vector512<int> horizontal512 = Vector512<int>.Zero;
        Vector512<int> vertical512 = Vector512<int>.Zero;
        Vector512<int> diagonal512 = Vector512<int>.Zero;
        Vector256<int> horizontal256 = Vector256<int>.Zero;
        Vector256<int> vertical256 = Vector256<int>.Zero;
        Vector256<int> diagonal256 = Vector256<int>.Zero;
        Vector128<int> horizontal128 = Vector128<int>.Zero;
        Vector128<int> vertical128 = Vector128<int>.Zero;
        Vector128<int> diagonal128 = Vector128<int>.Zero;
        int horizontalTotal = 0;
        int verticalTotal = 0;
        int diagonalTotal = 0;
        ref TSample lumaOrigin = ref MemoryMarshal.GetReference(lumaSamples);
        for (int row = 0; row < height - 1; row++)
        {
            ref TSample currentRow = ref Unsafe.Add(ref lumaOrigin, (nuint)luma.GetOffset(0, Math.Min(origin.Y + row, lastRow)));
            ref TSample nextRow = ref Unsafe.Add(ref lumaOrigin, (nuint)luma.GetOffset(0, Math.Min(origin.Y + row + 1, lastRow)));
            if (outside > 0)
            {
                int edge = Math.Abs(TOperator.Load(ref nextRow, (nuint)lastColumn) - TOperator.Load(ref currentRow, (nuint)lastColumn)) >> shift;
                verticalTotal += outside * edge;
            }

            if (inside == 0)
            {
                continue;
            }

            currentRow = ref Unsafe.Add(ref currentRow, origin.X);
            nextRow = ref Unsafe.Add(ref nextRow, origin.X);
            nuint x = 0;
            nuint count = (nuint)inside;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; x + (nuint)Vector512<ushort>.Count <= count; x += (nuint)Vector512<ushort>.Count)
                {
                    AccumulateGradients(
                        TOperator.Load(ref currentRow, x, Vector512<ushort>.Zero),
                        TOperator.Load(ref currentRow, x + 1, Vector512<ushort>.Zero),
                        TOperator.Load(ref nextRow, x, Vector512<ushort>.Zero),
                        shift,
                        ref horizontal512,
                        ref vertical512,
                        ref diagonal512);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x + (nuint)Vector256<ushort>.Count <= count; x += (nuint)Vector256<ushort>.Count)
                {
                    AccumulateGradients(
                        TOperator.Load(ref currentRow, x, Vector256<ushort>.Zero),
                        TOperator.Load(ref currentRow, x + 1, Vector256<ushort>.Zero),
                        TOperator.Load(ref nextRow, x, Vector256<ushort>.Zero),
                        shift,
                        ref horizontal256,
                        ref vertical256,
                        ref diagonal256);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x + (nuint)Vector128<ushort>.Count <= count; x += (nuint)Vector128<ushort>.Count)
                {
                    AccumulateGradients(
                        TOperator.Load(ref currentRow, x, Vector128<ushort>.Zero),
                        TOperator.Load(ref currentRow, x + 1, Vector128<ushort>.Zero),
                        TOperator.Load(ref nextRow, x, Vector128<ushort>.Zero),
                        shift,
                        ref horizontal128,
                        ref vertical128,
                        ref diagonal128);
                }
            }

            for (; x < count; x++)
            {
                int current = TOperator.Load(ref currentRow, x);
                int horizontalDifference = Math.Abs(TOperator.Load(ref currentRow, x + 1) - current) >> shift;
                int verticalDifference = Math.Abs(TOperator.Load(ref nextRow, x) - current) >> shift;
                horizontalTotal += horizontalDifference;
                verticalTotal += verticalDifference;
                diagonalTotal += horizontalDifference * verticalDifference;
            }
        }

        horizontal256 += horizontal512.GetLower() + horizontal512.GetUpper();
        vertical256 += vertical512.GetLower() + vertical512.GetUpper();
        diagonal256 += diagonal512.GetLower() + diagonal512.GetUpper();
        horizontal128 += horizontal256.GetLower() + horizontal256.GetUpper();
        vertical128 += vertical256.GetLower() + vertical256.GetUpper();
        diagonal128 += diagonal256.GetLower() + diagonal256.GetUpper();
        horizontal += horizontalTotal + Vector128.Sum(horizontal128);
        vertical += verticalTotal + Vector128.Sum(vertical128);
        diagonal += diagonalTotal + Vector128.Sum(diagonal128);
    }

    /// <summary>
    /// Adds the gradients of eight samples to lane totals whose spread across the lanes is not defined.
    /// </summary>
    /// <remarks>
    /// A shifted difference is at most 255, so the product fits sixteen bits. The totals wrap like the reference's
    /// integer sums, which only the lane sum needs.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateGradients(
        Vector128<ushort> current,
        Vector128<ushort> right,
        Vector128<ushort> below,
        int shift,
        ref Vector128<int> horizontal,
        ref Vector128<int> vertical,
        ref Vector128<int> diagonal)
    {
        Vector128<ushort> horizontalDifference = Vector128.ShiftRightLogical(Vector128.Max(current, right) - Vector128.Min(current, right), shift);
        Vector128<ushort> verticalDifference = Vector128.ShiftRightLogical(Vector128.Max(current, below) - Vector128.Min(current, below), shift);
        (Vector128<uint> horizontalLower, Vector128<uint> horizontalUpper) = Vector128.Widen(horizontalDifference);
        (Vector128<uint> verticalLower, Vector128<uint> verticalUpper) = Vector128.Widen(verticalDifference);
        (Vector128<uint> diagonalLower, Vector128<uint> diagonalUpper) = Vector128.Widen(horizontalDifference * verticalDifference);
        horizontal += (horizontalLower + horizontalUpper).AsInt32();
        vertical += (verticalLower + verticalUpper).AsInt32();
        diagonal += (diagonalLower + diagonalUpper).AsInt32();
    }

    /// <summary>
    /// Adds the gradients of sixteen samples to lane totals whose spread across the lanes is not defined.
    /// </summary>
    /// <remarks>
    /// A shifted difference is at most 255, so the product fits sixteen bits. The totals wrap like the reference's
    /// integer sums, which only the lane sum needs.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateGradients(
        Vector256<ushort> current,
        Vector256<ushort> right,
        Vector256<ushort> below,
        int shift,
        ref Vector256<int> horizontal,
        ref Vector256<int> vertical,
        ref Vector256<int> diagonal)
    {
        Vector256<ushort> horizontalDifference = Vector256.ShiftRightLogical(Vector256.Max(current, right) - Vector256.Min(current, right), shift);
        Vector256<ushort> verticalDifference = Vector256.ShiftRightLogical(Vector256.Max(current, below) - Vector256.Min(current, below), shift);
        (Vector256<uint> horizontalLower, Vector256<uint> horizontalUpper) = Vector256.Widen(horizontalDifference);
        (Vector256<uint> verticalLower, Vector256<uint> verticalUpper) = Vector256.Widen(verticalDifference);
        (Vector256<uint> diagonalLower, Vector256<uint> diagonalUpper) = Vector256.Widen(horizontalDifference * verticalDifference);
        horizontal += (horizontalLower + horizontalUpper).AsInt32();
        vertical += (verticalLower + verticalUpper).AsInt32();
        diagonal += (diagonalLower + diagonalUpper).AsInt32();
    }

    /// <summary>
    /// Adds the gradients of thirty-two samples to lane totals whose spread across the lanes is not defined.
    /// </summary>
    /// <remarks>
    /// A shifted difference is at most 255, so the product fits sixteen bits. The totals wrap like the reference's
    /// integer sums, which only the lane sum needs.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateGradients(
        Vector512<ushort> current,
        Vector512<ushort> right,
        Vector512<ushort> below,
        int shift,
        ref Vector512<int> horizontal,
        ref Vector512<int> vertical,
        ref Vector512<int> diagonal)
    {
        Vector512<ushort> horizontalDifference = Vector512.ShiftRightLogical(Vector512.Max(current, right) - Vector512.Min(current, right), shift);
        Vector512<ushort> verticalDifference = Vector512.ShiftRightLogical(Vector512.Max(current, below) - Vector512.Min(current, below), shift);
        (Vector512<uint> horizontalLower, Vector512<uint> horizontalUpper) = Vector512.Widen(horizontalDifference);
        (Vector512<uint> verticalLower, Vector512<uint> verticalUpper) = Vector512.Widen(verticalDifference);
        (Vector512<uint> diagonalLower, Vector512<uint> diagonalUpper) = Vector512.Widen(horizontalDifference * verticalDifference);
        horizontal += (horizontalLower + horizontalUpper).AsInt32();
        vertical += (verticalLower + verticalUpper).AsInt32();
        diagonal += (diagonalLower + diagonalUpper).AsInt32();
    }

    /// <summary>
    /// Loads eight-bit samples.
    /// </summary>
    internal readonly struct ByteTextureOperator : ITextureOperator<byte>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> Load(ref byte source, nuint index, Vector128<ushort> lanes)
            => Vector128.WidenLower(Vector128.CreateScalar(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, index))).AsByte());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> Load(ref byte source, nuint index, Vector256<ushort> lanes)
            => Vector256.WidenLower(Vector128.LoadUnsafe(ref source, index).ToVector256Unsafe());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> Load(ref byte source, nuint index, Vector512<ushort> lanes)
            => Vector512.WidenLower(Vector256.LoadUnsafe(ref source, index).ToVector512Unsafe());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Load(ref byte source, nuint index) => Unsafe.Add(ref source, index);
    }

    /// <summary>
    /// Loads high bit depth samples.
    /// </summary>
    internal readonly struct UInt16TextureOperator : ITextureOperator<ushort>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> Load(ref ushort source, nuint index, Vector128<ushort> lanes)
            => Vector128.LoadUnsafe(ref source, index);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> Load(ref ushort source, nuint index, Vector256<ushort> lanes)
            => Vector256.LoadUnsafe(ref source, index);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> Load(ref ushort source, nuint index, Vector512<ushort> lanes)
            => Vector512.LoadUnsafe(ref source, index);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Load(ref ushort source, nuint index) => Unsafe.Add(ref source, index);
    }
}
