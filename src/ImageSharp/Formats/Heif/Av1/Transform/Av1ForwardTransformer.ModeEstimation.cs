// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Provides the transform layouts used to estimate intra mode costs.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Transforms a square residual block for intra mode cost estimation.
    /// </summary>
    /// <param name="residual">The residual samples.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="size">The square transform width, four, eight or sixteen.</param>
    /// <param name="coefficients">The transformed coefficient destination.</param>
    /// <param name="workspace">The transform scratch.</param>
    /// <param name="highBitDepth">Whether coefficients use the high-bit-depth estimation layout.</param>
    public static void TransformForModeEstimation(
        ReadOnlySpan<short> residual,
        int stride,
        int size,
        Span<int> coefficients,
        Span<int> workspace,
        bool highBitDepth)
    {
        if (size == 4)
        {
            // The small-block estimator uses a Q14 cosine transform. Both passes keep wide products;
            // the input bias and final signed rounding are part of its coefficient scaling.
            const long cosine8 = 15137;
            const long cosine16 = 11585;
            const long cosine24 = 6270;
            Span<int> intermediate = workspace[..16];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int column = 0; column < 4; column++)
                {
                    long x0;
                    long x1;
                    long x2;
                    long x3;
                    if (pass == 0)
                    {
                        x0 = residual[column] * 16L;
                        x1 = residual[stride + column] * 16L;
                        x2 = residual[(2 * stride) + column] * 16L;
                        x3 = residual[(3 * stride) + column] * 16L;
                        if (column == 0 && x0 != 0)
                        {
                            x0++;
                        }
                    }
                    else
                    {
                        x0 = intermediate[column];
                        x1 = intermediate[4 + column];
                        x2 = intermediate[8 + column];
                        x3 = intermediate[12 + column];
                    }

                    long sum0 = x0 + x3;
                    long sum1 = x1 + x2;
                    long difference0 = x1 - x2;
                    long difference1 = x0 - x3;
                    int y0 = (int)((((sum0 + sum1) * cosine16) + 8192) >> 14);
                    int y1 = (int)(((difference0 * cosine24) + (difference1 * cosine8) + 8192) >> 14);
                    int y2 = (int)((((sum0 - sum1) * cosine16) + 8192) >> 14);
                    int y3 = (int)(((-difference0 * cosine8) + (difference1 * cosine24) + 8192) >> 14);
                    if (pass == 0)
                    {
                        intermediate[column * 4] = y0;
                        intermediate[(column * 4) + 1] = y1;
                        intermediate[(column * 4) + 2] = y2;
                        intermediate[(column * 4) + 3] = y3;
                    }
                    else
                    {
                        coefficients[column] = (y0 + 1) >> 2;
                        coefficients[4 + column] = (y1 + 1) >> 2;
                        coefficients[8 + column] = (y2 + 1) >> 2;
                        coefficients[12 + column] = (y3 + 1) >> 2;
                    }
                }
            }

            return;
        }

        Span<short> intermediate8 = MemoryMarshal.Cast<int, short>(workspace[..32]);
        int blockCount = size == 8 ? 1 : 4;
        for (int block = 0; block < blockCount; block++)
        {
            int offset = ((block >> 1) * 8 * stride) + ((block & 1) * 8);
            HadamardEstimationColumns(residual[offset..], stride, intermediate8);
            ref short intermediateBase = ref MemoryMarshal.GetReference(intermediate8);

            // Each lane initially holds one spatial column. Transposition exchanges the spatial and
            // frequency axes before the second butterfly pass, leaving the required coefficient order.
            Av1Transform2dOperations.Transpose8x8Int16(ref intermediateBase, 8, ref intermediateBase, 8, 0, false);
            HadamardEstimationColumns(intermediate8, 8, intermediate8);
            Span<int> destination = coefficients.Slice(block * 64, 64);
            ref int destinationBase = ref MemoryMarshal.GetReference(destination);
            for (nuint i = 0; i < 64; i += 8)
            {
                Vector128<short> values = Vector128.LoadUnsafe(ref intermediateBase, i);
                Vector128.WidenLower(values).StoreUnsafe(ref destinationBase, i);
                Vector128.WidenUpper(values).StoreUnsafe(ref destinationBase, i + 4);
            }
        }

        if (size == 16)
        {
            // Four eight-by-eight transforms are combined in quadrant order. Arithmetic shifts
            // halve the paired coefficients before the final sum to preserve the estimation scale.
            for (int i = 0; i < 64; i++)
            {
                int a0 = coefficients[i];
                int a1 = coefficients[64 + i];
                int a2 = coefficients[128 + i];
                int a3 = coefficients[192 + i];
                int b0 = (a0 + a1) >> 1;
                int b1 = (a0 - a1) >> 1;
                int b2 = (a2 + a3) >> 1;
                int b3 = (a2 - a3) >> 1;
                coefficients[i] = highBitDepth ? b0 + b2 : (short)(b0 + b2);
                coefficients[64 + i] = highBitDepth ? b1 + b3 : (short)(b1 + b3);
                coefficients[128 + i] = highBitDepth ? b0 - b2 : (short)(b0 - b2);
                coefficients[192 + i] = highBitDepth ? b1 - b3 : (short)(b1 - b3);
            }

            if (highBitDepth)
            {
                // The high-bit-depth scan addresses the middle four lanes of each sixteen-value group
                // in exchanged order. The low-bit-depth scan retains the quadrant layout above.
                for (int row = 0; row < 16; row++)
                {
                    for (int column = 0; column < 4; column++)
                    {
                        int first = (row * 16) + 4 + column;
                        int second = first + 4;
                        (coefficients[first], coefficients[second]) = (coefficients[second], coefficients[first]);
                    }
                }
            }
        }
    }

    private static void HadamardEstimationColumns(ReadOnlySpan<short> source, int stride, Span<short> destination)
    {
        ref short sourceBase = ref MemoryMarshal.GetReference(source);
        Vector128<short> row0 = Vector128.LoadUnsafe(ref sourceBase);
        Vector128<short> row1 = Vector128.LoadUnsafe(ref sourceBase, (nuint)stride);
        Vector128<short> row2 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(2 * stride));
        Vector128<short> row3 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(3 * stride));
        Vector128<short> row4 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(4 * stride));
        Vector128<short> row5 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(5 * stride));
        Vector128<short> row6 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(6 * stride));
        Vector128<short> row7 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(7 * stride));

        // Eight columns advance together through three signed sixteen-bit butterfly stages.
        // All input rows are loaded before output stores, allowing the second pass to operate in place.
        Vector128<short> b0 = row0 + row1;
        Vector128<short> b1 = row0 - row1;
        Vector128<short> b2 = row2 + row3;
        Vector128<short> b3 = row2 - row3;
        Vector128<short> b4 = row4 + row5;
        Vector128<short> b5 = row4 - row5;
        Vector128<short> b6 = row6 + row7;
        Vector128<short> b7 = row6 - row7;
        Vector128<short> c0 = b0 + b2;
        Vector128<short> c1 = b1 + b3;
        Vector128<short> c2 = b0 - b2;
        Vector128<short> c3 = b1 - b3;
        Vector128<short> c4 = b4 + b6;
        Vector128<short> c5 = b5 + b7;
        Vector128<short> c6 = b4 - b6;
        Vector128<short> c7 = b5 - b7;
        ref short destinationBase = ref MemoryMarshal.GetReference(destination);
        (c0 + c4).StoreUnsafe(ref destinationBase);
        (c1 + c5).StoreUnsafe(ref destinationBase, 56);
        (c2 + c6).StoreUnsafe(ref destinationBase, 24);
        (c3 + c7).StoreUnsafe(ref destinationBase, 32);
        (c0 - c4).StoreUnsafe(ref destinationBase, 16);
        (c1 - c5).StoreUnsafe(ref destinationBase, 48);
        (c2 - c6).StoreUnsafe(ref destinationBase, 8);
        (c3 - c7).StoreUnsafe(ref destinationBase, 40);
    }
}
