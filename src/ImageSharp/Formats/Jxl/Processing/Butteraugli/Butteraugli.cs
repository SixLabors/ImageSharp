// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;

/// <summary>
/// Implementation of Google Butteraugli, an advanced
/// image comparison system. Unlike PSNR or SSIM,
/// Butteraugli compares images the way humans may
/// spot differences instead of pixelwise.
/// </summary>
internal static class Butteraugli
{
    // NOTE: The meaning of those constants
    // is not perfectly understood.
    public const float WMfMalta = 37.0819870399f;
    public const float Norm1Mf = 130262059.556f;
    public const float WMfMaltaX = 8246.75321353f;
    public const float Norm1MfX = 1009002.70582f;

    public const float WHfMalta = 18.7237414387f;
    public const float Norm1Hf = 4498534.45232f;
    public const float WHfMaltaX = 6923.99476109f;
    public const float Norm1HfX = 8051.15833247f;

    public const float WUhfMalta = 1.10039032555f;
    public const float Norm1Uhf = 71.7800275169f;
    public const float WUhfMaltaX = 173.5f;
    public const float Norm1UhfX = 5.0f;

    private const float IntensityTargetNormalizationHack = 0.79079917404f;

    private const float InternalGoodQualityThreshold =
        17.83f * IntensityTargetNormalizationHack;

    private const float GlobalScale =
        1.0f / InternalGoodQualityThreshold;

#pragma warning disable // Just indentation warnings
    /// <summary>
    /// Underlying data for <see cref="Heatmap"/>.
    /// </summary>
    private static readonly float[,] HeatmapData =
    {
        {0, 0, 0},       {0, 0, 1},
        {0, 1, 1},       {0, 1, 0},  // Good level
        {1, 1, 0},       {1, 0, 0},  // Bad level
        {1, 0, 1},       {0.5f, 0.5f, 1.0f},
        {1.0f, 0.5f, 0.5f},  // Pastel colors for the very bad quality range.
        {1.0f, 1.0f, 0.5f}, {1, 1, 1},
        {1, 1, 1},  // Last color repeated to have a solid range of white.
    };
#pragma warning restore

    private static readonly DenseMatrix<float> Heatmap = new(HeatmapData);

    public static ReadOnlySpan<float> Wmul =>
    [
        400.0f,         1.50815703118f,  0f,
        2150.0f,        10.6195433239f,  16.2176043152f,
        29.2353797994f, 0.844626970982f, 0.703646627719f,
    ];

    public static ReadOnlySpan<float> ComputeKernel(float sigma)
    {
        const float m = 2.25f;  // Accuracy increases when m is increased
        float scaler = -1.0f / (2.0f * sigma * sigma);
        int diff = Math.Max(1, (int)(m * MathF.Abs(sigma)));

        // If sigma is very large we should not return a 'new float[]' allocation.
        // This guard is temporary so we can verify the range of the number of elements.
        // TODO: remove guard if the value doesn't exceed the limit for many JXL files
        DebugGuard.MustBeLessThanOrEqualTo(sigma, 32f, nameof(sigma));

        float[] kernel = new float[(2 * diff) + 1];

        for (int i = -diff; i <= diff; i++)
        {
            kernel[i + diff] = MathF.Exp(scaler * i * i);
        }

        return kernel;
    }

    public static void ConvolveBorderColumn(
        JxlPlane<float> input,
        ReadOnlySpan<float> kernel,
        int x,
        Span<float> rowOut)
    {
        int offset = kernel.Length / 2;

        int minX = x < offset ? 0 : x - offset;
        int maxX = Math.Min(input.XSize - 1, x + offset);

        float weight = 0.0f;

        for (int j = minX; j <= maxX; j++)
        {
            weight += kernel[j - x + offset];
        }

        float scale = 1.0f / weight;

        for (int y = 0; y < input.YSize; y++)
        {
            Span<float> rowIn = input.GetRow(y);

            float sum = 0.0f;

            for (int j = minX; j <= maxX; j++)
            {
                sum += rowIn[j] * kernel[j - x + offset];
            }

            rowOut[y] = sum * scale;
        }
    }

    public static bool ConvolutionWithTranspose(
        JxlPlane<float> input,
        ReadOnlySpan<float> kernel,
        JxlPlane<float> output)
    {
        if (output.XSize != input.YSize)
        {
            return false;
        }

        if (output.YSize != input.XSize)
        {
            return false;
        }

        int len = kernel.Length;
        int offset = len / 2;

        float weightNoBorder = 0.0f;

        for (int j = 0; j < len; j++)
        {
            weightNoBorder += kernel[j];
        }

        float scaleNoBorder = 1.0f / weightNoBorder;

        int border1 = Math.Min(input.XSize, offset);
        int border2 = input.XSize > offset ? input.XSize - offset : 0;

        Span<float> scaledKernel = stackalloc float[(len / 2) + 1];

        for (int i = 0; i <= len / 2; i++)
        {
            scaledKernel[i] = kernel[i] * scaleNoBorder;
        }

        // Middle
        switch (len)
        {
            case 7:
            {
                float sk0 = scaledKernel[0];
                float sk1 = scaledKernel[1];
                float sk2 = scaledKernel[2];
                float sk3 = scaledKernel[3];

                for (int y = 0; y < input.YSize; y++)
                {
                    Span<float> rowIn = input.GetRow(y);

                    for (int x = border1; x < border2; x++)
                    {
                        int i = x - border1;

                        float sum0 = (rowIn[i + 0] + rowIn[i + 6]) * sk0;
                        float sum1 = (rowIn[i + 1] + rowIn[i + 5]) * sk1;
                        float sum2 = (rowIn[i + 2] + rowIn[i + 4]) * sk2;
                        float sum = (rowIn[i + 3] * sk3) + sum0 + sum1 + sum2;

                        output.GetRow(x)[y] = sum;
                    }
                }

                break;
            }

            case 13:
            {
                for (int y = 0; y < input.YSize; y++)
                {
                    Span<float> rowIn = input.GetRow(y);

                    for (int x = border1; x < border2; x++)
                    {
                        int i = x - border1;

                        float sum0 = (rowIn[i + 0] + rowIn[i + 12]) * scaledKernel[0];
                        float sum1 = (rowIn[i + 1] + rowIn[i + 11]) * scaledKernel[1];
                        float sum2 = (rowIn[i + 2] + rowIn[i + 10]) * scaledKernel[2];
                        float sum3 = (rowIn[i + 3] + rowIn[i + 9]) * scaledKernel[3];

                        sum0 += (rowIn[i + 4] + rowIn[i + 8]) * scaledKernel[4];
                        sum1 += (rowIn[i + 5] + rowIn[i + 7]) * scaledKernel[5];

                        float sum = rowIn[i + 6] * scaledKernel[6];

                        output.GetRow(x)[y] = sum + sum0 + sum1 + sum2 + sum3;
                    }
                }

                break;
            }

            case 15:
            {
                for (int y = 0; y < input.YSize; y++)
                {
                    Span<float> rowIn = input.GetRow(y);

                    for (int x = border1; x < border2; x++)
                    {
                        int i = x - border1;

                        float sum0 = (rowIn[i + 0] + rowIn[i + 14]) * scaledKernel[0];
                        float sum1 = (rowIn[i + 1] + rowIn[i + 13]) * scaledKernel[1];
                        float sum2 = (rowIn[i + 2] + rowIn[i + 12]) * scaledKernel[2];
                        float sum3 = (rowIn[i + 3] + rowIn[i + 11]) * scaledKernel[3];

                        sum0 += (rowIn[i + 4] + rowIn[i + 10]) * scaledKernel[4];
                        sum1 += (rowIn[i + 5] + rowIn[i + 9]) * scaledKernel[5];
                        sum2 += (rowIn[i + 6] + rowIn[i + 8]) * scaledKernel[6];

                        float sum = rowIn[i + 7] * scaledKernel[7];

                        output.GetRow(x)[y] = sum + sum0 + sum1 + sum2 + sum3;
                    }
                }

                break;
            }

            case 33:
            {
                for (int y = 0; y < input.YSize; y++)
                {
                    Span<float> rowIn = input.GetRow(y);

                    for (int x = border1; x < border2; x++)
                    {
                        int i = x - border1;

                        float sum0 = (rowIn[i + 0] + rowIn[i + 32]) * scaledKernel[0];
                        float sum1 = (rowIn[i + 1] + rowIn[i + 31]) * scaledKernel[1];
                        float sum2 = (rowIn[i + 2] + rowIn[i + 30]) * scaledKernel[2];
                        float sum3 = (rowIn[i + 3] + rowIn[i + 29]) * scaledKernel[3];

                        sum0 += (rowIn[i + 4] + rowIn[i + 28]) * scaledKernel[4];
                        sum1 += (rowIn[i + 5] + rowIn[i + 27]) * scaledKernel[5];
                        sum2 += (rowIn[i + 6] + rowIn[i + 26]) * scaledKernel[6];
                        sum3 += (rowIn[i + 7] + rowIn[i + 25]) * scaledKernel[7];

                        sum0 += (rowIn[i + 8] + rowIn[i + 24]) * scaledKernel[8];
                        sum1 += (rowIn[i + 9] + rowIn[i + 23]) * scaledKernel[9];
                        sum2 += (rowIn[i + 10] + rowIn[i + 22]) * scaledKernel[10];
                        sum3 += (rowIn[i + 11] + rowIn[i + 21]) * scaledKernel[11];

                        sum0 += (rowIn[i + 12] + rowIn[i + 20]) * scaledKernel[12];
                        sum1 += (rowIn[i + 13] + rowIn[i + 19]) * scaledKernel[13];
                        sum2 += (rowIn[i + 14] + rowIn[i + 18]) * scaledKernel[14];
                        sum3 += (rowIn[i + 15] + rowIn[i + 17]) * scaledKernel[15];

                        float sum = rowIn[i + 16] * scaledKernel[16];

                        output.GetRow(x)[y] = sum + sum0 + sum1 + sum2 + sum3;
                    }
                }

                break;
            }

            default:
                throw new NotSupportedException($"Kernel size {len} not implemented.");
        }

        // Left border
        for (int x = 0; x < border1; x++)
        {
            ConvolveBorderColumn(input, kernel, x, output.GetRow(x));
        }

        // Right border
        for (int x = border2; x < input.XSize; x++)
        {
            ConvolveBorderColumn(input, kernel, x, output.GetRow(x));
        }

        return true;
    }

