// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing;
using System.Numerics.Tensors;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
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

    public static void QuantizeBlockAc(
        JxlQuantizer quantizer,
        int c,
        float qmMultiplier,
        JxlAcStrategyType quantKind,
        int width,
        int height,
        Span<float> thresholds,
        Span<float> blockIn,
        int quant,
        Span<int> blockOut)
    {
        ReadOnlySpan<float> qm = quantizer.InverseDequantMatrix(quantKind, c);
        float quantAc = quantizer.Scale * quant;

        if (c != 1 && width * height >= 4)
        {
            float sub = 0.00744f * width * width;

            for (int i = 0; i < 4; i++)
            {
                thresholds[i] -= sub;

                if (thresholds[i] < 0.5f)
                {
                    thresholds[i] = 0.5f;
                }
            }
        }

        // We need 8 floats at once; reference software
        // uses a fixed-size of 8 items in the vector (HWY_CAPPED(float, 8)).
        // TODO: can we update this to use Vector<T>?
        Vector256<float> quantV = Vector256.Create(quantAc * qmMultiplier);

        int yGeTarget = height * 8 / 2;
        int xGeTarget = width * 8 / 2;

        for (int y = 0; y < height * 8; y++)
        {
            int yFix = (y >= yGeTarget ? 1 : 0) * 2;
            int off = (y * 8) + width;

            for (int x = 0; x < width * 8; x += 8)
            {
                Vector256<float> threshold = Vector256<float>.Zero;

                if (width == 1)
                {
                    // 🐛: in reference software, it seems like
                    // this:
                    //    Load(kMask + x)
                    // has a potential for a program crash.
                    // https://github.com/libjxl/libjxl/blob/ef67fde2ec16d52e644c5a0969230b9a85e3eb31/lib/jxl/enc_group.cc#L85
                    // x can be > 0
                    // and there's 8 items to load, with kMask itself having 8 items
                    // So if x is 1 or higher and it tries loading kMask[1..], then there's only
                    // 7 items remaining - kaboom 💥
                    // So we're not implementing this. Seems like a bug.
                    throw new InvalidOperationException("width=1 not supported");
                }
                else
                {
                    threshold = Vector256.Create(thresholds[yFix + (x >= xGeTarget ? 1 : 0)]);
                }

                Vector256<float> q = Vector256.Create(qm[(off + x)..]) * quantV;
                Vector256<float> input = Vector256.Create<float>(blockIn[(off + x)..]);
                Vector256<float> value = q * input;
                Vector256<float> nonZeroMask = Vector256.GreaterThanOrEqual(Vector256.Abs(value), threshold);
                Vector256<int> v = Vector256.ConvertToInt32Native(Vector256.ConditionalSelect(nonZeroMask, Vector256.Round(value), Vector256<float>.Zero));
                v.CopyTo(blockOut[(off + x)..]);
            }
        }
    }

    public static void AdjustQuantBlockAC(
        JxlQuantizer quantizer,
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

        ReadOnlySpan<float> qm = quantizer.InverseDequantMatrix(quantKind, c);
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
            if (hfNonZeros[0] + hfNonZeros[1] + hfNonZeros[2] + hfNonZeros[3] < 11)
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

    public static void QuantizeRoundtripYBlockAc(
        JxlPassesEncoderState encoderState,
        int size,
        JxlQuantizer quantizer,
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
            1,
            1.0f,
            quantKind,
            width,
            height,
            thresholdY,
            inOut[size..],
            quant,
            quantized[size..]);

        ReadOnlySpan<float> dequantMatrix = quantizer.DequantMatrix(quantKind, 1);

        /*
           Implementing this is slightly tricky as we have to
           process at 64 lanes at a time.

           This is the reference code snippet from libjxl:

           HWY_CAPPED(float, kDCTBlockSize) df;
           HWY_CAPPED(int32_t, kDCTBlockSize) di;
           const auto inv_qac = Set(df, quantizer.inv_quant_ac(*quant));
           for (size_t k = 0; k < kDCTBlockSize * xsize * ysize; k += Lanes(df)) {
             const auto oquant = Load(di, quantized + size + k);
             const auto adj_quant = AdjustQuantBias(di, 1, oquant, biases);
             const auto dequantm = Load(df, dequant_matrix + k);
             Store(Mul(Mul(adj_quant, dequantm), inv_qac), df, inout + size + k);
           }
         */

        // Don't call .Clear(). All items will be populated.
        Span<float> inverseQAc = stackalloc float[64];
        Span<int> oquant = stackalloc int[64];
        Span<float> adjQuant = stackalloc float[64];
        Span<float> dequantm = stackalloc float[64];

        Span<float> mulAdjQuantDequantm = stackalloc float[64];
        Span<float> mulAdjQuantDequantmInverseQAc = stackalloc float[64];

        inverseQAc.Fill(quantizer.InverseQuantAc(quant));

        for (int k = 0; k < JxlFrameDimensions.DctBlockSize * width * height; k += 64)
        {
            quantized.Slice(size + k, 64).CopyTo(oquant);
            JxlQuantizerSimd.AdjustQuantBias64(1, oquant, biases, adjQuant);
            dequantMatrix.Slice(k, 64).CopyTo(dequantm);

            TensorPrimitives.Multiply(adjQuant, dequantm, mulAdjQuantDequantm);
            TensorPrimitives.Multiply(mulAdjQuantDequantm, inverseQAc, mulAdjQuantDequantmInverseQAc);

            inverseQAc.CopyTo(inOut.Slice(size + k, 64));
        }
    }
}
