// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Applies the reversible four-by-four inverse Walsh-Hadamard transform used by lossless AV1 segments.
/// </summary>
/// <remarks>
/// The vector path stores one transform row in each <see cref="Vector128{T}"/> and one column position in each lane.
/// Register transposes exchange the two transform dimensions between identical reversible butterflies. Reconstruction
/// then adds four consecutive residual lanes to each prediction row with exact-width output stores.
/// </remarks>
internal static class Av1InverseWalshHadamardTransformer
{
    /// <summary>
    /// The coefficient normalization shift applied before the first transform dimension.
    /// </summary>
    private const int UnitQuantizationShift = 2;

    /// <summary>
    /// Reconstructs a lossless transform block into eight-bit sample storage.
    /// </summary>
    /// <param name="coefficients">The sixteen dequantized coefficients in raster order.</param>
    /// <param name="readBuffer">The predicted samples read by reconstruction.</param>
    /// <param name="readStride">The number of read samples between rows.</param>
    /// <param name="writeBuffer">The destination reconstructed samples.</param>
    /// <param name="writeStride">The number of destination samples between rows.</param>
    /// <param name="coefficientCount">The decoded coefficient end position.</param>
    /// <param name="workspace">The reusable transform workspace.</param>
    public static void TransformAdd(
        ReadOnlySpan<int> coefficients,
        Span<byte> readBuffer,
        int readStride,
        Span<byte> writeBuffer,
        int writeStride,
        int coefficientCount,
        Span<int> workspace)
        => TransformAdd<byte, Av1InverseTransformer.OutputOperator<byte>>(
            coefficients,
            readBuffer,
            readStride,
            writeBuffer,
            writeStride,
            coefficientCount,
            workspace,
            8);

