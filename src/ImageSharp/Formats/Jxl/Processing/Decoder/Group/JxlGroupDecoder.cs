// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
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
    /// <summary>
    /// Restores one transform's AC planes, color correlation and separately coded lowest frequencies.
    /// </summary>
    /// <param name="acType">The stored integer coefficient width.</param>
    /// <param name="invGlobalScale">The inverse frame quantization scale.</param>
    /// <param name="quant">The positive block quantizer.</param>
    /// <param name="xDmMultiplier">The X matrix multiplier.</param>
    /// <param name="bDmMultiplier">The B matrix multiplier.</param>
    /// <param name="xCcMul">The Y contribution to X.</param>
    /// <param name="bCcMul">The Y contribution to B.</param>
    /// <param name="kind">The transform strategy.</param>
    /// <param name="size">The coefficient count per channel.</param>
    /// <param name="quantizer">The prepared frame quantizer and matrices.</param>
    /// <param name="matrixStorage">The complete coefficient tables borrowed before the group loops.</param>
    /// <param name="coveredBlocks">The number of 8-by-8 blocks covered by the transform.</param>
    /// <param name="sbx">The DC image's horizontal offset for each channel.</param>
    /// <param name="dcRow">The current DC samples.</param>
    /// <param name="dcStride">The DC row stride, in samples.</param>
    /// <param name="biases">The channel biases and shared reciprocal numerator.</param>
    /// <param name="qblock0">The X integer coefficients.</param>
    /// <param name="qblock1">The Y integer coefficients.</param>
    /// <param name="qblock2">The B integer coefficients.</param>
    /// <param name="block">The caller-owned consecutive X, Y and B float planes.</param>
    /// <param name="scratch">The caller-owned transform scratch.</param>
    private static void DequantBlock(
        JxlDctAcType acType,
        float invGlobalScale,
        int quant,
        float xDmMultiplier,
        float bDmMultiplier,
        float xCcMul,
        float bCcMul,
        JxlAcStrategyType kind,
        int size,
        JxlQuantizer quantizer,
        ReadOnlySpan<float> matrixStorage,
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
        float inverseQuantization = invGlobalScale / quant;
        float xScale = inverseQuantization * xDmMultiplier;
        float bScale = inverseQuantization * bDmMultiplier;
        ReadOnlySpan<float> matrices = quantizer.DequantMatrix(matrixStorage, kind, 0);
        int coefficientCount = coveredBlocks * JxlFrameDimensions.DctBlockSize;

        // The matrix table stores X, Y and B consecutively for this transform. Only logical coefficients participate.
        // Select the stored integer width once. Both paths share numeric conversion, bias correction and correlation.
        if (acType == JxlDctAcType.Ac16)
        {
            JxlQuantizer.Dequantize(
                qblock0.Pointer16[..coefficientCount],
                qblock1.Pointer16[..coefficientCount],
                qblock2.Pointer16[..coefficientCount],
                matrices,
                xScale,
                inverseQuantization,
                bScale,
                xCcMul,
                bCcMul,
                biases,
                block);
        }
        else
        {
            JxlQuantizer.Dequantize(
                qblock0.Pointer32[..coefficientCount],
                qblock1.Pointer32[..coefficientCount],
                qblock2.Pointer32[..coefficientCount],
                matrices,
                xScale,
                inverseQuantization,
                bScale,
                xCcMul,
                bCcMul,
                biases,
                block);
        }

        // AC dequantization does not supply the transform's lowest frequencies. Restore them from the separate DC image.
        for (int c = 0; c < 3; c++)
        {
            JxlTransformsDecoder.LowestFrequenciesFromDc(kind, dcRow[sbx[c]..], dcStride, block[(c * size)..], scratch);
        }
    }

    private static bool DecodeGroupCore(
        JxlFrameHeader frameHeader,
        IJxlGetBlock getBlock,
        JxlGroupDecoderCache groupDecCache,
        JxlPassesDecoderContext decState,
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

            ReadOnlySpan<JxlQuantizerEncoding> qe = decState.Shared.Matrices.GetEncodings();

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

            ReadOnlySpan<sbyte> rowCMap0 = decState.Shared.ColorMap.YToXMap.GetRow(ty);
            ReadOnlySpan<sbyte> rowCMap2 = decState.Shared.ColorMap.YToBMap.GetRow(ty);

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

                    if (!getBlock.TryLoadBlock(bx, by, acs, size, log2CoveredBlocks, qblock0, qblock1, qblock2, acType))
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

                        JxlDctAcPointer quantizedDct = c switch
                        {
                            0 => qblock0,
                            1 => qblock1,
                            2 => qblock2,
                            _ => throw new InvalidOperationException()
                        };

                        // JPEG XL stores the two coefficient axes in the opposite order to JPEG.
                        // Source coefficients belong to the frame or group cache. Write their transpose directly into the distinct JPEG block.
                        // Keeping source storage unchanged also preserves accumulated coefficients for later progressive passes.
                        SimdUtils.Transpose(
                            quantizedDct.Pointer32[..JxlFrameDimensions.DctBlockSize],
                            JxlFrameDimensions.BlockDimensions,
                            jpegPos[..JxlFrameDimensions.DctBlockSize],
                            JxlFrameDimensions.BlockDimensions,
                            JxlFrameDimensions.BlockDimensions,
                            JxlFrameDimensions.BlockDimensions);

                        if (cs.Is444 && (rowCMap0[absTx] != 0 || rowCMap2[absTx] != 0))
                        {
                            if (c == 1)
                            {
                                // Y is processed before X and B. Retain its transposed coefficients before the separately coded DC replaces entry zero.
                                jpegPos[..JxlFrameDimensions.DctBlockSize].CopyTo(transposedDctY);
                            }
                            else
                            {
                                // Correlation updates the completed JPEG block in place. The retained Y block keeps its pre-DC coefficient values.
                                // The quantization-table ratio and color ratio both carry 11 fractional bits.
                                // Each multiplication drops one fixed-point scale; add half a unit before the native arithmetic right shift.
                                Vector<int> scale = new(JxlColorCorrelation.RatioJpeg(c == 0 ? rowCMap0[absTx] : rowCMap2[absTx]));
                                Vector<int> round = new(1 << (JxlChromaFromLuma.CflFixedPointPrecision - 1));

                                for (int i = 0; i < JxlFrameDimensions.DctBlockSize; i += Vector<int>.Count)
                                {
                                    Vector<int> input = Vector.Create<int>(jpegPos[i..]);
                                    Vector<int> inputY = Vector.Create<int>(transposedDctY[i..]);
                                    Vector<int> qt = Vector.Create<int>(scaledQTable[((c * size) + i)..]);

                                    Vector<int> coeffScale = ((qt * scale) + round) >> JxlChromaFromLuma.CflFixedPointPrecision;
                                    Vector<int> cflFactor = ((inputY * coeffScale) + round) >> JxlChromaFromLuma.CflFixedPointPrecision;
                                    Vector<int> result = input + cflFactor;

                                    result.CopyTo(jpegPos[i..]);
                                }
                            }
                        }

                        // The DC offset is a coefficient value, not a column offset. Subtract it after reading this block's DC sample.
                        // JPEG stores this separately coded coefficient in the signed range [-2047,2047].
                        float dc = c == 0 ? dcRow0[sbx[c]] : c == 1 ? dcRow1[sbx[c]] : dcRow2[sbx[c]];
                        jpegPos[0] = (int)Math.Clamp(dc - dcOff[c], -2047F, 2047F);

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
        int shift)
    {
        // Equal to number of LLF coefficients.
        int coveredBlocks = 1 << log2CoveredBlocks;
        int size = coveredBlocks * JxlFrameDimensions.DctBlockSize;

        int predictedNzeros = JxlEntropyCoder.PredictFromTopAndLeft(rowNzerosTop, rowNzeros, bx, 32);
        int ord = JxlCoefficientOrder.StrategyOrder[acs.RawStrategy];
        ReadOnlySpan<int> order = coeffOrder[JxlCoefficientOrder.CoeffOrderOffset(ord, c)..];

        int blockCtx = blockCtxMap.Context(qdcRow[lbx], (uint)qfRow[bx], ord, c);
        int nzeroCtx = blockCtxMap.NonZeroContext(predictedNzeros, blockCtx) + ctxOffset;

        // Borrow history once for this block. Every coefficient advances the same pass reader.
        Span<int> window = decoder.Lz77Window;
        int nzeros = decoder.ReadHybridUInt(usesLz77, nzeroCtx, br, contextMap, window);

        if ((uint)nzeros > (uint)(size - coveredBlocks))
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

            int uCoeff = decoder.ReadHybridUInt(usesLz77, ctx, br, contextMap, window);

            // Interpret the complete unsigned code before applying the progressive pass's coefficient shift.
            int coeff = JxlPackSigned.UnpackSigned((uint)uCoeff) << shift;

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

    /// <summary>
    /// Decodes the requested coefficient passes and draws the group when the frame settings require it.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns entropy histories.</param>
    /// <param name="frameHeader">The parsed frame and pass settings.</param>
    /// <param name="readers">One bit reader per requested pass.</param>
    /// <param name="numPasses">The number of passes to decode.</param>
    /// <param name="groupIndex">The frame's group index.</param>
    /// <param name="decState">The owner of frame tables and reconstructed coefficients.</param>
    /// <param name="groupDecCache">The reusable coefficient and transform workspace.</param>
    /// <param name="thread">The existing render-workspace index.</param>
    /// <param name="renderPipelineInput">The destination group buffers.</param>
    /// <param name="jpegData">The JPEG reconstruction destination, when requested.</param>
    /// <param name="firstPass">The first frame pass to decode.</param>
    /// <param name="forceDraw">Whether to draw before the last pass.</param>
    /// <param name="dcOnly">Whether this request contains only DC data.</param>
    /// <param name="shouldRunPipeline">Whether the decoded group requires rendering.</param>
    /// <returns>Whether coefficient decoding and entropy-state checks succeeded.</returns>
    public static bool DecodeGroup(
        Configuration configuration,
        JxlFrameHeader frameHeader,
        JxlBitReader[] readers,
        int numPasses,
        int groupIndex,
        JxlPassesDecoderContext decState,
        JxlGroupDecoderCache groupDecCache,
        int thread,
        JxlRenderPipelineInput renderPipelineInput,
        JpegData? jpegData,
        int firstPass,
        bool forceDraw,
        bool dcOnly,
        ref bool shouldRunPipeline)
    {
        bool draw = numPasses + firstPass == frameHeader.Passes.NumberOfPasses || forceDraw;

        shouldRunPipeline = draw;

        if (draw && numPasses == 0 && firstPass == 0)
        {
            groupDecCache.InitializeDcBufferOnce();

            JxlYCbCrChromaSubsampling cs = frameHeader.ChromaSubsampling!;

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

                    Span<float> row = groupDecCache.DcBuffer.GetRow(y);

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
                JxlPlane<float> upsamplingDst = buffer.Image;

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

        // The group owns its entropy readers. Dispose them on all exits, including partial initialization.
        using JxlBitStreamBlockLoader getBlock = new(frameHeader, decState, groupDecCache, readers, firstPass, numPasses, blockGroupRect);
        if (!getBlock.Init(configuration, histogramSelectorBits, decState.Shared.NumHistograms))
        {
            return false;
        }

        if (!DecodeGroupCore(
            frameHeader,
            getBlock,
            groupDecCache,
            decState,
            thread,
            groupIndex,
            renderPipelineInput,
            jpegData,
            draw ? JxlDrawMode.Draw : JxlDrawMode.DoNotDraw))
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
        JxlPassesDecoderContext decState,
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

        groupDecCache.InitializeOnce(
            numPasses: 0,
            usedACStrategy: (1 << JxlAcStrategy.NumberOfValidStrategies) - 1);

        return DecodeGroupCore(
            frameHeader,
            getBlock,
            groupDecCache,
            decState,
            thread,
            groupIndex,
            renderPipelineInput,
            jpegData,
            JxlDrawMode.Draw);
    }
}
