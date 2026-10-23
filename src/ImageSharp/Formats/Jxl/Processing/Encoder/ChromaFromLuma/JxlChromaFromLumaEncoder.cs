// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.ChromaFromLuma;

internal static class JxlChromaFromLumaEncoder
{
    public static JxlImageF InitializeDcStorage(Configuration configuration, int numBlocks)
    {
        JxlImageF dcValues = new(configuration, JxlMath.RoundUpTo(numBlocks, Vector<float>.Count), 4);

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

    public static int FindBestMultiplier(Span<float> valuesM, Span<float> valuesS, int num, float baseValue, float distanceMul, bool fast)
    {
        if (num == 0)
        {
            return 0;
        }

        float x;

        if (fast)
        {
            const float inverseColorFactor = 1.0f / JxlChromaFromLuma.DefaultColorFactor;

            Vector<float> ca = Vector<float>.Zero;
            Vector<float> cb = Vector<float>.Zero;
            Vector<float> invColorFactor = new(inverseColorFactor);
            Vector<float> baseV = new(baseValue);

            int i = 0;
            int lanes = Vector<float>.Count;

            for (; i + lanes <= num; i += lanes)
            {
                Vector<float> m = new(valuesM[i..]);
                Vector<float> s = new(valuesS[i..]);

                Vector<float> a = invColorFactor * m;
                Vector<float> b = (baseV * m) - s;

                ca += a * a;
                cb += a * b;
            }

            float sumCa = Vector.Sum(ca);
            float sumCb = Vector.Sum(cb);

            for (; i < num; i++)
            {
                float m = valuesM[i];
                float s = valuesS[i];
                float a = inverseColorFactor * m;
                float b = (baseValue * m) - s;

                sumCa += a * a;
                sumCb += a * b;
            }

            x = -sumCb / (sumCa + (num * distanceMul * 0.5f));
        }
        else
        {
            const float eps = 100.0f;
            const float clamp = 20.0f;

            JxlCflFunction fn = new(valuesM, valuesS, num, baseValue, distanceMul);
            x = 0;

            for (int i = 0; i < 20; i++)
            {
                float dFPeps = 0;
                float dFMeps = 0;
                float dF = fn.Compute(x, eps, ref dFPeps, ref dFMeps);

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

        return (int)Math.Clamp(MathF.Round(x), -128.0f, 127.0f);
    }

    public static void ComputeTile(JxlImage3F opsin, Rectangle opsinRect, JxlDequantMatrices dequant, JxlAcStrategyImage? acStrategy, JxlImageI? rawQuantField, JxlQuantizer quantizer, Rectangle rect, bool fast, bool useDct8, JxlImageSB mapX, JxlImageSB mapB, JxlImageF dcValues, Span<float> mem)
    {
        int widthBlocks = opsinRect.Width / JxlFrameDimensions.BlockDimensions;
        const float distanceMultiplierAC = 1e-9f;
        int dctScratchSize = 3 * (JxlSimdUtils.MaxVectorSize / sizeof(float)) * JxlAcStrategy.MaximumBlockDimension;

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

        Span<float> blockY = mem;
        Span<float> blockX = blockY[JxlAcStrategy.MaximumCoefficientArea..];
        Span<float> blockB = blockX[JxlAcStrategy.MaximumCoefficientArea..];

        mem = mem[(3 * JxlAcStrategy.MaximumCoefficientArea)..];

        Span<float> coeffsYX = mem;
        Span<float> coeffsX = coeffsYX[(JxlChromaFromLuma.ColorTileDimension * JxlChromaFromLuma.ColorTileDimension)..];
        Span<float> coeffsYB = coeffsX[(JxlChromaFromLuma.ColorTileDimension * JxlChromaFromLuma.ColorTileDimension)..];
        Span<float> coeffsB = coeffsYB[(JxlChromaFromLuma.ColorTileDimension * JxlChromaFromLuma.ColorTileDimension)..];

        mem = mem[(4 * JxlAcStrategy.MaximumCoefficientArea)..];

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

                Span<float> qmX = dequant.GetInverseMatrix(acs.Strategy, 0);
                Span<float> qmB = dequant.GetInverseMatrix(acs.Strategy, 2);
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