    /// <summary>
    /// Reconstructs a lossless transform block into high-bit-depth sample storage.
    /// </summary>
    /// <param name="coefficients">The sixteen dequantized coefficients in raster order.</param>
    /// <param name="readBuffer">The predicted samples read by reconstruction.</param>
    /// <param name="readStride">The number of read samples between rows.</param>
    /// <param name="writeBuffer">The destination reconstructed samples.</param>
    /// <param name="writeStride">The number of destination samples between rows.</param>
    /// <param name="coefficientCount">The decoded coefficient end position.</param>
    /// <param name="workspace">The reusable transform workspace.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    public static void TransformAdd(
        ReadOnlySpan<int> coefficients,
        Span<short> readBuffer,
        int readStride,
        Span<short> writeBuffer,
        int writeStride,
        int coefficientCount,
        Span<int> workspace,
        int bitDepth)
        => TransformAdd<short, Av1InverseTransformer.OutputOperator<short>>(
            coefficients,
            readBuffer,
            readStride,
            writeBuffer,
            writeStride,
            coefficientCount,
            workspace,
            bitDepth);

    /// <summary>
    /// Selects the packed or scalar four-by-four reconstruction path.
    /// </summary>
    private static void TransformAdd<TSample, TOutputOperator>(
        ReadOnlySpan<int> coefficients,
        Span<TSample> readBuffer,
        int readStride,
        Span<TSample> writeBuffer,
        int writeStride,
        int coefficientCount,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
    {
        if (Vector128.IsHardwareAccelerated)
        {
            TransformVector<TSample, TOutputOperator>(coefficients, readBuffer, readStride, writeBuffer, writeStride, coefficientCount, bitDepth);
            return;
        }

        TransformScalar<TSample, TOutputOperator>(coefficients, readBuffer, readStride, writeBuffer, writeStride, coefficientCount, workspace, bitDepth);
    }

    /// <summary>
    /// Applies both reversible transform dimensions to four packed coefficient rows.
    /// </summary>
    private static void TransformVector<TSample, TOutputOperator>(
        ReadOnlySpan<int> coefficients,
        Span<TSample> readBuffer,
        int readStride,
        Span<TSample> writeBuffer,
        int writeStride,
        int coefficientCount,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
    {
        ref int coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        Vector128<int> row0;
        Vector128<int> row1;
        Vector128<int> row2;
        Vector128<int> row3;

        if (coefficientCount == 1)
        {
            // The DC-only form bypasses fifteen known-zero coefficients and both full butterflies. These divisions
            // deliberately use arithmetic shifts because negative coefficients must round toward negative infinity.
            int first = Unsafe.Add(ref coefficientBase, 0) >> UnitQuantizationShift;
            int half = first >> 1;
            Vector128<int> intermediate = Vector128.Create(first - half, half, half, half);

            row1 = intermediate >> 1;
            row0 = intermediate - row1;
            row2 = row1;
            row3 = row1;
        }
        else
        {
            row0 = Vector128.LoadUnsafe(ref coefficientBase) >> UnitQuantizationShift;
            row1 = Vector128.LoadUnsafe(ref coefficientBase, 4) >> UnitQuantizationShift;
            row2 = Vector128.LoadUnsafe(ref coefficientBase, 8) >> UnitQuantizationShift;
            row3 = Vector128.LoadUnsafe(ref coefficientBase, 12) >> UnitQuantizationShift;

            // Entropy decoding normalizes AV1's column-major coefficient positions to the row-major transform
            // workspace. Restore the normative dimension order before either reversible butterfly performs its
            // signed half shift; swapping the dimensions after those shifts would not preserve lossless rounding.
            Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3);
            Transform(ref row0, ref row1, ref row2, ref row3);

            // The first pass produces four packed intermediate columns. Transposition turns those columns into rows
            // so the same reversible butterfly implements the second dimension without scratch.
            Av1Transform2dOperations.Transpose(ref row0, ref row1, ref row2, ref row3);
            Transform(ref row0, ref row1, ref row2, ref row3);
        }

        AddRows<TSample, TOutputOperator>(readBuffer, readStride, writeBuffer, writeStride, row0, row1, row2, row3, bitDepth);
    }

    /// <summary>
    /// Applies both reversible transform dimensions without hardware intrinsics.
    /// </summary>
    private static void TransformScalar<TSample, TOutputOperator>(
        ReadOnlySpan<int> coefficients,
        Span<TSample> readBuffer,
        int readStride,
        Span<TSample> writeBuffer,
        int writeStride,
        int coefficientCount,
        Span<int> workspace,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
    {
        ref TSample readBase = ref MemoryMarshal.GetReference(readBuffer);
        ref TSample writeBase = ref MemoryMarshal.GetReference(writeBuffer);

        if (coefficientCount == 1)
        {
            int first = coefficients[0] >> UnitQuantizationShift;
            int half = first >> 1;
            int firstResidual = first - half;

            for (int column = 0; column < 4; column++)
            {
                int intermediate = column == 0 ? firstResidual : half;
                int repeatedResidual = intermediate >> 1;
                int topResidual = intermediate - repeatedResidual;

                Unsafe.Add(ref writeBase, column) = TOutputOperator.Add(Unsafe.Add(ref readBase, column), topResidual, bitDepth);

                for (int row = 1; row < 4; row++)
                {
                    int readOffset = (row * readStride) + column;
                    int writeOffset = (row * writeStride) + column;
                    Unsafe.Add(ref writeBase, writeOffset) = TOutputOperator.Add(Unsafe.Add(ref readBase, readOffset), repeatedResidual, bitDepth);
                }
            }

            return;
        }

        ref int coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        ref int intermediateBase = ref MemoryMarshal.GetReference(workspace);

        // Entropy decoding stores the transposed scan in row-major order, so each contiguous local row is one
        // normative transform column. Writing those results down the intermediate columns preserves libaom's
        // dimension order without a separate transpose or per-block allocation.
        for (int row = 0; row < 4; row++)
        {
            int coefficientOffset = row * 4;
            int a = Unsafe.Add(ref coefficientBase, coefficientOffset) >> UnitQuantizationShift;
            int c = Unsafe.Add(ref coefficientBase, coefficientOffset + 1) >> UnitQuantizationShift;
            int d = Unsafe.Add(ref coefficientBase, coefficientOffset + 2) >> UnitQuantizationShift;
            int b = Unsafe.Add(ref coefficientBase, coefficientOffset + 3) >> UnitQuantizationShift;

            Transform(ref a, ref b, ref c, ref d);
            Unsafe.Add(ref intermediateBase, row) = a;
            Unsafe.Add(ref intermediateBase, 4 + row) = b;
            Unsafe.Add(ref intermediateBase, 8 + row) = c;
            Unsafe.Add(ref intermediateBase, 12 + row) = d;
        }

        for (int column = 0; column < 4; column++)
        {
            int intermediateOffset = column * 4;
            int a = Unsafe.Add(ref intermediateBase, intermediateOffset);
            int c = Unsafe.Add(ref intermediateBase, intermediateOffset + 1);
            int d = Unsafe.Add(ref intermediateBase, intermediateOffset + 2);
            int b = Unsafe.Add(ref intermediateBase, intermediateOffset + 3);

            Transform(ref a, ref b, ref c, ref d);

            Unsafe.Add(ref writeBase, column) = TOutputOperator.Add(Unsafe.Add(ref readBase, column), a, bitDepth);
            Unsafe.Add(ref writeBase, writeStride + column) = TOutputOperator.Add(Unsafe.Add(ref readBase, readStride + column), b, bitDepth);
            Unsafe.Add(ref writeBase, (2 * writeStride) + column) = TOutputOperator.Add(Unsafe.Add(ref readBase, (2 * readStride) + column), c, bitDepth);
            Unsafe.Add(ref writeBase, (3 * writeStride) + column) = TOutputOperator.Add(Unsafe.Add(ref readBase, (3 * readStride) + column), d, bitDepth);
        }
    }

    /// <summary>
    /// Applies one packed four-point reversible Walsh-Hadamard dimension.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transform(
        ref Vector128<int> row0,
        ref Vector128<int> row1,
        ref Vector128<int> row2,
        ref Vector128<int> row3)
    {
        Vector128<int> a = row0;
        Vector128<int> c = row1;
        Vector128<int> d = row2;
        Vector128<int> b = row3;

        a += c;
        d -= b;
        Vector128<int> middle = (a - d) >> 1;
        b = middle - b;
        c = middle - c;
        a -= b;
        d += c;

        // The transform's arithmetic names the fourth input b and the second input c. Restore raster row order
        // explicitly so the transpose and packed output stages see a, b, c, d exactly as the reference does.
        row0 = a;
        row1 = b;
        row2 = c;
        row3 = d;
    }

    /// <summary>
    /// Applies one scalar four-point reversible Walsh-Hadamard dimension.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transform(ref int a, ref int b, ref int c, ref int d)
    {
        a += c;
        d -= b;
        int middle = (a - d) >> 1;
        b = middle - b;
        c = middle - c;
        a -= b;
        d += c;
    }

    /// <summary>
    /// Adds four packed residual rows to their prediction rows through the active sample operator.
    /// </summary>
    private static void AddRows<TSample, TOutputOperator>(
        Span<TSample> readBuffer,
        int readStride,
        Span<TSample> writeBuffer,
        int writeStride,
        Vector128<int> row0,
        Vector128<int> row1,
        Vector128<int> row2,
        Vector128<int> row3,
        int bitDepth)
        where TSample : unmanaged
        where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
    {
        ref TSample readBase = ref MemoryMarshal.GetReference(readBuffer);
        ref TSample writeBase = ref MemoryMarshal.GetReference(writeBuffer);
        TOutputOperator.Add(ref readBase, ref writeBase, row0, bitDepth);
        TOutputOperator.Add(ref Unsafe.Add(ref readBase, readStride), ref Unsafe.Add(ref writeBase, writeStride), row1, bitDepth);
        TOutputOperator.Add(ref Unsafe.Add(ref readBase, 2 * readStride), ref Unsafe.Add(ref writeBase, 2 * writeStride), row2, bitDepth);
        TOutputOperator.Add(ref Unsafe.Add(ref readBase, 3 * readStride), ref Unsafe.Add(ref writeBase, 3 * writeStride), row3, bitDepth);
    }
}
