// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Provides the transform layouts used to estimate intra mode costs.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// The Q14 cosine of pi/4, cospi_16_64.
    /// </summary>
    private const short Cosine16 = 11585;

    /// <summary>
    /// The Q14 cosine of pi/8, cospi_8_64.
    /// </summary>
    private const short Cosine8 = 15137;

    /// <summary>
    /// The Q14 cosine of 3pi/8, cospi_24_64.
    /// </summary>
    private const short Cosine24 = 6270;

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
        => TransformRowForModeEstimation(residual, stride, size, 1, coefficients, workspace, highBitDepth);

    /// <summary>
    /// Transforms a row of horizontally adjacent square residual blocks for intra mode cost estimation.
    /// Reference: the transform stage of av1_block_yrd(): aom_fdct4x4_lp() or aom_fdct4x4() for 4x4,
    /// aom_hadamard_lp_8x8_dual() or aom_hadamard_lp_8x8() for 8x8, and aom_hadamard_lp_16x16() for 16x16.
    /// </summary>
    /// <param name="residual">The first block's top-left residual sample.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="size">The square transform width, four, eight or sixteen.</param>
    /// <param name="blockCount">The number of adjacent blocks.</param>
    /// <param name="coefficients">Receives each block's coefficients, one block after another.</param>
    /// <param name="workspace">The transform scratch.</param>
    /// <param name="highBitDepth">Whether coefficients use the high-bit-depth estimation layout.</param>
    public static void TransformRowForModeEstimation(
        ReadOnlySpan<short> residual,
        int stride,
        int size,
        int blockCount,
        Span<int> coefficients,
        Span<int> workspace,
        bool highBitDepth)
    {
        ref short residualBase = ref MemoryMarshal.GetReference(residual);
        ref int coefficientBase = ref MemoryMarshal.GetReference(coefficients);
        if (size == 4)
        {
            ForwardDct4x4Row(ref residualBase, stride, blockCount, ref coefficientBase);
            return;
        }

        // The Hadamard transforms keep sixteen-bit lanes throughout, as aom_hadamard_lp_8x8 does, and each
        // 128-bit lane holds one eight-by-eight block.
        int blockLength = size * size;
        Span<short> packed = MemoryMarshal.Cast<int, short>(workspace[..((blockCount * blockLength) / 2)]);
        ref short packedBase = ref MemoryMarshal.GetReference(packed);
        if (size == 8)
        {
            HadamardRow8x8(ref residualBase, stride, blockCount, ref packedBase);
        }
        else
        {
            for (int block = 0; block < blockCount; block++)
            {
                ref short blockResidual = ref Unsafe.Add(ref residualBase, block * 16);
                ref short blockCoefficients = ref Unsafe.Add(ref packedBase, block * blockLength);

                // A 16x16 block is four eight-by-eight blocks in quadrant order: two per 256-bit vector, as
                // aom_hadamard_lp_8x8_dual_avx2 lays them out, or all four per 512-bit vector.
                if (Vector512.IsHardwareAccelerated)
                {
                    HadamardQuadrants(ref blockResidual, stride, ref blockCoefficients);
                }
                else if (Vector256.IsHardwareAccelerated)
                {
                    HadamardBlocks(ref blockResidual, stride, ref blockCoefficients, Vector256<short>.Zero);
                    HadamardBlocks(ref Unsafe.Add(ref blockResidual, 8 * stride), stride, ref Unsafe.Add(ref blockCoefficients, 128), Vector256<short>.Zero);
                }
                else
                {
                    for (int quadrant = 0; quadrant < 4; quadrant++)
                    {
                        int offset = ((quadrant >> 1) * 8 * stride) + ((quadrant & 1) * 8);
                        HadamardBlocks(ref Unsafe.Add(ref blockResidual, offset), stride, ref Unsafe.Add(ref blockCoefficients, quadrant * 64), Vector128<short>.Zero);
                    }
                }

                // Four eight-by-eight transforms are combined in quadrant order. Arithmetic shifts halve the
                // paired coefficients before the final sum to preserve the estimation scale. Every sum stays in a
                // sixteen-bit lane, so a high-bit-depth residual wraps before the shift and again after the final
                // sum, as the reference x64 encoder computes it. Reference: aom_hadamard_lp_16x16_avx2().
                CombineHadamardQuadrants(ref blockCoefficients);
            }
        }

        // The high-bit-depth 16x16 scan addresses the middle four lanes of each sixteen-value group in exchanged
        // order. Every other layout keeps its coefficient order.
        bool exchangeMiddle = size == 16 && highBitDepth;
        nuint length = (nuint)(blockCount * blockLength);
        for (nuint i = 0; i < length; i += 16)
        {
            Vector128<short> first = Vector128.LoadUnsafe(ref packedBase, i);
            Vector128<short> second = Vector128.LoadUnsafe(ref packedBase, i + 8);
            Vector128.WidenLower(first).StoreUnsafe(ref coefficientBase, i);
            Vector128.WidenUpper(second).StoreUnsafe(ref coefficientBase, i + 12);
            if (exchangeMiddle)
            {
                Vector128.WidenLower(second).StoreUnsafe(ref coefficientBase, i + 4);
                Vector128.WidenUpper(first).StoreUnsafe(ref coefficientBase, i + 8);
            }
            else
            {
                Vector128.WidenUpper(first).StoreUnsafe(ref coefficientBase, i + 4);
                Vector128.WidenLower(second).StoreUnsafe(ref coefficientBase, i + 8);
            }
        }
    }

    /// <summary>
    /// Transforms a row of eight-by-eight blocks, widest first: four per 512-bit vector, two per 256-bit vector as
    /// aom_hadamard_lp_8x8_dual_avx2() does, then one per 128-bit vector.
    /// </summary>
    /// <param name="residual">The first block's top-left residual sample.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="blockCount">The number of adjacent blocks.</param>
    /// <param name="coefficients">Receives each block's 64 coefficients, one block after another.</param>
    private static void HadamardRow8x8(ref short residual, int stride, int blockCount, ref short coefficients)
    {
        int block = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; block + 4 <= blockCount; block += 4)
            {
                HadamardRowOfFour(ref Unsafe.Add(ref residual, block * 8), stride, ref Unsafe.Add(ref coefficients, block * 64));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; block + 2 <= blockCount; block += 2)
            {
                HadamardBlocks(ref Unsafe.Add(ref residual, block * 8), stride, ref Unsafe.Add(ref coefficients, block * 64), Vector256<short>.Zero);
            }
        }

        for (; block < blockCount; block++)
        {
            HadamardBlocks(ref Unsafe.Add(ref residual, block * 8), stride, ref Unsafe.Add(ref coefficients, block * 64), Vector128<short>.Zero);
        }
    }

    /// <summary>
    /// Transforms four horizontally adjacent eight-by-eight blocks, one per 128-bit lane.
    /// </summary>
    /// <param name="residual">The first block's top-left residual sample.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="coefficients">Receives the four blocks' 64 coefficients each, left to right.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HadamardRowOfFour(ref short residual, int stride, ref short coefficients)
    {
        InlineArray8<Vector512<short>> rows = default;
        for (int row = 0; row < 8; row++)
        {
            rows[row] = Vector512.LoadUnsafe(ref residual, (nuint)(row * stride));
        }

        HadamardColumns(ref rows);
        TransposeLanes(ref rows);
        HadamardColumns(ref rows);
        StoreLanes(ref rows, ref coefficients);
    }

    /// <summary>
    /// Transforms the four eight-by-eight quadrants of a 16x16 block, one per 128-bit lane.
    /// </summary>
    /// <param name="residual">The 16x16 block's top-left residual sample.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="coefficients">Receives the four quadrants' 64 coefficients each, in quadrant order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HadamardQuadrants(ref short residual, int stride, ref short coefficients)
    {
        InlineArray8<Vector512<short>> rows = default;
        for (int row = 0; row < 8; row++)
        {
            // The top quadrant pair fills the lower 256 bits and the bottom pair the upper 256 bits.
            rows[row] = Vector512.Create(
                Vector256.LoadUnsafe(ref residual, (nuint)(row * stride)),
                Vector256.LoadUnsafe(ref residual, (nuint)((8 + row) * stride)));
        }

        HadamardColumns(ref rows);
        TransposeLanes(ref rows);
        HadamardColumns(ref rows);
        StoreLanes(ref rows, ref coefficients);
    }

    /// <summary>
    /// Stores the eight-by-eight block of each 128-bit lane as 64 consecutive coefficients, lane by lane.
    /// </summary>
    /// <param name="rows">The transformed rows.</param>
    /// <param name="coefficients">Receives the four blocks' coefficients.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreLanes(ref InlineArray8<Vector512<short>> rows, ref short coefficients)
    {
        for (int row = 0; row < 8; row++)
        {
            Vector256<short> lower = rows[row].GetLower();
            Vector256<short> upper = rows[row].GetUpper();
            lower.GetLower().StoreUnsafe(ref coefficients, (nuint)(row * 8));
            lower.GetUpper().StoreUnsafe(ref coefficients, (nuint)(64 + (row * 8)));
            upper.GetLower().StoreUnsafe(ref coefficients, (nuint)(128 + (row * 8)));
            upper.GetUpper().StoreUnsafe(ref coefficients, (nuint)(192 + (row * 8)));
        }
    }

    /// <summary>
    /// Applies the 4x4 forward DCT to a row of blocks, widest first: four blocks per 512-bit vector, two per
    /// 256-bit vector, then one per 128-bit vector.
    /// </summary>
    /// <param name="residual">The first block's top-left residual sample.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="blockCount">The number of adjacent blocks.</param>
    /// <param name="coefficients">Receives each block's sixteen coefficients, one block after another.</param>
    private static void ForwardDct4x4Row(ref short residual, int stride, int blockCount, ref int coefficients)
    {
        int block = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; block + 4 <= blockCount; block += 4)
            {
                ref short blockResidual = ref Unsafe.Add(ref residual, block * 4);
                Vector512<short> upper = Vector512.Create(
                    Vector256.Create(
                        LoadDctRows(ref blockResidual, stride, 0, 3),
                        LoadDctRows(ref Unsafe.Add(ref blockResidual, 4), stride, 0, 3)),
                    Vector256.Create(
                        LoadDctRows(ref Unsafe.Add(ref blockResidual, 8), stride, 0, 3),
                        LoadDctRows(ref Unsafe.Add(ref blockResidual, 12), stride, 0, 3)));

                Vector512<short> lower = Vector512.Create(
                    Vector256.Create(
                        LoadDctRows(ref blockResidual, stride, 1, 2),
                        LoadDctRows(ref Unsafe.Add(ref blockResidual, 4), stride, 1, 2)),
                    Vector256.Create(
                        LoadDctRows(ref Unsafe.Add(ref blockResidual, 8), stride, 1, 2),
                        LoadDctRows(ref Unsafe.Add(ref blockResidual, 12), stride, 1, 2)));

                ForwardDct4x4(ref upper, ref lower);
                ref int blockCoefficients = ref Unsafe.Add(ref coefficients, block * 16);
                StoreDctCoefficients(upper.GetLower().GetLower(), ref blockCoefficients);
                StoreDctCoefficients(lower.GetLower().GetLower(), ref Unsafe.Add(ref blockCoefficients, 8));
                StoreDctCoefficients(upper.GetLower().GetUpper(), ref Unsafe.Add(ref blockCoefficients, 16));
                StoreDctCoefficients(lower.GetLower().GetUpper(), ref Unsafe.Add(ref blockCoefficients, 24));
                StoreDctCoefficients(upper.GetUpper().GetLower(), ref Unsafe.Add(ref blockCoefficients, 32));
                StoreDctCoefficients(lower.GetUpper().GetLower(), ref Unsafe.Add(ref blockCoefficients, 40));
                StoreDctCoefficients(upper.GetUpper().GetUpper(), ref Unsafe.Add(ref blockCoefficients, 48));
                StoreDctCoefficients(lower.GetUpper().GetUpper(), ref Unsafe.Add(ref blockCoefficients, 56));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; block + 2 <= blockCount; block += 2)
            {
                ref short blockResidual = ref Unsafe.Add(ref residual, block * 4);
                Vector256<short> upper = Vector256.Create(
                    LoadDctRows(ref blockResidual, stride, 0, 3),
                    LoadDctRows(ref Unsafe.Add(ref blockResidual, 4), stride, 0, 3));

                Vector256<short> lower = Vector256.Create(
                    LoadDctRows(ref blockResidual, stride, 1, 2),
                    LoadDctRows(ref Unsafe.Add(ref blockResidual, 4), stride, 1, 2));

                ForwardDct4x4(ref upper, ref lower);
                ref int blockCoefficients = ref Unsafe.Add(ref coefficients, block * 16);
                StoreDctCoefficients(upper.GetLower(), ref blockCoefficients);
                StoreDctCoefficients(lower.GetLower(), ref Unsafe.Add(ref blockCoefficients, 8));
                StoreDctCoefficients(upper.GetUpper(), ref Unsafe.Add(ref blockCoefficients, 16));
                StoreDctCoefficients(lower.GetUpper(), ref Unsafe.Add(ref blockCoefficients, 24));
            }
        }

        for (; block < blockCount; block++)
        {
            ref short blockResidual = ref Unsafe.Add(ref residual, block * 4);
            Vector128<short> upper = LoadDctRows(ref blockResidual, stride, 0, 3);
            Vector128<short> lower = LoadDctRows(ref blockResidual, stride, 1, 2);
            ForwardDct4x4(ref upper, ref lower);
            ref int blockCoefficients = ref Unsafe.Add(ref coefficients, block * 16);
            StoreDctCoefficients(upper, ref blockCoefficients);
            StoreDctCoefficients(lower, ref Unsafe.Add(ref blockCoefficients, 8));
        }
    }

    /// <summary>
    /// Loads two four-sample residual rows into one 128-bit vector, as the aom_fdct4x4 helper arranges them.
    /// </summary>
    /// <param name="residual">The block's top-left residual sample.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="first">The row loaded into the low half.</param>
    /// <param name="second">The row loaded into the high half.</param>
    /// <returns>The two rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> LoadDctRows(ref short residual, int stride, int first, int second)
        => Vector128.Create(
            Unsafe.ReadUnaligned<long>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref residual, first * stride))),
            Unsafe.ReadUnaligned<long>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref residual, second * stride)))).AsInt16();

    /// <summary>
    /// Widens and stores eight 4x4 DCT coefficients.
    /// </summary>
    /// <param name="value">The eight coefficients.</param>
    /// <param name="coefficients">The destination.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreDctCoefficients(Vector128<short> value, ref int coefficients)
    {
        Vector128.WidenLower(value).StoreUnsafe(ref coefficients);
        Vector128.WidenUpper(value).StoreUnsafe(ref coefficients, 4);
    }

    /// <summary>
    /// Applies the 4x4 forward DCT of one block. The arithmetic is the sixteen-bit x86 form, with wrapping adds and
    /// saturating packs, so high-bit-depth residuals give the reference x64 results. Reference: fdct4x4_helper() in
    /// aom_fdct4x4_sse2() and aom_fdct4x4_lp_sse2().
    /// </summary>
    /// <param name="upper">Rows 0 and 3, replaced by coefficients 0 to 7.</param>
    /// <param name="lower">Rows 1 and 2, replaced by coefficients 8 to 15.</param>
    private static void ForwardDct4x4(ref Vector128<short> upper, ref Vector128<short> lower)
    {
        Vector128<short> in0 = upper << 4;
        Vector128<short> in1 = lower << 4;

        // Add one to the top-left sample when it is not zero. Only that lane can equal the zero of the comparison
        // pattern, because every other lane is compared with one and a value shifted left by four is never one.
        Vector128<short> mask = Vector128.Equals(in0, Vector128.Create((short)0, 1, 1, 1, 1, 1, 1, 1));
        in0 += mask + Vector128.Create((short)1, 0, 0, 0, 0, 0, 0, 0);

        // Stage 1: pair the rows so each 32-bit lane holds the two samples of one butterfly.
        Vector128<int> r2 = Vector128.Shuffle(Vector128_.UnpackLow(in0, in1).AsInt32(), Vector128.Create(0, 1, 3, 2));
        Vector128<int> r3 = Vector128.Shuffle(Vector128_.UnpackHigh(in0, in1).AsInt32(), Vector128.Create(0, 1, 3, 2));
        Vector128<short> t0 = r2.AsInt16() + r3.AsInt16();
        Vector128<short> t1 = r2.AsInt16() - r3.AsInt16();

        // Stage 2: the vertical multiplies, rounded back to sixteen bits.
        Vector128<int> rounding = Vector128.Create(8192);
        Vector128<int> w0 = (Vector128_.MultiplyAddAdjacent(t0, Vector128.Create(Cosine16, Cosine16, Cosine16, Cosine16, Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16)) + rounding) >> 14;
        Vector128<int> w2 = (Vector128_.MultiplyAddAdjacent(t0, Vector128.Create(Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16, Cosine16, Cosine16, Cosine16, Cosine16)) + rounding) >> 14;
        Vector128<int> w1 = (Vector128_.MultiplyAddAdjacent(t1, Vector128.Create(Cosine8, Cosine24, Cosine8, Cosine24, Cosine24, (short)-Cosine8, Cosine24, (short)-Cosine8)) + rounding) >> 14;
        Vector128<int> w3 = (Vector128_.MultiplyAddAdjacent(t1, Vector128.Create(Cosine24, (short)-Cosine8, Cosine24, (short)-Cosine8, Cosine8, Cosine24, Cosine8, Cosine24)) + rounding) >> 14;
        in0 = Vector128.Shuffle(Vector128_.PackSignedSaturate(w0, w1).AsInt32(), Vector128.Create(0, 2, 1, 3)).AsInt16();
        in1 = Vector128.Shuffle(Vector128_.PackSignedSaturate(w2, w3).AsInt32(), Vector128.Create(1, 3, 0, 2)).AsInt16();

        // Stage 3 and 4: the horizontal butterflies and multiplies. The rounding folds in the final (v + 1) >> 2.
        t0 = in0 + in1;
        t1 = in0 - in1;
        Vector128<int> rounding2 = Vector128.Create(8192 + 16384);
        w0 = (Vector128_.MultiplyAddAdjacent(t0, Vector128.Create(Cosine16)) + rounding2) >> 16;
        w1 = (Vector128_.MultiplyAddAdjacent(t0, Vector128.Create(Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16)) + rounding2) >> 16;
        w2 = (Vector128_.MultiplyAddAdjacent(t1, Vector128.Create(Cosine8, Cosine24, Cosine8, Cosine24, (short)-Cosine8, (short)-Cosine24, (short)-Cosine8, (short)-Cosine24)) + rounding2) >> 16;
        w3 = (Vector128_.MultiplyAddAdjacent(t1, Vector128.Create(Cosine24, (short)-Cosine8, Cosine24, (short)-Cosine8, (short)-Cosine24, Cosine8, (short)-Cosine24, Cosine8)) + rounding2) >> 16;
        upper = Vector128_.PackSignedSaturate(w0, w2);
        lower = Vector128_.PackSignedSaturate(w1, w3);
    }

    /// <summary>
    /// Applies the 4x4 forward DCT of two blocks, one per 128-bit lane.
    /// </summary>
    /// <param name="upper">Rows 0 and 3 of each block, replaced by coefficients 0 to 7.</param>
    /// <param name="lower">Rows 1 and 2 of each block, replaced by coefficients 8 to 15.</param>
    private static void ForwardDct4x4(ref Vector256<short> upper, ref Vector256<short> lower)
    {
        Vector256<short> in0 = upper << 4;
        Vector256<short> in1 = lower << 4;
        Vector128<short> biasA = Vector128.Create((short)0, 1, 1, 1, 1, 1, 1, 1);
        Vector128<short> biasB = Vector128.Create((short)1, 0, 0, 0, 0, 0, 0, 0);
        Vector256<short> mask = Vector256.Equals(in0, Vector256.Create(biasA, biasA));
        in0 += mask + Vector256.Create(biasB, biasB);

        Vector256<int> r2 = Vector256.Shuffle(Vector256_.UnpackLow(in0, in1).AsInt32(), Vector256.Create(0, 1, 3, 2, 4, 5, 7, 6));
        Vector256<int> r3 = Vector256.Shuffle(Vector256_.UnpackHigh(in0, in1).AsInt32(), Vector256.Create(0, 1, 3, 2, 4, 5, 7, 6));
        Vector256<short> t0 = r2.AsInt16() + r3.AsInt16();
        Vector256<short> t1 = r2.AsInt16() - r3.AsInt16();

        Vector256<int> rounding = Vector256.Create(8192);
        Vector256<int> w0 = (Vector256_.MultiplyAddAdjacent(t0, Repeat(Vector128.Create(Cosine16, Cosine16, Cosine16, Cosine16, Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16))) + rounding) >> 14;
        Vector256<int> w2 = (Vector256_.MultiplyAddAdjacent(t0, Repeat(Vector128.Create(Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16, Cosine16, Cosine16, Cosine16, Cosine16))) + rounding) >> 14;
        Vector256<int> w1 = (Vector256_.MultiplyAddAdjacent(t1, Repeat(Vector128.Create(Cosine8, Cosine24, Cosine8, Cosine24, Cosine24, (short)-Cosine8, Cosine24, (short)-Cosine8))) + rounding) >> 14;
        Vector256<int> w3 = (Vector256_.MultiplyAddAdjacent(t1, Repeat(Vector128.Create(Cosine24, (short)-Cosine8, Cosine24, (short)-Cosine8, Cosine8, Cosine24, Cosine8, Cosine24))) + rounding) >> 14;
        in0 = Vector256.Shuffle(Vector256_.PackSignedSaturate(w0, w1).AsInt32(), Vector256.Create(0, 2, 1, 3, 4, 6, 5, 7)).AsInt16();
        in1 = Vector256.Shuffle(Vector256_.PackSignedSaturate(w2, w3).AsInt32(), Vector256.Create(1, 3, 0, 2, 5, 7, 4, 6)).AsInt16();

        t0 = in0 + in1;
        t1 = in0 - in1;
        Vector256<int> rounding2 = Vector256.Create(8192 + 16384);
        w0 = (Vector256_.MultiplyAddAdjacent(t0, Vector256.Create(Cosine16)) + rounding2) >> 16;
        w1 = (Vector256_.MultiplyAddAdjacent(t0, Repeat(Vector128.Create(Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16))) + rounding2) >> 16;
        w2 = (Vector256_.MultiplyAddAdjacent(t1, Repeat(Vector128.Create(Cosine8, Cosine24, Cosine8, Cosine24, (short)-Cosine8, (short)-Cosine24, (short)-Cosine8, (short)-Cosine24))) + rounding2) >> 16;
        w3 = (Vector256_.MultiplyAddAdjacent(t1, Repeat(Vector128.Create(Cosine24, (short)-Cosine8, Cosine24, (short)-Cosine8, (short)-Cosine24, Cosine8, (short)-Cosine24, Cosine8))) + rounding2) >> 16;
        upper = Vector256_.PackSignedSaturate(w0, w2);
        lower = Vector256_.PackSignedSaturate(w1, w3);
    }

    /// <summary>
    /// Applies the 4x4 forward DCT of four blocks, one per 128-bit lane.
    /// </summary>
    /// <param name="upper">Rows 0 and 3 of each block, replaced by coefficients 0 to 7.</param>
    /// <param name="lower">Rows 1 and 2 of each block, replaced by coefficients 8 to 15.</param>
    private static void ForwardDct4x4(ref Vector512<short> upper, ref Vector512<short> lower)
    {
        Vector512<short> in0 = upper << 4;
        Vector512<short> in1 = lower << 4;
        Vector512<short> mask = Vector512.Equals(in0, Repeat(Repeat(Vector128.Create((short)0, 1, 1, 1, 1, 1, 1, 1))));
        in0 += mask + Repeat(Repeat(Vector128.Create((short)1, 0, 0, 0, 0, 0, 0, 0)));

        Vector512<int> pairIndices = Vector512.Create(0, 1, 3, 2, 4, 5, 7, 6, 8, 9, 11, 10, 12, 13, 15, 14);
        Vector512<int> r2 = Vector512.Shuffle(Vector512_.UnpackLow(in0, in1).AsInt32(), pairIndices);
        Vector512<int> r3 = Vector512.Shuffle(Vector512_.UnpackHigh(in0, in1).AsInt32(), pairIndices);
        Vector512<short> t0 = r2.AsInt16() + r3.AsInt16();
        Vector512<short> t1 = r2.AsInt16() - r3.AsInt16();

        Vector512<int> rounding = Vector512.Create(8192);
        Vector512<int> w0 = (Vector512_.MultiplyAddAdjacent(t0, Repeat(Repeat(Vector128.Create(Cosine16, Cosine16, Cosine16, Cosine16, Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16)))) + rounding) >> 14;
        Vector512<int> w2 = (Vector512_.MultiplyAddAdjacent(t0, Repeat(Repeat(Vector128.Create(Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16, Cosine16, Cosine16, Cosine16, Cosine16)))) + rounding) >> 14;
        Vector512<int> w1 = (Vector512_.MultiplyAddAdjacent(t1, Repeat(Repeat(Vector128.Create(Cosine8, Cosine24, Cosine8, Cosine24, Cosine24, (short)-Cosine8, Cosine24, (short)-Cosine8)))) + rounding) >> 14;
        Vector512<int> w3 = (Vector512_.MultiplyAddAdjacent(t1, Repeat(Repeat(Vector128.Create(Cosine24, (short)-Cosine8, Cosine24, (short)-Cosine8, Cosine8, Cosine24, Cosine8, Cosine24)))) + rounding) >> 14;
        in0 = Vector512.Shuffle(Vector512_.PackSignedSaturate(w0, w1).AsInt32(), Vector512.Create(0, 2, 1, 3, 4, 6, 5, 7, 8, 10, 9, 11, 12, 14, 13, 15)).AsInt16();
        in1 = Vector512.Shuffle(Vector512_.PackSignedSaturate(w2, w3).AsInt32(), Vector512.Create(1, 3, 0, 2, 5, 7, 4, 6, 9, 11, 8, 10, 13, 15, 12, 14)).AsInt16();

        t0 = in0 + in1;
        t1 = in0 - in1;
        Vector512<int> rounding2 = Vector512.Create(8192 + 16384);
        w0 = (Vector512_.MultiplyAddAdjacent(t0, Vector512.Create(Cosine16)) + rounding2) >> 16;
        w1 = (Vector512_.MultiplyAddAdjacent(t0, Repeat(Repeat(Vector128.Create(Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16, Cosine16, (short)-Cosine16)))) + rounding2) >> 16;
        w2 = (Vector512_.MultiplyAddAdjacent(t1, Repeat(Repeat(Vector128.Create(Cosine8, Cosine24, Cosine8, Cosine24, (short)-Cosine8, (short)-Cosine24, (short)-Cosine8, (short)-Cosine24)))) + rounding2) >> 16;
        w3 = (Vector512_.MultiplyAddAdjacent(t1, Repeat(Repeat(Vector128.Create(Cosine24, (short)-Cosine8, Cosine24, (short)-Cosine8, (short)-Cosine24, Cosine8, (short)-Cosine24, Cosine8)))) + rounding2) >> 16;
        upper = Vector512_.PackSignedSaturate(w0, w2);
        lower = Vector512_.PackSignedSaturate(w1, w3);
    }

    /// <summary>
    /// Repeats a 128-bit pattern in both halves of a 256-bit vector.
    /// </summary>
    /// <param name="value">The pattern.</param>
    /// <returns>The repeated pattern.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> Repeat(Vector128<short> value) => Vector256.Create(value, value);

    /// <summary>
    /// Repeats a 256-bit pattern in both halves of a 512-bit vector.
    /// </summary>
    /// <param name="value">The pattern.</param>
    /// <returns>The repeated pattern.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<short> Repeat(Vector256<short> value) => Vector512.Create(value, value);

    /// <summary>
    /// Combines the four eight-by-eight Hadamard quadrants of a 16x16 block in place with sixteen-bit wrapping.
    /// Reference: the combine loop of aom_hadamard_lp_16x16_avx2().
    /// </summary>
    /// <param name="packed">The four 64-coefficient quadrants in quadrant order.</param>
    private static void CombineHadamardQuadrants(ref short packed)
    {
        nuint index = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; index < 64; index += (nuint)Vector512<short>.Count)
            {
                Vector512<short> a0 = Vector512.LoadUnsafe(ref packed, index);
                Vector512<short> a1 = Vector512.LoadUnsafe(ref packed, index + 64);
                Vector512<short> a2 = Vector512.LoadUnsafe(ref packed, index + 128);
                Vector512<short> a3 = Vector512.LoadUnsafe(ref packed, index + 192);
                Vector512<short> b0 = (a0 + a1) >> 1;
                Vector512<short> b1 = (a0 - a1) >> 1;
                Vector512<short> b2 = (a2 + a3) >> 1;
                Vector512<short> b3 = (a2 - a3) >> 1;
                (b0 + b2).StoreUnsafe(ref packed, index);
                (b1 + b3).StoreUnsafe(ref packed, index + 64);
                (b0 - b2).StoreUnsafe(ref packed, index + 128);
                (b1 - b3).StoreUnsafe(ref packed, index + 192);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index < 64; index += (nuint)Vector256<short>.Count)
            {
                Vector256<short> a0 = Vector256.LoadUnsafe(ref packed, index);
                Vector256<short> a1 = Vector256.LoadUnsafe(ref packed, index + 64);
                Vector256<short> a2 = Vector256.LoadUnsafe(ref packed, index + 128);
                Vector256<short> a3 = Vector256.LoadUnsafe(ref packed, index + 192);
                Vector256<short> b0 = (a0 + a1) >> 1;
                Vector256<short> b1 = (a0 - a1) >> 1;
                Vector256<short> b2 = (a2 + a3) >> 1;
                Vector256<short> b3 = (a2 - a3) >> 1;
                (b0 + b2).StoreUnsafe(ref packed, index);
                (b1 + b3).StoreUnsafe(ref packed, index + 64);
                (b0 - b2).StoreUnsafe(ref packed, index + 128);
                (b1 - b3).StoreUnsafe(ref packed, index + 192);
            }
        }

        for (; index < 64; index += (nuint)Vector128<short>.Count)
        {
            Vector128<short> a0 = Vector128.LoadUnsafe(ref packed, index);
            Vector128<short> a1 = Vector128.LoadUnsafe(ref packed, index + 64);
            Vector128<short> a2 = Vector128.LoadUnsafe(ref packed, index + 128);
            Vector128<short> a3 = Vector128.LoadUnsafe(ref packed, index + 192);
            Vector128<short> b0 = (a0 + a1) >> 1;
            Vector128<short> b1 = (a0 - a1) >> 1;
            Vector128<short> b2 = (a2 + a3) >> 1;
            Vector128<short> b3 = (a2 - a3) >> 1;
            (b0 + b2).StoreUnsafe(ref packed, index);
            (b1 + b3).StoreUnsafe(ref packed, index + 64);
            (b0 - b2).StoreUnsafe(ref packed, index + 128);
            (b1 - b3).StoreUnsafe(ref packed, index + 192);
        }
    }

    /// <summary>
    /// Transforms one eight-by-eight block with sixteen-bit Hadamard butterflies.
    /// Reference: aom_hadamard_lp_8x8_sse2().
    /// </summary>
    /// <param name="residual">The block's top-left residual sample.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="coefficients">The block's 64 coefficients, row by row.</param>
    /// <param name="width">Selects the 128-bit form.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HadamardBlocks(ref short residual, int stride, ref short coefficients, Vector128<short> width)
    {
        InlineArray8<Vector128<short>> rows = default;
        for (int row = 0; row < 8; row++)
        {
            rows[row] = Vector128.LoadUnsafe(ref residual, (nuint)(row * stride));
        }

        HadamardColumns(ref rows);
        TransposeLanes(ref rows);
        HadamardColumns(ref rows);
        for (int row = 0; row < 8; row++)
        {
            rows[row].StoreUnsafe(ref coefficients, (nuint)(row * 8));
        }
    }

    /// <summary>
    /// Transforms two horizontally adjacent eight-by-eight blocks, one per 128-bit lane.
    /// Reference: aom_hadamard_lp_8x8_dual_avx2().
    /// </summary>
    /// <param name="residual">The left block's top-left residual sample.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="coefficients">The two blocks' 64 coefficients each, left block first.</param>
    /// <param name="width">Selects the 256-bit form.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HadamardBlocks(ref short residual, int stride, ref short coefficients, Vector256<short> width)
    {
        InlineArray8<Vector256<short>> rows = default;
        for (int row = 0; row < 8; row++)
        {
            rows[row] = Vector256.LoadUnsafe(ref residual, (nuint)(row * stride));
        }

        HadamardColumns(ref rows);
        TransposeLanes(ref rows);
        HadamardColumns(ref rows);
        for (int row = 0; row < 8; row++)
        {
            rows[row].GetLower().StoreUnsafe(ref coefficients, (nuint)(row * 8));
            rows[row].GetUpper().StoreUnsafe(ref coefficients, (nuint)(64 + (row * 8)));
        }
    }

    /// <summary>
    /// Applies the three butterfly stages to eight columns per 128-bit lane and writes the rows in the order
    /// hadamard_col8_sse2() produces them.
    /// </summary>
    /// <param name="rows">The eight rows, replaced by the transformed rows.</param>
    private static void HadamardColumns(ref InlineArray8<Vector128<short>> rows)
    {
        Vector128<short> b0 = rows[0] + rows[1];
        Vector128<short> b1 = rows[0] - rows[1];
        Vector128<short> b2 = rows[2] + rows[3];
        Vector128<short> b3 = rows[2] - rows[3];
        Vector128<short> b4 = rows[4] + rows[5];
        Vector128<short> b5 = rows[4] - rows[5];
        Vector128<short> b6 = rows[6] + rows[7];
        Vector128<short> b7 = rows[6] - rows[7];
        Vector128<short> c0 = b0 + b2;
        Vector128<short> c1 = b1 + b3;
        Vector128<short> c2 = b0 - b2;
        Vector128<short> c3 = b1 - b3;
        Vector128<short> c4 = b4 + b6;
        Vector128<short> c5 = b5 + b7;
        Vector128<short> c6 = b4 - b6;
        Vector128<short> c7 = b5 - b7;
        rows[0] = c0 + c4;
        rows[7] = c1 + c5;
        rows[3] = c2 + c6;
        rows[4] = c3 + c7;
        rows[2] = c0 - c4;
        rows[6] = c1 - c5;
        rows[1] = c2 - c6;
        rows[5] = c3 - c7;
    }

    /// <summary>
    /// Applies the three butterfly stages to eight columns per 128-bit lane and writes the rows in the order
    /// hadamard_col8x2_avx2() produces them.
    /// </summary>
    /// <param name="rows">The eight rows, replaced by the transformed rows.</param>
    private static void HadamardColumns(ref InlineArray8<Vector256<short>> rows)
    {
        Vector256<short> b0 = rows[0] + rows[1];
        Vector256<short> b1 = rows[0] - rows[1];
        Vector256<short> b2 = rows[2] + rows[3];
        Vector256<short> b3 = rows[2] - rows[3];
        Vector256<short> b4 = rows[4] + rows[5];
        Vector256<short> b5 = rows[4] - rows[5];
        Vector256<short> b6 = rows[6] + rows[7];
        Vector256<short> b7 = rows[6] - rows[7];
        Vector256<short> c0 = b0 + b2;
        Vector256<short> c1 = b1 + b3;
        Vector256<short> c2 = b0 - b2;
        Vector256<short> c3 = b1 - b3;
        Vector256<short> c4 = b4 + b6;
        Vector256<short> c5 = b5 + b7;
        Vector256<short> c6 = b4 - b6;
        Vector256<short> c7 = b5 - b7;
        rows[0] = c0 + c4;
        rows[7] = c1 + c5;
        rows[3] = c2 + c6;
        rows[4] = c3 + c7;
        rows[2] = c0 - c4;
        rows[6] = c1 - c5;
        rows[1] = c2 - c6;
        rows[5] = c3 - c7;
    }

    /// <summary>
    /// Applies the three butterfly stages to eight columns per 128-bit lane and writes the rows in the order the
    /// narrower forms produce them.
    /// </summary>
    /// <param name="rows">The eight rows, replaced by the transformed rows.</param>
    private static void HadamardColumns(ref InlineArray8<Vector512<short>> rows)
    {
        Vector512<short> b0 = rows[0] + rows[1];
        Vector512<short> b1 = rows[0] - rows[1];
        Vector512<short> b2 = rows[2] + rows[3];
        Vector512<short> b3 = rows[2] - rows[3];
        Vector512<short> b4 = rows[4] + rows[5];
        Vector512<short> b5 = rows[4] - rows[5];
        Vector512<short> b6 = rows[6] + rows[7];
        Vector512<short> b7 = rows[6] - rows[7];
        Vector512<short> c0 = b0 + b2;
        Vector512<short> c1 = b1 + b3;
        Vector512<short> c2 = b0 - b2;
        Vector512<short> c3 = b1 - b3;
        Vector512<short> c4 = b4 + b6;
        Vector512<short> c5 = b5 + b7;
        Vector512<short> c6 = b4 - b6;
        Vector512<short> c7 = b5 - b7;
        rows[0] = c0 + c4;
        rows[7] = c1 + c5;
        rows[3] = c2 + c6;
        rows[4] = c3 + c7;
        rows[2] = c0 - c4;
        rows[6] = c1 - c5;
        rows[1] = c2 - c6;
        rows[5] = c3 - c7;
    }

    /// <summary>
    /// Transposes the eight-by-eight block held in each 128-bit lane.
    /// </summary>
    /// <param name="rows">The eight rows, replaced by the transposed rows.</param>
    private static void TransposeLanes(ref InlineArray8<Vector128<short>> rows)
    {
        Vector128<int> pair0 = Vector128_.UnpackLow(rows[0], rows[1]).AsInt32();
        Vector128<int> pair1 = Vector128_.UnpackHigh(rows[0], rows[1]).AsInt32();
        Vector128<int> pair2 = Vector128_.UnpackLow(rows[2], rows[3]).AsInt32();
        Vector128<int> pair3 = Vector128_.UnpackHigh(rows[2], rows[3]).AsInt32();
        Vector128<int> pair4 = Vector128_.UnpackLow(rows[4], rows[5]).AsInt32();
        Vector128<int> pair5 = Vector128_.UnpackHigh(rows[4], rows[5]).AsInt32();
        Vector128<int> pair6 = Vector128_.UnpackLow(rows[6], rows[7]).AsInt32();
        Vector128<int> pair7 = Vector128_.UnpackHigh(rows[6], rows[7]).AsInt32();
        Vector128<long> quad0 = Vector128_.UnpackLow(pair0, pair2).AsInt64();
        Vector128<long> quad1 = Vector128_.UnpackHigh(pair0, pair2).AsInt64();
        Vector128<long> quad2 = Vector128_.UnpackLow(pair1, pair3).AsInt64();
        Vector128<long> quad3 = Vector128_.UnpackHigh(pair1, pair3).AsInt64();
        Vector128<long> quad4 = Vector128_.UnpackLow(pair4, pair6).AsInt64();
        Vector128<long> quad5 = Vector128_.UnpackHigh(pair4, pair6).AsInt64();
        Vector128<long> quad6 = Vector128_.UnpackLow(pair5, pair7).AsInt64();
        Vector128<long> quad7 = Vector128_.UnpackHigh(pair5, pair7).AsInt64();
        rows[0] = Vector128_.UnpackLow(quad0, quad4).AsInt16();
        rows[1] = Vector128_.UnpackHigh(quad0, quad4).AsInt16();
        rows[2] = Vector128_.UnpackLow(quad1, quad5).AsInt16();
        rows[3] = Vector128_.UnpackHigh(quad1, quad5).AsInt16();
        rows[4] = Vector128_.UnpackLow(quad2, quad6).AsInt16();
        rows[5] = Vector128_.UnpackHigh(quad2, quad6).AsInt16();
        rows[6] = Vector128_.UnpackLow(quad3, quad7).AsInt16();
        rows[7] = Vector128_.UnpackHigh(quad3, quad7).AsInt16();
    }

    /// <summary>
    /// Transposes the eight-by-eight block held in each 128-bit lane.
    /// </summary>
    /// <param name="rows">The eight rows, replaced by the transposed rows.</param>
    private static void TransposeLanes(ref InlineArray8<Vector256<short>> rows)
    {
        Vector256<int> pair0 = Vector256_.UnpackLow(rows[0], rows[1]).AsInt32();
        Vector256<int> pair1 = Vector256_.UnpackHigh(rows[0], rows[1]).AsInt32();
        Vector256<int> pair2 = Vector256_.UnpackLow(rows[2], rows[3]).AsInt32();
        Vector256<int> pair3 = Vector256_.UnpackHigh(rows[2], rows[3]).AsInt32();
        Vector256<int> pair4 = Vector256_.UnpackLow(rows[4], rows[5]).AsInt32();
        Vector256<int> pair5 = Vector256_.UnpackHigh(rows[4], rows[5]).AsInt32();
        Vector256<int> pair6 = Vector256_.UnpackLow(rows[6], rows[7]).AsInt32();
        Vector256<int> pair7 = Vector256_.UnpackHigh(rows[6], rows[7]).AsInt32();
        Vector256<long> quad0 = Vector256_.UnpackLow(pair0, pair2).AsInt64();
        Vector256<long> quad1 = Vector256_.UnpackHigh(pair0, pair2).AsInt64();
        Vector256<long> quad2 = Vector256_.UnpackLow(pair1, pair3).AsInt64();
        Vector256<long> quad3 = Vector256_.UnpackHigh(pair1, pair3).AsInt64();
        Vector256<long> quad4 = Vector256_.UnpackLow(pair4, pair6).AsInt64();
        Vector256<long> quad5 = Vector256_.UnpackHigh(pair4, pair6).AsInt64();
        Vector256<long> quad6 = Vector256_.UnpackLow(pair5, pair7).AsInt64();
        Vector256<long> quad7 = Vector256_.UnpackHigh(pair5, pair7).AsInt64();
        rows[0] = Vector256_.UnpackLow(quad0, quad4).AsInt16();
        rows[1] = Vector256_.UnpackHigh(quad0, quad4).AsInt16();
        rows[2] = Vector256_.UnpackLow(quad1, quad5).AsInt16();
        rows[3] = Vector256_.UnpackHigh(quad1, quad5).AsInt16();
        rows[4] = Vector256_.UnpackLow(quad2, quad6).AsInt16();
        rows[5] = Vector256_.UnpackHigh(quad2, quad6).AsInt16();
        rows[6] = Vector256_.UnpackLow(quad3, quad7).AsInt16();
        rows[7] = Vector256_.UnpackHigh(quad3, quad7).AsInt16();
    }

    /// <summary>
    /// Transposes the eight-by-eight block held in each 128-bit lane.
    /// </summary>
    /// <param name="rows">The eight rows, replaced by the transposed rows.</param>
    private static void TransposeLanes(ref InlineArray8<Vector512<short>> rows)
    {
        Vector512<int> pair0 = Vector512_.UnpackLow(rows[0], rows[1]).AsInt32();
        Vector512<int> pair1 = Vector512_.UnpackHigh(rows[0], rows[1]).AsInt32();
        Vector512<int> pair2 = Vector512_.UnpackLow(rows[2], rows[3]).AsInt32();
        Vector512<int> pair3 = Vector512_.UnpackHigh(rows[2], rows[3]).AsInt32();
        Vector512<int> pair4 = Vector512_.UnpackLow(rows[4], rows[5]).AsInt32();
        Vector512<int> pair5 = Vector512_.UnpackHigh(rows[4], rows[5]).AsInt32();
        Vector512<int> pair6 = Vector512_.UnpackLow(rows[6], rows[7]).AsInt32();
        Vector512<int> pair7 = Vector512_.UnpackHigh(rows[6], rows[7]).AsInt32();
        Vector512<long> quad0 = Vector512_.UnpackLow(pair0, pair2).AsInt64();
        Vector512<long> quad1 = Vector512_.UnpackHigh(pair0, pair2).AsInt64();
        Vector512<long> quad2 = Vector512_.UnpackLow(pair1, pair3).AsInt64();
        Vector512<long> quad3 = Vector512_.UnpackHigh(pair1, pair3).AsInt64();
        Vector512<long> quad4 = Vector512_.UnpackLow(pair4, pair6).AsInt64();
        Vector512<long> quad5 = Vector512_.UnpackHigh(pair4, pair6).AsInt64();
        Vector512<long> quad6 = Vector512_.UnpackLow(pair5, pair7).AsInt64();
        Vector512<long> quad7 = Vector512_.UnpackHigh(pair5, pair7).AsInt64();
        rows[0] = Vector512_.UnpackLow(quad0, quad4).AsInt16();
        rows[1] = Vector512_.UnpackHigh(quad0, quad4).AsInt16();
        rows[2] = Vector512_.UnpackLow(quad1, quad5).AsInt16();
        rows[3] = Vector512_.UnpackHigh(quad1, quad5).AsInt16();
        rows[4] = Vector512_.UnpackLow(quad2, quad6).AsInt16();
        rows[5] = Vector512_.UnpackHigh(quad2, quad6).AsInt16();
        rows[6] = Vector512_.UnpackLow(quad3, quad7).AsInt16();
        rows[7] = Vector512_.UnpackHigh(quad3, quad7).AsInt16();
    }
}
