// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group.BlockLoader;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;
using SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group;

internal static class JxlGroupDecoder
{
    public static void Transpose8x8InPlace(Span<int> block)
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                RuntimeUtility.Swap(ref block[(y * 8) + x], ref block[(x * 8) + y]);
            }
        }
    }

    public static void DequantLane(
        JxlDctAcType acType,
        Vector<float> scaledDequantX,
        Vector<float> scaledDequantY,
        Vector<float> scaledDequantB,
        ReadOnlySpan<float> dequantMatrices,
        int size,
        int k,
        Vector<float> xCCMul,
        Vector<float> bCCMul,
        ReadOnlySpan<float> biases,
        JxlDctAcPointer qblock0,
        JxlDctAcPointer qblock1,
        JxlDctAcPointer qblock2,
        Span<float> block)
    {
        Vector<float> xMul = Vector.Create(dequantMatrices[k..]) * scaledDequantX;
        Vector<float> yMul = Vector.Create(dequantMatrices[(k + size)..]) * scaledDequantY;
        Vector<float> bMul = Vector.Create(dequantMatrices[(k + (size * 2))..]) * scaledDequantB;

        Vector<int> quantizedXInt;
        Vector<int> quantizedYInt;
        Vector<int> quantizedBInt;

        if (acType == JxlDctAcType.Ac16)
        {
            quantizedXInt = Vector.WidenLower(Vector.Create<short>(qblock0.Pointer16[k..]));
            quantizedYInt = Vector.WidenLower(Vector.Create<short>(qblock1.Pointer16[k..]));
            quantizedBInt = Vector.WidenLower(Vector.Create<short>(qblock2.Pointer16[k..]));
        }
        else
        {
            quantizedXInt = Vector.Create<int>(qblock0.Pointer32[k..]);
            quantizedYInt = Vector.Create<int>(qblock1.Pointer32[k..]);
            quantizedBInt = Vector.Create<int>(qblock2.Pointer32[k..]);
        }

        Vector<float> dequantXCC = JxlQuantizerSimd.AdjustQuantBias(0, quantizedXInt, biases) * xMul;
        Vector<float> dequantY = JxlQuantizerSimd.AdjustQuantBias(1, quantizedYInt, biases) * yMul;
        Vector<float> dequantBCC = JxlQuantizerSimd.AdjustQuantBias(2, quantizedBInt, biases) * bMul;

        Vector<float> dequantX = (xCCMul * dequantY) + dequantXCC;
        Vector<float> dequantB = (bCCMul * dequantY) + dequantBCC;

        dequantX.CopyTo(block[k..]);
        dequantY.CopyTo(block[(k + size)..]);
        dequantB.CopyTo(block[(k + (size * 2))..]);
    }

    private static void DequantBlock(
        JxlDctAcType acType,
        float invGlobalScale,
        int quant,
        float xDmMultiplier,
        float bDmMultiplier,
        Vector<float> xCcMul,
        Vector<float> bCcMul,
        JxlAcStrategyType kind,
        int size,
        JxlQuantizer quantizer,
        int coveredBlocks,
        Span<int> sbx,
        Span<float> dcRow,
        int dcStride,
        Span<float> biases,
        JxlDctAcPointer qblock0,
        JxlDctAcPointer qblock1,
        JxlDctAcPointer qblock2,
        Span<float> block,
        Span<float> scratch)
    {
        float scaledDequantS = invGlobalScale / quant;

        Vector<float> scaledDequantX = new(scaledDequantS * xDmMultiplier);
        Vector<float> scaledDequantY = new(scaledDequantS);
        Vector<float> scaledDequantB = new(scaledDequantS * bDmMultiplier);

        ReadOnlySpan<float> dequantMatrices = quantizer.DequantMatrix(kind, 0);

        for (int k = 0; k < coveredBlocks * JxlFrameDimensions.DctBlockSize; k += Vector<float>.Count)
        {
            DequantLane(acType, scaledDequantX, scaledDequantY, scaledDequantB, dequantMatrices, size, k, xCcMul, bCcMul, biases, qblock0, qblock1, qblock2, block);
        }

        for (int c = 0; c < 3; c++)
        {
            JxlTransformsDecoder.LowestFrequenciesFromDc(kind, dcRow[sbx[c]..], dcStride, block[(c * size)..], scratch);
        }
    }

    private static bool DecodeGroupCore(
        JxlFrameHeader frameHeader,
        JxlGetBlock getBlock,
        JxlGroupDecoderCache groupDecCache,
        JxlPassesDecoderState decState,
        int thread,
        int groupIdx,
        JxlRenderPipelineInput renderPipelineInput,
        JpegData? jpegData,
        JxlDrawMode draw)
    {
        Rectangle blockRect = decState.Shared.FrameDimensions.BlockGroupRect(groupIdx);
        JxlAcStrategyImage acStrategy = decState.Shared.AcStrategy;

        int xsizeBlocks = blockRect.Width;
        int ysizeBlocks = blockRect.Height;

        int dcStride = decState.Shared.Dc.PixelsPerRow;
        float invGlobalScale = decState.Shared.Quantizer.InverseGlobalScale;

        JxlYCbCrChromaSubsampling cs = frameHeader.ChromaSubsampling!;

        Vector<int> jpegDctMin = new(-4095);
        Vector<int> jpegDctMax = new(4095);

        InlineArray3<int> idctStride = default;
        for (int c = 0; c < 3; c++)
        {
            idctStride[c] = renderPipelineInput.GetBuffer(c).First.PixelsPerRow();
        }

        Span<int> scaledQTable = stackalloc int[64 * 3];

        JxlDctAcType acType = decState.Coefficients.Type;

        bool accumulate = !decState.Coefficients.IsEmpty;
        int offset = 0;

        InlineArray3<int> jpegCMap = default;
        bool jpegIsGray = false;
        InlineArray3<int> dcOff = default;

        JxlColorCorrelation colorCorrelation = decState.Shared.ColorMap.Base;

        if (jpegData is not null)
        {
            if (!colorCorrelation.IsJpegCompatible)
            {
                return false;
            }

            jpegIsGray = jpegData.Components.Count == 1;

            if (frameHeader.ColorTransform == JxlColorTransform.Xyb)
            {
                return false;
            }

            JxlFrameHeader.JpegOrder(frameHeader.ColorTransform, jpegIsGray, out jpegCMap);

            Span<JxlQuantizerEncoding> qe = decState.Shared.Matrices.GetEncodings();

            if (qe.Length == 0 ||
                qe[0].Mode != JxlQuantMode.Raw ||
                MathF.Abs(qe[0].QuantizationTableDenominator - (1.0f / (8 * 255))) > 1e-8f)
            {
                return false;
            }

            if (qe[0].QuantizationTable!.Length != 3 * 8 * 8)
            {
                return false;
            }

            ReadOnlySpan<int> qTable = qe[0].QuantizationTable.AsSpan();

            for (int c = 0; c < 3; c++)
            {
                if (frameHeader.ColorTransform == JxlColorTransform.None)
                {
                    dcOff[c] = 1024 / qTable[64 * c];
                }

                for (int i = 0; i < 64; i++)
                {
                    int num = qTable[64 + i];
                    int den = qTable[(64 * c) + i];

                    if (num <= 0 || den <= 0 || num >= 65536 || den >= 65536)
                    {
                        return false;
                    }

                    scaledQTable[(64 * c) + ((i % 8) * 8) + (i / 8)] = (1 << JxlChromaFromLuma.CflFixedPointPrecision) * num / den;
                }
            }
        }

        Span<int> hShift =
        [
            cs!.HShift(0),
            cs.HShift(1),
            cs.HShift(2)
        ];

        Span<int> vShift =
        [
            cs!.VShift(0),
            cs.VShift(1),
            cs.VShift(2)
        ];

        Span<Rectangle> r = stackalloc Rectangle[3];
        r.Clear();

        for (int i = 0; i < 3; i++)
        {
            r[i] = new Rectangle(
                blockRect.X0() >> hShift[i],
                blockRect.Y0() >> vShift[i],
                blockRect.Width >> hShift[i],
                blockRect.Height >> vShift[i]);

            if (!r[i].IsInside(new Rectangle(
                0,
                0,
                decState.Shared.Dc.Plane(i).XSize,
                decState.Shared.Dc.Plane(i).YSize)))
            {
                return false;
            }
        }

        Span<int> transposedDctY = stackalloc int[JxlFrameDimensions.DctBlockSize];
        InlineArray3<int> sby = default;

        for (int by = 0; by < ysizeBlocks; by++)
        {
            getBlock.StartRow(by);

            sby[0] = by >> vShift[0];
            sby[1] = by >> vShift[1];
            sby[2] = by >> vShift[2];

            ReadOnlySpan<int> rowQuant = decState.Shared.RawQuantField.GetRow(blockRect, by);

            Span<float> dcRow0 = decState.Shared.Dc.PlaneRow(r[0], 0, sby[0]);
            Span<float> dcRow1 = decState.Shared.Dc.PlaneRow(r[1], 1, sby[1]);
            Span<float> dcRow2 = decState.Shared.Dc.PlaneRow(r[2], 2, sby[2]);

            int ty = (blockRect.Y0() + by) / JxlChromaFromLuma.ColorTileDimensionInBlocks;

            JxlAcStrategyRow acsRow = acStrategy.GetRow(blockRect, by);

            ReadOnlySpan<sbyte> rowCMap0 = decState.Shared.ColorMap.YToXMap!.GetRow(ty);
            ReadOnlySpan<sbyte> rowCMap2 = decState.Shared.ColorMap.YToBMap!.GetRow(ty);

            Span<float> idctRow0;
            Span<float> idctRow1;
            Span<float> idctRow2;

            Span<int> jpegRow0 = default;
            Span<int> jpegRow1 = default;
            Span<int> jpegRow2 = default;

            for (int c = 0; c < 3; c++)
            {
                var buffer = renderPipelineInput.GetBuffer(c);
                Span<float> idctRow = buffer.Second.Row(buffer.First, sby[c] * JxlFrameDimensions.BlockDimensions);

                if (c == 0)
                {
                    idctRow0 = idctRow;
                }
                else if (c == 1)
                {
                    idctRow1 = idctRow;
                }
                else
                {
                    idctRow2 = idctRow;
                }

                if (jpegData is not null)
                {
                    JpegComponent component = jpegData.Components[jpegCMap[c]];

                    Span<int> jpegRow =
                        component.DangerousCoefficientsAsSpan()[
                            (((component.WidthInBlocks * (r[c].Y0() + sby[c])) + r[c].X0()) *
                            JxlFrameDimensions.DctBlockSize)..];

                    if (c == 0)
                    {
                        jpegRow0 = jpegRow;
                    }
                    else if (c == 1)
                    {
                        jpegRow1 = jpegRow;
                    }
                    else
                    {
                        jpegRow2 = jpegRow;
                    }
                }
            }

            int bx = 0;

            for (int tx = 0;
                 tx < JxlMath.DivCeil(xsizeBlocks, JxlChromaFromLuma.ColorTileDimensionInBlocks);
                 tx++)
            {
                int absTx = tx + (blockRect.X0() / JxlChromaFromLuma.ColorTileDimensionInBlocks);

                Vector<float> xCcMul = new(colorCorrelation.YToXRatio(rowCMap0[absTx]));
                Vector<float> bCcMul = new(colorCorrelation.YToBRatio(rowCMap2[absTx]));

                for (; bx < xsizeBlocks && bx < (tx + 1) * JxlChromaFromLuma.ColorTileDimensionInBlocks;)
                {
                    InlineArray3<int> sbx = default;
                    sbx[0] = bx >> hShift[0];
                    sbx[1] = bx >> hShift[1];
                    sbx[2] = bx >> hShift[2];

                    JxlAcStrategy acs = acsRow[bx];
                    int llfX = acs.CoveredBlocksX;

                    if (!acs.IsFirstBlock)
                    {
                        bx += llfX;
                        continue;
                    }

                    int log2CoveredBlocks = acs.Log2CoveredBlocks;
                    int coveredBlocks = 1 << log2CoveredBlocks;
                    int size = coveredBlocks * JxlFrameDimensions.DctBlockSize;

                    JxlDctAcPointer qblock0;
                    JxlDctAcPointer qblock1;
                    JxlDctAcPointer qblock2;

                    if (accumulate)
                    {
                        qblock0 = decState.Coefficients.GetPlaneRow(0, groupIdx, offset);
                        qblock1 = decState.Coefficients.GetPlaneRow(1, groupIdx, offset);
                        qblock2 = decState.Coefficients.GetPlaneRow(2, groupIdx, offset);
                    }
                    else
                    {
                        if (draw != JxlDrawMode.Draw)
                        {
                            return false;
                        }

                        if (acType == JxlDctAcType.Ac16)
                        {
                            groupDecCache.DecodedGroupQBlock16.Clear();

                            qblock0 = new JxlDctAcPointer(groupDecCache.DecodedGroupQBlock16.Slice(0, size));
                            qblock1 = new JxlDctAcPointer(groupDecCache.DecodedGroupQBlock16.Slice(size, size));
                            qblock2 = new JxlDctAcPointer(groupDecCache.DecodedGroupQBlock16.Slice(size * 2, size));
                        }
                        else
                        {
                            groupDecCache.DecodedGroupQBlock.Clear();

                            qblock0 = new JxlDctAcPointer(groupDecCache.DecodedGroupQBlock.Slice(0, size));
                            qblock1 = new JxlDctAcPointer(groupDecCache.DecodedGroupQBlock.Slice(size, size));
                            qblock2 = new JxlDctAcPointer(groupDecCache.DecodedGroupQBlock.Slice(size * 2, size));
                        }
                    }

                    if (!getBlock.LoadBlock(bx, by, acs, size, log2CoveredBlocks, qblock0, qblock1, qblock2, acType))
                    {
                        return false;
                    }

                    offset += size;

                    if (draw == JxlDrawMode.DoNotDraw)
                    {
                        bx += llfX;
                        continue;
                    }

                    if (acs.Strategy != JxlAcStrategyType.DCT)
                    {
                        return false;
                    }

                    foreach (int c in (ReadOnlySpan<int>)[1, 0, 2])
                    {
                        // Propagate only Y for grayscale.
                        if (jpegIsGray && c != 1)
                        {
                            continue;
                        }

                        if ((sbx[c] << hShift[c]) != bx || (sby[c] << vShift[c]) != by)
                        {
                            continue;
                        }

                        // We can't have a Span<T> inside of an array, another Span<T>,
                        // or even an inline array.
                        Span<int> jpegPos =
                            c == 0
                            ? jpegRow0[(sbx[c] * JxlFrameDimensions.DctBlockSize)..]
                            : c == 1
                              ? jpegRow1[(sbx[c] * JxlFrameDimensions.DctBlockSize)..]
                              : jpegRow2[(sbx[c] * JxlFrameDimensions.DctBlockSize)..];

                        // JPEG XL is transposed, JPEG is not.
                        JxlDctAcPointer transposedDct = c switch
                        {
                            0 => qblock0,
                            1 => qblock1,
                            2 => qblock2,
                            _ => throw new InvalidOperationException()
                        };

                        Transpose8x8InPlace(transposedDct.Pointer32);

                        // No CfL, so there is no need to store the Y block converted to integers.
                        if (!cs.Is444 || (rowCMap0[absTx] == 0 && rowCMap2[absTx] == 0))
                        {
                            for (int i = 0; i < JxlFrameDimensions.DctBlockSize; i += Vector<int>.Count)
                            {
                                Vector<int> ini = Vector.Create<int>(transposedDct.Pointer32[i..]);
                                ini.CopyTo(jpegPos[i..]);
                            }
                        }
                        else if (c == 1)
                        {
                            // Y channel: save for restoring X/B, but nothing else to do.
                            for (int i = 0; i < JxlFrameDimensions.DctBlockSize; i += Vector<int>.Count)
                            {
                                Vector<int> ini = Vector.Create<int>(transposedDct.Pointer32[i..]);
                                ini.CopyTo(transposedDctY[i..]);
                                ini.CopyTo(jpegPos[i..]);
                            }
                        }
                        else
                        {
                            // transposedDctY contains the Y channel block, transposed.
                            Vector<int> scale = new(JxlColorCorrelation.RatioJpeg(c == 0 ? rowCMap0[absTx] : rowCMap2[absTx]));
                            Vector<int> round = new(1 << (JxlChromaFromLuma.ColorTileDimensionInBlocks - 1));

                            for (int i = 0; i < JxlFrameDimensions.DctBlockSize; i += Vector<int>.Count)
                            {
                                Vector<int> input = Vector.Create<int>(transposedDct.Pointer32[i..]);
                                Vector<int> inputY = Vector.Create<int>(transposedDctY[i..]);
                                Vector<int> qt = Vector.Create<int>(scaledQTable[((c * size) + i)..]);

                                Vector<int> coeffScale = ((qt * scale) + round) >> JxlChromaFromLuma.ColorTileDimensionInBlocks;
                                Vector<int> cflFactor = ((inputY * coeffScale) + round) >> JxlChromaFromLuma.ColorTileDimensionInBlocks;
                                Vector<int> result = input + cflFactor;

                                result.CopyTo(jpegPos[i..]);
                            }
                        }

                        jpegPos[0] = (int)Math.Clamp(
                            c == 0
                            ? dcRow0[sbx[c] - dcOff[c]]
                            : c == 1
                              ? dcRow1[sbx[c] - dcOff[c]]
                              : dcRow2[sbx[c] - dcOff[c]],
                            -2047.0f,
                            2047.0f);

                        Vector<int> overflow = Vector<int>.Zero;
                        Vector<int> underflow = Vector<int>.Zero;

                        for (int i = 0; i < JxlFrameDimensions.DctBlockSize; i += Vector<int>.Count)
                        {
                            Vector<int> input = Vector.Create<int>(jpegPos[i..]);

                            overflow |= Vector.GreaterThan(input, jpegDctMax);
                            underflow |= Vector.LessThan(input, jpegDctMin);
                        }

                        if (!Vector.All(overflow | underflow, 0))
                        {
                            return false;
                        }
                    }

                    bx += llfX;
                }
            }
        }

        return true;
    }

    public static bool DecodeAcVarBlock(
        JxlDctAcType acType,
        bool usesLz77,
        int ctxOffset,
        int log2CoveredBlocks,
        Span<int> rowNzeros,
        ReadOnlySpan<int> rowNzerosTop,
        int nzerosStride,
        int c,
        int bx,
        int lbx,
        JxlAcStrategy acs,
        ReadOnlySpan<int> coeffOrder,
        JxlBitReader br,
        JxlAnsSymbolReader decoder,
        ReadOnlySpan<byte> contextMap,
        ReadOnlySpan<byte> qdcRow,
        ReadOnlySpan<int> qfRow,
        JxlBlockContextMap blockCtxMap,
        JxlDctAcPointer block,
        int shift = 0)
    {
        // Equal to number of LLF coefficients.
        int coveredBlocks = 1 << log2CoveredBlocks;
        int size = coveredBlocks * JxlFrameDimensions.DctBlockSize;

        int predictedNzeros = JxlEntropyCoder.PredictFromTopAndLeft(rowNzerosTop, rowNzeros, bx, 32);
        int ord = JxlCoefficientOrder.StrategyOrder[acs.RawStrategy];
        ReadOnlySpan<int> order = coeffOrder[JxlCoefficientOrder.CoeffOrderOffset(ord, c)..];

        int blockCtx = blockCtxMap.Context(qdcRow[lbx], (uint)qfRow[bx], ord, c);
        int nzeroCtx = blockCtxMap.NonZeroContext(predictedNzeros, blockCtx) + ctxOffset;
        int nzeros = decoder.ReadHybridUintInlined(usesLz77, nzeroCtx, br, contextMap);

        if (nzeros > size - coveredBlocks)
        {
            return false;
        }

        for (int y = 0; y < acs.CoveredBlocksY; y++)
        {
            for (int x = 0; x < acs.CoveredBlocksX; x++)
            {
                rowNzeros[bx + x + (y * nzerosStride)] = (nzeros + coveredBlocks - 1) >> log2CoveredBlocks;
            }
        }

        int histoOffset = ctxOffset + blockCtxMap.ZeroDensityContextOffset(blockCtx);

        int prev = nzeros > size / 16 ? 0 : 1;

        for (int k = coveredBlocks; k < size && nzeros != 0; k++)
        {
            int ctx =
                histoOffset +
                JxlAcContext.ZeroDensityContext(
                    nzeros,
                    k,
                    coveredBlocks,
                    log2CoveredBlocks,
                    prev);

            int uCoeff = decoder.ReadHybridUintInlined(usesLz77, ctx, br, contextMap);

            // Hand-rolled version of UnpackSigned, shifting before conversion
            // to signed integer to avoid undefined behavior.
            int magnitude = uCoeff >> 1;
            int negSign = (~uCoeff) & 1;

            int coeff = (magnitude ^ (negSign - 1)) << shift;

            if (acType == JxlDctAcType.Ac16)
            {
                block.Pointer16[order[k]] += (short)coeff;
            }
            else
            {
                block.Pointer32[order[k]] += coeff;
            }

            prev = uCoeff != 0 ? 1 : 0;
            nzeros -= prev;
        }

        if (nzeros != 0)
        {
            return false;
        }

        return true;
    }

    public static bool DecodeGroup(
        JxlFrameHeader frameHeader,
        JxlBitReader[] readers,
        int numPasses,
        int groupIndex,
        JxlPassesDecoderState decState,
        JxlGroupDecoderCache groupDecCache,
        int thread,
        JxlRenderPipelineInput renderPipelineInput,
        JpegData? jpegData,
        int firstPass,
        bool forceDraw,
        bool dcOnly,
        ref bool shouldRunPipeline)
    {
        bool draw = numPasses + firstPass == frameHeader.Passes.NumPasses || forceDraw;

        shouldRunPipeline = draw;

        if (draw && numPasses == 0 && firstPass == 0)
        {
            groupDecCache.InitializeDcBufferOnce();

            JxlYCbCrChromaSubsampling cs = frameHeader.ChromaSubsampling;

            for (int c = 0; c < 3; c++)
            {
                int hs = cs!.HShift(c);
                int vs = cs.VShift(c);

                Rectangle srcRectPrecs = decState.Shared.FrameDimensions.BlockGroupRect(groupIndex);

                Rectangle srcRect = new(
                    srcRectPrecs.X0() >> hs,
                    srcRectPrecs.Y0() >> vs,
                    srcRectPrecs.Width >> hs,
                    srcRectPrecs.Height >> vs);

                Rectangle copyRect = new(
                    RenderPipelineStageBase.RenderPipelineXOffset,
                    2,
                    srcRect.Width,
                    srcRect.Height);

                JxlPlane<float> dcPlane = decState.Shared.Dc.Plane(c);

                if (!JxlImageOperations.CopyImageToWithPadding(
                    srcRect,
                    dcPlane,
                    2,
                    copyRect,
                    groupDecCache.DcBuffer))
                {
                    return false;
                }

                // Mirror-pad the left and right edges.
                // Interleaving the two padding pixels ensures that padding
                // also works correctly when the DC size is one pixel.
                for (int y = 0; y < srcRect.Height + 4; y++)
                {
                    int xEnd =
                        RenderPipelineStageBase.RenderPipelineXOffset +
                        (dcPlane.XSize >> hs) -
                        srcRect.X0();

                    Span<float> row = groupDecCache.DcBuffer.Row(y);

                    for (int ix = 0; ix < 2; ix++)
                    {
                        if (srcRect.X0() == 0)
                        {
                            row[RenderPipelineStageBase.RenderPipelineXOffset - ix - 1] = row[RenderPipelineStageBase.RenderPipelineXOffset + ix];
                        }

                        if (srcRect.X0() + srcRect.Width + 2 >= (decState.Shared.Dc.XSize >> hs))
                        {
                            row[xEnd + ix] = row[xEnd - ix - 1];
                        }
                    }
                }

                JxlRenderPipelineBuffer buffer = renderPipelineInput.GetBuffer(c);

                Rectangle dstRect = buffer.Bounds;
                JxlImageF upsamplingDst = buffer.Image;

                if (!dstRect.IsInside(upsamplingDst.GetRectangle()))
                {
                    return false;
                }

                JxlRenderPipelineRowInfo inputRows = new(1, 5);
                JxlRenderPipelineRowInfo outputRows = new(1, 8);

                for (int y = srcRect.Y0(); y < srcRect.Y0() + srcRect.Height; y++)
                {
                    for (int iy = 0; iy < 5; iy++)
                    {
                        int inputY =
                            JxlImageOperations.Mirror(
                                y + iy - 2,
                                decState.Shared.Dc.Plane(c).YSize) +
                            2 -
                            srcRect.Y0();

                        inputRows[0][iy] =
                            groupDecCache.DcBuffer.Row(inputY);
                    }

                    for (int iy = 0; iy < 8; iy++)
                    {
                        outputRows[0][iy] =
                            upsamplingDst.GetRowMemory(
                                dstRect,
                                ((y - srcRect.Y0()) << 3) + iy)[
                                    RenderPipelineStageBase.RenderPipelineXOffset..];
                    }

                    // throws on failure
                    decState.Upsampler8x.ProcessRow(
                        inputRows,
                        outputRows,
                        0,
                        0,
                        srcRect.Width,
                        0,
                        0,
                        thread);
                }
            }

            return true;
        }

        int histogramSelectorBits = 0;

        if (dcOnly)
        {
            if (numPasses != 0)
            {
                return false;
            }
        }
        else
        {
            if (decState.Shared.NumHistograms <= 0)
            {
                return false;
            }

            histogramSelectorBits = JxlMath.CeilLog2Nonzero(decState.Shared.NumHistograms);
        }

        Rectangle blockGroupRect = decState.Shared.FrameDimensions.BlockGroupRect(groupIndex);

        JxlBitStreamBlockLoader getBlock = new();

        if (!getBlock.Init(
            frameHeader,
            readers,
            numPasses,
            groupIndex,
            histogramSelectorBits,
            blockGroupRect,
            decState,
            firstPass))
        {
            return false;
        }

        if (!DecodeGroupCore(
            frameHeader,
            ref getBlock,
            groupDecCache,
            decState,
            thread,
            groupIndex,
            renderPipelineInput,
            jpegData,
            draw))
        {
            return false;
        }

        for (int pass = 0; pass < numPasses; pass++)
        {
            if (!getBlock.Decoders[pass].CheckAnsFinalState())
            {
                return false;
            }
        }

        return true;
    }

    public static bool DecodeGroupForRoundtrip(
        JxlFrameHeader frameHeader,
        IReadOnlyList<IJxlDctAcImage> ac,
        int groupIndex,
        JxlPassesDecoderState decState,
        JxlGroupDecoderCache groupDecCache,
        int thread,
        JxlRenderPipelineInput renderPipelineInput,
        JpegData? jpegData,
        JxlAuxiliaryOutput? auxOut)
    {
        JxlEncoderBlockLoader getBlock =
            JxlEncoderBlockLoader.Create(
                ac,
                groupIndex,
                frameHeader.Passes.Shift);

        if (!groupDecCache.InitializeOnce(
            numPasses: 0,
            usedAcs: (1 << JxlAcStrategy.NumberOfValidStrategies) - 1))
        {
            return false;
        }

        return DecodeGroupCore(
            frameHeader,
            ref getBlock,
            groupDecCache,
            decState,
            thread,
            groupIndex,
            renderPipelineInput,
            jpegData,
            true);
    }
}
