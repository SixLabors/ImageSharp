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
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

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
    /// <param name="dcRow0">The current X-channel DC samples.</param>
    /// <param name="dcRow1">The current Y-channel DC samples.</param>
    /// <param name="dcRow2">The current B-channel DC samples.</param>
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
        Span<float> dcRow0,
        Span<float> dcRow1,
        Span<float> dcRow2,
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
            Span<float> dcRow = c == 0 ? dcRow0 : c == 1 ? dcRow1 : dcRow2;
            JxlTransformsDecoder.LowestFrequenciesFromDc(kind, dcRow[sbx[c]..], dcStride, block[(c * size)..], scratch);
        }
    }

    /// <summary>
    /// Reconstructs one group's coefficients into the borrowed group planes or a JPEG coefficient image.
    /// </summary>
    /// <typeparam name="TBlockLoader">The coefficient source selected before block traversal.</typeparam>
    /// <param name="frameHeader">The parsed frame settings.</param>
    /// <param name="getBlock">The coefficient source, advanced in transform order.</param>
    /// <param name="groupDecCache">The reusable group sample and transform workspace.</param>
    /// <param name="decState">The frame's prepared quantization and optional accumulation storage.</param>
    /// <param name="groupIdx">The group index in the frame.</param>
    /// <param name="acType">The integer width selected by entropy metadata or the encoder's coefficient storage.</param>
    /// <param name="jpegData">The JPEG coefficient destination, or null for pixel reconstruction.</param>
    /// <param name="draw">Whether to reconstruct pixels after decoding the requested passes.</param>
    /// <returns>Whether all coefficient blocks are valid.</returns>
    private static bool DecodeGroupCore<TBlockLoader>(
        JxlFrameHeader frameHeader,
        ref TBlockLoader getBlock,
        JxlGroupDecoderCache groupDecCache,
        JxlPassesDecoderContext decState,
        int groupIdx,
        JxlDctAcType acType,
        JpegData? jpegData,
        JxlDrawMode draw)
        where TBlockLoader : IJxlGetBlock, allows ref struct
    {
        Rectangle blockRect = decState.Shared.FrameDimensions.BlockGroupRect(groupIdx);
        JxlAcStrategyImage acStrategy = decState.Shared.AcStrategy;

        int xsizeBlocks = blockRect.Width;
        int ysizeBlocks = blockRect.Height;

        int dcStride = decState.Shared.Dc.PixelsPerRow;
        float invGlobalScale = decState.Shared.Quantizer.InverseGlobalScale;

        JxlYCbCrChromaSubsampling cs = frameHeader.ChromaSubsampling;

        Vector<int> jpegDctMin = new(-4095);
        Vector<int> jpegDctMax = new(4095);

        // Borrow each allocation once. Row and block offsets below use these spans and the owner's physical strides.
        // Group planes start at the group's origin; cross-group filter borders belong to frame reconstruction.
        ReadOnlySpan<JxlPlane<float>> channels = groupDecCache.Channels;
        Span<float> idct0 = channels[0].GetRow(0);
        Span<float> idct1 = channels[1].GetRow(0);
        Span<float> idct2 = channels[2].GetRow(0);
        InlineArray3<int> idctStride = default;
        idctStride[0] = channels[0].PixelsPerRow;
        idctStride[1] = channels[1].PixelsPerRow;
        idctStride[2] = channels[2].PixelsPerRow;

        Span<float> decodedBlock = groupDecCache.DecodeGroupBlockScratchSpace.Span;
        Span<float> scratch = groupDecCache.ScratchSpaceMemory.Span;
        Span<short> quantized16 = groupDecCache.DecodeGroupQBlock16ScratchSpace.Span;
        Span<int> quantized32 = groupDecCache.DecodeGroupQBlockScratchSpace.Span;
        ReadOnlySpan<float> matrixStorage = decState.Shared.Matrices.GetPreparedStorage();
        InlineArray4<float> biases = decState.Shared.CodecMetadata.CustomTransformData.OpsinInverseMatrix.QuantBiases;

        IJxlDctAcImage? coefficients = decState.Coefficients;
        bool accumulate = coefficients is not null;

        // Progressive storage is optional, but the entropy metadata always selects a coefficient width.
        // Borrow each retained group row before traversal so block selection does not reacquire Memory.Span.
        JxlDctAcPointer accumulated0 = coefficients is null ? default : coefficients.GetPlaneRow(0, groupIdx, 0);
        JxlDctAcPointer accumulated1 = coefficients is null ? default : coefficients.GetPlaneRow(1, groupIdx, 0);
        JxlDctAcPointer accumulated2 = coefficients is null ? default : coefficients.GetPlaneRow(2, groupIdx, 0);

        Span<int> scaledQTable = stackalloc int[64 * 3];

        int offset = 0;

        ReadOnlySpan<int> quantization = decState.Shared.RawQuantField.GetRow(0);
        int quantizationStride = decState.Shared.RawQuantField.PixelsPerRow;
        ReadOnlySpan<byte> strategies = acStrategy.GetRows();
        Span<float> dc0 = decState.Shared.Dc.PlaneRow(0, 0);
        Span<float> dc1 = decState.Shared.Dc.PlaneRow(1, 0);
        Span<float> dc2 = decState.Shared.Dc.PlaneRow(2, 0);
        ReadOnlySpan<sbyte> correlation0 = decState.Shared.ColorMap.YToXMap.GetRow(0);
        ReadOnlySpan<sbyte> correlation2 = decState.Shared.ColorMap.YToBMap.GetRow(0);
        int correlationStride = decState.Shared.ColorMap.YToXMap.PixelsPerRow;

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

            if (qe[0].QuantizationTable.Length != 3 * 8 * 8)
            {
                return false;
            }

            ReadOnlySpan<int> qTable = qe[0].QuantizationTable.Span;

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
            cs.HShift(0),
            cs.HShift(1),
            cs.HShift(2)
        ];

        Span<int> vShift =
        [
            cs.VShift(0),
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

            ReadOnlySpan<int> rowQuant = quantization[(((blockRect.Top + by) * quantizationStride) + blockRect.Left)..];

            Span<float> dcRow0 = dc0[(((r[0].Top + sby[0]) * dcStride) + r[0].Left)..];
            Span<float> dcRow1 = dc1[(((r[1].Top + sby[1]) * dcStride) + r[1].Left)..];
            Span<float> dcRow2 = dc2[(((r[2].Top + sby[2]) * dcStride) + r[2].Left)..];

            int ty = (blockRect.Y0() + by) / JxlChromaFromLuma.ColorTileDimensionInBlocks;

            JxlAcStrategyRow acsRow = new(strategies[(((blockRect.Top + by) * acStrategy.PixelsPerRow) + blockRect.Left)..]);

            ReadOnlySpan<sbyte> rowCMap0 = correlation0[(ty * correlationStride)..];
            ReadOnlySpan<sbyte> rowCMap2 = correlation2[(ty * correlationStride)..];

            Span<float> idctRow0 = idct0[(sby[0] * JxlFrameDimensions.BlockDimensions * idctStride[0])..];
            Span<float> idctRow1 = idct1[(sby[1] * JxlFrameDimensions.BlockDimensions * idctStride[1])..];
            Span<float> idctRow2 = idct2[(sby[2] * JxlFrameDimensions.BlockDimensions * idctStride[2])..];

            Span<int> jpegRow0 = default;
            Span<int> jpegRow1 = default;
            Span<int> jpegRow2 = default;

            for (int c = 0; c < 3; c++)
            {
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

                float xCcMul = colorCorrelation.YToXRatio(rowCMap0[absTx]);
                float bCcMul = colorCorrelation.YToBRatio(rowCMap2[absTx]);

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
                        if (acType == JxlDctAcType.Ac16)
                        {
                            qblock0 = new() { Pointer16 = accumulated0.Pointer16.Slice(offset, size) };
                            qblock1 = new() { Pointer16 = accumulated1.Pointer16.Slice(offset, size) };
                            qblock2 = new() { Pointer16 = accumulated2.Pointer16.Slice(offset, size) };
                        }
                        else
                        {
                            qblock0 = new() { Pointer32 = accumulated0.Pointer32.Slice(offset, size) };
                            qblock1 = new() { Pointer32 = accumulated1.Pointer32.Slice(offset, size) };
                            qblock2 = new() { Pointer32 = accumulated2.Pointer32.Slice(offset, size) };
                        }
                    }
                    else
                    {
                        if (draw != JxlDrawMode.Draw)
                        {
                            return false;
                        }

                        if (acType == JxlDctAcType.Ac16)
                        {
                            quantized16[..(3 * size)].Clear();

                            qblock0 = new() { Pointer16 = quantized16.Slice(0, size) };
                            qblock1 = new() { Pointer16 = quantized16.Slice(size, size) };
                            qblock2 = new() { Pointer16 = quantized16.Slice(size * 2, size) };
                        }
                        else
                        {
                            quantized32[..(3 * size)].Clear();

                            qblock0 = new() { Pointer32 = quantized32.Slice(0, size) };
                            qblock1 = new() { Pointer32 = quantized32.Slice(size, size) };
                            qblock2 = new() { Pointer32 = quantized32.Slice(size * 2, size) };
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

                    if (jpegData is null)
                    {
                        // Pixel reconstruction uses every supported transform. The separate JPEG path accepts only DCT8.
                        // Dequantization restores color correlation and the independently coded DC frequencies before IDCT.
                        DequantBlock(
                            acType,
                            invGlobalScale,
                            rowQuant[bx],
                            decState.XDmMultiplier,
                            decState.BDmMultiplier,
                            xCcMul,
                            bCcMul,
                            acs.Strategy,
                            size,
                            decState.Shared.Quantizer,
                            matrixStorage,
                            coveredBlocks,
                            sbx,
                            dcRow0,
                            dcRow1,
                            dcRow2,
                            dcStride,
                            biases,
                            qblock0,
                            qblock1,
                            qblock2,
                            decodedBlock,
                            scratch);

                        // Subsampled channels contribute only at their own block origins. Each plane has its own stride.
                        foreach (int c in (ReadOnlySpan<int>)[1, 0, 2])
                        {
                            if ((sbx[c] << hShift[c]) != bx || (sby[c] << vShift[c]) != by)
                            {
                                continue;
                            }

                            Span<float> pixels = c == 0 ? idctRow0 : c == 1 ? idctRow1 : idctRow2;
                            JxlTransformsDecoder.TransformToPixels(
                                acs.Strategy,
                                decodedBlock.Slice(c * size, size),
                                pixels[(sbx[c] * JxlFrameDimensions.BlockDimensions)..],
                                idctStride[c],
                                scratch);
                        }

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
    /// <param name="jpegData">The JPEG reconstruction destination, when requested.</param>
    /// <param name="firstPass">The first frame pass to decode.</param>
    /// <param name="forceDraw">Whether to draw before the last pass.</param>
    /// <param name="dcOnly">Whether this request contains only DC data.</param>
    /// <param name="groupReady">Whether successful decoding completed the requested group draw.</param>
    /// <returns>Whether coefficient decoding and entropy-state checks succeeded.</returns>
    public static bool DecodeGroup(
        Configuration configuration,
        JxlFrameHeader frameHeader,
        JxlBitReader[] readers,
        int numPasses,
        int groupIndex,
        JxlPassesDecoderContext decState,
        JxlGroupDecoderCache groupDecCache,
        JpegData? jpegData,
        int firstPass,
        bool forceDraw,
        bool dcOnly,
        out bool groupReady)
    {
        bool draw = numPasses + firstPass == frameHeader.Passes.NumberOfPasses || forceDraw;

        groupReady = false;

        if (draw && numPasses == 0 && firstPass == 0)
        {
            DrawDcGroup(frameHeader, groupIndex, decState, groupDecCache);
            groupReady = true;
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

        // A ref-struct loader borrows frame spans and mutates its row state. Keep one local through traversal,
        // then release its entropy histories on every exit, including failed initialization and truncated input.
        JxlBitStreamBlockLoader getBlock = new(frameHeader, decState, groupDecCache, readers, firstPass, numPasses, blockGroupRect);
        try
        {
            if (!getBlock.Init(configuration, histogramSelectorBits, decState.Shared.NumHistograms))
            {
                return false;
            }

            if (!DecodeGroupCore(
                frameHeader,
                ref getBlock,
                groupDecCache,
                decState,
                groupIndex,
                decState.CoefficientType,
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

            groupReady = draw;
            return true;
        }
        finally
        {
            getBlock.Dispose();
        }
    }

    /// <summary>
    /// Reconstructs one group's lowest-frequency samples with the codestream's eightfold upsampling kernels.
    /// </summary>
    /// <param name="frameHeader">The parsed chroma sampling settings.</param>
    /// <param name="groupIndex">The group index in the frame.</param>
    /// <param name="decoder">The prepared decoder and borrowed DC samples.</param>
    /// <param name="cache">The reusable group destinations and padded DC workspace.</param>
    private static void DrawDcGroup(JxlFrameHeader frameHeader, int groupIndex, JxlPassesDecoderContext decoder, JxlGroupDecoderCache cache)
    {
        cache.InitializeDcBufferOnce();
        Span<float> padded = cache.DcBuffer.GetRow(0);
        int paddedStride = cache.DcBuffer.PixelsPerRow;
        int border = JxlGroupDecoderCache.DcPadding;
        Rectangle group = decoder.Shared.FrameDimensions.BlockGroupRect(groupIndex);
        JxlUpsampler upsampler = decoder.GetUpsampler(JxlFrameDimensions.BlockDimensions);

        for (int channel = 0; channel < 3; channel++)
        {
            int horizontalShift = frameHeader.ChromaSubsampling.HShift(channel);
            int verticalShift = frameHeader.ChromaSubsampling.VShift(channel);
            Rectangle region = new(group.Left >> horizontalShift, group.Top >> verticalShift, group.Width >> horizontalShift, group.Height >> verticalShift);
            JxlPlane<float> dc = decoder.Shared.Dc.Plane(channel);
            ReadOnlySpan<float> source = dc.GetRow(0);
            int sourceStride = dc.PixelsPerRow;
            int inputLeft = region.Left - border;
            int copyLeft = Math.Max(inputLeft, 0);
            int copyRight = Math.Min(region.Right + border, dc.XSize);
            int destinationLeft = copyLeft - inputLeft;
            int copyLength = copyRight - copyLeft;
            int paddedWidth = region.Width + (2 * border);

            // Real neighboring DC samples cross group boundaries. Mirror only at the frame's outer edges.
            // Copy the contiguous interior in bulk; at most two samples on either side need individual edge lookup.
            for (int y = 0; y < region.Height + (2 * border); y++)
            {
                int sourceY = JxlImageOperations.Mirror(region.Top + y - border, dc.YSize);
                ReadOnlySpan<float> row = source.Slice(sourceY * sourceStride, dc.XSize);
                Span<float> destination = padded.Slice(y * paddedStride, paddedWidth);
                row.Slice(copyLeft, copyLength).CopyTo(destination[destinationLeft..]);

                for (int x = 0; x < destinationLeft; x++)
                {
                    destination[x] = row[JxlImageOperations.Mirror(inputLeft + x, dc.XSize)];
                }

                for (int x = destinationLeft + copyLength; x < paddedWidth; x++)
                {
                    destination[x] = row[JxlImageOperations.Mirror(inputLeft + x, dc.XSize)];
                }
            }

            // The logical DC origin is (2,2). The upsampler consumes that explicit halo and writes only this group's samples.
            JxlPlane<float> output = cache.Channels[channel];
            upsampler.Upsample(padded, paddedStride, output.GetRow(0), output.PixelsPerRow, region.Width, region.Height);
        }
    }

    /// <summary>
    /// Reconstructs encoder coefficients through the same dequantization and inverse transforms as decoded coefficients.
    /// </summary>
    /// <param name="frameHeader">The frame settings used to prepare the coefficients.</param>
    /// <param name="ac">The full-width coefficient images for the progressive passes.</param>
    /// <param name="groupIndex">The group index in each coefficient image.</param>
    /// <param name="decState">The decoder workspace that borrows the encoder's prepared common controls.</param>
    /// <param name="groupDecCache">The reusable group destinations and transform scratch.</param>
    /// <returns>Whether the group coefficients were reconstructed successfully.</returns>
    public static bool DecodeGroupForRoundtrip(
        JxlFrameHeader frameHeader,
        IReadOnlyList<IJxlDctAcImage> ac,
        int groupIndex,
        JxlPassesDecoderContext decState,
        JxlGroupDecoderCache groupDecCache)
    {
        JxlEncoderBlockLoader getBlock = JxlEncoderBlockLoader.Create(ac, groupIndex, frameHeader.Passes.Shift);
        groupDecCache.InitializeOnce(0, (1 << JxlAcStrategy.NumberOfValidStrategies) - 1);

        return DecodeGroupCore(
            frameHeader,
            ref getBlock,
            groupDecCache,
            decState,
            groupIndex,
            JxlDctAcType.Ac32,
            null,
            JxlDrawMode.Draw);
    }
}
