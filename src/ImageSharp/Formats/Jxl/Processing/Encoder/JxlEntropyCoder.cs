// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal static class JxlEntropyCoder
{
    private static int NumNonZero8x8ExceptDC(ReadOnlySpan<int> block, Span<int> nzerosPos)
    {
        Vector<int> zero = Vector<int>.Zero;
        Vector<int> negSumZero = zero;

        Span<int> dcMaskLanes = stackalloc int[Vector<int>.Count];
        dcMaskLanes[0] = -1;

        for (int x = 0; x < 8; x += Vector<int>.Count)
        {
            Vector<int> dcMask = Vector.Create<int>(dcMaskLanes.Slice(x, Vector<int>.Count));
            Vector<int> coef = (~dcMask) & Vector.Create(block.Slice(x, Vector<int>.Count));

            negSumZero += Vector.ConditionalSelect(
                Vector.Equals(coef, zero),
                Vector<int>.One,
                zero);
        }

        for (int y = 1; y < 8; y++)
        {
            ReadOnlySpan<int> row = block.Slice(y * 8, 8);

            for (int x = 0; x < 8; x += Vector<int>.Count)
            {
                Vector<int> coef = Vector.Create(row.Slice(x, Vector<int>.Count));

                negSumZero += Vector.ConditionalSelect(
                    Vector.Equals(coef, zero),
                    Vector<int>.One,
                    zero);
            }
        }

        int nzeros = 64 + Vector.Sum(negSumZero);
        nzerosPos[0] = nzeros;
        return nzeros;
    }

    private static int NumNonZeroExceptLlf(int cx, int cy, JxlAcStrategy acs, int coveredBlocks, int log2CoveredBlocks, ReadOnlySpan<int> block, int nzerosStride, Span<int> nzerosPos)
    {
        Vector<int> zero = Vector<int>.Zero;
        Vector<int> negSumZero = zero;

        Span<int> llfMaskLanes = stackalloc int[JxlAcStrategy.MaximumCoefficientBlocks * (1 + JxlFrameDimensions.BlockDimensions)];
        llfMaskLanes[..JxlAcStrategy.MaximumCoefficientBlocks].Fill(-1);

        ReadOnlySpan<int> llfMaskPos = llfMaskLanes[(JxlAcStrategy.MaximumCoefficientBlocks - cx)..];

        // Rows with LLF: mask out the LLF.
        int rowWidth = cx * JxlFrameDimensions.BlockDimensions;

        for (int y = 0; y < cy; y++)
        {
            int rowOffset = y * rowWidth;

            for (int x = 0; x < rowWidth; x += Vector<int>.Count)
            {
                Vector<int> llfMask = Vector.Create(llfMaskPos.Slice(x, Vector<int>.Count));
                Vector<int> coef = (~llfMask) & Vector.Create(block.Slice(rowOffset + x, Vector<int>.Count));

                negSumZero += Vector.ConditionalSelect(
                    Vector.Equals(coef, zero),
                    Vector<int>.One,
                    zero);
            }
        }

        // Remaining rows: no mask.
        for (int y = cy; y < cy * JxlFrameDimensions.BlockDimensions; y++)
        {
            int rowOffset = y * rowWidth;

            for (int x = 0; x < rowWidth; x += Vector<int>.Count)
            {
                Vector<int> coef = Vector.Create(block.Slice(rowOffset + x, Vector<int>.Count));

                negSumZero += Vector.ConditionalSelect(
                    Vector.Equals(coef, zero),
                    Vector<int>.One,
                    zero);
            }
        }

        // We want area sum_zero, add because negSumZero is already negated.
        int nzeros = (cx * cy * JxlFrameDimensions.DctBlockSize) + Vector.Sum(negSumZero);

        int shiftedNzeros = (nzeros + coveredBlocks - 1) >> log2CoveredBlocks;

        int coveredBlocksY = acs.CoveredBlocksY;
        int coveredBlocksX = acs.CoveredBlocksX;

        for (int y = 0; y < coveredBlocksY; y++)
        {
            int rowOffset = y * nzerosStride;

            for (int x = 0; x < coveredBlocksX; x++)
            {
                nzerosPos[rowOffset + x] = shiftedNzeros;
            }
        }

        return nzeros;
    }

    public static bool TokenizeCoefficients(
        ReadOnlySpan<int> orders,
        Rectangle rect,
        ReadOnlyMemory<int>[] acRows,
        JxlAcStrategyImage acStrategy,
        JxlYCbCrChromaSubsampling cs,
        JxlImage3I tmpNumNzeroes,
        List<JxlToken> output,
        JxlImageB qdc,
        JxlImageI qf,
        JxlBlockContextMap blockCtxMap)
    {
        int xsizeBlocks = rect.Width;
        int ysizeBlocks = rect.Height;

        output.Clear();

        output.EnsureCapacity(3 * xsizeBlocks * ysizeBlocks * DctBlockSize);

        Span<int> offset = stackalloc int[3];
        int nzerosStride = tmpNumNzeroes.PixelsPerRow;

        InlineArray3<int> sby = default;
        InlineArray3<int> sbx = default;

        for (int by = 0; by < ysizeBlocks; by++)
        {
            sby[0] = by >> cs.VShift(0);
            sby[1] = by >> cs.VShift(1);
            sby[2] = by >> cs.VShift(2);

            // Span<Span<T>> is not legal C#, so keep the three planes explicitly.
            Span<int> rowNzeros0 = tmpNumNzeroes.PlaneRow(0, sby[0]);
            Span<int> rowNzeros1 = tmpNumNzeroes.PlaneRow(1, sby[1]);
            Span<int> rowNzeros2 = tmpNumNzeroes.PlaneRow(2, sby[2]);

            ReadOnlySpan<int> rowNzerosTop0 = sby[0] == 0 ? ReadOnlySpan<int>.Empty : tmpNumNzeroes.ConstPlaneRow(0, sby[0] - 1);
            ReadOnlySpan<int> rowNzerosTop1 = sby[1] == 0 ? ReadOnlySpan<int>.Empty : tmpNumNzeroes.ConstPlaneRow(1, sby[1] - 1);
            ReadOnlySpan<int> rowNzerosTop2 = sby[2] == 0 ? ReadOnlySpan<int>.Empty : tmpNumNzeroes.ConstPlaneRow(2, sby[2] - 1);

            ReadOnlySpan<byte> rowQdc = qdc.GetRow(rect.Y0() + by).Slice(rect.X0());

            ReadOnlySpan<int> rowQf = qf.GetRow(rect, by);
            JxlAcStrategyRow acsRow = acStrategy.GetRow(rect, by);

            for (int bx = 0; bx < xsizeBlocks; bx++)
            {
                JxlAcStrategy acs = acsRow[bx];

                if (!acs.IsFirstBlock)
                {
                    continue;
                }

                sbx[0] = bx >> cs.HShift(0);
                sbx[1] = bx >> cs.HShift(1);
                sbx[2] = bx >> cs.HShift(2);

                int cx = acs.CoveredBlocksX;
                int cy = acs.CoveredBlocksY;

                int coveredBlocks = cx * cy;
                int log2CoveredBlocks = (int)JxlMath.Num0BitsBelowLS1Bit_Nonzero(coveredBlocks);

                int size = coveredBlocks * JxlFrameDimensions.BlockDimensions;

                // Swap cx/cy to canonical order.
                JxlForwardCoefficientOrder.CoefficientLayout(ref cy, ref cx);

                // C++: for (int c : {1, 0, 2})
                for (int c = 0; c < 3; c++)
                {
                    int channel = c switch
                    {
                        0 => 1,
                        1 => 0,
                        _ => 2
                    };

                    if ((sbx[channel] << cs.HShift(channel)) != bx)
                    {
                        continue;
                    }

                    if ((sby[channel] << cs.VShift(channel)) != by)
                    {
                        continue;
                    }

                    ReadOnlySpan<int> block = acRows[channel].Span[offset[channel]..];

                    Span<int> rowNzeros = channel switch
                    {
                        0 => rowNzeros0,
                        1 => rowNzeros1,
                        _ => rowNzeros2
                    };

                    int nzeros;

                    if (coveredBlocks == 1)
                    {
                        nzeros = NumNonZero8x8ExceptDC(block, rowNzeros[sbx[channel]..]);
                    }
                    else
                    {
                        nzeros = NumNonZeroExceptLlf(
                            cx,
                            cy,
                            acs,
                            coveredBlocks,
                            log2CoveredBlocks,
                            block,
                            nzerosStride,
                            rowNzeros[sbx[channel]..]);
                    }

                    int ord = StrategyOrder[acs.RawStrategy];
                    int coeffOrderOffset = CoeffOrderOffset(ord, channel);
                    ReadOnlySpan<int> order = orders[coeffOrderOffset..];

                    ReadOnlySpan<int> rowNzerosTop = channel switch
                    {
                        0 => rowNzerosTop0,
                        1 => rowNzerosTop1,
                        _ => rowNzerosTop2
                    };

                    int predictedNzeros = PredictFromTopAndLeft(
                        rowNzerosTop,
                        rowNzeros,
                        sbx[channel],
                        32);

                    int blockCtx = blockCtxMap.Context(
                        rowQdc[bx],
                        (uint)rowQf[sbx[channel]],
                        ord,
                        channel);

                    int nzeroCtx = blockCtxMap.NonZeroContext(predictedNzeros, blockCtx);

                    output.Add(new JxlToken((JxlMaTreeContext)nzeroCtx, (uint)nzeros));

                    int histoOffset = blockCtxMap.ZeroDensityContextOffset(blockCtx);
                    int prev = nzeros > size / 16 ? 0 : 1;

                    for (int k = coveredBlocks; k < size && nzeros != 0; k++)
                    {
                        int coeff = block[order[k]];

                        int ctx =
                            histoOffset + ZeroDensityContext(
                                nzeros,
                                k,
                                coveredBlocks,
                                log2CoveredBlocks,
                                prev);

                        uint uCoeff = JxlPackSigned.PackUnsigned(coeff);

                        output.Add(new JxlToken((JxlMaTreeContext)ctx, uCoeff));

                        prev = coeff != 0 ? 1 : 0;
                        nzeros -= prev;
                    }

                    offset[channel] += size;
                }
            }
        }

        return true;
    }
}
