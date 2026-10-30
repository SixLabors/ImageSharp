// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal static class JxlGroupEncoder
{
    private const float QuantNormalizer = 2.2942708343284721f;

    private static ReadOnlySpan<double> Limit => [0.46, 0.46, 0.46, 0.46];

    private static ReadOnlySpan<float> Multipliers => [0.9999f, 0.9999f, 0.9999f, 0.9999f];

    private static readonly double[,] Mul1Data =
    {
        {
            0.22080615753848404,
            0.45797479824262011,
            0.29859235095977965,
        },
        {
            0.70109486510286834,
            0.16185281305512639,
            0.14387691730035473,
        },
        {
            0.114985964456218638,
            0.44656840441027695,
            0.10587658215149048,
        },
        {
            0.46849665264409396,
            0.41239077937781954,
            0.088667407767185444,
        },
    };

    private static readonly double[,] Mul2Data =
    {
        {
            0.27450281941822197,
            1.1255766549984996,
            0.98950459134128388,
        },
        {
            0.4652168675598285,
            0.40945807983455818,
            0.36581899811751367,
        },
        {
            0.28034972424715715,
            0.9182653201929738,
            1.5581531543057416,
        },
        {
            0.26873118114033728,
            0.68863712390392484,
            1.2082185408666786,
        },
    };

    private static readonly DenseMatrix<double> Mul1 = new(Mul1Data);
    private static readonly DenseMatrix<double> Mul2 = new(Mul2Data);

    /// <summary>
    /// Quantizes one transform's AC plane using its four frequency-quadrant thresholds.
    /// </summary>
    /// <param name="quantizer">The prepared frame quantizer and inverse matrices.</param>
    /// <param name="matrixStorage">The matrix storage borrowed before the group block loops.</param>
    /// <param name="channelIndex">The XYB channel index.</param>
    /// <param name="matrixMultiplier">The channel's inverse matrix multiplier.</param>
    /// <param name="strategy">The transform strategy.</param>
    /// <param name="blocksWide">The canonical coefficient width, in 8-sample blocks.</param>
    /// <param name="blocksHigh">The canonical coefficient height, in 8-sample blocks.</param>
    /// <param name="thresholds">The four quadrant thresholds, adjusted in place for larger chroma blocks.</param>
    /// <param name="coefficients">The float coefficient plane.</param>
    /// <param name="quant">The positive block quantizer.</param>
    /// <param name="destination">The caller-owned integer coefficient plane.</param>
    public static void QuantizeBlockAc(
        JxlQuantizer quantizer,
        ReadOnlySpan<float> matrixStorage,
        int channelIndex,
        float matrixMultiplier,
        JxlAcStrategyType strategy,
        int blocksWide,
        int blocksHigh,
        Span<float> thresholds,
        Span<float> coefficients,
        int quant,
        Span<int> destination)
    {
        ReadOnlySpan<float> inverseMatrix = quantizer.InverseDequantMatrix(matrixStorage, strategy, channelIndex);
        float scale = (quantizer.Scale * quant) * matrixMultiplier;
        int blockCount = blocksWide * blocksHigh;

        if (channelIndex != 1 && blockCount >= 4)
        {
            // The native chroma heuristic uses covered area, not width squared. It never lowers a threshold below one half.
            float reduction = 0.00744F * blockCount;
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                thresholds[quadrant] = MathF.Max(thresholds[quadrant] - reduction, 0.5F);
            }
        }

        int rowLength = blocksWide * JxlFrameDimensions.BlockDimensions;
        int rowCount = blocksHigh * JxlFrameDimensions.BlockDimensions;
        for (int y = 0; y < rowCount; y++)
        {
            int offset = y * rowLength;
            int quadrant = y < rowCount / 2 ? 0 : 2;

            // Each half-row has one threshold. The quantizer keeps narrow rows in one register and traverses wider constant intervals.
            JxlQuantizer.Quantize(
                coefficients.Slice(offset, rowLength),
                inverseMatrix.Slice(offset, rowLength),
                scale,
                thresholds[quadrant],
                thresholds[quadrant + 1],
                destination.Slice(offset, rowLength));
        }
    }

    /// <summary>Refines the integer block quantizer against frequency-weighted coefficient error.</summary>
    /// <param name="quantizer">The prepared frame quantizer.</param>
    /// <param name="matrixStorage">The complete coefficient tables borrowed before the group loops.</param>
    /// <param name="c">The XYB channel index.</param>
    /// <param name="qmMultiplier">The channel's inverse matrix multiplier.</param>
    /// <param name="quantKind">The transform strategy.</param>
    /// <param name="width">The canonical coefficient width, in blocks.</param>
    /// <param name="height">The canonical coefficient height, in blocks.</param>
    /// <param name="thresholds">The four frequency-quadrant thresholds, adjusted in place.</param>
    /// <param name="blockIn">The source coefficient plane.</param>
    /// <param name="quant">The positive integer quantizer, updated with the selected value.</param>
    public static void AdjustQuantBlockAC(
        JxlQuantizer quantizer,
        ReadOnlySpan<float> matrixStorage,
        int c,
        float qmMultiplier,
        JxlAcStrategyType quantKind,
        int width,
        int height,
        Span<float> thresholds,
        Span<float> blockIn,
        ref int quant)
    {
        const int partialBlockKinds =
            (1 << (int)JxlAcStrategyType.IDENTITY) |
            (1 << (int)JxlAcStrategyType.DCT2X2) |
            (1 << (int)JxlAcStrategyType.DCT4X4) |
            (1 << (int)JxlAcStrategyType.DCT4X8) |
            (1 << (int)JxlAcStrategyType.DCT8X4) |
            (1 << (int)JxlAcStrategyType.AFV0) |
            (1 << (int)JxlAcStrategyType.AFV1) |
            (1 << (int)JxlAcStrategyType.AFV2) |
            (1 << (int)JxlAcStrategyType.AFV3);

        if (((1 << (int)quantKind) & partialBlockKinds) != 0)
        {
            return;
        }

        ReadOnlySpan<float> qm = quantizer.InverseDequantMatrix(matrixStorage, quantKind, c);
        float qac = quantizer.Scale * quant;

        if (width > 1 || height > 1)
        {
            for (int i = 0; i < 4; ++i)
            {
                thresholds[i] -= Math.Clamp(0.003f * width * height, 0f, 0.08f);

                if (thresholds[i] < 0.54)
                {
                    thresholds[i] = 0.54f;
                }
            }
        }

        float sumOfHighestFreqRowAndColumn = 0;
        float sumOfError = 0;
        float sumOfVals = 0;

        InlineArray4<float> hfNonZeros = default;
        InlineArray4<float> hfMaxError = default;

        for (int y = 0; y < height * JxlFrameDimensions.BlockDimensions; y++)
        {
            for (int x = 0; x < width * JxlFrameDimensions.BlockDimensions; x++)
            {
                int pos = (y * JxlFrameDimensions.BlockDimensions * width) + x;
                if (x < width && y < height)
                {
                    continue;
                }

                int hfix = ((y >= height * JxlFrameDimensions.BlockDimensions / 2 ? 1 : 0) * 2) + (x >= width * JxlFrameDimensions.BlockDimensions / 2 ? 1 : 0);

                float val = blockIn[pos] * (qm[pos] * qac * qmMultiplier);
                float v = (MathF.Abs(val) < thresholds[hfix]) ? 0 : MathF.Round(val, MidpointRounding.ToEven);
                float error = MathF.Abs(val - v);
                sumOfError += error;

                sumOfVals += MathF.Abs(v);

                if (c == 1 && v == 0)
                {
                    if (hfMaxError[hfix] < error)
                    {
                        hfMaxError[hfix] = error;
                    }
                }

                if (v != 0.0f)
                {
                    hfNonZeros[hfix] += MathF.Abs(v);

                    bool inCorner = y >= 7 * height && x >= 7 * width;
                    bool onBorder = y == (height * JxlFrameDimensions.BlockDimensions) - 1 || x == (width * JxlFrameDimensions.BlockDimensions) - 1;
                    bool inLargerCorner = x >= 4 * width && y >= 4 * height;

                    if (inCorner || (onBorder && inLargerCorner))
                    {
                        sumOfHighestFreqRowAndColumn += MathF.Abs(val);
                    }
                }
            }
        }

        if (c == 1 && sumOfVals * 8 < width * height)
        {
            int originalQuant = quant;
            int newQuant = quant;

            for (int i = 1; i < 4; ++i)
            {
                if (hfNonZeros[i] == 0.0 && hfMaxError[i] > Limit[i])
                {
                    newQuant = originalQuant + 1;
                    break;
                }
            }

            quant = newQuant;

            if (hfNonZeros[3] == 0.0 && hfMaxError[3] > Limit[3])
            {
                thresholds[3] = Multipliers[3] * hfMaxError[3] * newQuant / originalQuant;
            }
            else if ((hfNonZeros[1] == 0.0 && hfMaxError[1] > Limit[1]) || (hfNonZeros[2] == 0.0 && hfMaxError[2] > Limit[2]))
            {
                thresholds[1] = Multipliers[1] * Math.Max(hfMaxError[1], hfMaxError[2]) * newQuant / originalQuant;
                thresholds[2] = thresholds[1];
            }
            else if (hfNonZeros[0] == 0.0 && hfMaxError[0] > Limit[0])
            {
                thresholds[0] = Multipliers[0] * hfMaxError[0] * newQuant / originalQuant;
            }
        }

        {
            float all = hfNonZeros[0] + hfNonZeros[1] + hfNonZeros[2] + hfNonZeros[3] + 1;
            ReadOnlySpan<float> multipliers = [70f, 30f, 60f];

            if (multipliers[c] * sumOfHighestFreqRowAndColumn >= all)
            {
                quant += (int)(multipliers[c] * sumOfHighestFreqRowAndColumn / all);

                if (quant >= JxlQuantizer.MaxQuant)
                {
                    quant = JxlQuantizer.MaxQuant - 1;
                }
            }
        }

        if (quantKind == JxlAcStrategyType.DCT)
        {
            // If this 8x8 block is too flat, increase the adaptive quantization level
            // a bit to reduce visible block boundaries and requantize the block.
            if (hfNonZeros[3] + hfNonZeros[2] + hfNonZeros[1] + hfNonZeros[0] < 11)
            {
                quant++;

                if (quant >= JxlQuantizer.MaxQuant)
                {
                    quant = JxlQuantizer.MaxQuant - 1;
                }
            }
        }

        {
            sumOfError *= QuantNormalizer;
            sumOfVals *= QuantNormalizer;

            if (quantKind >= JxlAcStrategyType.DCT16X16)
            {
                int ix = 3;
                if (quantKind is JxlAcStrategyType.DCT32X16 or JxlAcStrategyType.DCT16X32)
                {
                    ix = 1;
                }
                else if (quantKind == JxlAcStrategyType.DCT16X16)
                {
                    ix = 0;
                }
                else if (quantKind == JxlAcStrategyType.DCT32X32)
                {
                    ix = 2;
                }

                int step = (int)(sumOfError / ((Mul1[ix, c] * width * height * JxlFrameDimensions.BlockDimensions * JxlFrameDimensions.BlockDimensions) + (Mul2[ix, c] * sumOfVals)));

                if (step >= 2)
                {
                    step = 2;
                }

                if (step < 0)
                {
                    step = 0;
                }

                if (sumOfError > (Mul1[ix, c] * width * height * JxlFrameDimensions.BlockDimensions * JxlFrameDimensions.BlockDimensions) + (Mul2[ix, c] * sumOfVals))
                {
                    quant += step;

                    if (quant >= JxlQuantizer.MaxQuant)
                    {
                        quant = JxlQuantizer.MaxQuant - 1;
                    }
                }
            }
        }

        {
            // Reduce quant in highly active areas
            int div = width * height;
            float minHfNonZeroes = MathF.Min(hfNonZeros[0], MathF.Min(hfNonZeros[1], MathF.Min(hfNonZeros[2], hfNonZeros[3])));
            int activity = 15;

            if (minHfNonZeroes < 15.0f * div)
            {
                activity = ((int)minHfNonZeroes + (div / 2)) / div;
            }

            int qp = quant - activity;

            if (c == 1)
            {
                for (int i = 1; i < 4; ++i)
                {
                    thresholds[i] += 0.01f * activity;
                }
            }

            int originalQpLimit = Math.Max(4, quant / 2);

            if (qp < originalQpLimit)
            {
                qp = originalQpLimit;
            }

            quant = qp;
        }
    }

    /// <summary>Quantizes and reconstructs Y before the encoder removes Y correlation from X and B.</summary>
    /// <param name="encoderState">The borrowed encoder controls and channel multipliers.</param>
    /// <param name="size">The coefficient count per channel.</param>
    /// <param name="quantizer">The prepared frame quantizer.</param>
    /// <param name="matrixStorage">The complete coefficient tables borrowed before the group loops.</param>
    /// <param name="errorDiffusion">The requested error-diffusion setting.</param>
    /// <param name="quantKind">The transform strategy.</param>
    /// <param name="width">The canonical coefficient width, in blocks.</param>
    /// <param name="height">The canonical coefficient height, in blocks.</param>
    /// <param name="biases">The prepared channel biases and shared bias numerator.</param>
    /// <param name="quant">The positive integer quantizer, updated with the selected value.</param>
    /// <param name="inOut">Three coefficient planes. The Y plane receives reconstructed coefficients.</param>
    /// <param name="quantized">The integer destination for the same three coefficient planes.</param>
    public static void QuantizeRoundtripYBlockAc(
        JxlPassesEncoderState encoderState,
        int size,
        JxlQuantizer quantizer,
        ReadOnlySpan<float> matrixStorage,
        bool errorDiffusion,
        JxlAcStrategyType quantKind,
        int width,
        int height,
        ReadOnlySpan<float> biases,
        ref int quant,
        Span<float> inOut,
        Span<int> quantized)
    {
        Span<float> thresholdY = [0.58f, 0.64f, 0.64f, 0.64f];

        if (encoderState.CompressionParameters.SpeedTier <= JxlSpeedTier.Hare)
        {
            int maxQuant = 0;
            int originalQuant = quant;

            Span<float> val =
            [
                encoderState.XQmMultiplier,
                1.0f,
                encoderState.BQmMultiplier
            ];

            foreach (int channel in (ReadOnlySpan<int>)[1, 0, 2])
            {
                Span<float> thresholds = [0.58f, 0.64f, 0.64f, 0.64f];
                quant = originalQuant;

                AdjustQuantBlockAC(
                    quantizer,
                    matrixStorage,
                    channel,
                    val[channel],
                    quantKind,
                    width,
                    height,
                    thresholds,
                    inOut[(channel * size)..],
                    ref quant);

                if (channel == 1)
                {
                    thresholds.CopyTo(thresholdY);
                }

                maxQuant = Math.Max(quant, maxQuant);
            }

            quant = maxQuant;
        }
        else
        {
            thresholdY[0] = 0.56f;
            thresholdY[1] = 0.62f;
            thresholdY[2] = 0.62f;
            thresholdY[3] = 0.62f;
        }

        QuantizeBlockAc(
            quantizer,
            matrixStorage,
            1,
            1.0f,
            quantKind,
            width,
            height,
            thresholdY,
            inOut[size..],
            quant,
            quantized[size..]);

        ReadOnlySpan<float> dequantMatrix = quantizer.DequantMatrix(matrixStorage, quantKind, 1);

        // Reconstruct Y before quantizing its correlated X/B residuals. Bias, matrix and scale stay in registers;
        // the quantizer writes directly into the existing float coefficient plane and leaves integer output unchanged.
        int coefficientCount = JxlFrameDimensions.DctBlockSize * width * height;
        JxlQuantizer.Dequantize(quantized.Slice(size, coefficientCount), dequantMatrix, quantizer.InverseQuantAc(quant), 1, biases, inOut[size..]);
    }

    /// <summary>Transforms and quantizes one group while borrowing its prepared matrices once.</summary>
    /// <param name="configuration">The configuration that owns coefficient scratch.</param>
    /// <param name="groupIndex">The frame group index.</param>
    /// <param name="encoderState">The borrowed frame controls and coefficient destinations.</param>
    /// <param name="opsin">The source XYB planes.</param>
    /// <param name="rectangle">The source region.</param>
    /// <param name="dc">The destination DC planes.</param>
    public static void ComputeCoefficients(
        Configuration configuration,
        int groupIndex,
        JxlPassesEncoderState encoderState,
        JxlImage3<float> opsin,
        Rectangle rectangle,
        JxlImage3<float> dc)
    {
        ReadOnlySpan<float> matrixStorage = encoderState.Shared.Matrices.GetPreparedStorage();
        Rectangle blockGroupRect = encoderState.Shared.FrameDimensions.BlockGroupRect(groupIndex);
        Rectangle cmapRect = new(
            blockGroupRect.X0() / JxlChromaFromLuma.ColorTileDimensionInBlocks,
            blockGroupRect.Y0() / JxlChromaFromLuma.ColorTileDimensionInBlocks,
            JxlMath.DivCeil(blockGroupRect.Width, JxlChromaFromLuma.ColorTileDimensionInBlocks),
            JxlMath.DivCeil(blockGroupRect.Height, JxlChromaFromLuma.ColorTileDimensionInBlocks));
        Rectangle groupRect = encoderState.Shared.FrameDimensions.GroupRect(groupIndex).Translate(rectangle.X0(), rectangle.Y0());

        int widthBlocks = blockGroupRect.Width;
        int heightBlocks = blockGroupRect.Height;

        int dcStride = dc.PixelsPerRow;
        int opsinStride = opsin.PixelsPerRow;

        JxlPlane<int> fullQuantField = encoderState.RawQuantField;
        JxlCompressParameters cparams = encoderState.CompressParameters;

        int dctScratchSize = JxlDct.MaximumWorkingSetLength;

        using IMemoryOwner<int> mem = configuration.MemoryAllocator.Allocate<int>(3 * JxlAcStrategy.MaximumCoefficientArea);
        using IMemoryOwner<float> fmem = configuration.MemoryAllocator.Allocate<float>((5 * JxlAcStrategy.MaximumCoefficientArea) + dctScratchSize);

        Span<float> scratchSpace = fmem.Memory.Span[(3 * JxlAcStrategy.MaximumCoefficientArea)..];

        {
            // Error diffusion is slow. Only allow it on squirrel 🐿️ speed tiers
            // or below.
            bool errorDiffusion = cparams.SpeedTier <= JxlSpeedTier.Squirrel;

            InlineArray3<InlineArray11<Memory<int>>> coeffs = default;
            int numPasses = encoderState.ProgressiveSplitter.GetNumPasses();

            if (numPasses <= 0)
            {
                throw new InvalidOperationException("Image must have at least one pass; found " + numPasses);
            }

            for (int i = 0; i < numPasses; i++)
            {
                IJxlDctAcImage acimg = encoderState.Coefficients[i];

                if (acimg.Type != JxlDctAcType.Ac32)
                {
                    throw new InvalidOperationException("AC image must be 32-bit");
                }

                coeffs[0][i] = acimg.GetPlaneRowMemory(0, groupIndex, 0).Pointer32;
                coeffs[1][i] = acimg.GetPlaneRowMemory(1, groupIndex, 0).Pointer32;
                coeffs[2][i] = acimg.GetPlaneRowMemory(2, groupIndex, 0).Pointer32;
            }

            Span<float> coeffsIn = fmem.Memory.Span;
            Span<int> quantized = mem.Memory.Span;

            // SIMD on 64-bit lanes equivalent
            // No need to call .Clear(). All values will be populated.
            Span<float> shared = stackalloc float[64 * 7];
            Span<float> xFactor = shared.Slice(64 * 0, 64);
            Span<float> bFactor = shared.Slice(64 * 1, 64);
            Span<float> inX = shared.Slice(64 * 2, 64);
            Span<float> inY = shared.Slice(64 * 3, 64);
            Span<float> inB = shared.Slice(64 * 4, 64);
            Span<float> outX = shared.Slice(64 * 5, 64);
            Span<float> outB = shared.Slice(64 * 6, 64);

            for (int by = 0; by < heightBlocks; by++)
            {
                Span<int> rowQuantAc = fullQuantField.GetRow(blockGroupRect, by);
                int ty = by / JxlChromaFromLuma.ColorTileDimensionInBlocks;

                InlineArray3<Memory<byte>> rowCMap = default;
                rowCMap[0] = encoderState.Shared.ContextMap.YToXMap.GetRow(cmapRect, ty);
                rowCMap[1] = default;
                rowCMap[2] = encoderState.Shared.ContextMap.YToBMap.GetRow(cmapRect, ty);

                InlineArray3<Memory<float>> opsinRows = default;
                opsinRows[0] = opsin.Plane(0).GetRowMemory(groupRect, by * JxlFrameDimensions.BlockDimensions);
                opsinRows[1] = opsin.Plane(1).GetRowMemory(groupRect, by * JxlFrameDimensions.BlockDimensions);
                opsinRows[2] = opsin.Plane(2).GetRowMemory(groupRect, by * JxlFrameDimensions.BlockDimensions);

                InlineArray3<Memory<float>> dcRows = default;
                dcRows[0] = dc.Plane(0).GetRowMemory(blockGroupRect, by);
                dcRows[1] = dc.Plane(1).GetRowMemory(blockGroupRect, by);
                dcRows[2] = dc.Plane(2).GetRowMemory(blockGroupRect, by);

                JxlAcStrategyRow acStrategyRow = encoderState.Shared.AcStrategy.GetRow(blockGroupRect, by);

                for (int tx = 0; tx < JxlMath.DivCeil(widthBlocks, JxlChromaFromLuma.ColorTileDimensionInBlocks); tx++)
                {
                    xFactor.Fill(encoderState.Shared.ContextMap.Base.YToXRatio(rowCMap[0].Span[tx]));
                    bFactor.Fill(encoderState.Shared.ContextMap.Base.YToBRatio(rowCMap[2].Span[tx]));

                    for (int bx = tx * JxlChromaFromLuma.ColorTileDimensionInBlocks; bx < widthBlocks && bx < (tx + 1) * JxlChromaFromLuma.ColorTileDimensionInBlocks; bx++)
                    {
                        JxlAcStrategy acs = acStrategyRow[bx];

                        if (!acs.IsFirstBlock)
                        {
                            continue;
                        }

                        int xBlocks = acs.CoveredBlocksX;
                        int yBlocks = acs.CoveredBlocksY;

                        JxlForwardCoefficientOrder.CoefficientLayout(ref yBlocks, ref xBlocks);

                        int size = JxlFrameDimensions.DctBlockSize * xBlocks * yBlocks;
                        int quantAc = rowQuantAc[bx];

                        for (int c = 0; c < 3; c++)
                        {
                            JxlTransformsEncoder.TransformFromPixels(
                                acs.Strategy,
                                opsinRows[c].Span[(bx * JxlFrameDimensions.BlockDimensions)..],
                                opsinStride,
                                coeffsIn[(c * size)..],
                                scratchSpace);
                        }

                        JxlTransformsEncoder.DcFromLowestFrequencies(
                            acs.Strategy,
                            coeffsIn[size..],
                            dcRows[1].Span[bx..],
                            dcStride,
                            scratchSpace);

                        QuantizeRoundtripYBlockAc(
                            encoderState,
                            size,
                            encoderState.Shared.Quantizer,
                            matrixStorage,
                            errorDiffusion,
                            acs.Strategy,
                            xBlocks,
                            yBlocks,
                            JxlQuantizer.DefaultQuantBias,
                            ref quantAc,
                            coeffsIn,
                            quantized);

                        for (int k = 0; k <= size - 64; k += 64)
                        {
                            coeffsIn.Slice(k, 64).CopyTo(inX);
                            coeffsIn.Slice(size + k, 64).CopyTo(inY);
                            coeffsIn.Slice((2 * size) + k, 64).CopyTo(inB);
                            NegMulAdd(xFactor, inY, inX, outX);
                            NegMulAdd(bFactor, inY, inB, outB);
                            outX.CopyTo(coeffsIn.Slice(k, 64));
                            outB.CopyTo(coeffsIn.Slice((2 * size) + k, 64));
                        }

                        foreach (int c in (ReadOnlySpan<int>)[0, 2])
                        {
                            Span<float> thres = [0.58f, 0.62f, 0.62f, 0.62f];

                            QuantizeBlockAc(
                                encoderState.Shared.Quantizer,
                                matrixStorage,
                                c,
                                c == 0 ? encoderState.XQmMultiplier : encoderState.BQmMultiplier,
                                acs.Strategy,
                                xBlocks,
                                yBlocks,
                                thres,
                                coeffsIn[(c * size)..],
                                quantAc,
                                quantized[(c * size)..]);

                            JxlTransformsEncoder.DcFromLowestFrequencies(
                                acs.Strategy,
                                coeffsIn[(c * size)..],
                                dcRows[c].Span[bx..],
                                dcStride,
                                scratchSpace);
                        }

                        rowQuantAc[bx] = quantAc;

                        for (int c = 0; c < 3; c++)
                        {
                            encoderState.ProgressiveSplitter.SplitAcCoefficients(quantized[(c * size)..], acs, bx, by, coeffs[c]);

                            for (int p = 0; p < numPasses; p++)
                            {
                                coeffs[c][p] = coeffs[c][p][size..];
                            }
                        }
                    }
                }
            }
        }
    }

    private static void NegMulAdd(Span<float> a, Span<float> b, Span<float> c, Span<float> d)
    {
        // d[n] = c[n] - (a[n] * b[n])
        TensorPrimitives.Multiply(a, b, d);
        TensorPrimitives.Subtract(c, d, d);
    }

    public static bool EncodeGroupTokenizedCoefficients(
        int groupIndex,
        int passIndex,
        int histogramIndex,
        JxlPassesEncoderState encState,
        JxlBitWriter writer,
        JxlAuxiliaryOutput auxOut)
    {
        int numHistograms = encState.Shared.HistogramCount;

        if (!(numHistograms == 0 || histogramIndex < numHistograms))
        {
            throw new InvalidOperationException("Histogram index is out of bounds");
        }

        int histogramSelectorBits = JxlMath.CeilLog2Nonzero(numHistograms);

        if (histogramSelectorBits != 0)
        {
            return writer.WithMaxBits((ulong)histogramSelectorBits, JxlLayerType.Ac, auxOut, () =>
            {
                writer.Write(histogramSelectorBits, histogramIndex);
                return true;
            });
        }

        int contextOffset = histogramIndex * encState.Shared.BlockContextMap.NumAcContexts;

        return WriteTokens(
            encState.Passes[passIndex].AcTokens[groupIndex],
            encState.Passes[passIndex].Codes,
            contextOffset,
            writer,
            JxlLayerType.AcTokens,
            auxOut);
    }
}
