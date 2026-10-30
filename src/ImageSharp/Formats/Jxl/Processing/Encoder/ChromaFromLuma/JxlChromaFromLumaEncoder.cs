// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.ChromaFromLuma;

internal static class JxlChromaFromLumaEncoder
{
    public static JxlPlane<float> InitializeDcStorage(Configuration configuration, int numBlocks)
    {
        JxlPlane<float> dcValues = new(configuration, JxlMath.RoundUpTo(numBlocks, Vector<float>.Count), 4);

        for (int y = 0; y < 4; y++)
        {
            Span<float> row = dcValues.GetRow(y);

            for (int x = dcValues.XSize - Vector<float>.Count; x < dcValues.XSize; x++)
            {
                row[x] = 0;
            }
        }

        return dcValues;
    }

    /// <summary>
    /// Selects the signed luma-to-chroma multiplier adjustment from observed coefficient pairs.
    /// </summary>
    /// <param name="valuesM">The luma-derived multipliers for the observations.</param>
    /// <param name="valuesS">The chroma values for the observations.</param>
    /// <param name="num">The number of observations, excluding storage padding.</param>
    /// <param name="baseValue">The base luma-to-chroma multiplier before the encoded adjustment.</param>
    /// <param name="distanceMul">The regularization strength for the encoded adjustment.</param>
    /// <param name="fast">Whether to use the quadratic estimate instead of the iterative loss.</param>
    /// <returns>The adjustment in [-128,127]. Midpoints round away from zero.</returns>
    public static int FindBestMultiplier(Span<float> valuesM, Span<float> valuesS, int num, float baseValue, float distanceMul, bool fast)
    {
        if (num == 0)
        {
            return 0;
        }

        JxlCflFunction function = new(valuesM, valuesS, num, baseValue, distanceMul);
        float x;

        if (fast)
        {
            x = function.EstimateMinimum();
        }
        else
        {
            const float eps = 100.0f;
            const float clamp = 20.0f;

            x = 0;

            for (int i = 0; i < 20; i++)
            {
                float dFPeps = 0;
                float dFMeps = 0;
                float dF = function.Compute(x, eps, ref dFPeps, ref dFMeps);

                float ddF = (dFPeps - dFMeps) / (2.0f * eps);
                const float experimentalInsignificantStabilizer = 0.85f;

                float step = dF / (ddF + experimentalInsignificantStabilizer);

                x -= MathF.Min(clamp, MathF.Max(-clamp, step));

                if (MathF.Abs(step) < 3e-3f)
                {
                    break;
                }
            }
        }

        const float towardsZero = 2.6f;

        if (x >= towardsZero)
        {
            x -= towardsZero;
        }
        else if (x <= -towardsZero)
        {
            x += towardsZero;
        }
        else
        {
            x = 0;
        }

        // The encoded adjustment uses native std::round midpoint rules. Ties-to-even would choose a different correlation table entry.
        return (int)Math.Clamp(MathF.Round(x, MidpointRounding.AwayFromZero), -128.0f, 127.0f);
    }