    private static bool Blur(
        Configuration configuration,
        JxlPlane<float> input,
        float sigma,
        ButteraugliBlurTemp temp,
        JxlPlane<float> output)
    {
        ReadOnlySpan<float> kernel = ComputeKernel(sigma);

        // Separable5 does an in-place convolution, so this fast path is not safe
        // if input aliases output.
        if (kernel.Length == 5 && !ReferenceEquals(input, output))
        {
            float sumWeights = 0.0f;

            foreach (float w in kernel)
            {
                sumWeights += w;
            }

            float scale = 1.0f / sumWeights;

            float w0 = kernel[2] * scale;
            float w1 = kernel[1] * scale;
            float w2 = kernel[0] * scale;

            JxlWeightsSeparable5 weights = default;
            FillRep4(ref weights.Horizontal, w0, w1, w2);
            FillRep4(ref weights.Vertical, w0, w1, w2);

            if (!Separable5(input, input.GetRectangle(), weights, null, output))
            {
                return false;
            }

            return true;
        }

        JxlPlane<float> tempT = temp.GetTransposed(configuration, input);

        if (!ConvolutionWithTranspose(input, kernel, tempT))
        {
            return false;
        }

        if (!ConvolutionWithTranspose(tempT, kernel, output))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Equivalent to HWY_REP4.
    /// </summary>
    private static void FillRep4(ref InlineArray12<float> values, float a, float b, float c)
    {
        for (int i = 0; i < 4; i++)
        {
            values[i] = a;
            values[4 + i] = b;
            values[8 + i] = c;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> MaximumClamp(Vector<float> value, float maximum)
    {
        Vector<float> multiplier = new(0.724216145665f);
        Vector<float> maximumValue = new(maximum);
        Vector<float> ifPositive = ((value - maximumValue) * multiplier) + maximumValue;
        Vector<float> ifNegative = ((value + maximumValue) * multiplier) - maximumValue;
        Vector<float> positiveOrValue = Vector.ConditionalSelect(Vector.GreaterThan(value, maximumValue), ifPositive, value);
        Vector<float> result = Vector.ConditionalSelect(Vector.LessThan(value, Vector.Negate(maximumValue)), ifNegative, positiveOrValue);

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> RemoveRangeAroundZero(Vector<float> x, float width)
    {
        Vector<float> w = new(width);

        return Vector.ConditionalSelect(
            Vector.GreaterThan(x, w),
            x - w,
            Vector.ConditionalSelect(
                Vector.LessThan(x, -w),
                x + w,
                Vector<float>.Zero));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> AmplifyRangeAroundZero(Vector<float> x, float width)
    {
        Vector<float> w = new(width);

        return Vector.ConditionalSelect(
            Vector.GreaterThan(x, w),
            x + w,
            Vector.ConditionalSelect(
                Vector.LessThan(x, -w),
                x - w,
                x + x));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void XybLowFrequencyToValues(
        Vector<float> x,
        Vector<float> y,
        Vector<float> bArg,
        out Vector<float> valX,
        out Vector<float> valY,
        out Vector<float> valB)
    {
        Vector<float> xMul = new(33.832837186260f);
        Vector<float> yMul = new(14.458268100570f);
        Vector<float> bMul = new(49.87984651440f);
        Vector<float> yToBMul = new(-0.362267051518f);

        Vector<float> b = (yToBMul * y) + bArg;

        valB = b * bMul;
        valX = x * xMul;
        valY = y * yMul;
    }

    public static void XybLowFrequencyToValues(JxlImage3F xybLf)
    {
        int lanes = Vector<float>.Count;

        for (int y = 0; y < xybLf.YSize; y++)
        {
            Span<float> rowX = xybLf.PlaneRow(0, y);
            Span<float> rowY = xybLf.PlaneRow(1, y);
            Span<float> rowB = xybLf.PlaneRow(2, y);

            for (int x = 0; x < xybLf.XSize; x += lanes)
            {
                Vector<float> valX = new(rowX.Slice(x, lanes));
                Vector<float> valY = new(rowY.Slice(x, lanes));
                Vector<float> valB = new(rowB.Slice(x, lanes));

                XybLowFrequencyToValues(
                    valX,
                    valY,
                    valB,
                    out valX,
                    out valY,
                    out valB);

                valX.CopyTo(rowX.Slice(x, lanes));
                valY.CopyTo(rowY.Slice(x, lanes));
                valB.CopyTo(rowB.Slice(x, lanes));
            }
        }
    }

    public static bool SuppressXByY(JxlPlane<float> inY, JxlPlane<float> inOutX)
    {
        if (!JxlImageOperations.SameSize(inOutX, inY))
        {
            return false;
        }

        int xSize = inY.XSize;
        int ySize = inY.YSize;
        int lanes = Vector<float>.Count;

        const float suppress = 46.0f;
        const float s = 0.653020556257f;

        Vector<float> sv = new(s);
        Vector<float> oneMinusS = new(1.0f - s);
        Vector<float> ywv = new(suppress);

        for (int y = 0; y < ySize; y++)
        {
            ReadOnlySpan<float> rowY = inY.GetRow(y);
            Span<float> rowX = inOutX.GetRow(y);

            for (int x = 0; x < xSize; x += lanes)
            {
                Vector<float> vx = new(rowX.Slice(x, lanes));
                Vector<float> vy = new(rowY.Slice(x, lanes));

                Vector<float> scaler =
                    ((ywv / ((vy * vy) + ywv)) * oneMinusS) + sv;

                (scaler * vx).CopyTo(rowX.Slice(x, lanes));
            }
        }

        return true;
    }

    public static void Subtract(JxlPlane<float> a, JxlPlane<float> b, JxlPlane<float> c)
    {
        int lanes = Vector<float>.Count;

        for (int y = 0; y < a.YSize; y++)
        {
            ReadOnlySpan<float> rowA = a.GetRow(y);
            ReadOnlySpan<float> rowB = b.GetRow(y);
            Span<float> rowC = c.GetRow(y);

            for (int x = 0; x < a.XSize; x += lanes)
            {
                Vector<float> va = new(rowA.Slice(x, lanes));
                Vector<float> vb = new(rowB.Slice(x, lanes));

                (va - vb).CopyTo(rowC.Slice(x, lanes));
            }
        }
    }

    public static bool SeparateLFAndMF(
        Configuration configuration,
        JxlImage3F xyb,
        JxlImage3F lf,
        JxlImage3F mf,
        ButteraugliBlurTemp blurTemp)
    {
        const float sigmaLf = 7.15593339443f;

        for (int i = 0; i < 3; i++)
        {
            if (!Blur(
                    configuration,
                    xyb.Plane(i),
                    sigmaLf,
                    blurTemp,
                    lf.Plane(i)))
            {
                return false;
            }

            Subtract(
                xyb.Plane(i),
                lf.Plane(i),
                mf.Plane(i));
        }

        XybLowFrequencyToValues(lf);

        return true;
    }

    public static bool SeparateMfAndHf(
        Configuration configuration,
        JxlImage3F mf,
        ref InlineArray2<JxlPlane<float>> hf,
        ButteraugliBlurTemp blurTemp)
    {
        const float sigmaHf = 3.22489901262f;

        int xSize = mf.XSize;
        int ySize = mf.YSize;

        hf[0] = JxlPlane<float>.Create(configuration, xSize, ySize);
        hf[1] = JxlPlane<float>.Create(configuration, xSize, ySize);

        int lanes = Vector<float>.Count;

        for (int i = 0; i < 3; i++)
        {
            if (i == 2)
            {
                if (!Blur(configuration, mf.Plane(i), sigmaHf, blurTemp, mf.Plane(i)))
                {
                    return false;
                }

                break;
            }

            for (int y = 0; y < ySize; y++)
            {
                Span<float> rowMf = mf.PlaneRow(i, y);
                Span<float> rowHf = hf[i].GetRow(y);

                for (int x = 0; x < xSize; x += lanes)
                {
                    new Vector<float>(rowMf.Slice(x, lanes))
                        .CopyTo(rowHf.Slice(x, lanes));
                }
            }

            if (!Blur(configuration, mf.Plane(i), sigmaHf, blurTemp, mf.Plane(i)))
            {
                return false;
            }

            if (i == 0)
            {
                const float removeMfRange = 0.29f;

                for (int y = 0; y < ySize; y++)
                {
                    Span<float> rowMf = mf.PlaneRow(0, y);
                    Span<float> rowHf = hf[0].GetRow(y);

                    for (int x = 0; x < xSize; x += lanes)
                    {
                        Vector<float> mfv = new(rowMf.Slice(x, lanes));
                        Vector<float> hfv = new Vector<float>(rowHf.Slice(x, lanes)) - mfv;

                        mfv = RemoveRangeAroundZero(mfv, removeMfRange);

                        mfv.CopyTo(rowMf.Slice(x, lanes));
                        hfv.CopyTo(rowHf.Slice(x, lanes));
                    }
                }
            }
            else
            {
                const float addMfRange = 0.1f;

                for (int y = 0; y < ySize; y++)
                {
                    Span<float> rowMf = mf.PlaneRow(1, y);
                    Span<float> rowHf = hf[1].GetRow(y);

                    for (int x = 0; x < xSize; x += lanes)
                    {
                        Vector<float> mfv = new(rowMf.Slice(x, lanes));
                        Vector<float> hfv = new Vector<float>(rowHf.Slice(x, lanes)) - mfv;

                        mfv = AmplifyRangeAroundZero(mfv, addMfRange);

                        mfv.CopyTo(rowMf.Slice(x, lanes));
                        hfv.CopyTo(rowHf.Slice(x, lanes));
                    }
                }
            }
        }

        return SuppressXByY(hf[1], hf[0]);
    }

    public static bool SeparateHFAndUHF(
        Configuration configuration,
        InlineArray2<JxlPlane<float>> hf,
        InlineArray2<JxlPlane<float>> uhf,
        ButteraugliBlurTemp blurTemp)
    {
        const float sigmaUhf = 1.56416327805f;

        int xSize = hf[0].XSize;
        int ySize = hf[0].YSize;

        uhf[0] = JxlPlane<float>.Create(configuration, xSize, ySize);
        uhf[1] = JxlPlane<float>.Create(configuration, xSize, ySize);

        int lanes = Vector<float>.Count;

        for (int i = 0; i < 2; i++)
        {
            for (int y = 0; y < ySize; y++)
            {
                Span<float> rowUhf = uhf[i].GetRow(y);
                Span<float> rowHf = hf[i].GetRow(y);

                for (int x = 0; x < xSize; x += lanes)
                {
                    new Vector<float>(rowHf.Slice(x, lanes)).CopyTo(rowUhf.Slice(x, lanes));
                }
            }

            if (!Blur(configuration, hf[i], sigmaUhf, blurTemp, hf[i]))
            {
                return false;
            }

            if (i == 0)
            {
                const float removeHfRange = 1.5f;
                const float removeUhfRange = 0.04f;

                for (int y = 0; y < ySize; y++)
                {
                    Span<float> rowUhf = uhf[0].GetRow(y);
                    Span<float> rowHf = hf[0].GetRow(y);

                    for (int x = 0; x < xSize; x += lanes)
                    {
                        Vector<float> hfv = new(rowHf.Slice(x, lanes));
                        Vector<float> uhfv = new Vector<float>(rowUhf.Slice(x, lanes)) - hfv;

                        hfv = RemoveRangeAroundZero(hfv, removeHfRange);
                        uhfv = RemoveRangeAroundZero(uhfv, removeUhfRange);

                        hfv.CopyTo(rowHf.Slice(x, lanes));
                        uhfv.CopyTo(rowUhf.Slice(x, lanes));
                    }
                }
            }
            else
            {
                const float addHfRange = 0.132f;
                const float maxClampHf = 28.4691806922f;
                const float maxClampUhf = 5.19175294647f;
                const float mulYHf = 2.155f;
                const float mulYUhf = 2.69313763794f;

                Vector<float> mulHf = new(mulYHf);
                Vector<float> mulUhf = new(mulYUhf);

                for (int y = 0; y < ySize; y++)
                {
                    Span<float> rowUhf = uhf[1].GetRow(y);
                    Span<float> rowHf = hf[1].GetRow(y);

                    for (int x = 0; x < xSize; x += lanes)
                    {
                        Vector<float> hfv = new(rowHf.Slice(x, lanes));
                        hfv = MaximumClamp(hfv, maxClampHf);

                        Vector<float> uhfv = new Vector<float>(rowUhf.Slice(x, lanes)) - hfv;

                        uhfv = MaximumClamp(uhfv, maxClampUhf);
                        uhfv *= mulUhf;

                        uhfv.CopyTo(rowUhf.Slice(x, lanes));

                        hfv *= mulHf;
                        hfv = AmplifyRangeAroundZero(hfv, addHfRange);

                        hfv.CopyTo(rowHf.Slice(x, lanes));
                    }
                }
            }
        }

        return true;
    }

    public static void DeallocateHFAndUHF(InlineArray2<JxlPlane<float>> hf, InlineArray2<JxlPlane<float>> uhf)
    {
        for (int i = 0; i < 2; i++)
        {
            hf[i].Dispose();
            uhf[i].Dispose();

            hf[i] = new JxlPlane<float>();
            uhf[i] = new JxlPlane<float>();
        }
    }

    public static bool SeparateFrequencies(
        Configuration configuration,
        ButteraugliBlurTemp blurTemp,
        JxlImage3F xyb,
        ButteraugliPsychoImage ps)
    {
        ps.Lf = new JxlImage3F(
            configuration,
            xyb.XSize,
            xyb.YSize);

        ps.Mf = new JxlImage3F(
            configuration,
            xyb.XSize,
            xyb.YSize);

        if (!SeparateLFAndMF(
                configuration,
                xyb,
                ps.Lf,
                ps.Mf,
                blurTemp))
        {
            return false;
        }

        if (!SeparateMfAndHf(
                configuration,
                ps.Mf,
                ref ps.Hf,
                blurTemp))
        {
            return false;
        }

        if (!SeparateHFAndUHF(
                configuration,
                ps.Hf,
                ps.Uhf,
                blurTemp))
        {
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> Sum(
        Vector<float> a,
        Vector<float> b,
        Vector<float> c,
        Vector<float> d)
        => (a + b) + (c + d);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> Sum(
        Vector<float> a,
        Vector<float> b,
        Vector<float> c,
        Vector<float> d,
        Vector<float> e)
        => Sum(a, b, c, d + e);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> Sum(
        Vector<float> a,
        Vector<float> b,
        Vector<float> c,
        Vector<float> d,
        Vector<float> e,
        Vector<float> f,
        Vector<float> g)
        => Sum(a, b, c, Sum(d, e, f, g));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> Sum(
        Vector<float> a,
        Vector<float> b,
        Vector<float> c,
        Vector<float> d,
        Vector<float> e,
        Vector<float> f,
        Vector<float> g,
        Vector<float> h,
        Vector<float> i)
        => (Sum(a, b, c, d) + Sum(e, f, g, h)) + i;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> MaltaUnitLF(
        ReadOnlySpan<float> row,
        int index,
        int xs)
    {
        // helper
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector<float> Load(ReadOnlySpan<float> row, int index)
            => new(row.Slice(index, Vector<float>.Count));

        int xs3 = 3 * xs;

        Vector<float> center = Load(row, index);

        Vector<float> sumYConst = Sum(
            Load(row, index - 4),
            Load(row, index - 2),
            center,
            Load(row, index + 2),
            Load(row, index + 4));

        Vector<float> retval = sumYConst * sumYConst;

        Vector<float> sum;

        sum = Sum(
            Load(row, index - xs3 - xs),
            Load(row, index - xs - xs),
            center,
            Load(row, index + xs + xs),
            Load(row, index + xs3 + xs));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index - xs3 - 3),
            Load(row, index - xs - xs - 2),
            center,
            Load(row, index + xs + xs + 2),
            Load(row, index + xs3 + 3));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index - xs3 + 3),
            Load(row, index - xs - xs + 2),
            center,
            Load(row, index + xs + xs - 2),
            Load(row, index + xs3 - 3));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index - xs3 - xs + 1),
            Load(row, index - xs - xs + 1),
            center,
            Load(row, index + xs + xs - 1),
            Load(row, index + xs3 + xs - 1));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index - xs3 - xs - 1),
            Load(row, index - xs - xs - 1),
            center,
            Load(row, index + xs + xs + 1),
            Load(row, index + xs3 + xs + 1));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index - 4 - xs),
            Load(row, index - 2 - xs),
            center,
            Load(row, index + 2 + xs),
            Load(row, index + 4 + xs));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index - 4 + xs),
            Load(row, index - 2 + xs),
            center,
            Load(row, index + 2 - xs),
            Load(row, index + 4 - xs));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index - xs3 - 2),
            Load(row, index - xs - xs - 1),
            center,
            Load(row, index + xs + xs + 1),
            Load(row, index + xs3 + 2));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index - xs3 + 2),
            Load(row, index - xs - xs + 1),
            center,
            Load(row, index + xs + xs - 1),
            Load(row, index + xs3 - 2));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index - xs - xs - 3),
            Load(row, index - xs - 2),
            center,
            Load(row, index + xs + 2),
            Load(row, index + xs + xs + 3));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index - xs - xs + 3),
            Load(row, index - xs + 2),
            center,
            Load(row, index + xs - 2),
            Load(row, index + xs + xs - 3));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index + xs + xs - 4),
            Load(row, index + xs - 2),
            center,
            Load(row, index - xs + 2),
            Load(row, index - xs - xs + 4));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index - xs - xs - 4),
            Load(row, index - xs - 2),
            center,
            Load(row, index + xs + 2),
            Load(row, index + xs + xs + 4));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index - xs3 - xs - 2),
            Load(row, index - xs - xs - 1),
            center,
            Load(row, index + xs + xs + 1),
            Load(row, index + xs3 + xs + 2));
        retval = (sum * sum) + retval;

        sum = Sum(
            Load(row, index - xs3 - xs + 2),
            Load(row, index - xs - xs + 1),
            center,
            Load(row, index + xs + xs - 1),
            Load(row, index + xs3 + xs - 2));
        retval = (sum * sum) + retval;

        return retval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> MaltaUnit(
        ReadOnlySpan<float> row,
        int index,
        int xs)
    {
        // helper
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector<float> Load(ReadOnlySpan<float> row, int index)
            => new(row.Slice(index, Vector<float>.Count));

        int xs3 = 3 * xs;

        Vector<float> center = Load(row, index);

        Vector<float> sumYConst = Sum(
            Load(row, index - 4),
            Load(row, index - 3),
            Load(row, index - 2),
            Load(row, index - 1),
            center,
            Load(row, index + 1),
            Load(row, index + 2),
            Load(row, index + 3),
            Load(row, index + 4));

        Vector<float> retval = sumYConst * sumYConst;

        Vector<float> sum;

        sum = Sum(
            Load(row, index - xs3 - xs),
            Load(row, index - xs3),
            Load(row, index - xs - xs),
            Load(row, index - xs),
            center,
            Load(row, index + xs),
            Load(row, index + xs + xs),
            Load(row, index + xs3),
            Load(row, index + xs3 + xs));
        retval += sum * sum;

        sum = Sum(
            Load(row, index - xs3 - 3),
            Load(row, index - xs - xs - 2),
            Load(row, index - xs - 1),
            center,
            Load(row, index + xs + 1),
            Load(row, index + xs + xs + 2),
            Load(row, index + xs3 + 3));
        retval += sum * sum;

        sum = Sum(
            Load(row, index - xs3 + 3),
            Load(row, index - xs - xs + 2),
            Load(row, index - xs + 1),
            center,
            Load(row, index + xs - 1),
            Load(row, index + xs + xs - 2),
            Load(row, index + xs3 - 3));
        retval += sum * sum;

        sum = Sum(
            Load(row, index - xs3 - xs + 1),
            Load(row, index - xs3 + 1),
            Load(row, index - xs - xs + 1),
            Load(row, index - xs),
            center,
            Load(row, index + xs),
            Load(row, index + xs + xs - 1),
            Load(row, index + xs3 - 1),
            Load(row, index + xs3 + xs - 1));
        retval += sum * sum;

        sum = Sum(
            Load(row, index - xs3 - xs - 1),
            Load(row, index - xs3 - 1),
            Load(row, index - xs - xs - 1),
            Load(row, index - xs),
            center,
            Load(row, index + xs),
            Load(row, index + xs + xs + 1),
            Load(row, index + xs3 + 1),
            Load(row, index + xs3 + xs + 1));
        retval += sum * sum;

        sum = Sum(
            Load(row, index - 4 - xs),
            Load(row, index - 3 - xs),
            Load(row, index - 2 - xs),
            Load(row, index - 1),
            center,
            Load(row, index + 1),
            Load(row, index + 2 + xs),
            Load(row, index + 3 + xs),
            Load(row, index + 4 + xs));
        retval += sum * sum;

        sum = Sum(
            Load(row, index - 4 + xs),
            Load(row, index - 3 + xs),
            Load(row, index - 2 + xs),
            Load(row, index - 1),
            center,
            Load(row, index + 1),
            Load(row, index + 2 - xs),
            Load(row, index + 3 - xs),
            Load(row, index + 4 - xs));
        retval += sum * sum;

        sum = Sum(
            Load(row, index - xs3 - 2),
            Load(row, index - xs - xs - 1),
            Load(row, index - xs - 1),
            center,
            Load(row, index + xs + 1),
            Load(row, index + xs + xs + 1),
            Load(row, index + xs3 + 2));
        retval += sum * sum;

        sum = Sum(
            Load(row, index - xs3 + 2),
            Load(row, index - xs - xs + 1),
            Load(row, index - xs + 1),
            center,
            Load(row, index + xs - 1),
            Load(row, index + xs + xs - 1),
            Load(row, index + xs3 - 2));
        retval += sum * sum;

        sum = Sum(
            Load(row, index - xs - xs - 3),
            Load(row, index - xs - 2),
            Load(row, index - xs - 1),
            center,
            Load(row, index + xs + 1),
            Load(row, index + xs + 2),
            Load(row, index + xs + xs + 3));
        retval += sum * sum;

        sum = Sum(
            Load(row, index - xs - xs + 3),
            Load(row, index - xs + 2),
            Load(row, index - xs + 1),
            center,
            Load(row, index + xs - 1),
            Load(row, index + xs - 2),
            Load(row, index + xs + xs - 3));
        retval += sum * sum;

        sum = Sum(
            Load(row, index + xs - 4),
            Load(row, index + xs - 3),
            Load(row, index + xs - 2),
            Load(row, index - 1),
            center,
            Load(row, index + 1),
            Load(row, index - xs + 2),
            Load(row, index - xs + 3),
            Load(row, index - xs + 4));
        retval += sum * sum;

        sum = Sum(
            Load(row, index - xs - 4),
            Load(row, index - xs - 3),
            Load(row, index - xs - 2),
            Load(row, index - 1),
            center,
            Load(row, index + 1),
            Load(row, index + xs + 2),
            Load(row, index + xs + 3),
            Load(row, index + xs + 4));
        retval += sum * sum;

        sum = Sum(
            Load(row, index - xs3 - xs - 1),
            Load(row, index - xs3 - 1),
            Load(row, index - xs - xs - 1),
            Load(row, index - xs),
            center,
            Load(row, index + xs),
            Load(row, index + xs + xs + 1),
            Load(row, index + xs3 + 1),
            Load(row, index + xs3 + xs + 1));
        retval += sum * sum;

        sum = Sum(
            Load(row, index - xs3 - xs + 1),
            Load(row, index - xs3 + 1),
            Load(row, index - xs - xs + 1),
            Load(row, index - xs),
            center,
            Load(row, index + xs),
            Load(row, index + xs + xs - 1),
            Load(row, index + xs3 - 1),
            Load(row, index + xs3 + xs - 1));
        retval += sum * sum;

        return retval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float PaddedMaltaUnit(
        JxlPlane<float> diffs,
        int x0,
        int y0,
        bool isLF)
    {
        if (x0 >= 4 &&
            y0 >= 4 &&
            x0 < diffs.XSize - 4 &&
            y0 < diffs.YSize - 4)
        {
            return isLF
                ? MaltaUnitLF(
                    diffs.GetRow(y0),
                    x0,
                    diffs.PixelsPerRow)[0]
                : MaltaUnit(
                    diffs.GetRow(y0),
                    x0,
                    diffs.PixelsPerRow)[0];
        }

        Span<float> borderImage = stackalloc float[12 * 9];

        for (int dy = 0; dy < 9; dy++)
        {
            int y = y0 + dy - 4;

            if (y < 0 || y >= diffs.YSize)
            {
                borderImage.Slice(dy * 12, 12).Clear();
                continue;
            }

            ReadOnlySpan<float> rowDiffs = diffs.GetRow(y);

            for (int dx = 0; dx < 9; dx++)
            {
                int x = x0 + dx - 4;

                borderImage[(dy * 12) + dx] =
                    x < 0 || x >= diffs.XSize
                        ? 0.0f
                        : rowDiffs[x];
            }

            borderImage.Slice((dy * 12) + 9, 3).Clear();
        }

        return isLF
                ? MaltaUnitLF(
                    diffs.GetRow(y0),
                    x0,
                    diffs.PixelsPerRow)[0]
                : MaltaUnit(
                    borderImage,
                    (4 * 12) + 4,
                    12)[0];
    }

    public static bool MaltaDiffMap(
        bool isLf,
        JxlPlane<float> lum0,
        JxlPlane<float> lum1,
        float w0Gt1,
        float w0Lt1,
        float norm1,
        JxlPlane<float> diffs,
        JxlPlane<float> blockDiffAc)
    {
        if (isLf)
        {
            const float len = 3.75f;
            const float mulli = 0.39905817637f;

            return MaltaDiffMap(true, lum0, lum1, w0Gt1, w0Lt1, norm1, len, mulli, diffs, blockDiffAc);
        }
        else
        {
            const float len = 3.75f;
            const float mulli = 0.611612573796f;

            return MaltaDiffMap(false, lum0, lum1, w0Gt1, w0Lt1, norm1, len, mulli, diffs, blockDiffAc);
        }
    }

    public static bool MaltaDiffMap(
        bool isLf,
        JxlPlane<float> lum0,
        JxlPlane<float> lum1,
        float w0Gt1,
        float w0Lt1,
        float norm1,
        float len,
        float mulli,
        JxlPlane<float> diffs,
        JxlPlane<float> blockDiffAc)
    {
        if (!JxlImageOperations.SameSize(lum0, lum1) || !JxlImageOperations.SameSize(lum0, diffs))
        {
            return false;
        }

        int width = lum0.XSize;
        int height = lum0.YSize;

        const float weight0 = 0.5f;
        const float weight1 = 0.33f;

        float norm2_0Gt1 =
            (float)(mulli * MathF.Sqrt(weight0 * w0Gt1) / ((len * 2) + 1) * norm1);

        float norm2_0Lt1 =
            (float)(mulli * MathF.Sqrt(weight1 * w0Lt1) / ((len * 2) + 1) * norm1);

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<float> row0 = lum0.GetRow(y);
            ReadOnlySpan<float> row1 = lum1.GetRow(y);
            Span<float> rowDiffs = diffs.GetRow(y);

            for (int x = 0; x < width; x++)
            {
                float absVal = 0.5f *
                    (MathF.Abs(row0[x]) + MathF.Abs(row1[x]));

                float diff = row0[x] - row1[x];

                float scaler = norm2_0Gt1 / ((float)norm1 + absVal);

                rowDiffs[x] = scaler * diff;

                float scaler2 = norm2_0Lt1 / ((float)norm1 + absVal);

                float fabs0 = MathF.Abs(row0[x]);

                float tooSmall = 0.55f * fabs0;
                float tooBig = 1.05f * fabs0;

                if (row0[x] < 0)
                {
                    if (row1[x] > -tooSmall)
                    {
                        rowDiffs[x] -= scaler2 * (row1[x] + tooSmall);
                    }
                    else if (row1[x] < -tooBig)
                    {
                        rowDiffs[x] += scaler2 * (-row1[x] - tooBig);
                    }
                }
                else
                {
                    if (row1[x] < tooSmall)
                    {
                        rowDiffs[x] += scaler2 * (tooSmall - row1[x]);
                    }
                    else if (row1[x] > tooBig)
                    {
                        rowDiffs[x] -= scaler2 * (row1[x] - tooBig);
                    }
                }
            }
        }

        int y0 = 0;

        for (; y0 < 4; y0++)
        {
            Span<float> row = blockDiffAc.GetRow(y0);

            for (int x = 0; x < width; x++)
            {
                row[x] += PaddedMaltaUnit(diffs, x, y0, isLf);
            }
        }

        int lanes = Vector<float>.Count;
        int alignedX = Math.Max(4, lanes);

        int stride = diffs.PixelsPerRow;

        for (; y0 < height - 4; y0++)
        {
            ReadOnlySpan<float> input = diffs.GetRow(y0);
            Span<float> output = blockDiffAc.GetRow(y0);
            ref float outputReference = ref MemoryMarshal.GetReference(output);

            int x = 0;

            for (; x < alignedX; x++)
            {
                output[x] += PaddedMaltaUnit(diffs, x, y0, isLf);
            }

            for (; x + lanes + 4 <= width; x += lanes)
            {
                Vector<float> value = Vector.LoadUnsafe(ref Unsafe.Add(ref outputReference, x));

                Vector<float> malta = isLf
                    ? MaltaUnitLF(input, x, stride)
                    : MaltaUnit(input, x, stride);

                (value + malta).CopyTo(output[x..]);
            }

            for (; x < width; x++)
            {
                output[x] += PaddedMaltaUnit(diffs, x, y0, isLf);
            }
        }

        for (; y0 < height; y0++)
        {
            Span<float> row = blockDiffAc.GetRow(y0);

            for (int x = 0; x < width; x++)
            {
                row[x] += PaddedMaltaUnit(diffs, x, y0, isLf);
            }
        }

        return true;
    }

    public static bool MaltaDiffMap(
        JxlImageF lum0,
        JxlImageF lum1,
        float w0Gt1,
        float w0Lt1,
        float norm1,
        JxlImageF diffs,
        JxlImageF blockDiffAc)
    {
        const float len = 3.75f;
        const float mulli = 0.39905817637f;

        return MaltaDiffMap(
            false,
            lum0,
            lum1,
            w0Gt1,
            w0Lt1,
            norm1,
            len,
            mulli,
            diffs,
            blockDiffAc);
    }

    public static bool MaltaDiffMapLf(
        JxlPlane<float> lum0,
        JxlPlane<float> lum1,
        float w0Gt1,
        float w0Lt1,
        float norm1,
        JxlPlane<float> diffs,
        JxlPlane<float> blockDiffAc)
    {
        const float len = 3.75f;
        const float mulli = 0.611612573796f;

        return MaltaDiffMap(
            true,
            lum0,
            lum1,
            w0Gt1,
            w0Lt1,
            norm1,
            len,
            mulli,
            diffs,
            blockDiffAc);
    }

    public static void CombineChannelsForMasking(
        InlineArray2<JxlPlane<float>> hf,
        InlineArray2<JxlPlane<float>> uhf,
        JxlPlane<float> output)
    {
        // Only X and Y components are involved in masking.
        ReadOnlySpan<float> muls =
        [
            2.5f,
            0.4f,
            0.4f
        ];

        int width = hf[0].XSize;
        int height = hf[0].YSize;

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<float> rowYHf = hf[1].GetRow(y);
            ReadOnlySpan<float> rowYUhf = uhf[1].GetRow(y);
            ReadOnlySpan<float> rowXHf = hf[0].GetRow(y);
            ReadOnlySpan<float> rowXUhf = uhf[0].GetRow(y);

            Span<float> row = output.GetRow(y);

            for (int x = 0; x < width; x++)
            {
                float xDiff = (rowXUhf[x] + rowXHf[x]) * muls[0];
                float yDiff = (rowYUhf[x] * muls[1]) + (rowYHf[x] * muls[2]);

                row[x] = MathF.Sqrt((xDiff * xDiff) + (yDiff * yDiff));
            }
        }
    }

    public static void DiffPrecompute(
        JxlImageF xyb,
        float mul,
        float biasArg,
        JxlImageF output)
    {
        int width = xyb.XSize;
        int height = xyb.YSize;

        float bias = mul * biasArg;
        float sqrtBias = MathF.Sqrt(bias);

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<float> rowIn = xyb.GetRow(y);
            Span<float> rowOut = output.GetRow(y);

            for (int x = 0; x < width; x++)
            {
                rowOut[x] =
                    MathF.Sqrt(
                        (mul * MathF.Abs(rowIn[x])) + bias)
                    - sqrtBias;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreMin3(
        float value,
        ref float min0,
        ref float min1,
        ref float min2)
    {
        if (value < min2)
        {
            if (value < min0)
            {
                min2 = min1;
                min1 = min0;
                min0 = value;
            }
            else if (value < min1)
            {
                min2 = min1;
                min1 = value;
            }
            else
            {
                min2 = value;
            }
        }
    }

    public static void FuzzyErosion(JxlImageF from, JxlImageF to)
    {
        int width = from.XSize;
        int height = from.YSize;

        const int step = 3;

        for (int y = 0; y < height; y++)
        {
            Span<float> output = to.GetRow(y);

            for (int x = 0; x < width; x++)
            {
                float min0 = from.GetRow(y)[x];
                float min1 = 2 * min0;
                float min2 = min1;

                if (x >= step)
                {
                    StoreMin3(
                        from.GetRow(y)[x - step],
                        ref min0,
                        ref min1,
                        ref min2);

                    if (y >= step)
                    {
                        StoreMin3(
                            from.GetRow(y - step)[x - step],
                            ref min0,
                            ref min1,
                            ref min2);
                    }

                    if (y < height - step)
                    {
                        StoreMin3(
                            from.GetRow(y + step)[x - step],
                            ref min0,
                            ref min1,
                            ref min2);
                    }
                }

                if (x < width - step)
                {
                    StoreMin3(
                        from.GetRow(y)[x + step],
                        ref min0,
                        ref min1,
                        ref min2);

                    if (y >= step)
                    {
                        StoreMin3(
                            from.GetRow(y - step)[x + step],
                            ref min0,
                            ref min1,
                            ref min2);
                    }

                    if (y < height - step)
                    {
                        StoreMin3(
                            from.GetRow(y + step)[x + step],
                            ref min0,
                            ref min1,
                            ref min2);
                    }
                }

                if (y >= step)
                {
                    StoreMin3(
                        from.GetRow(y - step)[x],
                        ref min0,
                        ref min1,
                        ref min2);
                }

                if (y < height - step)
                {
                    StoreMin3(
                        from.GetRow(y + step)[x],
                        ref min0,
                        ref min1,
                        ref min2);
                }

                output[x] =
                    (0.45f * min0) +
                    (0.3f * min1) +
                    (0.25f * min2);
            }
        }
    }

    public static bool Mask(
        Configuration configuration,
        JxlImageF mask0,
        JxlImageF mask1,
        ButteraugliBlurTemp blurTemp,
        JxlImageF? diffAc,
        out JxlImageF mask)
    {
        int width = mask0.XSize;
        int height = mask0.YSize;

        mask = new(configuration, width, height);

        const float mul = 6.19424080439f;
        const float bias = 12.61050594197f;
        const float radius = 2.7f;

        JxlImageF diff0 = new(configuration, width, height);
        JxlImageF diff1 = new(configuration, width, height);
        JxlImageF blurred0 = new(configuration, width, height);
        JxlImageF blurred1 = new(configuration, width, height);

        DiffPrecompute(mask0, mul, bias, diff0);
        DiffPrecompute(mask1, mul, bias, diff1);

        if (!Blur(configuration, diff0, radius, blurTemp, blurred0))
        {
            return false;
        }

        FuzzyErosion(blurred0, diff0);

        if (!Blur(configuration, diff1, radius, blurTemp, blurred1))
        {
            return false;
        }

        for (int y = 0; y < height; y++)
        {
            Span<float> maskRow = mask.GetRow(y);
            Span<float> diffRow = diffAc is not null ? diffAc.GetRow(y) : [];

            ReadOnlySpan<float> diff0Row = diff0.GetRow(y);
            ReadOnlySpan<float> blur0Row = blurred0.GetRow(y);
            ReadOnlySpan<float> blur1Row = blurred1.GetRow(y);

            for (int x = 0; x < width; x++)
            {
                maskRow[x] = diff0Row[x];

                if (diffRow.Length > 0)
                {
                    const float maskToErrorMul = 10.0f;
                    float diff = blur0Row[x] - blur1Row[x];
                    diffRow[x] += maskToErrorMul * diff * diff;
                }
            }
        }

        return true;
    }

    public static bool MaskButteraugliPsychoImage(
        Configuration configuration,
        ButteraugliPsychoImage pi0,
        ButteraugliPsychoImage pi1,
        int width,
        int height,
        ButteraugliBlurTemp blurTemp,
        JxlImageF mask,
        out JxlImageF? diffAc)
    {
        JxlImageF mask0 = new(configuration, width, height);
        JxlImageF mask1 = new(configuration, width, height);

        CombineChannelsForMasking(
            pi0.Hf,
            pi0.Uhf,
            mask0);

        CombineChannelsForMasking(
            pi1.Hf,
            pi1.Uhf,
            mask1);

        return Mask(
            configuration,
            mask0,
            mask1,
            blurTemp,
            mask,
            out diffAc);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MaskY(float delta)
    {
        const float offset = 0.829591754942f;
        const float scaler = 0.451936922203f;
        const float mul = 2.5485944793f;

        float c = mul / ((scaler * delta) + offset);
        float result = GlobalScale * (1.0f + c);

        return result * result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MaskDcY(float delta)
    {
        const float offset = 0.20025578522f;
        const float scaler = 3.87449418804f;
        const float mul = 0.505054525019f;

        float c = mul / ((scaler * delta) + offset);
        float result = GlobalScale * (1.0f + c);

        return result * result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MaskColor(
        ReadOnlySpan<float> color,
        float mask)
        => (color[0] * mask) + (color[1] * mask) + (color[2] * mask);

    public static bool CombineChannelsToDiffmap(
        JxlImageF mask,
        JxlImage3F blockDiffDc,
        JxlImage3<float> blockDiffAc,
        float xmul,
        JxlImageF result)
    {
        if (!JxlImageOperations.SameSize(mask, result))
        {
            return false;
        }

        Span<float> diffDc = stackalloc float[3];
        Span<float> diffAc = stackalloc float[3];

        int xsize = mask.XSize;
        int ysize = mask.YSize;

        for (int y = 0; y < ysize; ++y)
        {
            Span<float> rowOut = result.GetRow(y);

            for (int x = 0; x < xsize; ++x)
            {
                float val = mask.GetRow(y)[x];

                float maskVal = MaskY(val);
                float dcMaskVal = MaskDcY(val);

                for (int i = 0; i < 3; ++i)
                {
                    diffDc[i] = blockDiffDc.PlaneRow(i, y)[x];
                    diffAc[i] = blockDiffAc.PlaneRow(i, y)[x];
                }

                diffAc[0] *= xmul;
                diffDc[0] *= xmul;

                rowOut[x] = MathF.Sqrt(
                    MaskColor(diffDc, dcMaskVal) +
                    MaskColor(diffAc, maskVal));
            }
        }

        return true;
    }

    public static void L2Diff(
        JxlPlane<float> i0,
        JxlPlane<float> i1,
        float w,
        JxlPlane<float> diffmap)
    {
        if (w == 0)
        {
            return;
        }

        for (int y = 0; y < i0.YSize; ++y)
        {
            ReadOnlySpan<float> row0 = i0.GetRow(y);
            ReadOnlySpan<float> row1 = i1.GetRow(y);
            Span<float> rowDiff = diffmap.GetRow(y);

            for (int x = 0; x < i0.XSize; ++x)
            {
                float diff = row0[x] - row1[x];
                rowDiff[x] += diff * diff * w;
            }
        }
    }

    public static void SetL2Diff(
        JxlPlane<float> i0,
        JxlPlane<float> i1,
        float w,
        JxlPlane<float> diffmap)
    {
        if (w == 0)
        {
            return;
        }

        for (int y = 0; y < i0.YSize; ++y)
        {
            ReadOnlySpan<float> row0 = i0.GetRow(y);
            ReadOnlySpan<float> row1 = i1.GetRow(y);
            Span<float> rowDiff = diffmap.GetRow(y);

            for (int x = 0; x < i0.XSize; ++x)
            {
                float diff = row0[x] - row1[x];
                rowDiff[x] = diff * diff * w;
            }
        }
    }

    public static void L2DiffAsymmetric(
        JxlPlane<float> i0,
        JxlPlane<float> i1,
        float w0gt1,
        float w0lt1,
        JxlPlane<float> diffmap)
    {
        if (w0gt1 == 0 && w0lt1 == 0)
        {
            return;
        }

        Vector<float> vw0gt1 = new(w0gt1 * 0.8f);
        Vector<float> vw0lt1 = new(w0lt1 * 0.8f);

        int lanes = Vector<float>.Count;

        for (int y = 0; y < i0.YSize; ++y)
        {
            ReadOnlySpan<float> row0 = i0.GetRow(y);
            ReadOnlySpan<float> row1 = i1.GetRow(y);
            Span<float> rowDiff = diffmap.GetRow(y);

            for (int x = 0; x < i0.XSize; x += lanes)
            {
                Vector<float> val0 = new(row0[x..]);
                Vector<float> val1 = new(row1[x..]);

                // Primary symmetric quadratic objective.
                Vector<float> diff = val0 - val1;

                Vector<float> total =
                    (diff * diff * vw0gt1) + new Vector<float>(rowDiff[x..]);

                Vector<float> fabs0 = Vector.Abs(val0);

                Vector<float> tooSmall = fabs0 * new Vector<float>(0.4f);

                Vector<float> tooBig = fabs0;

                Vector<float> ifNeg =
                    Vector.ConditionalSelect(
                        Vector.GreaterThan(val1, -tooSmall),
                        val1 + tooSmall,
                        Vector.ConditionalSelect(
                            Vector.LessThan(val1, -tooBig),
                            -val1 - tooBig,
                            Vector<float>.Zero));

                Vector<float> ifPos =
                    Vector.ConditionalSelect(
                        Vector.LessThan(val1, tooSmall),
                        tooSmall - val1,
                        Vector.ConditionalSelect(
                            Vector.GreaterThan(val1, tooBig),
                            val1 - tooBig,
                            Vector<float>.Zero));

                Vector<float> v =
                    Vector.ConditionalSelect(
                        Vector.LessThan(val0, Vector<float>.Zero),
                        ifNeg,
                        ifPos);

                total += vw0lt1 * v * v;

                total.CopyTo(rowDiff[x..]);
            }
        }
    }

    public static void OpsinAbsorbance(
        bool clamp,
        Vector<float> in0,
        Vector<float> in1,
        Vector<float> in2,
        out Vector<float> out0,
        out Vector<float> out1,
        out Vector<float> out2)
    {
        Vector<float> mix0 = new(0.29956550340058319f);
        Vector<float> mix1 = new(0.63373087833825936f);
        Vector<float> mix2 = new(0.077705617820981968f);
        Vector<float> mix3 = new(1.7557483643287353f);

        Vector<float> mix4 = new(0.22158691104574774f);
        Vector<float> mix5 = new(0.69391388044116142f);
        Vector<float> mix6 = new(0.0987313588422f);
        Vector<float> mix7 = new(1.7557483643287353f);

        Vector<float> mix8 = new(0.02f);
        Vector<float> mix9 = new(0.02f);
        Vector<float> mix10 = new(0.20480129041026129f);
        Vector<float> mix11 = new(12.226454707163354f);

        out0 = (mix0 * in0) + ((mix1 * in1) + ((mix2 * in2) + mix3));
        out1 = (mix4 * in0) + ((mix5 * in1) + ((mix6 * in2) + mix7));
        out2 = (mix8 * in0) + ((mix9 * in1) + ((mix10 * in2) + mix11));

        if (clamp)
        {
            out0 = Vector.Max(out0, mix3);
            out1 = Vector.Max(out1, mix7);
            out2 = Vector.Max(out2, mix11);
        }
    }

    // A simple HDR-compatible gamma function
    private static Vector<float> Gamma(Vector<float> v)
    {
        Vector<float> kRetMul = Vector.Create(19.245013259874995f * JxlMath.InverseLog2E);
        Vector<float> kRetAdd = Vector.Create(-23.16046239805755f);

        // if (value < 0) value = 0;
        v = Vector.ConditionalSelect(Vector.LessThan(v, Vector<float>.Zero), Vector<float>.Zero, v);

        Vector<float> biased = v + Vector.Create(9.9710635769299145f);
        Vector<float> log = Vector.Log2(biased);

        return (kRetMul * log) + kRetAdd;
    }

    public static bool OpsinDynamicsImage(
        Configuration configuration,
        JxlImage3F rgb,
        in ButteraugliParameters parameters,
        JxlImage3F blurred,
        ButteraugliBlurTemp blurTemp,
        JxlImage3F xyb)
    {
        if (blurred == null)
        {
            return false;
        }

        const float sigma = 1.2f;

        if (!Blur(configuration, rgb.Plane(0), sigma, blurTemp, blurred.Plane(0)))
        {
            return false;
        }

        if (!Blur(configuration, rgb.Plane(1), sigma, blurTemp, blurred.Plane(1)))
        {
            return false;
        }

        if (!Blur(configuration, rgb.Plane(2), sigma, blurTemp, blurred.Plane(2)))
        {
            return false;
        }

        Vector<float> intensityMultiplier = new((float)parameters.IntensityTarget);

        int lanes = Vector<float>.Count;

        Vector<float> minValue = new(1e-4f);

        for (int y = 0; y < rgb.YSize; ++y)
        {
            ReadOnlySpan<float> rowR = rgb.PlaneRow(0, y);
            ReadOnlySpan<float> rowG = rgb.PlaneRow(1, y);
            ReadOnlySpan<float> rowB = rgb.PlaneRow(2, y);

            ReadOnlySpan<float> blurredR = blurred.PlaneRow(0, y);
            ReadOnlySpan<float> blurredG = blurred.PlaneRow(1, y);
            ReadOnlySpan<float> blurredB = blurred.PlaneRow(2, y);

            Span<float> outX = xyb.PlaneRow(0, y);
            Span<float> outY = xyb.PlaneRow(1, y);
            Span<float> outB = xyb.PlaneRow(2, y);

            for (int x = 0; x < rgb.XSize; x += lanes)
            {
                Vector<float> sensitivity0;
                Vector<float> sensitivity1;
                Vector<float> sensitivity2;
                {
                    OpsinAbsorbance(
                        true,
                        new Vector<float>(blurredR[x..]) * intensityMultiplier,
                        new Vector<float>(blurredG[x..]) * intensityMultiplier,
                        new Vector<float>(blurredB[x..]) * intensityMultiplier,
                        out Vector<float> pre0,
                        out Vector<float> pre1,
                        out Vector<float> pre2);

                    pre0 = Vector.Max(pre0, minValue);
                    pre1 = Vector.Max(pre1, minValue);
                    pre2 = Vector.Max(pre2, minValue);

                    sensitivity0 = Gamma(pre0) / pre0;
                    sensitivity1 = Gamma(pre1) / pre1;
                    sensitivity2 = Gamma(pre2) / pre2;

                    sensitivity0 = Vector.Max(sensitivity0, minValue);
                    sensitivity1 = Vector.Max(sensitivity1, minValue);
                    sensitivity2 = Vector.Max(sensitivity2, minValue);
                }

                OpsinAbsorbance(
                    false,
                    new Vector<float>(rowR[x..]) * intensityMultiplier,
                    new Vector<float>(rowG[x..]) * intensityMultiplier,
                    new Vector<float>(rowB[x..]) * intensityMultiplier,
                    out Vector<float> cur0,
                    out Vector<float> cur1,
                    out Vector<float> cur2);

                cur0 *= sensitivity0;
                cur1 *= sensitivity1;
                cur2 *= sensitivity2;

                Vector<float> min01 = new(1.7557483643287353f);
                Vector<float> min2 = new(12.226454707163354f);

                cur0 = Vector.Max(cur0, min01);
                cur1 = Vector.Max(cur1, min01);
                cur2 = Vector.Max(cur2, min2);

                (cur0 - cur1).CopyTo(outX[x..]);
                (cur0 + cur1).CopyTo(outY[x..]);
                cur2.CopyTo(outB[x..]);
            }
        }

        return true;
    }

    public static bool ButteraugliDiffmapInPlace(
        Configuration configuration,
        JxlImage3F image0,
        JxlImage3F image1,
        in ButteraugliParameters parameters,
        JxlImageF diffmap)
    {
        int xSize = image0.XSize;
        int ySize = image0.YSize;

        using ButteraugliBlurTemp blurTemp = new();

        using (JxlImage3F temp = new(configuration, xSize, ySize))
        {
            if (!OpsinDynamicsImage(configuration, image0, parameters, temp, blurTemp, image0))
            {
                return false;
            }

            if (!OpsinDynamicsImage(configuration, image1, parameters, temp, blurTemp, image1))
            {
                return false;
            }
        }

        using JxlPlane<float> blockDiffDc = JxlImageF.Create(configuration, xSize, ySize);
        blockDiffDc.Clear();

        // LF/DC
        using (JxlImage3F lf0 = new(configuration, xSize, ySize))
        using (JxlImage3F lf1 = new(configuration, xSize, ySize))
        {
            if (!SeparateLFAndMF(configuration, image0, lf0, image0, blurTemp))
            {
                return false;
            }

            if (!SeparateLFAndMF(configuration, image1, lf1, image1, blurTemp))
            {
                return false;
            }

            for (int c = 0; c < 3; c++)
            {
                L2Diff(
                    lf0.Plane(c),
                    lf1.Plane(c),
                    Wmul[6 + c],
                    blockDiffDc);
            }
        }

        InlineArray2<JxlPlane<float>> hf0 = default;
        InlineArray2<JxlPlane<float>> hf1 = default;

        if (!SeparateMfAndHf(configuration, image0, ref hf0, blurTemp))
        {
            return false;
        }

        if (!SeparateMfAndHf(configuration, image1, ref hf1, blurTemp))
        {
            return false;
        }

        using JxlImageF blockDiffAc = new(configuration, xSize, ySize);
        blockDiffAc.Clear();

        using (JxlImageF diffs = new(configuration, xSize, ySize))
        {
            if (!MaltaDiffMap(
                    true,
                    image0.Plane(1),
                    image1.Plane(1),
                    WMfMalta,
                    WMfMalta,
                    Norm1Mf,
                    diffs,
                    blockDiffAc))
            {
                return false;
            }

            if (!MaltaDiffMap(
                    true,
                    image0.Plane(0),
                    image1.Plane(0),
                    WMfMaltaX,
                    WMfMaltaX,
                    Norm1MfX,
                    diffs,
                    blockDiffAc))
            {
                return false;
            }
        }

        for (int c = 0; c < 3; c++)
        {
            L2Diff(
                image0.Plane(c),
                image1.Plane(c),
                Wmul[3 + c],
                blockDiffAc);
        }

        // Free MF images
        image0.Dispose();
        image1.Dispose();

        InlineArray2<JxlPlane<float>> uhf0 = default;
        InlineArray2<JxlPlane<float>> uhf1 = default;

        if (!SeparateHFAndUHF(configuration, hf0, uhf0, blurTemp))
        {
            return false;
        }

        if (!SeparateHFAndUHF(configuration, hf1, uhf1, blurTemp))
        {
            return false;
        }

        float hfAsymmetry = parameters.HfAsymmetry;

        using (JxlImageF diffs = new(configuration, xSize, ySize))
        {
            _ = MaltaDiffMap(
                false,
                uhf0[1],
                uhf1[1],
                WUhfMalta * hfAsymmetry,
                WUhfMalta / hfAsymmetry,
                Norm1Uhf,
                diffs,
                blockDiffAc);

            _ = MaltaDiffMap(
                false,
                uhf0[0],
                uhf1[0],
                WUhfMaltaX * hfAsymmetry,
                WUhfMaltaX / hfAsymmetry,
                Norm1UhfX,
                diffs,
                blockDiffAc);

            float sqrtAsym = MathF.Sqrt(hfAsymmetry);

            _ = MaltaDiffMap(
                true,
                hf0[1],
                hf1[1],
                WHfMalta * sqrtAsym,
                WHfMalta / sqrtAsym,
                Norm1Hf,
                diffs,
                blockDiffAc);

            _ = MaltaDiffMap(
                true,
                hf0[0],
                hf1[0],
                WHfMaltaX * sqrtAsym,
                WHfMaltaX / sqrtAsym,
                Norm1HfX,
                diffs,
                blockDiffAc);
        }

        for (int c = 0; c < 2; c++)
        {
            L2DiffAsymmetric(
                hf0[c],
                hf1[c],
                Wmul[c] * hfAsymmetry,
                Wmul[c] / hfAsymmetry,
                blockDiffAc);
        }

        // Mask
        using JxlImageF mask = new(configuration, xSize, ySize);
        using JxlImageF mask0 = new(configuration, xSize, ySize);
        using JxlImageF mask1 = new(configuration, xSize, ySize);

        CombineChannelsForMasking(hf0, uhf0, mask0);
        CombineChannelsForMasking(hf1, uhf1, mask1);

        DeallocateHFAndUHF(hf0, uhf0);
        DeallocateHFAndUHF(hf1, uhf1);

        if (!Mask(configuration, mask0, mask1, blurTemp, mask, out JxlImageF newBlockDiffAc))
        {
            return false;
        }

        for (int y = 0; y < ySize; y++)
        {
            ReadOnlySpan<float> dc = newBlockDiffAc.GetRow(y);
            ReadOnlySpan<float> ac = newBlockDiffAc.GetRow(y);
            Span<float> output = diffmap.GetRow(y);
            ReadOnlySpan<float> maskRow = mask.GetRow(y);

            for (int x = 0; x < xSize; x++)
            {
                float m = maskRow[x];

                output[x] = MathF.Sqrt((dc[x] * (float)MaskDcY(m)) + (ac[x] * (float)MaskY(m)));
            }
        }

        return true;
    }

    // Calculate a 2x2 subsampled image for purposes of recursive butteraugli at
    // multiresolution.
    public static JxlImage3F SubSample2x(Configuration configuration, JxlImage3F input)
    {
        int xs = (input.XSize + 1) / 2;
        int ys = (input.YSize + 1) / 2;

        JxlImage3F retval = new(configuration, xs, ys);

        for (int c = 0; c < 3; ++c)
        {
            for (int y = 0; y < ys; ++y)
            {
                for (int x = 0; x < xs; ++x)
                {
                    retval.PlaneRow(c, y)[x] = 0.0f;
                }
            }
        }

        for (int c = 0; c < 3; ++c)
        {
            for (int y = 0; y < input.YSize; ++y)
            {
                ReadOnlySpan<float> srcRow = input.PlaneRow(c, y);

                for (int x = 0; x < input.XSize; ++x)
                {
                    retval.PlaneRow(c, y / 2)[x / 2] += 0.25f * srcRow[x];
                }
            }

            if ((input.XSize & 1) != 0)
            {
                for (int y = 0; y < retval.YSize; ++y)
                {
                    int lastColumn = retval.XSize - 1;
                    retval.PlaneRow(c, y)[lastColumn] *= 2.0f;
                }
            }

            if ((input.YSize & 1) != 0)
            {
                for (int x = 0; x < retval.XSize; ++x)
                {
                    int lastRow = retval.YSize - 1;
                    retval.PlaneRow(c, lastRow)[x] *= 2.0f;
                }
            }
        }

        return retval;
    }

    public static void AddSupersampled2x(JxlImageF src, float w, JxlImageF dest)
    {
        const float heuristicMixingValue = 0.3f;

        for (int y = 0; y < dest.YSize; ++y)
        {
            Span<float> destRow = dest.GetRow(y);

            for (int x = 0; x < dest.XSize; ++x)
            {
                destRow[x] *= 1.0f - (heuristicMixingValue * w);
                destRow[x] += w * src.GetRow(y / 2)[x / 2];
            }
        }
    }

    private static void ScoreToRgb(float score, float goodThreshold, float badThreshold, ref InlineArray3<float> rgb)
    {
        if (score < goodThreshold)
        {
            score = (score / goodThreshold) * 0.3f;
        }
        else if (score < badThreshold)
        {
            score = 0.3f + ((score - goodThreshold) / (badThreshold - goodThreshold) * 0.15f);
        }
        else
        {
            score = 0.45f + ((score - badThreshold) / (badThreshold * 12) * 0.5f);
        }

        int tableSize = HeatmapData.GetLength(0);
        score = Math.Clamp(score * (tableSize - 1), 0f, tableSize - 2);

        int ix = (int)score;
        ix = Math.Clamp(ix, 0, tableSize - 2); // handle NaN
        float mix = score - ix;

        for (int i = 0; i < 3; ++i)
        {
            float v = (mix * Heatmap[ix + 1, i]) + ((1 - mix) * Heatmap[ix, i]);
            rgb[i] = MathF.Pow(v, 0.5f);
        }
    }

    public static JxlImage3F CreateHeatMapImage(Configuration configuration, JxlImageF distmap, float goodThreshold, float badThreshold)
    {
        // Do not dispose (this is the return value; it is caller's responsibility to dispose this
        // or keep it)
        JxlImage3F heatmap = new(configuration, distmap.XSize, distmap.YSize);

        for (int y = 0; y < distmap.YSize; y++)
        {
            Span<float> row_distmap = distmap.GetRow(y);
            Span<float> row_h0 = heatmap.PlaneRow(0, y);
            Span<float> row_h1 = heatmap.PlaneRow(1, y);
            Span<float> row_h2 = heatmap.PlaneRow(2, y);

            for (int x = 0; x < distmap.XSize; ++x)
            {
                float d = row_distmap[x];
                InlineArray3<float> rgb = default;
                ScoreToRgb(d, goodThreshold, badThreshold, ref rgb);
                row_h0[x] = rgb[0];
                row_h1[x] = rgb[1];
                row_h2[x] = rgb[2];
            }
        }

        return heatmap;
    }

    public static float ButteraugliFuzzyInverse(float seek)
    {
        float pos = 0f;

        for (float range = 1.0f; range >= 1e-10f; range *= 0.5f)
        {
            float cur = ButteraugliFuzzyClass(pos);

            if (cur < seek)
            {
                pos -= range;
            }
            else
            {
                pos += range;
            }
        }

        // Normalization (pos) can be printed if seek == 1.0, for example
        // when debugging.
        return pos;
    }

    public static float ButteraugliFuzzyClass(float score)
    {
        const float fuzzyWidthUp = 4.8f;
        const float fuzzyWidthDown = 4.8f;
        const float m0 = 2.0f;
        const float scaler = 0.7777f;

        float val;

        if (score < 1.0)
        {
            val = m0 / (1.0f + MathF.Exp((score - 1.0f) * fuzzyWidthDown));
            val -= 1.0f;
            val *= 2.0f - scaler;
            val += scaler;
        }
        else
        {
            val = m0 / (1.0f + MathF.Exp((score - 1.0f) * fuzzyWidthUp));
            val *= scaler;
        }

        return val;
    }

    public static bool ButteraugliInterfaceInPlace(Configuration configuration, JxlImage3F rgb0, JxlImage3F rgb1, ButteraugliParameters parameters, JxlImageF diffmap, out double diffvalue)
    {
        diffvalue = 0;

        int xsize = rgb0.XSize;
        int ysize = rgb0.YSize;

        if ((xsize & ysize) == 0) // equivalent to xsize == 0 || ysize == 0, minus one branch
        {
            throw new InvalidOperationException("Zero-sized image");
        }

        if (!JxlImageOperations.SameSize(rgb0, rgb1))
        {
            throw new InvalidOperationException("Size mismatch");
        }

        const int max = 8;

        if (xsize < max || ysize < max)
        {
            bool ok = ButteraugliDiffmapSmall(configuration, max, rgb0, rgb1, parameters, out diffmap);
            diffvalue = ButteraugliScoreFromDiffmap(diffmap, parameters);
            return ok;
        }

        JxlImageF subDiffmap = new();

        if (xsize >= 15 && ysize >= 15)
        {
            using JxlImage3F rgb0Sub = SubSample2x(configuration, rgb0);
            using JxlImage3F rgb1Sub = SubSample2x(configuration, rgb1);

            if (!ButteraugliDiffmapInPlace(configuration, rgb0Sub, rgb1Sub, parameters, diffmap))
            {
                return false;
            }
        }

        if (!ButteraugliDiffmapInPlace(configuration, rgb0, rgb1, parameters, diffmap))
        {
            return false;
        }

        if (xsize >= 15 && ysize >= 15)
        {
            AddSupersampled2x(subDiffmap, 0.5f, diffmap);
        }

        diffvalue = ButteraugliScoreFromDiffmap(diffmap, parameters);
        return true;
    }

    public static bool ButteraugliInterface(Configuration configuration, JxlImage3F rgb0, JxlImage3F rgb1, ButteraugliParameters parameters, JxlImageF diffmap, out double diffValue)
    {
        diffValue = 0;

        if (!ButteraugliDiffmap(configuration, rgb0, rgb1, parameters, out diffmap))
        {
            return false;
        }

        diffValue = ButteraugliScoreFromDiffmap(diffmap, parameters);
        return true;
    }

    public static bool ButteraugliInterface(Configuration configuration, JxlImage3F rgb0, JxlImage3F rgb1, float hfAsymmetry, float xmul, JxlImageF diffmap, out double diffValue)
    {
        ButteraugliParameters parameters = new()
        {
            HfAsymmetry = hfAsymmetry,
            XMultiplier = xmul
        };

        return ButteraugliInterface(configuration, rgb0, rgb1, parameters, diffmap, out diffValue);
    }

    public static bool ButteraugliDiffmap(Configuration configuration, JxlImage3F rgb0, JxlImage3F rgb1, ButteraugliParameters parameters, out JxlImageF diffmap)
    {
        diffmap = new();

        int xsize = rgb0.XSize;
        int ysize = rgb0.YSize;

        if ((xsize & ysize) == 0) // equivalent to xsize == 0 || ysize == 0, minus one branch
        {
            throw new InvalidOperationException("Zero-sized image");
        }

        if (!JxlImageOperations.SameSize(rgb0, rgb1))
        {
            throw new InvalidOperationException("Size mismatch");
        }

        const int max = 8;

        if (xsize < max || ysize < max)
        {
            if (!ButteraugliDiffmapSmall(configuration, max, rgb0, rgb1, parameters, out diffmap))
            {
                return false;
            }
        }

        ButteraugliComparator butteraugli = ButteraugliComparator.Make(configuration, rgb0, parameters);

        return butteraugli.Diffmap(configuration, rgb1, diffmap);
    }

    public static bool ButteraugliDiffmapSmall(Configuration configuration, int max, JxlImage3F rgb0, JxlImage3F rgb1, ButteraugliParameters parameters, out JxlImageF diffmap)
    {
        int xsize = rgb0.XSize;
        int ysize = rgb0.YSize;

        int xborder = xsize < max ? (max - xsize) / 2 : 0;
        int yborder = ysize < max ? (max - ysize) / 2 : 0;
        int xscaled = Math.Max(max, xsize);
        int yscaled = Math.Max(max, ysize);

        using JxlImage3F scaled0 = new(configuration, xscaled, yscaled);
        using JxlImage3F scaled1 = new(configuration, xscaled, yscaled);

        for (int i = 0; i < 3; ++i)
        {
            for (int y = 0; y < yscaled; y++)
            {
                for (int x = 0; x < xscaled; x++)
                {
                    int x2 = Math.Min(xsize - 1, x > xborder ? x - xborder : 0);
                    int y2 = Math.Min(ysize - 1, y > yborder ? y - yborder : 0);

                    scaled0.PlaneRow(i, y)[x] = rgb0.PlaneRow(i, y2)[x2];
                    scaled1.PlaneRow(i, y)[x] = rgb1.PlaneRow(i, y2)[x2];
                }
            }
        }

        JxlImageF diffmapScaled = new();
        bool ok = ButteraugliDiffmap(configuration, scaled0, scaled1, parameters, out diffmapScaled);

        diffmap = new(configuration, xsize, ysize);

        for (int y = 0; y < ysize; y++)
        {
            Span<float> diffmapRow = diffmap.GetRow(y);
            Span<float> diffmapScaledRow = diffmapScaled.GetRow(y + yborder);

            for (int x = 0; x < xsize; x++)
            {
                diffmapRow[x] = diffmapScaledRow[x + xborder];
            }
        }

        return ok;
    }

    public static float ButteraugliScoreFromDiffmap(JxlImageF diffmap, ButteraugliParameters parameters)
    {
        float retval = 0.0f;

        for (int y = 0; y < diffmap.YSize; ++y)
        {
            Span<float> row = diffmap.GetRow(y);
            for (int x = 0; x < diffmap.XSize; ++x)
            {
                retval = Math.Max(retval, row[x]);
            }
        }

        return retval;
    }

    public static bool MaltaDiffMap(JxlPlane<float> lum0, JxlPlane<float> lum1, float w0Gt1, float w0Lt1, float norm1, JxlPlane<float> diffs, JxlImage3<float> blockDiffAc, int c)
        => MaltaDiffMap(isLf: false, lum0, lum1, w0Gt1, w0Lt1, norm1, diffs, blockDiffAc.Plane(c));

    public static bool MaltaDiffMapLf(JxlPlane<float> lum0, JxlPlane<float> lum1, float w0Gt1, float w0Lt1, float norm1, JxlPlane<float> diffs, JxlImage3<float> blockDiffAc, int c)
        => MaltaDiffMap(isLf: true, lum0, lum1, w0Gt1, w0Lt1, norm1, diffs, blockDiffAc.Plane(c));
}