    /// <summary>
    /// Computes the X and B correlation multipliers for one tile and records its frame-wide DC pairs.
    /// </summary>
    /// <param name="opsin">The borrowed XYB source planes.</param>
    /// <param name="opsinRect">The block-aligned source region in pixels.</param>
    /// <param name="dequant">The prepared dequantization matrices.</param>
    /// <param name="acStrategy">The selected strategies, unused when <paramref name="useDct8"/> is true.</param>
    /// <param name="rawQuantField">The quantization field, or null to use a unit field.</param>
    /// <param name="quantizer">The frame's quantization controls.</param>
    /// <param name="rect">The tile in block coordinates relative to <paramref name="opsinRect"/>, at most eight by eight blocks.</param>
    /// <param name="fast">Whether to use the existing fast correlation estimate.</param>
    /// <param name="useDct8">Whether to use DCT8 rather than reading selected strategies.</param>
    /// <param name="mapX">The destination X-correlation map.</param>
    /// <param name="mapB">The destination B-correlation map.</param>
    /// <param name="dcValues">Four owned-by-caller DC rows, with vector-rounded frame block count as their logical width.</param>
    /// <param name="mem">The borrowed float workspace, partitioned into transform blocks, tile coefficients, DC values and transform scratch.</param>
    public static void ComputeTile(
        JxlImage3<float> opsin,
        Rectangle opsinRect,
        JxlDequantMatrices dequant,
        JxlAcStrategyImage? acStrategy,
        JxlPlane<int>? rawQuantField,
        JxlQuantizer quantizer,
        Rectangle rect,
        bool fast,
        bool useDct8,
        JxlPlane<sbyte> mapX,
        JxlPlane<sbyte> mapB,
        JxlPlane<float> dcValues,
        Span<float> mem)
    {
        ReadOnlySpan<float> matrixStorage = dequant.GetPreparedStorage();
        int widthBlocks = opsinRect.Width / JxlFrameDimensions.BlockDimensions;
        const float distanceMultiplierAC = 1e-9f;
        int dctScratchSize = JxlDct.MaximumWorkingSetLength;

        int y0 = rect.Y0();
        int x0 = rect.X0();
        int x1 = rect.X0() + rect.Width;
        int y1 = rect.Y0() + rect.Height;

        int ty = y0 / JxlChromaFromLuma.ColorTileDimensionInBlocks;
        int tx = x0 / JxlChromaFromLuma.ColorTileDimensionInBlocks;

        Span<sbyte> rowOutX = mapX.GetRow(ty);
        Span<sbyte> rowOutB = mapB.GetRow(ty);

        Span<float> dcValuesYX = dcValues.GetRow(0);
        Span<float> dcValuesX = dcValues.GetRow(1);
        Span<float> dcValuesYB = dcValues.GetRow(2);
        Span<float> dcValuesB = dcValues.GetRow(3);

        // Maximum transform blocks hold 256 by 256 samples, including candidates too large for this color tile.
        // The kernel records their DC values but uses only transforms that fit the tile for its AC correlation estimate.
        Span<float> blockY = mem;
        Span<float> blockX = blockY[JxlAcStrategy.MaximumCoefficientArea..];
        Span<float> blockB = blockX[JxlAcStrategy.MaximumCoefficientArea..];

        mem = mem[(3 * JxlAcStrategy.MaximumCoefficientArea)..];

        Span<float> coeffsYX = mem;
        Span<float> coeffsX = coeffsYX[(JxlChromaFromLuma.ColorTileDimension * JxlChromaFromLuma.ColorTileDimension)..];
        Span<float> coeffsYB = coeffsX[(JxlChromaFromLuma.ColorTileDimension * JxlChromaFromLuma.ColorTileDimension)..];
        Span<float> coeffsB = coeffsYB[(JxlChromaFromLuma.ColorTileDimension * JxlChromaFromLuma.ColorTileDimension)..];

        // The four coefficient arrays hold one color tile each, not one maximum-size transform each.
        // Advance by exactly those tile arrays so the following DC arrays and transform scratch match the owner's allocation.
        mem = mem[(4 * JxlChromaFromLuma.ColorTileDimension * JxlChromaFromLuma.ColorTileDimension)..];

        const int dcSize = JxlAcStrategy.MaximumCoefficientBlocks * JxlAcStrategy.MaximumCoefficientBlocks;

        Span<float> dcY = mem;
        Span<float> dcX = dcY[dcSize..];
        Span<float> dcB = dcX[dcSize..];

        mem = mem[(3 * dcSize)..];

        Span<float> scratchSpace = mem;

        if (mem.Length != (2 * JxlAcStrategy.MaximumCoefficientArea) + dctScratchSize)
        {
            throw new InvalidOperationException("Invalid memory length");
        }

        int numAC = 0;

        for (int y = y0; y < y1; y++)
        {
            Span<float> rowY = opsin.PlaneRow(opsinRect, 1, y * JxlFrameDimensions.BlockDimensions);
            Span<float> rowX = opsin.PlaneRow(opsinRect, 0, y * JxlFrameDimensions.BlockDimensions);
            Span<float> rowB = opsin.PlaneRow(opsinRect, 2, y * JxlFrameDimensions.BlockDimensions);
            int stride = opsin.PixelsPerRow;

            for (int x = x0; x < x1; x++)
            {
                JxlAcStrategy acs = useDct8
                    ? new(JxlAcStrategyType.DCT)
                    : acStrategy!.GetRow(y)[x]; // useDct8 = acStrategy == null, so acStrategy isn't null here

                if (!acs.IsFirstBlock)
                {
                    continue;
                }

                int xs = acs.CoveredBlocksX;

                // Y
                JxlTransformsEncoder.TransformFromPixels(acs.Strategy, rowY[(x * JxlFrameDimensions.BlockDimensions)..], stride, blockY, scratchSpace);
                JxlTransformsEncoder.DcFromLowestFrequencies(acs.Strategy, blockY, dcY, xs, scratchSpace);

                // X
                JxlTransformsEncoder.TransformFromPixels(acs.Strategy, rowX[(x * JxlFrameDimensions.BlockDimensions)..], stride, blockX, scratchSpace);
                JxlTransformsEncoder.DcFromLowestFrequencies(acs.Strategy, blockX, dcX, xs, scratchSpace);

                // B
                JxlTransformsEncoder.TransformFromPixels(acs.Strategy, rowB[(x * JxlFrameDimensions.BlockDimensions)..], stride, blockB, scratchSpace);
                JxlTransformsEncoder.DcFromLowestFrequencies(acs.Strategy, blockB, dcB, xs, scratchSpace);

                ReadOnlySpan<float> qmX = dequant.GetInverseMatrix(matrixStorage, acs.Strategy, 0);
                ReadOnlySpan<float> qmB = dequant.GetInverseMatrix(matrixStorage, acs.Strategy, 2);
                float qDcX = useDct8 ? 1 : 1.0f / quantizer.GetInverseDcStep(0);
                float qDcB = useDct8 ? 1 : 1.0f / quantizer.GetInverseDcStep(2);

                for (int iy = 0; iy < acs.CoveredBlocksY; iy++)
                {
                    for (int ix = 0; ix < xs; ix++)
                    {
                        dcValuesYX[((iy + y) * widthBlocks) + ix + x] = dcY[(iy * xs) + ix] * qDcX;
                        dcValuesX[((iy + y) * widthBlocks) + ix + x] = dcX[(iy * xs) + ix] * qDcX;
                        dcValuesYB[((iy + y) * widthBlocks) + ix + x] = dcY[(iy * xs) + ix] * qDcB;
                        dcValuesB[((iy + y) * widthBlocks) + ix + x] = dcB[(iy * xs) + ix] * qDcB;
                    }
                }

                if (acs.CoveredBlocksX + x0 > x1 || acs.CoveredBlocksY + y0 > y1)
                {
                    continue;
                }

                int cx = acs.CoveredBlocksX;
                int cy = acs.CoveredBlocksY;

                JxlForwardCoefficientOrder.CoefficientLayout(ref cy, ref cx);

                for (int iy = 0; iy < cy; iy++)
                {
                    for (int ix = 0; ix < cx; ix++)
                    {
                        blockY[(cx * JxlFrameDimensions.BlockDimensions * iy) + ix] = 0;
                        blockX[(cx * JxlFrameDimensions.BlockDimensions * iy) + ix] = 0;
                        blockB[(cx * JxlFrameDimensions.BlockDimensions * iy) + ix] = 0;
                    }
                }

                float qq = rawQuantField is null ? 1.0f : rawQuantField.GetRow(y)[x];
                float strangeMultiplier = 128f;

                float q = useDct8 ? 1 : quantizer.Scale * strangeMultiplier * qq;

                Vector<float> qv = Vector.Create(q);
                for (int i = 0; i <= (cx * cy * 64) - Vector<float>.Count; i += Vector<float>.Count)
                {
                    Vector<float> bY = Vector.Create<float>(blockY[i..]);
                    Vector<float> bX = Vector.Create<float>(blockX[i..]);
                    Vector<float> bB = Vector.Create<float>(blockB[i..]);
                    Vector<float> qqmX = qv * Vector.Create<float>(qmX[i..]);
                    Vector<float> qqmB = qv * Vector.Create<float>(qmB[i..]);

                    (bY * qqmX).CopyTo(coeffsYX[numAC..]);
                    (bX * qqmX).CopyTo(coeffsX[numAC..]);
                    (bY * qqmB).CopyTo(coeffsYB[numAC..]);
                    (bB * qqmB).CopyTo(coeffsB[numAC..]);

                    numAC += Vector<float>.Count;
                }
            }
        }

        if (numAC % Vector<float>.Count != 0)
        {
            throw new InvalidOperationException("Number of AC wasn't aligned");
        }

        rowOutX[tx] = (sbyte)FindBestMultiplier(coeffsYX, coeffsX, numAC, 0f, distanceMultiplierAC, fast);
        rowOutB[tx] = (sbyte)FindBestMultiplier(coeffsYB, coeffsB, numAC, JxlOpsinConstants.YToBRatio, distanceMultiplierAC, fast);
    }

    public static bool ColorCorrelationEncodeDc(JxlColorCorrelation colorCorrelation, JxlBitWriter writer, JxlLayerType layer, JxlAuxiliaryOutput auxOut)
    {
        float colorFactor = colorCorrelation.ColorFactor;
        float baseCorrelationX = colorCorrelation.BaseCorrelationX;
        float baseCorrelationB = colorCorrelation.BaseCorrelationB;
        int yToXDc = colorCorrelation.YToXDc;
        int yToBDc = colorCorrelation.YToBDc;

        return writer.WithMaxBits(1 + (2 * JxlMath.BitsPerByte) + 12 + 32, layer, auxOut, () =>
        {
            if (yToXDc == 0 && yToBDc == 0 &&
                colorFactor == JxlChromaFromLuma.DefaultColorFactor && baseCorrelationX == 0.0f &&
                baseCorrelationB == JxlOpsinConstants.YToBRatio)
            {
                writer.Write(1, 1);
                return true;
            }

            writer.Write(1, 0);
            if (!JxlU32Coder.Write(JxlChromaFromLuma.ColorFactorDistribution, (uint)colorFactor, writer))
            {
                return false;
            }

            if (!JxlF16Coder.Write(baseCorrelationX, writer))
            {
                return false;
            }

            if (!JxlF16Coder.Write(baseCorrelationB, writer))
            {
                return false;
            }

            writer.Write(JxlMath.BitsPerByte, yToXDc - sbyte.MinValue);
            writer.Write(JxlMath.BitsPerByte, yToBDc - sbyte.MinValue);

            return true;
        });
    }
}
