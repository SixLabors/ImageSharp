// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
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

    /// <summary>
    /// The offset in the high-frequency masking denominator.
    /// </summary>
    private const float MaskYOffset = 0.829591754942F;

    /// <summary>
    /// The scale in the high-frequency masking denominator.
    /// </summary>
    private const float MaskYScaler = 0.451936922203F;

    /// <summary>
    /// The numerator in the high-frequency masking response.
    /// </summary>
    private const float MaskYMultiplier = 2.5485944793F;

    /// <summary>
    /// The offset in the low-frequency masking denominator.
    /// </summary>
    private const float MaskDcYOffset = 0.20025578522F;

    /// <summary>
    /// The scale in the low-frequency masking denominator.
    /// </summary>
    private const float MaskDcYScaler = 3.87449418804F;

    /// <summary>
    /// The numerator in the low-frequency masking response.
    /// </summary>
    private const float MaskDcYMultiplier = 0.505054525019F;

    // These five Gaussian kernels are fixed by the comparison equations. Build them once, outside repeated comparisons.
    private static readonly float[] OpsinDynamicsKernel = ComputeKernel(1.2F);
    private static readonly float[] LowFrequencyKernel = ComputeKernel(7.15593339443F);
    private static readonly float[] HighFrequencyKernel = ComputeKernel(3.22489901262F);
    private static readonly float[] UltraHighFrequencyKernel = ComputeKernel(1.56416327805F);
    private static readonly float[] MaskingKernel = ComputeKernel(2.7F);

    /// <summary>
    /// The normalized five-tap weights used by the mirrored single-scan Opsin blur.
    /// </summary>
    private static readonly JxlWeightsSeparable5 OpsinBlurWeights;

    /// <summary>
    /// Prepares the five-tap weights once using the native Gaussian normalization and replicated coefficient layout.
    /// </summary>
    static Butteraugli()
    {
        float weightSum = 0F;
        foreach (float weight in OpsinDynamicsKernel)
        {
            weightSum += weight;
        }

        float scale = 1F / weightSum;
        float center = OpsinDynamicsKernel[2] * scale;
        float near = OpsinDynamicsKernel[1] * scale;
        float far = OpsinDynamicsKernel[0] * scale;
        JxlWeightsSeparable5 weights = default;

        // The shared convolution stores each coefficient four times for native SIMD broadcasts.
        for (int i = 0; i < 4; i++)
        {
            weights.Horizontal[i] = weights.Vertical[i] = center;
            weights.Horizontal[4 + i] = weights.Vertical[4 + i] = near;
            weights.Horizontal[8 + i] = weights.Vertical[8 + i] = far;
        }

        OpsinBlurWeights = weights;
    }

    /// <summary>
    /// Builds one fixed Gaussian kernel without normalizing its border weights.
    /// </summary>
    /// <param name="sigma">The standard deviation fixed by the comparison stage.</param>
    /// <returns>The symmetric samples from minus to plus two-and-one-quarter standard deviations.</returns>
    private static float[] ComputeKernel(float sigma)
    {
        const float m = 2.25f;  // Accuracy increases when m is increased
        float scaler = -1.0f / (2.0f * sigma * sigma);
        int diff = Math.Max(1, (int)(m * MathF.Abs(sigma)));

        float[] kernel = new float[(2 * diff) + 1];

        for (int i = -diff; i <= diff; i++)
        {
            kernel[i + diff] = MathF.Exp(scaler * i * i);
        }

        return kernel;
    }

    /// <summary>
    /// Convolves one border column, normalizing only the Gaussian weights whose source samples exist.
    /// </summary>
    /// <param name="input">The complete borrowed source storage.</param>
    /// <param name="width">The logical source width.</param>
    /// <param name="height">The logical source height.</param>
    /// <param name="stride">The physical source row stride.</param>
    /// <param name="kernel">The fixed symmetric Gaussian coefficients.</param>
    /// <param name="x">The source column to convolve.</param>
    /// <param name="rowOut">The destination row in the transposed output.</param>
    public static void ConvolveBorderColumn(ReadOnlySpan<float> input, int width, int height, int stride, ReadOnlySpan<float> kernel, int x, Span<float> rowOut)
    {
        int offset = kernel.Length / 2;

        int minX = x < offset ? 0 : x - offset;
        int maxX = Math.Min(width - 1, x + offset);

        float weight = 0.0f;

        for (int j = minX; j <= maxX; j++)
        {
            weight += kernel[j - x + offset];
        }

        float scale = 1.0f / weight;

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<float> rowIn = input[(y * stride)..];

            float sum = 0.0f;

            for (int j = minX; j <= maxX; j++)
            {
                sum += rowIn[j] * kernel[j - x + offset];
            }

            rowOut[y] = sum * scale;
        }
    }

    /// <summary>
    /// Applies the native truncated Gaussian FIR and transposes its output into prepared storage.
    /// </summary>
    /// <param name="input">The borrowed source plane.</param>
    /// <param name="kernel">The fixed symmetric Gaussian coefficients.</param>
    /// <param name="output">The distinct preallocated output with width and height exchanged.</param>
    /// <returns>Whether the output has the required transposed dimensions.</returns>
    public static bool ConvolutionWithTranspose(JxlPlane<float> input, ReadOnlySpan<float> kernel, JxlPlane<float> output)
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

        ReadOnlySpan<float> inputSamples = input.GetRow(0);
        Span<float> outputSamples = output.GetRow(0);

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
                    ReadOnlySpan<float> rowIn = inputSamples[(y * input.PixelsPerRow)..];

                    for (int x = border1; x < border2; x++)
                    {
                        int i = x - border1;

                        float sum0 = (rowIn[i + 0] + rowIn[i + 6]) * sk0;
                        float sum1 = (rowIn[i + 1] + rowIn[i + 5]) * sk1;
                        float sum2 = (rowIn[i + 2] + rowIn[i + 4]) * sk2;
                        float sum = (rowIn[i + 3] * sk3) + sum0 + sum1 + sum2;

                        outputSamples[(x * output.PixelsPerRow) + y] = sum;
                    }
                }

                break;
            }

            case 13:
            {
                for (int y = 0; y < input.YSize; y++)
                {
                    ReadOnlySpan<float> rowIn = inputSamples[(y * input.PixelsPerRow)..];

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

                        outputSamples[(x * output.PixelsPerRow) + y] = sum + sum0 + sum1 + sum2 + sum3;
                    }
                }

                break;
            }

            case 15:
            {
                for (int y = 0; y < input.YSize; y++)
                {
                    ReadOnlySpan<float> rowIn = inputSamples[(y * input.PixelsPerRow)..];

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

                        outputSamples[(x * output.PixelsPerRow) + y] = sum + sum0 + sum1 + sum2 + sum3;
                    }
                }

                break;
            }

            case 33:
            {
                for (int y = 0; y < input.YSize; y++)
                {
                    ReadOnlySpan<float> rowIn = inputSamples[(y * input.PixelsPerRow)..];

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

                        outputSamples[(x * output.PixelsPerRow) + y] = sum + sum0 + sum1 + sum2 + sum3;
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
            ConvolveBorderColumn(inputSamples, input.XSize, input.YSize, input.PixelsPerRow, kernel, x, outputSamples[(x * output.PixelsPerRow)..]);
        }

        // Right border
        for (int x = border2; x < input.XSize; x++)
        {
            ConvolveBorderColumn(inputSamples, input.XSize, input.YSize, input.PixelsPerRow, kernel, x, outputSamples[(x * output.PixelsPerRow)..]);
        }

        return true;
    }

    /// <summary>
    /// Applies a separable Gaussian blur using prepared storage and the native border rule for its fixed kernel.
    /// </summary>
    /// <param name="input">The borrowed source samples.</param>
    /// <param name="kernel">The fixed, symmetric kernel for this stage.</param>
    /// <param name="temp">The prepared transposed scratch plane, distinct from input and output.</param>
    /// <param name="output">The preallocated result plane, distinct from input for the five-tap Opsin kernel.</param>
    /// <returns>Whether both passes completed with matching dimensions.</returns>
    /// <remarks>Other kernels can update input in place because their first pass writes only transposed scratch.</remarks>
    private static bool Blur(JxlPlane<float> input, ReadOnlySpan<float> kernel, ButteraugliBlurTemp temp, JxlPlane<float> output)
    {
        if (kernel.Length == 5)
        {
            // The native five-tap path mirrors edges and combines both dimensions in one scan.
            // Other kernels use the truncated, renormalized FIR below; these border rules are intentionally different.
            return JxlConvolve.Separable5(input, input.GetRectangle(), OpsinBlurWeights, output);
        }

        // The first pass writes only transposed scratch. The second pass can therefore overwrite the original input safely.
        JxlPlane<float> transposed = temp.TransposedTemp;
        return ConvolutionWithTranspose(input, kernel, transposed) && ConvolutionWithTranspose(transposed, kernel, output);
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

    /// <summary>
    /// Converts low-frequency XYB planes to the weighted values used by the comparison equations.
    /// </summary>
    /// <param name="xybLf">The preallocated low-frequency image, updated in place without changing row padding.</param>
    public static void XybLowFrequencyToValues(JxlImage3<float> xybLf)
    {
        // These are the native Butteraugli low-frequency weights. B first removes its correlated Y contribution,
        // then applies its own weight. X and Y receive independent weights for the later squared differences.
        const float xMultiplier = 33.832837186260F;
        const float yMultiplier = 14.458268100570F;
        const float bMultiplier = 49.87984651440F;
        const float yToBMultiplier = -0.362267051518F;
        JxlPlane<float> planeX = xybLf.Plane(0);
        JxlPlane<float> planeY = xybLf.Plane(1);
        JxlPlane<float> planeB = xybLf.Plane(2);
        Span<float> samplesX = planeX.GetRow(0);
        Span<float> samplesY = planeY.GetRow(0);
        Span<float> samplesB = planeB.GetRow(0);
        int strideX = planeX.PixelsPerRow;
        int strideY = planeY.PixelsPerRow;
        int strideB = planeB.PixelsPerRow;
        int width = xybLf.XSize;

        for (int y = 0; y < xybLf.YSize; y++)
        {
            ref float rowX = ref MemoryMarshal.GetReference(samplesX.Slice(y * strideX, width));
            ref float rowY = ref MemoryMarshal.GetReference(samplesY.Slice(y * strideY, width));
            ref float rowB = ref MemoryMarshal.GetReference(samplesB.Slice(y * strideB, width));
            int x = 0;

            // Each lane contains one position from each plane. Load all three original components before writing;
            // in particular, B must use the original Y rather than its weighted value. Only complete logical blocks
            // enter SIMD, so no load or store depends on the allocator's physical row padding.
            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<float>.Count; x += Vector512<float>.Count)
                {
                    Vector512<float> inputX = Vector512.LoadUnsafe(ref rowX, (uint)x);
                    Vector512<float> inputY = Vector512.LoadUnsafe(ref rowY, (uint)x);
                    Vector512<float> inputB = Vector512.LoadUnsafe(ref rowB, (uint)x);
                    Vector512<float> outputB = Vector512.MultiplyAddEstimate(Vector512.Create(yToBMultiplier), inputY, inputB);
                    (inputX * Vector512.Create(xMultiplier)).StoreUnsafe(ref rowX, (uint)x);
                    (inputY * Vector512.Create(yMultiplier)).StoreUnsafe(ref rowY, (uint)x);
                    (outputB * Vector512.Create(bMultiplier)).StoreUnsafe(ref rowB, (uint)x);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<float>.Count; x += Vector256<float>.Count)
                {
                    Vector256<float> inputX = Vector256.LoadUnsafe(ref rowX, (uint)x);
                    Vector256<float> inputY = Vector256.LoadUnsafe(ref rowY, (uint)x);
                    Vector256<float> inputB = Vector256.LoadUnsafe(ref rowB, (uint)x);
                    Vector256<float> outputB = Vector256.MultiplyAddEstimate(Vector256.Create(yToBMultiplier), inputY, inputB);
                    (inputX * Vector256.Create(xMultiplier)).StoreUnsafe(ref rowX, (uint)x);
                    (inputY * Vector256.Create(yMultiplier)).StoreUnsafe(ref rowY, (uint)x);
                    (outputB * Vector256.Create(bMultiplier)).StoreUnsafe(ref rowB, (uint)x);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<float>.Count; x += Vector128<float>.Count)
                {
                    Vector128<float> inputX = Vector128.LoadUnsafe(ref rowX, (uint)x);
                    Vector128<float> inputY = Vector128.LoadUnsafe(ref rowY, (uint)x);
                    Vector128<float> inputB = Vector128.LoadUnsafe(ref rowB, (uint)x);
                    Vector128<float> outputB = Vector128.MultiplyAddEstimate(Vector128.Create(yToBMultiplier), inputY, inputB);
                    (inputX * Vector128.Create(xMultiplier)).StoreUnsafe(ref rowX, (uint)x);
                    (inputY * Vector128.Create(yMultiplier)).StoreUnsafe(ref rowY, (uint)x);
                    (outputB * Vector128.Create(bMultiplier)).StoreUnsafe(ref rowB, (uint)x);
                }
            }

            // The remainder uses the same multiply-add followed by multiplication as each SIMD lane and native MulAdd.
            for (; x < width; x++)
            {
                float inputX = Unsafe.Add(ref rowX, x);
                float inputY = Unsafe.Add(ref rowY, x);
                float inputB = Unsafe.Add(ref rowB, x);
                float outputB = float.MultiplyAddEstimate(yToBMultiplier, inputY, inputB);
                Unsafe.Add(ref rowX, x) = inputX * xMultiplier;
                Unsafe.Add(ref rowY, x) = inputY * yMultiplier;
                Unsafe.Add(ref rowB, x) = outputB * bMultiplier;
            }
        }
    }

    /// <summary>
    /// Reduces high-frequency X according to the corresponding Y magnitude.
    /// </summary>
    /// <param name="inY">The borrowed Y frequency samples.</param>
    /// <param name="inOutX">The equally sized X frequency samples, updated in place.</param>
    /// <returns>Whether both plane dimensions match.</returns>
    public static bool SuppressXByY(JxlPlane<float> inY, JxlPlane<float> inOutX)
    {
        if (!JxlImageOperations.SameSize(inOutX, inY))
        {
            return false;
        }

        ReadOnlySpan<float> samplesY = inY.GetRow(0);
        Span<float> samplesX = inOutX.GetRow(0);

        for (int y = 0; y < inY.YSize; y++)
        {
            ReadOnlySpan<float> rowY = samplesY.Slice(y * inY.PixelsPerRow, inY.XSize);
            Span<float> rowX = samplesX.Slice(y * inOutX.PixelsPerRow, inOutX.XSize);

            // Each lane combines X and Y at the same position. The shared loop handles complete registers and scalar tails,
            // so the result can replace X without reading or changing physical row padding.
            TensorPrimitives_.Invoke(rowX, rowY, rowX, default(SuppressXByYOperator));
        }

        return true;
    }

    /// <summary>
    /// Subtracts equally sized sample planes into preallocated output, which may alias either input.
    /// </summary>
    /// <param name="a">The minuend plane.</param>
    /// <param name="b">The subtrahend plane.</param>
    /// <param name="c">The preallocated result plane.</param>
    public static void Subtract(JxlPlane<float> a, JxlPlane<float> b, JxlPlane<float> c)
    {
        ReadOnlySpan<float> samplesA = a.GetRow(0);
        ReadOnlySpan<float> samplesB = b.GetRow(0);
        Span<float> samplesC = c.GetRow(0);

        for (int y = 0; y < a.YSize; y++)
        {
            ReadOnlySpan<float> rowA = samplesA.Slice(y * a.PixelsPerRow, a.XSize);
            ReadOnlySpan<float> rowB = samplesB.Slice(y * b.PixelsPerRow, a.XSize);
            Span<float> rowC = samplesC.Slice(y * c.PixelsPerRow, a.XSize);
            TensorPrimitives.Subtract(rowA, rowB, rowC);
        }
    }

    /// <summary>
    /// Writes low-frequency samples and the remaining middle-frequency samples into prepared images.
    /// </summary>
    /// <param name="configuration">The comparison configuration.</param>
    /// <param name="xyb">The borrowed transformed input.</param>
    /// <param name="lf">The preallocated low-frequency output.</param>
    /// <param name="mf">The preallocated middle-frequency output.</param>
    /// <param name="blurTemp">The prepared transposed blur workspace.</param>
    /// <returns>Whether the frequency blur completed.</returns>
    public static bool SeparateLFAndMF(
        Configuration configuration,
        JxlImage3<float> xyb,
        JxlImage3<float> lf,
        JxlImage3<float> mf,
        ButteraugliBlurTemp blurTemp)
    {
        for (int i = 0; i < 3; i++)
        {
            if (!Blur(xyb.Plane(i), LowFrequencyKernel, blurTemp, lf.Plane(i)))
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

    /// <summary>
    /// Splits the middle-frequency XYB image in place and writes the high-frequency X and Y planes.
    /// </summary>
    /// <param name="configuration">The comparison configuration.</param>
    /// <param name="mf">The caller-owned middle-frequency image, updated in place.</param>
    /// <param name="hf">Two preallocated, caller-owned planes in X, Y order. Their identities remain unchanged.</param>
    /// <param name="blurTemp">The borrowed blur workspace.</param>
    /// <returns>Whether all blur and frequency-separation stages succeeded.</returns>
    public static bool SeparateMfAndHf(Configuration configuration, JxlImage3<float> mf, Span<JxlPlane<float>> hf, ButteraugliBlurTemp blurTemp)
    {
        int xSize = mf.XSize;
        int ySize = mf.YSize;

        int lanes = Vector<float>.Count;

        for (int i = 0; i < 3; i++)
        {
            if (i == 2)
            {
                if (!Blur(mf.Plane(i), HighFrequencyKernel, blurTemp, mf.Plane(i)))
                {
                    return false;
                }

                break;
            }

            Span<float> firstSamples = mf.Plane(i).GetRow(0);
            Span<float> secondSamples = hf[i].GetRow(0);

            for (int y = 0; y < ySize; y++)
            {
                Span<float> rowMf = firstSamples[(y * mf.Plane(i).PixelsPerRow)..];
                Span<float> rowHf = secondSamples[(y * hf[i].PixelsPerRow)..];

                for (int x = 0; x < xSize; x += lanes)
                {
                    new Vector<float>(rowMf.Slice(x, lanes))
                        .CopyTo(rowHf.Slice(x, lanes));
                }
            }

            if (!Blur(mf.Plane(i), HighFrequencyKernel, blurTemp, mf.Plane(i)))
            {
                return false;
            }

            if (i == 0)
            {
                const float removeMfRange = 0.29f;

                for (int y = 0; y < ySize; y++)
                {
                    Span<float> rowMf = firstSamples[(y * mf.Plane(0).PixelsPerRow)..];
                    Span<float> rowHf = secondSamples[(y * hf[0].PixelsPerRow)..];

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
                    Span<float> rowMf = firstSamples[(y * mf.Plane(1).PixelsPerRow)..];
                    Span<float> rowHf = secondSamples[(y * hf[1].PixelsPerRow)..];

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

    /// <summary>
    /// Splits the high-frequency X and Y planes in place and writes their ultra-high-frequency planes.
    /// </summary>
    /// <param name="configuration">The comparison configuration.</param>
    /// <param name="hf">Two borrowed high-frequency planes in X, Y order, updated in place.</param>
    /// <param name="uhf">Two preallocated, caller-owned output planes in X, Y order. Their identities remain unchanged.</param>
    /// <param name="blurTemp">The borrowed blur workspace.</param>
    /// <returns>Whether both frequency separations succeeded.</returns>
    public static bool SeparateHFAndUHF(Configuration configuration, Span<JxlPlane<float>> hf, Span<JxlPlane<float>> uhf, ButteraugliBlurTemp blurTemp)
    {
        int xSize = hf[0].XSize;
        int ySize = hf[0].YSize;

        int lanes = Vector<float>.Count;

        for (int i = 0; i < 2; i++)
        {
            Span<float> firstSamples = hf[i].GetRow(0);
            Span<float> secondSamples = uhf[i].GetRow(0);

            for (int y = 0; y < ySize; y++)
            {
                Span<float> rowUhf = secondSamples[(y * uhf[i].PixelsPerRow)..];
                Span<float> rowHf = firstSamples[(y * hf[i].PixelsPerRow)..];

                for (int x = 0; x < xSize; x += lanes)
                {
                    new Vector<float>(rowHf.Slice(x, lanes)).CopyTo(rowUhf.Slice(x, lanes));
                }
            }

            if (!Blur(hf[i], UltraHighFrequencyKernel, blurTemp, hf[i]))
            {
                return false;
            }

            if (i == 0)
            {
                const float removeHfRange = 1.5f;
                const float removeUhfRange = 0.04f;

                for (int y = 0; y < ySize; y++)
                {
                    Span<float> rowUhf = secondSamples[(y * uhf[0].PixelsPerRow)..];
                    Span<float> rowHf = firstSamples[(y * hf[0].PixelsPerRow)..];

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
                    Span<float> rowUhf = secondSamples[(y * uhf[1].PixelsPerRow)..];
                    Span<float> rowHf = firstSamples[(y * hf[1].PixelsPerRow)..];

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

    /// <summary>
    /// Releases high- and ultra-high-frequency sample storage after masking has consumed it.
    /// </summary>
    /// <param name="hf">The two borrowed high-frequency planes.</param>
    /// <param name="uhf">The two borrowed ultra-high-frequency planes.</param>
    public static void DeallocateHFAndUHF(Span<JxlPlane<float>> hf, Span<JxlPlane<float>> uhf)
    {
        for (int i = 0; i < 2; i++)
        {
            hf[i].Dispose();
            uhf[i].Dispose();
        }
    }

    /// <summary>
    /// Writes all frequency bands into the supplied preallocated owner.
    /// </summary>
    /// <param name="configuration">The comparison configuration.</param>
    /// <param name="blurTemp">The borrowed blur workspace.</param>
    /// <param name="xyb">The borrowed transformed input image.</param>
    /// <param name="ps">The owner of every preallocated output plane. Frequency separation does not resize or replace its storage.</param>
    /// <returns>Whether all frequency bands were produced.</returns>
    public static bool SeparateFrequencies(Configuration configuration, ButteraugliBlurTemp blurTemp, JxlImage3<float> xyb, ButteraugliPsychoImage ps)
    {
        if (!SeparateLFAndMF(configuration, xyb, ps.Lf, ps.Mf, blurTemp))
        {
            return false;
        }

        if (!SeparateMfAndHf(configuration, ps.Mf, ps.Hf, blurTemp))
        {
            return false;
        }

        if (!SeparateHFAndUHF(configuration, ps.Hf, ps.Uhf, blurTemp))
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

    /// <summary>
    /// Evaluates one Malta result using a zero-padded neighborhood when a complete SIMD load would cross an image edge.
    /// </summary>
    /// <param name="diffs">The complete borrowed difference plane, starting at its first logical sample.</param>
    /// <param name="width">The logical plane width.</param>
    /// <param name="height">The logical plane height.</param>
    /// <param name="stride">The physical row stride.</param>
    /// <param name="x0">The sample column.</param>
    /// <param name="y0">The sample row.</param>
    /// <param name="isLf">Whether to use the low-frequency Malta equation.</param>
    /// <returns>The first lane of the selected Malta equation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float PaddedMaltaUnit(ReadOnlySpan<float> diffs, int width, int height, int stride, int x0, int y0, bool isLf)
    {
        int index = (y0 * stride) + x0;
        if (x0 >= 4 && y0 >= 4 && x0 + Vector<float>.Count + 4 <= width && y0 < height - 4)
        {
            return isLf ? MaltaUnitLF(diffs, index, stride)[0] : MaltaUnit(diffs, index, stride)[0];
        }

        // The equation reaches four rows and columns from the center. Extra columns keep every SIMD load
        // inside this local buffer even though only its first result lane is used at image borders.
        int paddedStride = 8 + Vector<float>.Count;
        Span<float> borderImage = stackalloc float[9 * paddedStride];
        borderImage.Clear();

        for (int dy = 0; dy < 9; dy++)
        {
            int y = y0 + dy - 4;
            if ((uint)y >= (uint)height)
            {
                continue;
            }

            ReadOnlySpan<float> rowDiffs = diffs[(y * stride)..];
            Span<float> paddedRow = borderImage[(dy * paddedStride)..];

            for (int dx = 0; dx < 9; dx++)
            {
                int x = x0 + dx - 4;
                if ((uint)x < (uint)width)
                {
                    paddedRow[dx] = rowDiffs[x];
                }
            }
        }

        int center = (4 * paddedStride) + 4;
        return isLf ? MaltaUnitLF(borderImage, center, paddedStride)[0] : MaltaUnit(borderImage, center, paddedStride)[0];
    }

    /// <summary>
    /// Accumulates a Malta difference with the native scaling for its frequency range.
    /// </summary>
    /// <param name="isLf">Whether to use the low-frequency Malta neighborhood and scaling.</param>
    /// <param name="lum0">The borrowed reference band.</param>
    /// <param name="lum1">The borrowed distorted band.</param>
    /// <param name="w0Gt1">The penalty for the first error direction.</param>
    /// <param name="w0Lt1">The penalty for the opposite error direction.</param>
    /// <param name="norm1">The band-specific normalization.</param>
    /// <param name="diffs">The preallocated difference workspace.</param>
    /// <param name="blockDiffAc">The existing AC error accumulation plane.</param>
    /// <returns>Whether the input and workspace dimensions match.</returns>
    public static bool MaltaDiffMap(
        bool isLf,
        JxlPlane<float> lum0,
        JxlPlane<float> lum1,
        float w0Gt1,
        float w0Lt1,
        float norm1,
        JxlPlane<float> diffs,
        JxlPlane<float> blockDiffAc)
        // Keep neighborhood selection and its normalization together in the existing frequency-specific entry points.
        => isLf
            ? MaltaDiffMapLf(lum0, lum1, w0Gt1, w0Lt1, norm1, diffs, blockDiffAc)
            : MaltaDiffMap(lum0, lum1, w0Gt1, w0Lt1, norm1, diffs, blockDiffAc);

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

        // Malta neighborhoods reach four rows and four columns from each center. Borrow complete planes once,
        // then retain global indices so negative relative offsets never refer to a sliced-away preceding row.
        ReadOnlySpan<float> samples0 = lum0.GetRow(0);
        ReadOnlySpan<float> samples1 = lum1.GetRow(0);
        Span<float> differenceSamples = diffs.GetRow(0);
        Span<float> outputSamples = blockDiffAc.GetRow(0);

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<float> row0 = samples0[(y * lum0.PixelsPerRow)..];
            ReadOnlySpan<float> row1 = samples1[(y * lum1.PixelsPerRow)..];
            Span<float> rowDiffs = differenceSamples[(y * diffs.PixelsPerRow)..];

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
            Span<float> row = outputSamples[(y0 * blockDiffAc.PixelsPerRow)..];

            for (int x = 0; x < width; x++)
            {
                row[x] += PaddedMaltaUnit(differenceSamples, width, height, diffs.PixelsPerRow, x, y0, isLf);
            }
        }

        int lanes = Vector<float>.Count;
        int alignedX = Math.Max(4, lanes);

        int stride = diffs.PixelsPerRow;

        for (; y0 < height - 4; y0++)
        {
            Span<float> output = outputSamples[(y0 * blockDiffAc.PixelsPerRow)..];
            ref float outputReference = ref MemoryMarshal.GetReference(output);

            int x = 0;

            for (; x < Math.Min(alignedX, width); x++)
            {
                output[x] += PaddedMaltaUnit(differenceSamples, width, height, diffs.PixelsPerRow, x, y0, isLf);
            }

            // Every lane and its four neighboring columns remain inside the logical width. Border calls use the zero-padded scalar-lane path.
            for (; x + lanes + 4 <= width; x += lanes)
            {
                Vector<float> value = Vector.LoadUnsafe(ref Unsafe.Add(ref outputReference, x));

                Vector<float> malta = isLf
                    ? MaltaUnitLF(differenceSamples, (y0 * stride) + x, stride)
                    : MaltaUnit(differenceSamples, (y0 * stride) + x, stride);

                (value + malta).CopyTo(output[x..]);
            }

            for (; x < width; x++)
            {
                output[x] += PaddedMaltaUnit(differenceSamples, width, height, diffs.PixelsPerRow, x, y0, isLf);
            }
        }

        for (; y0 < height; y0++)
        {
            Span<float> row = outputSamples[(y0 * blockDiffAc.PixelsPerRow)..];

            for (int x = 0; x < width; x++)
            {
                row[x] += PaddedMaltaUnit(differenceSamples, width, height, diffs.PixelsPerRow, x, y0, isLf);
            }
        }

        return true;
    }

    public static bool MaltaDiffMap(
        JxlPlane<float> lum0,
        JxlPlane<float> lum1,
        float w0Gt1,
        float w0Lt1,
        float norm1,
        JxlPlane<float> diffs,
        JxlPlane<float> blockDiffAc)
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

    /// <summary>
    /// Combines X and Y high-frequency bands into the perceptual masking response.
    /// </summary>
    /// <param name="hf">The borrowed high-frequency X and Y planes.</param>
    /// <param name="uhf">The borrowed ultra-high-frequency X and Y planes.</param>
    /// <param name="output">The distinct preallocated masking output.</param>
    public static void CombineChannelsForMasking(InlineArray2<JxlPlane<float>> hf, InlineArray2<JxlPlane<float>> uhf, JxlPlane<float> output)
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

        Span<float> hfSamples0 = hf[0].GetRow(0);
        Span<float> hfSamples1 = hf[1].GetRow(0);
        Span<float> uhfSamples0 = uhf[0].GetRow(0);
        Span<float> uhfSamples1 = uhf[1].GetRow(0);
        Span<float> outputSamples = output.GetRow(0);

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<float> rowYHf = hfSamples1[(y * hf[1].PixelsPerRow)..];
            ReadOnlySpan<float> rowYUhf = uhfSamples1[(y * uhf[1].PixelsPerRow)..];
            ReadOnlySpan<float> rowXHf = hfSamples0[(y * hf[0].PixelsPerRow)..];
            ReadOnlySpan<float> rowXUhf = uhfSamples0[(y * uhf[0].PixelsPerRow)..];

            Span<float> row = outputSamples[(y * output.PixelsPerRow)..];

            int x = 0;

            // Each lane processes one logical sample. The scalar remainder retains the same arithmetic order.
            for (; x <= width - Vector<float>.Count; x += Vector<float>.Count)
            {
                Vector<float> xDiff = (new Vector<float>(rowXUhf[x..]) + new Vector<float>(rowXHf[x..])) * Vector.Create(muls[0]);
                Vector<float> yDiff = (new Vector<float>(rowYUhf[x..]) * Vector.Create(muls[1])) + (new Vector<float>(rowYHf[x..]) * Vector.Create(muls[2]));
                Vector.SquareRoot((xDiff * xDiff) + (yDiff * yDiff)).CopyTo(row[x..]);
            }

            for (; x < width; x++)
            {
                float xDiff = (rowXUhf[x] + rowXHf[x]) * muls[0];
                float yDiff = (rowYUhf[x] * muls[1]) + (rowYHf[x] * muls[2]);

                row[x] = MathF.Sqrt((xDiff * xDiff) + (yDiff * yDiff));
            }
        }
    }

    /// <summary>
    /// Writes the biased square-root response used before masking blur and erosion.
    /// </summary>
    /// <param name="xyb">The borrowed input samples.</param>
    /// <param name="mul">The fixed scale applied to the absolute samples.</param>
    /// <param name="biasArg">The bias before multiplication by the scale.</param>
    /// <param name="output">The preallocated output plane.</param>
    public static void DiffPrecompute(JxlPlane<float> xyb, float mul, float biasArg, JxlPlane<float> output)
    {
        int width = xyb.XSize;
        int height = xyb.YSize;

        float bias = mul * biasArg;
        float sqrtBias = MathF.Sqrt(bias);

        Span<float> inputSamples = xyb.GetRow(0);
        Span<float> outputSamples = output.GetRow(0);

        MaskPrecomputeOperator operation = new(mul, bias, sqrtBias);

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<float> rowIn = inputSamples[(y * xyb.PixelsPerRow)..];
            Span<float> rowOut = outputSamples[(y * output.PixelsPerRow)..];

            TensorPrimitives_.Invoke(rowIn[..width], rowOut[..width], operation);
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

    /// <summary>
    /// Writes the weighted three-smallest neighborhood response into distinct, preallocated storage.
    /// </summary>
    /// <param name="from">The borrowed input plane, retained unchanged during neighboring reads.</param>
    /// <param name="to">The distinct preallocated output plane.</param>
    public static void FuzzyErosion(JxlPlane<float> from, JxlPlane<float> to)
    {
        int width = from.XSize;
        int height = from.YSize;
        const int step = 3;
        ReadOnlySpan<float> inputSamples = from.GetRow(0);
        Span<float> outputSamples = to.GetRow(0);

        for (int y = 0; y < height; y++)
        {
            Span<float> output = outputSamples[(y * to.PixelsPerRow)..];
            int x = 0;
            int firstVector = y >= step && y < height - step ? Math.Min(step, width) : width;

            for (; x < firstVector; x++)
            {
                output[x] = FuzzyErosionAt(inputSamples, from.PixelsPerRow, width, height, x, y);
            }

            // Each lane selects the same three minima from its nine neighbors. The border path omits unavailable neighbors.
            for (; x <= width - step - Vector<float>.Count; x += Vector<float>.Count)
            {
                int center = (y * from.PixelsPerRow) + x;
                Vector<float> min0 = new(inputSamples[center..]);
                Vector<float> min1 = Vector.Create(2F) * min0;
                Vector<float> min2 = min1;
                int above = center - (step * from.PixelsPerRow);
                int below = center + (step * from.PixelsPerRow);

                StoreMin3(new Vector<float>(inputSamples[(center - step)..]), ref min0, ref min1, ref min2);
                StoreMin3(new Vector<float>(inputSamples[(above - step)..]), ref min0, ref min1, ref min2);
                StoreMin3(new Vector<float>(inputSamples[(below - step)..]), ref min0, ref min1, ref min2);
                StoreMin3(new Vector<float>(inputSamples[(center + step)..]), ref min0, ref min1, ref min2);
                StoreMin3(new Vector<float>(inputSamples[(above + step)..]), ref min0, ref min1, ref min2);
                StoreMin3(new Vector<float>(inputSamples[(below + step)..]), ref min0, ref min1, ref min2);
                StoreMin3(new Vector<float>(inputSamples[above..]), ref min0, ref min1, ref min2);
                StoreMin3(new Vector<float>(inputSamples[below..]), ref min0, ref min1, ref min2);
                ((Vector.Create(0.45F) * min0) + (Vector.Create(0.3F) * min1) + (Vector.Create(0.25F) * min2)).CopyTo(output[x..]);
            }

            for (; x < width; x++)
            {
                output[x] = FuzzyErosionAt(inputSamples, from.PixelsPerRow, width, height, x, y);
            }
        }
    }

    /// <summary>
    /// Inserts one sample per SIMD lane into the three retained minima, using the scalar comparison order.
    /// </summary>
    /// <param name="value">The samples to insert.</param>
    /// <param name="min0">The first minimum in each lane.</param>
    /// <param name="min1">The second minimum in each lane.</param>
    /// <param name="min2">The third minimum in each lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreMin3(Vector<float> value, ref Vector<float> min0, ref Vector<float> min1, ref Vector<float> min2)
    {
        Vector<int> less2 = Vector.LessThan(value, min2);
        Vector<int> less1 = less2 & Vector.LessThan(value, min1);
        Vector<int> less0 = less1 & Vector.LessThan(value, min0);
        min2 = Vector.ConditionalSelect(less2, Vector.ConditionalSelect(less1, min1, value), min2);
        min1 = Vector.ConditionalSelect(less1, Vector.ConditionalSelect(less0, min0, value), min1);
        min0 = Vector.ConditionalSelect(less0, value, min0);
    }

    /// <summary>
    /// Evaluates the erosion equation at a border sample, ignoring neighbors outside the image.
    /// </summary>
    /// <param name="inputSamples">The complete borrowed input storage.</param>
    /// <param name="stride">The physical row stride.</param>
    /// <param name="width">The logical width.</param>
    /// <param name="height">The logical height.</param>
    /// <param name="x">The sample column.</param>
    /// <param name="y">The sample row.</param>
    /// <returns>The same weighted minima used by the SIMD interior.</returns>
    private static float FuzzyErosionAt(ReadOnlySpan<float> inputSamples, int stride, int width, int height, int x, int y)
    {
        const int step = 3;
        float min0 = inputSamples[(y * stride) + x];
        float min1 = 2 * min0;
        float min2 = min1;

        if (x >= step)
        {
            StoreMin3(
                inputSamples[(y * stride) + x - step],
                ref min0,
                ref min1,
                ref min2);

            if (y >= step)
            {
                StoreMin3(
                    inputSamples[((y - step) * stride) + x - step],
                    ref min0,
                    ref min1,
                    ref min2);
            }

            if (y < height - step)
            {
                StoreMin3(
                    inputSamples[((y + step) * stride) + x - step],
                    ref min0,
                    ref min1,
                    ref min2);
            }
        }

        if (x < width - step)
        {
            StoreMin3(
                inputSamples[(y * stride) + x + step],
                ref min0,
                ref min1,
                ref min2);

            if (y >= step)
            {
                StoreMin3(
                    inputSamples[((y - step) * stride) + x + step],
                    ref min0,
                    ref min1,
                    ref min2);
            }

            if (y < height - step)
            {
                StoreMin3(
                    inputSamples[((y + step) * stride) + x + step],
                    ref min0,
                    ref min1,
                    ref min2);
            }
        }

        if (y >= step)
        {
            StoreMin3(
                inputSamples[((y - step) * stride) + x],
                ref min0,
                ref min1,
                ref min2);
        }

        if (y < height - step)
        {
            StoreMin3(
                inputSamples[((y + step) * stride) + x],
                ref min0,
                ref min1,
                ref min2);
        }

        return (0.45F * min0) + (0.3F * min1) + (0.25F * min2);
    }

    /// <summary>
    /// Builds a perceptual mask in borrowed storage and optionally updates an existing AC accumulation plane.
    /// </summary>
    /// <param name="configuration">The comparison configuration.</param>
    /// <param name="mask0">The borrowed reference masking samples.</param>
    /// <param name="mask1">The borrowed distorted masking samples.</param>
    /// <param name="blurTemp">The prepared transposed blur workspace.</param>
    /// <param name="diffAc">The existing AC accumulation plane, or null when no AC correction is needed.</param>
    /// <param name="mask">The preallocated output plane.</param>
    /// <param name="diff0">The preallocated reference difference and erosion workspace.</param>
    /// <param name="diff1">The preallocated distorted difference workspace.</param>
    /// <param name="blurred0">The preallocated reference blur workspace, distinct from diff0 during erosion.</param>
    /// <param name="blurred1">The preallocated distorted blur workspace.</param>
    /// <returns>Whether mask construction succeeded.</returns>
    public static bool Mask(
        Configuration configuration,
        JxlPlane<float> mask0,
        JxlPlane<float> mask1,
        ButteraugliBlurTemp blurTemp,
        JxlPlane<float>? diffAc,
        JxlPlane<float> mask,
        JxlPlane<float> diff0,
        JxlPlane<float> diff1,
        JxlPlane<float> blurred0,
        JxlPlane<float> blurred1)
    {
        const float mul = 6.19424080439f;
        const float bias = 12.61050594197f;
        int width = mask0.XSize;
        int height = mask0.YSize;

        DiffPrecompute(mask0, mul, bias, diff0);
        DiffPrecompute(mask1, mul, bias, diff1);

        if (!Blur(diff0, MaskingKernel, blurTemp, blurred0))
        {
            return false;
        }

        // Erosion reads neighboring samples. Its source must remain separate from its output until every row is complete.
        FuzzyErosion(blurred0, diff0);

        if (!Blur(diff1, MaskingKernel, blurTemp, blurred1))
        {
            return false;
        }

        Span<float> maskSamples = mask.GetRow(0);
        ReadOnlySpan<float> diff0Samples = diff0.GetRow(0);
        ReadOnlySpan<float> blur0Samples = blurred0.GetRow(0);
        ReadOnlySpan<float> blur1Samples = blurred1.GetRow(0);
        Span<float> diffSamples = diffAc is not null ? diffAc.GetRow(0) : default;

        for (int y = 0; y < height; y++)
        {
            Span<float> maskRow = maskSamples.Slice(y * mask.PixelsPerRow, width);
            ReadOnlySpan<float> diff0Row = diff0Samples.Slice(y * diff0.PixelsPerRow, width);
            diff0Row.CopyTo(maskRow);

            if (diffAc is not null)
            {
                Span<float> diffRow = diffSamples.Slice(y * diffAc.PixelsPerRow, width);
                ReadOnlySpan<float> blur0Row = blur0Samples.Slice(y * blurred0.PixelsPerRow, width);
                ReadOnlySpan<float> blur1Row = blur1Samples.Slice(y * blurred1.PixelsPerRow, width);
                TensorPrimitives_.Invoke(blur0Row, blur1Row, diffRow, diffRow, default(MaskCorrectionOperator));
            }
        }

        return true;
    }

    /// <summary>
    /// Builds a perceptual mask from borrowed frequency bands using six preallocated scratch planes.
    /// </summary>
    /// <param name="configuration">The comparison configuration.</param>
    /// <param name="pi0">The unchanged reference frequency bands.</param>
    /// <param name="pi1">The distorted frequency bands. Their high-frequency planes remain readable during this operation.</param>
    /// <param name="width">The comparison width.</param>
    /// <param name="height">The comparison height.</param>
    /// <param name="blurTemp">The prepared transposed blur workspace.</param>
    /// <param name="mask">The preallocated result plane.</param>
    /// <param name="diffAc">The existing AC accumulation plane, or null when only the mask is needed.</param>
    /// <param name="mask0">The reference masking workspace.</param>
    /// <param name="mask1">The distorted masking workspace.</param>
    /// <param name="diff0">The reference difference and erosion workspace.</param>
    /// <param name="diff1">The distorted difference workspace.</param>
    /// <param name="blurred0">The reference blur workspace.</param>
    /// <param name="blurred1">The distorted blur workspace.</param>
    /// <returns>Whether mask construction succeeded.</returns>
    /// <remarks>
    /// All six scratch planes are distinct and match the comparison dimensions. After frequency comparison,
    /// distorted middle- and low-frequency bands can provide this storage. Reference bands cannot provide scratch storage.
    /// </remarks>
    public static bool MaskButteraugliPsychoImage(
        Configuration configuration,
        ButteraugliPsychoImage pi0,
        ButteraugliPsychoImage pi1,
        int width,
        int height,
        ButteraugliBlurTemp blurTemp,
        JxlPlane<float> mask,
        JxlPlane<float>? diffAc,
        JxlPlane<float> mask0,
        JxlPlane<float> mask1,
        JxlPlane<float> diff0,
        JxlPlane<float> diff1,
        JxlPlane<float> blurred0,
        JxlPlane<float> blurred1)
    {
        CombineChannelsForMasking(pi0.Hf, pi0.Uhf, mask0);
        CombineChannelsForMasking(pi1.Hf, pi1.Uhf, mask1);
        return Mask(configuration, mask0, mask1, blurTemp, diffAc, mask, diff0, diff1, blurred0, blurred1);
    }

    public static float MaskY(float delta)
    {
        float c = MaskYMultiplier / ((MaskYScaler * delta) + MaskYOffset);
        float result = GlobalScale * (1.0f + c);

        return result * result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MaskDcY(float delta)
    {
        float c = MaskDcYMultiplier / ((MaskDcYScaler * delta) + MaskDcYOffset);
        float result = GlobalScale * (1.0f + c);

        return result * result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MaskColor(
        ReadOnlySpan<float> color,
        float mask)
        => (color[0] * mask) + (color[1] * mask) + (color[2] * mask);

    /// <summary>
    /// Combines the masked low- and high-frequency channel errors into the caller's prepared difference plane.
    /// </summary>
    /// <param name="mask">The borrowed perceptual mask.</param>
    /// <param name="blockDiffDc">The borrowed low-frequency channel errors.</param>
    /// <param name="blockDiffAc">The borrowed high-frequency channel errors.</param>
    /// <param name="xmul">The additional weight for the X channel.</param>
    /// <param name="result">The preallocated output plane.</param>
    /// <returns>Whether the result dimensions match the mask.</returns>
    public static bool CombineChannelsToDiffmap(
        JxlPlane<float> mask,
        JxlImage3<float> blockDiffDc,
        JxlImage3<float> blockDiffAc,
        float xmul,
        JxlPlane<float> result)
    {
        if (!JxlImageOperations.SameSize(mask, result))
        {
            return false;
        }

        ReadOnlySpan<float> maskSamples = mask.GetRow(0);
        ReadOnlySpan<float> dc0 = blockDiffDc.Plane(0).GetRow(0);
        ReadOnlySpan<float> dc1 = blockDiffDc.Plane(1).GetRow(0);
        ReadOnlySpan<float> dc2 = blockDiffDc.Plane(2).GetRow(0);
        ReadOnlySpan<float> ac0 = blockDiffAc.Plane(0).GetRow(0);
        ReadOnlySpan<float> ac1 = blockDiffAc.Plane(1).GetRow(0);
        ReadOnlySpan<float> ac2 = blockDiffAc.Plane(2).GetRow(0);
        Span<float> outputSamples = result.GetRow(0);
        Vector<float> xWeight = Vector.Create(xmul);
        Vector<float> globalScale = Vector.Create(GlobalScale);
        int width = mask.XSize;

        for (int y = 0; y < mask.YSize; y++)
        {
            ReadOnlySpan<float> maskRow = maskSamples[(y * mask.PixelsPerRow)..];
            ReadOnlySpan<float> dcRow0 = dc0[(y * blockDiffDc.Plane(0).PixelsPerRow)..];
            ReadOnlySpan<float> dcRow1 = dc1[(y * blockDiffDc.Plane(1).PixelsPerRow)..];
            ReadOnlySpan<float> dcRow2 = dc2[(y * blockDiffDc.Plane(2).PixelsPerRow)..];
            ReadOnlySpan<float> acRow0 = ac0[(y * blockDiffAc.Plane(0).PixelsPerRow)..];
            ReadOnlySpan<float> acRow1 = ac1[(y * blockDiffAc.Plane(1).PixelsPerRow)..];
            ReadOnlySpan<float> acRow2 = ac2[(y * blockDiffAc.Plane(2).PixelsPerRow)..];
            Span<float> outputRow = outputSamples[(y * result.PixelsPerRow)..];
            int x = 0;

            // Lanes hold consecutive pixels. Keep channel weighting and the left-to-right sums identical to MaskColor.
            for (; x <= width - Vector<float>.Count; x += Vector<float>.Count)
            {
                Vector<float> value = new(maskRow[x..]);
                Vector<float> acMask = Vector.Create(MaskYMultiplier) / ((Vector.Create(MaskYScaler) * value) + Vector.Create(MaskYOffset));
                Vector<float> dcMask = Vector.Create(MaskDcYMultiplier) / ((Vector.Create(MaskDcYScaler) * value) + Vector.Create(MaskDcYOffset));
                acMask = globalScale * (Vector<float>.One + acMask);
                dcMask = globalScale * (Vector<float>.One + dcMask);
                acMask *= acMask;
                dcMask *= dcMask;

                Vector<float> dc = (((new Vector<float>(dcRow0[x..]) * xWeight) * dcMask) + (new Vector<float>(dcRow1[x..]) * dcMask))
                    + (new Vector<float>(dcRow2[x..]) * dcMask);
                Vector<float> ac = (((new Vector<float>(acRow0[x..]) * xWeight) * acMask) + (new Vector<float>(acRow1[x..]) * acMask))
                    + (new Vector<float>(acRow2[x..]) * acMask);
                Vector.SquareRoot(dc + ac).CopyTo(outputRow[x..]);
            }

            for (; x < width; x++)
            {
                float acMask = MaskY(maskRow[x]);
                float dcMask = MaskDcY(maskRow[x]);
                float dc = (((dcRow0[x] * xmul) * dcMask) + (dcRow1[x] * dcMask)) + (dcRow2[x] * dcMask);
                float ac = (((acRow0[x] * xmul) * acMask) + (acRow1[x] * acMask)) + (acRow2[x] * acMask);
                outputRow[x] = MathF.Sqrt(dc + ac);
            }
        }

        return true;
    }

    /// <summary>
    /// Adds weighted squared component differences to an existing error plane.
    /// </summary>
    /// <param name="i0">The borrowed reference samples.</param>
    /// <param name="i1">The equally sized distorted samples.</param>
    /// <param name="w">The squared-error weight. Zero leaves the output unchanged.</param>
    /// <param name="diffmap">The preallocated accumulation plane.</param>
    public static void L2Diff(JxlPlane<float> i0, JxlPlane<float> i1, float w, JxlPlane<float> diffmap)
    {
        if (w == 0)
        {
            return;
        }

        Span<float> samples0 = i0.GetRow(0);
        Span<float> samples1 = i1.GetRow(0);
        Span<float> differenceSamples = diffmap.GetRow(0);

        AccumulateSquaredDifferenceOperator operation = new(w);

        for (int y = 0; y < i0.YSize; ++y)
        {
            ReadOnlySpan<float> row0 = samples0[(y * i0.PixelsPerRow)..];
            ReadOnlySpan<float> row1 = samples1[(y * i1.PixelsPerRow)..];
            Span<float> rowDiff = differenceSamples[(y * diffmap.PixelsPerRow)..];

            TensorPrimitives_.Invoke(row0[..i0.XSize], row1[..i0.XSize], rowDiff[..i0.XSize], rowDiff[..i0.XSize], operation);
        }
    }

    /// <summary>
    /// Writes weighted squared component differences into a prepared error plane.
    /// </summary>
    /// <param name="i0">The borrowed reference samples.</param>
    /// <param name="i1">The equally sized distorted samples.</param>
    /// <param name="w">The squared-error weight. Zero leaves the output unchanged.</param>
    /// <param name="diffmap">The preallocated output plane.</param>
    public static void SetL2Diff(JxlPlane<float> i0, JxlPlane<float> i1, float w, JxlPlane<float> diffmap)
    {
        if (w == 0)
        {
            return;
        }

        Span<float> samples0 = i0.GetRow(0);
        Span<float> samples1 = i1.GetRow(0);
        Span<float> differenceSamples = diffmap.GetRow(0);

        SetSquaredDifferenceOperator operation = new(w);

        for (int y = 0; y < i0.YSize; ++y)
        {
            ReadOnlySpan<float> row0 = samples0[(y * i0.PixelsPerRow)..];
            ReadOnlySpan<float> row1 = samples1[(y * i1.PixelsPerRow)..];
            Span<float> rowDiff = differenceSamples[(y * diffmap.PixelsPerRow)..];

            TensorPrimitives_.Invoke(row0[..i0.XSize], row1[..i0.XSize], rowDiff[..i0.XSize], operation);
        }
    }

    /// <summary>
    /// Accumulates asymmetric high-frequency error with identical scalar and SIMD equations.
    /// </summary>
    /// <param name="i0">The borrowed reference samples.</param>
    /// <param name="i1">The equally sized distorted samples.</param>
    /// <param name="w0gt1">The primary symmetric error weight.</param>
    /// <param name="w0lt1">The additional asymmetric error weight.</param>
    /// <param name="diffmap">The preallocated accumulation plane.</param>
    public static void L2DiffAsymmetric(JxlPlane<float> i0, JxlPlane<float> i1, float w0gt1, float w0lt1, JxlPlane<float> diffmap)
    {
        if (w0gt1 == 0 && w0lt1 == 0)
        {
            return;
        }

        AsymmetricDifferenceOperator operation = new(w0gt1, w0lt1);

        Span<float> samples0 = i0.GetRow(0);
        Span<float> samples1 = i1.GetRow(0);
        Span<float> differenceSamples = diffmap.GetRow(0);

        for (int y = 0; y < i0.YSize; ++y)
        {
            ReadOnlySpan<float> row0 = samples0[(y * i0.PixelsPerRow)..];
            ReadOnlySpan<float> row1 = samples1[(y * i1.PixelsPerRow)..];
            Span<float> rowDiff = differenceSamples[(y * diffmap.PixelsPerRow)..];

            TensorPrimitives_.Invoke(row0[..i0.XSize], row1[..i0.XSize], rowDiff[..i0.XSize], rowDiff[..i0.XSize], operation);
        }
    }

    /// <summary>
    /// Reduces X sensitivity where the same sample has a large Y response.
    /// </summary>
    private readonly struct SuppressXByYOperator : TensorPrimitives_.IBinaryOperator
    {
        /// <summary>The squared Y magnitude that halves the variable part of the response.</summary>
        private const float Suppression = 46F;

        /// <summary>The response retained when the Y magnitude grows without bound.</summary>
        private const float MinimumResponse = 0.653020556257F;

        /// <summary>The variable response, rounded once from the native coefficient.</summary>
        private const float VariableResponse = (float)(1D - 0.653020556257D);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float first, float second)
        {
            // Native Butteraugli uses Y squared, so positive and negative Y suppress X equally.
            // The response is one at Y = 0 and tends to MinimumResponse as the Y magnitude increases.
            float denominator = float.MultiplyAddEstimate(second, second, Suppression);

            return float.MultiplyAddEstimate(Suppression / denominator, VariableResponse, MinimumResponse) * first;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second)
        {
            Vector128<float> denominator = Vector128.MultiplyAddEstimate(second, second, Vector128.Create(Suppression));

            return Vector128.MultiplyAddEstimate(
                Vector128.Create(Suppression) / denominator,
                Vector128.Create(VariableResponse),
                Vector128.Create(MinimumResponse)) * first;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second)
        {
            Vector256<float> denominator = Vector256.MultiplyAddEstimate(second, second, Vector256.Create(Suppression));

            return Vector256.MultiplyAddEstimate(
                Vector256.Create(Suppression) / denominator,
                Vector256.Create(VariableResponse),
                Vector256.Create(MinimumResponse)) * first;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second)
        {
            Vector512<float> denominator = Vector512.MultiplyAddEstimate(second, second, Vector512.Create(Suppression));

            return Vector512.MultiplyAddEstimate(
                Vector512.Create(Suppression) / denominator,
                Vector512.Create(VariableResponse),
                Vector512.Create(MinimumResponse)) * first;
        }
    }

    /// <summary>
    /// Applies the native low- and high-frequency masks to independently accumulated squared errors.
    /// </summary>
    private readonly struct MaskedDifferenceOperator : TensorPrimitives_.ITernaryOperator
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float first, float second, float third)
            => MathF.Sqrt((second * MaskDcY(first)) + (third * MaskY(first)));

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second, Vector128<float> third)
        {
            // The mask plane controls sensitivity; second and third retain separate DC and AC squared-error totals.
            Vector128<float> dcMask = Vector128.Create(MaskDcYMultiplier) / ((Vector128.Create(MaskDcYScaler) * first) + Vector128.Create(MaskDcYOffset));
            Vector128<float> acMask = Vector128.Create(MaskYMultiplier) / ((Vector128.Create(MaskYScaler) * first) + Vector128.Create(MaskYOffset));
            dcMask = Vector128.Create(GlobalScale) * (Vector128<float>.One + dcMask);
            acMask = Vector128.Create(GlobalScale) * (Vector128<float>.One + acMask);
            dcMask *= dcMask;
            acMask *= acMask;
            return Vector128.Sqrt((second * dcMask) + (third * acMask));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second, Vector256<float> third)
        {
            // The mask plane controls sensitivity; second and third retain separate DC and AC squared-error totals.
            Vector256<float> dcMask = Vector256.Create(MaskDcYMultiplier) / ((Vector256.Create(MaskDcYScaler) * first) + Vector256.Create(MaskDcYOffset));
            Vector256<float> acMask = Vector256.Create(MaskYMultiplier) / ((Vector256.Create(MaskYScaler) * first) + Vector256.Create(MaskYOffset));
            dcMask = Vector256.Create(GlobalScale) * (Vector256<float>.One + dcMask);
            acMask = Vector256.Create(GlobalScale) * (Vector256<float>.One + acMask);
            dcMask *= dcMask;
            acMask *= acMask;
            return Vector256.Sqrt((second * dcMask) + (third * acMask));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second, Vector512<float> third)
        {
            // The mask plane controls sensitivity; second and third retain separate DC and AC squared-error totals.
            Vector512<float> dcMask = Vector512.Create(MaskDcYMultiplier) / ((Vector512.Create(MaskDcYScaler) * first) + Vector512.Create(MaskDcYOffset));
            Vector512<float> acMask = Vector512.Create(MaskYMultiplier) / ((Vector512.Create(MaskYScaler) * first) + Vector512.Create(MaskYOffset));
            dcMask = Vector512.Create(GlobalScale) * (Vector512<float>.One + dcMask);
            acMask = Vector512.Create(GlobalScale) * (Vector512<float>.One + acMask);
            dcMask *= dcMask;
            acMask *= acMask;
            return Vector512.Sqrt((second * dcMask) + (third * acMask));
        }
    }

    /// <summary>
    /// Accumulates a weighted squared difference without changing the subtraction or multiplication order.
    /// </summary>
    /// <param name="weight">The fixed squared-error weight.</param>
    private readonly struct AccumulateSquaredDifferenceOperator(float weight) : TensorPrimitives_.ITernaryOperator
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float first, float second, float third)
        {
            float difference = first - second;
            return third + (difference * difference * weight);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second, Vector128<float> third)
        {
            Vector128<float> difference = first - second;
            return third + (difference * difference * Vector128.Create(weight));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second, Vector256<float> third)
        {
            Vector256<float> difference = first - second;
            return third + (difference * difference * Vector256.Create(weight));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second, Vector512<float> third)
        {
            Vector512<float> difference = first - second;
            return third + (difference * difference * Vector512.Create(weight));
        }
    }

    /// <summary>
    /// Sets a weighted squared difference from its two source components.
    /// </summary>
    /// <param name="weight">The fixed squared-error weight.</param>
    private readonly struct SetSquaredDifferenceOperator(float weight) : TensorPrimitives_.IBinaryOperator
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float first, float second)
        {
            float difference = first - second;
            return difference * difference * weight;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second)
        {
            Vector128<float> difference = first - second;
            return difference * difference * Vector128.Create(weight);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second)
        {
            Vector256<float> difference = first - second;
            return difference * difference * Vector256.Create(weight);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second)
        {
            Vector512<float> difference = first - second;
            return difference * difference * Vector512.Create(weight);
        }
    }

    /// <summary>
    /// Computes the masking response from one sample without widening its arithmetic.
    /// </summary>
    /// <param name="multiplier">The fixed scale applied before the square root.</param>
    /// <param name="bias">The fixed offset inside the square root.</param>
    /// <param name="squareRootBias">The square root of bias, subtracted after the square root.</param>
    private readonly struct MaskPrecomputeOperator(float multiplier, float bias, float squareRootBias) : TensorPrimitives_.IUnaryOperator
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
            => MathF.Sqrt((multiplier * MathF.Abs(value)) + bias) - squareRootBias;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
            => Vector128.Sqrt((Vector128.Create(multiplier) * Vector128.Abs(value)) + Vector128.Create(bias)) - Vector128.Create(squareRootBias);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
            => Vector256.Sqrt((Vector256.Create(multiplier) * Vector256.Abs(value)) + Vector256.Create(bias)) - Vector256.Create(squareRootBias);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
            => Vector512.Sqrt((Vector512.Create(multiplier) * Vector512.Abs(value)) + Vector512.Create(bias)) - Vector512.Create(squareRootBias);

    }

    /// <summary>
    /// Adds the masking correction to an existing AC error sample.
    /// </summary>
    private readonly struct MaskCorrectionOperator : TensorPrimitives_.ITernaryOperator
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float first, float second, float third)
        {
            float difference = first - second;
            return third + (10F * difference * difference);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second, Vector128<float> third)
        {
            Vector128<float> difference = first - second;
            return third + (Vector128.Create(10F) * difference * difference);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second, Vector256<float> third)
        {
            Vector256<float> difference = first - second;
            return third + (Vector256.Create(10F) * difference * difference);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second, Vector512<float> third)
        {
            Vector512<float> difference = first - second;
            return third + (Vector512.Create(10F) * difference * difference);
        }
    }

    /// <summary>
    /// Accumulates the asymmetric high-frequency error with the same branch priorities in every SIMD lane.
    /// </summary>
    private readonly struct AsymmetricDifferenceOperator : TensorPrimitives_.ITernaryOperator
    {
        private readonly float largerWeight;
        private readonly float smallerWeight;

        /// <summary>
        /// Prepares the native weights once before component traversal.
        /// </summary>
        /// <param name="largerWeight">The primary symmetric weight.</param>
        /// <param name="smallerWeight">The asymmetric residual weight.</param>
        public AsymmetricDifferenceOperator(float largerWeight, float smallerWeight)
        {
            this.largerWeight = largerWeight * 0.8F;
            this.smallerWeight = smallerWeight * 0.8F;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float first, float second, float third)
        {
            float difference = first - second;
            float total = (difference * difference * this.largerWeight) + third;
            float magnitude = MathF.Abs(first);
            float tooSmall = magnitude * 0.4F;
            float residual = first < 0F
                ? second > -tooSmall ? second + tooSmall : second < -magnitude ? -second - magnitude : 0F
                : second < tooSmall ? tooSmall - second : second > magnitude ? second - magnitude : 0F;
            return total + (this.smallerWeight * residual * residual);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second, Vector128<float> third)
        {
            Vector128<float> difference = first - second;
            Vector128<float> total = (difference * difference * Vector128.Create(this.largerWeight)) + third;
            Vector128<float> magnitude = Vector128.Abs(first);
            Vector128<float> tooSmall = magnitude * Vector128.Create(0.4F);
            Vector128<float> negative = Vector128.ConditionalSelect(
                Vector128.GreaterThan(second, -tooSmall),
                second + tooSmall,
                Vector128.ConditionalSelect(Vector128.LessThan(second, -magnitude), -second - magnitude, Vector128<float>.Zero));
            Vector128<float> positive = Vector128.ConditionalSelect(
                Vector128.LessThan(second, tooSmall),
                tooSmall - second,
                Vector128.ConditionalSelect(Vector128.GreaterThan(second, magnitude), second - magnitude, Vector128<float>.Zero));
            Vector128<float> residual = Vector128.ConditionalSelect(Vector128.LessThan(first, Vector128<float>.Zero), negative, positive);
            return total + (Vector128.Create(this.smallerWeight) * residual * residual);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second, Vector256<float> third)
        {
            Vector256<float> difference = first - second;
            Vector256<float> total = (difference * difference * Vector256.Create(this.largerWeight)) + third;
            Vector256<float> magnitude = Vector256.Abs(first);
            Vector256<float> tooSmall = magnitude * Vector256.Create(0.4F);
            Vector256<float> negative = Vector256.ConditionalSelect(
                Vector256.GreaterThan(second, -tooSmall),
                second + tooSmall,
                Vector256.ConditionalSelect(Vector256.LessThan(second, -magnitude), -second - magnitude, Vector256<float>.Zero));
            Vector256<float> positive = Vector256.ConditionalSelect(
                Vector256.LessThan(second, tooSmall),
                tooSmall - second,
                Vector256.ConditionalSelect(Vector256.GreaterThan(second, magnitude), second - magnitude, Vector256<float>.Zero));
            Vector256<float> residual = Vector256.ConditionalSelect(Vector256.LessThan(first, Vector256<float>.Zero), negative, positive);
            return total + (Vector256.Create(this.smallerWeight) * residual * residual);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second, Vector512<float> third)
        {
            Vector512<float> difference = first - second;
            Vector512<float> total = (difference * difference * Vector512.Create(this.largerWeight)) + third;
            Vector512<float> magnitude = Vector512.Abs(first);
            Vector512<float> tooSmall = magnitude * Vector512.Create(0.4F);
            Vector512<float> negative = Vector512.ConditionalSelect(
                Vector512.GreaterThan(second, -tooSmall),
                second + tooSmall,
                Vector512.ConditionalSelect(Vector512.LessThan(second, -magnitude), -second - magnitude, Vector512<float>.Zero));
            Vector512<float> positive = Vector512.ConditionalSelect(
                Vector512.LessThan(second, tooSmall),
                tooSmall - second,
                Vector512.ConditionalSelect(Vector512.GreaterThan(second, magnitude), second - magnitude, Vector512<float>.Zero));
            Vector512<float> residual = Vector512.ConditionalSelect(Vector512.LessThan(first, Vector512<float>.Zero), negative, positive);
            return total + (Vector512.Create(this.smallerWeight) * residual * residual);
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

    /// <summary>
    /// Transforms borrowed RGB samples into the perceptual XYB representation using prepared blur and output storage.
    /// </summary>
    /// <param name="configuration">The comparison configuration.</param>
    /// <param name="rgb">The borrowed RGB image. It may also supply the output storage.</param>
    /// <param name="parameters">The perceptual comparison parameters.</param>
    /// <param name="blurred">The distinct preallocated three-plane blur workspace.</param>
    /// <param name="blurTemp">The prepared transposed blur workspace.</param>
    /// <param name="xyb">The preallocated transformed output.</param>
    /// <returns>Whether all blur stages completed.</returns>
    public static bool OpsinDynamicsImage(
        Configuration configuration,
        JxlImage3<float> rgb,
        in ButteraugliParameters parameters,
        JxlImage3<float> blurred,
        ButteraugliBlurTemp blurTemp,
        JxlImage3<float> xyb)
    {
        if (blurred == null)
        {
            return false;
        }


        if (!Blur(rgb.Plane(0), OpsinDynamicsKernel, blurTemp, blurred.Plane(0)))
        {
            return false;
        }

        if (!Blur(rgb.Plane(1), OpsinDynamicsKernel, blurTemp, blurred.Plane(1)))
        {
            return false;
        }

        if (!Blur(rgb.Plane(2), OpsinDynamicsKernel, blurTemp, blurred.Plane(2)))
        {
            return false;
        }

        Vector<float> intensityMultiplier = new((float)parameters.IntensityTarget);

        int lanes = Vector<float>.Count;

        Vector<float> minValue = new(1e-4f);

        Span<float> rgbSamples0 = rgb.Plane(0).GetRow(0);
        Span<float> rgbSamples1 = rgb.Plane(1).GetRow(0);
        Span<float> rgbSamples2 = rgb.Plane(2).GetRow(0);
        Span<float> blurredSamples0 = blurred.Plane(0).GetRow(0);
        Span<float> blurredSamples1 = blurred.Plane(1).GetRow(0);
        Span<float> blurredSamples2 = blurred.Plane(2).GetRow(0);
        Span<float> xybSamples0 = xyb.Plane(0).GetRow(0);
        Span<float> xybSamples1 = xyb.Plane(1).GetRow(0);
        Span<float> xybSamples2 = xyb.Plane(2).GetRow(0);

        for (int y = 0; y < rgb.YSize; ++y)
        {
            ReadOnlySpan<float> rowR = rgbSamples0[(y * rgb.Plane(0).PixelsPerRow)..];
            ReadOnlySpan<float> rowG = rgbSamples1[(y * rgb.Plane(1).PixelsPerRow)..];
            ReadOnlySpan<float> rowB = rgbSamples2[(y * rgb.Plane(2).PixelsPerRow)..];

            ReadOnlySpan<float> blurredR = blurredSamples0[(y * blurred.Plane(0).PixelsPerRow)..];
            ReadOnlySpan<float> blurredG = blurredSamples1[(y * blurred.Plane(1).PixelsPerRow)..];
            ReadOnlySpan<float> blurredB = blurredSamples2[(y * blurred.Plane(2).PixelsPerRow)..];

            Span<float> outX = xybSamples0[(y * xyb.Plane(0).PixelsPerRow)..];
            Span<float> outY = xybSamples1[(y * xyb.Plane(1).PixelsPerRow)..];
            Span<float> outB = xybSamples2[(y * xyb.Plane(2).PixelsPerRow)..];

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

    /// <summary>
    /// Compares two linear RGB images while replacing their samples with intermediate frequency bands.
    /// </summary>
    /// <param name="configuration">The configuration used for temporary comparison storage.</param>
    /// <param name="image0">The reference samples, overwritten during comparison.</param>
    /// <param name="image1">The distorted samples, overwritten during comparison.</param>
    /// <param name="parameters">The perceptual comparison parameters.</param>
    /// <param name="diffmap">The preallocated output plane, with the same dimensions as the inputs.</param>
    /// <returns>Whether all comparison stages succeeded.</returns>
    public static bool ButteraugliDiffmapInPlace(
        Configuration configuration,
        JxlImage3<float> image0,
        JxlImage3<float> image1,
        in ButteraugliParameters parameters,
        JxlPlane<float> diffmap)
    {
        int xSize = image0.XSize;
        int ySize = image0.YSize;

        using ButteraugliBlurTemp blurTemp = new(configuration, xSize, ySize);

        using (JxlImage3<float> temp = new(configuration, xSize, ySize))
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

        using JxlPlane<float> blockDiffDc = JxlPlane<float>.Create(configuration, xSize, ySize);
        blockDiffDc.Clear();

        // LF/DC
        using (JxlImage3<float> lf0 = new(configuration, xSize, ySize))
        using (JxlImage3<float> lf1 = new(configuration, xSize, ySize))
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

        // The inline arrays borrow these owners. Using declarations release every partially built band on early return.
        using JxlPlane<float> hf0X = new(configuration, xSize, ySize);
        using JxlPlane<float> hf0Y = new(configuration, xSize, ySize);
        using JxlPlane<float> hf1X = new(configuration, xSize, ySize);
        using JxlPlane<float> hf1Y = new(configuration, xSize, ySize);

        hf0[0] = hf0X;
        hf0[1] = hf0Y;
        hf1[0] = hf1X;
        hf1[1] = hf1Y;

        if (!SeparateMfAndHf(configuration, image0, hf0, blurTemp))
        {
            return false;
        }

        if (!SeparateMfAndHf(configuration, image1, hf1, blurTemp))
        {
            return false;
        }

        using JxlPlane<float> blockDiffAc = new(configuration, xSize, ySize);
        blockDiffAc.Clear();

        using (JxlPlane<float> diffs = new(configuration, xSize, ySize))
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

        // The input owners remain alive. Masking reuses their middle-frequency samples after the error stages consume them.

        InlineArray2<JxlPlane<float>> uhf0 = default;
        InlineArray2<JxlPlane<float>> uhf1 = default;

        using JxlPlane<float> uhf0X = new(configuration, xSize, ySize);
        using JxlPlane<float> uhf0Y = new(configuration, xSize, ySize);
        using JxlPlane<float> uhf1X = new(configuration, xSize, ySize);
        using JxlPlane<float> uhf1Y = new(configuration, xSize, ySize);

        uhf0[0] = uhf0X;
        uhf0[1] = uhf0Y;
        uhf1[0] = uhf1X;
        uhf1[1] = uhf1Y;

        if (!SeparateHFAndUHF(configuration, hf0, uhf0, blurTemp))
        {
            return false;
        }

        if (!SeparateHFAndUHF(configuration, hf1, uhf1, blurTemp))
        {
            return false;
        }

        float hfAsymmetry = parameters.HfAsymmetry;

        using (JxlPlane<float> diffs = new(configuration, xSize, ySize))
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
        using JxlPlane<float> mask = new(configuration, xSize, ySize);
        using JxlPlane<float> mask0 = new(configuration, xSize, ySize);
        using JxlPlane<float> mask1 = new(configuration, xSize, ySize);

        CombineChannelsForMasking(hf0, uhf0, mask0);
        CombineChannelsForMasking(hf1, uhf1, mask1);

        DeallocateHFAndUHF(hf0, uhf0);
        DeallocateHFAndUHF(hf1, uhf1);

        // Native Mask adds its correction to the accumulated AC errors and writes the mask separately.
        // DC errors remain independent: the final equation applies a different mask to each frequency range.
        if (!Mask(configuration, mask0, mask1, blurTemp, blockDiffAc, mask, image0.Plane(0), image0.Plane(1), image0.Plane(2), image1.Plane(0)))
        {
            return false;
        }

        ReadOnlySpan<float> maskSamples = mask.GetRow(0);
        ReadOnlySpan<float> dcSamples = blockDiffDc.GetRow(0);
        ReadOnlySpan<float> acSamples = blockDiffAc.GetRow(0);
        Span<float> outputSamples = diffmap.GetRow(0);

        for (int y = 0; y < ySize; y++)
        {
            ReadOnlySpan<float> maskRow = maskSamples.Slice(y * mask.PixelsPerRow, xSize);
            ReadOnlySpan<float> dcRow = dcSamples.Slice(y * blockDiffDc.PixelsPerRow, xSize);
            ReadOnlySpan<float> acRow = acSamples.Slice(y * blockDiffAc.PixelsPerRow, xSize);
            Span<float> outputRow = outputSamples.Slice(y * diffmap.PixelsPerRow, xSize);

            // Each lane is one pixel. The shared traversal selects 512-, 256-, 128-bit or scalar arithmetic and handles row tails.
            TensorPrimitives_.Invoke(maskRow, dcRow, acRow, outputRow, default(MaskedDifferenceOperator));
        }

        return true;
    }

    /// <summary>
    /// Allocates a half-resolution image for reference setup, averaging each available two-by-two source group.
    /// </summary>
    /// <param name="configuration">The allocation configuration.</param>
    /// <param name="input">The borrowed full-resolution image.</param>
    /// <returns>The caller-owned half-resolution image.</returns>
    public static JxlImage3<float> SubSample2x(Configuration configuration, JxlImage3<float> input)
    {
        JxlImage3<float> output = new(configuration, (input.XSize + 1) / 2, (input.YSize + 1) / 2);
        try
        {
            SubSample2x(input, output);
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Writes half-resolution samples into prepared storage without allocating or changing either image's dimensions.
    /// </summary>
    /// <param name="input">The borrowed full-resolution image.</param>
    /// <param name="output">The distinct preallocated image with each dimension rounded up after division by two.</param>
    public static void SubSample2x(JxlImage3<float> input, JxlImage3<float> output)
    {
        const float contribution = 0.25F;
        int pairedWidth = input.XSize / 2;
        int pairedHeight = input.YSize / 2;
        bool hasFinalColumn = (input.XSize & 1) != 0;
        bool hasFinalRow = (input.YSize & 1) != 0;

        for (int c = 0; c < 3; c++)
        {
            JxlPlane<float> inputPlane = input.Plane(c);
            JxlPlane<float> outputPlane = output.Plane(c);
            ReadOnlySpan<float> sourceSamples = inputPlane.GetRow(0);
            Span<float> outputSamples = outputPlane.GetRow(0);
            int sourceStride = inputPlane.PixelsPerRow;
            int outputStride = outputPlane.PixelsPerRow;

            for (int y = 0; y < pairedHeight; y++)
            {
                ref float upper = ref MemoryMarshal.GetReference(sourceSamples.Slice(2 * y * sourceStride, input.XSize));
                ref float lower = ref MemoryMarshal.GetReference(sourceSamples.Slice(((2 * y) + 1) * sourceStride, input.XSize));
                ref float destination = ref MemoryMarshal.GetReference(outputSamples.Slice(y * outputStride, output.XSize));
                int x = 0;

                // Two source registers contain consecutive even/odd pairs. Deinterleave2 places one source pair in each
                // output lane, without changing float bits. The four quarter contributions retain native row-major order:
                // upper even, upper odd, lower even, lower odd. The initial zero addition also retains native signed-zero behavior.
                // Only complete pairs enter SIMD; source padding and an unmatched final column are never loaded here.
                if (Vector512.IsHardwareAccelerated)
                {
                    for (; x <= pairedWidth - Vector512<float>.Count; x += Vector512<float>.Count)
                    {
                        uint sourceX = (uint)(2 * x);
                        Vector512<int> upperFirst = Vector512.LoadUnsafe(ref upper, sourceX).AsInt32();
                        Vector512<int> upperSecond = Vector512.LoadUnsafe(ref upper, sourceX + (uint)Vector512<float>.Count).AsInt32();
                        SimdUtils.Deinterleave2(upperFirst, upperSecond, out Vector512<int> upperEven, out Vector512<int> upperOdd);
                        Vector512<int> lowerFirst = Vector512.LoadUnsafe(ref lower, sourceX).AsInt32();
                        Vector512<int> lowerSecond = Vector512.LoadUnsafe(ref lower, sourceX + (uint)Vector512<float>.Count).AsInt32();
                        SimdUtils.Deinterleave2(lowerFirst, lowerSecond, out Vector512<int> lowerEven, out Vector512<int> lowerOdd);
                        Vector512<float> result = Vector512<float>.Zero + (contribution * upperEven.AsSingle());
                        result += contribution * upperOdd.AsSingle();
                        result += contribution * lowerEven.AsSingle();
                        result += contribution * lowerOdd.AsSingle();
                        result.StoreUnsafe(ref destination, (uint)x);
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    for (; x <= pairedWidth - Vector256<float>.Count; x += Vector256<float>.Count)
                    {
                        uint sourceX = (uint)(2 * x);
                        Vector256<int> upperFirst = Vector256.LoadUnsafe(ref upper, sourceX).AsInt32();
                        Vector256<int> upperSecond = Vector256.LoadUnsafe(ref upper, sourceX + (uint)Vector256<float>.Count).AsInt32();
                        SimdUtils.Deinterleave2(upperFirst, upperSecond, out Vector256<int> upperEven, out Vector256<int> upperOdd);
                        Vector256<int> lowerFirst = Vector256.LoadUnsafe(ref lower, sourceX).AsInt32();
                        Vector256<int> lowerSecond = Vector256.LoadUnsafe(ref lower, sourceX + (uint)Vector256<float>.Count).AsInt32();
                        SimdUtils.Deinterleave2(lowerFirst, lowerSecond, out Vector256<int> lowerEven, out Vector256<int> lowerOdd);
                        Vector256<float> result = Vector256<float>.Zero + (contribution * upperEven.AsSingle());
                        result += contribution * upperOdd.AsSingle();
                        result += contribution * lowerEven.AsSingle();
                        result += contribution * lowerOdd.AsSingle();
                        result.StoreUnsafe(ref destination, (uint)x);
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    for (; x <= pairedWidth - Vector128<float>.Count; x += Vector128<float>.Count)
                    {
                        uint sourceX = (uint)(2 * x);
                        Vector128<int> upperFirst = Vector128.LoadUnsafe(ref upper, sourceX).AsInt32();
                        Vector128<int> upperSecond = Vector128.LoadUnsafe(ref upper, sourceX + (uint)Vector128<float>.Count).AsInt32();
                        SimdUtils.Deinterleave2(upperFirst, upperSecond, out Vector128<int> upperEven, out Vector128<int> upperOdd);
                        Vector128<int> lowerFirst = Vector128.LoadUnsafe(ref lower, sourceX).AsInt32();
                        Vector128<int> lowerSecond = Vector128.LoadUnsafe(ref lower, sourceX + (uint)Vector128<float>.Count).AsInt32();
                        SimdUtils.Deinterleave2(lowerFirst, lowerSecond, out Vector128<int> lowerEven, out Vector128<int> lowerOdd);
                        Vector128<float> result = Vector128<float>.Zero + (contribution * upperEven.AsSingle());
                        result += contribution * upperOdd.AsSingle();
                        result += contribution * lowerEven.AsSingle();
                        result += contribution * lowerOdd.AsSingle();
                        result.StoreUnsafe(ref destination, (uint)x);
                    }
                }

                for (; x < pairedWidth; x++)
                {
                    int sourceX = 2 * x;
                    float result = 0F + (contribution * Unsafe.Add(ref upper, sourceX));
                    result += contribution * Unsafe.Add(ref upper, sourceX + 1);
                    result += contribution * Unsafe.Add(ref lower, sourceX);
                    result += contribution * Unsafe.Add(ref lower, sourceX + 1);
                    Unsafe.Add(ref destination, x) = result;
                }

                if (hasFinalColumn)
                {
                    // An unmatched column contributes two samples instead of four. Native doubles after both additions.
                    float result = 0F + (contribution * Unsafe.Add(ref upper, input.XSize - 1));
                    result += contribution * Unsafe.Add(ref lower, input.XSize - 1);
                    Unsafe.Add(ref destination, pairedWidth) = result * 2F;
                }
            }

            if (hasFinalRow)
            {
                ref float upper = ref MemoryMarshal.GetReference(sourceSamples.Slice((input.YSize - 1) * sourceStride, input.XSize));
                ref float destination = ref MemoryMarshal.GetReference(outputSamples.Slice(pairedHeight * outputStride, output.XSize));
                int x = 0;

                // The unmatched row has only the two upper contributions. Keep it outside the full-row loops so their
                // register loads need no missing-row branch. Native doubles this completed row after its column correction.
                if (Vector512.IsHardwareAccelerated)
                {
                    for (; x <= pairedWidth - Vector512<float>.Count; x += Vector512<float>.Count)
                    {
                        uint sourceX = (uint)(2 * x);
                        Vector512<int> upperFirst = Vector512.LoadUnsafe(ref upper, sourceX).AsInt32();
                        Vector512<int> upperSecond = Vector512.LoadUnsafe(ref upper, sourceX + (uint)Vector512<float>.Count).AsInt32();
                        SimdUtils.Deinterleave2(upperFirst, upperSecond, out Vector512<int> upperEven, out Vector512<int> upperOdd);
                        Vector512<float> result = Vector512<float>.Zero + (contribution * upperEven.AsSingle());
                        result += contribution * upperOdd.AsSingle();
                        result *= 2F;
                        result.StoreUnsafe(ref destination, (uint)x);
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    for (; x <= pairedWidth - Vector256<float>.Count; x += Vector256<float>.Count)
                    {
                        uint sourceX = (uint)(2 * x);
                        Vector256<int> upperFirst = Vector256.LoadUnsafe(ref upper, sourceX).AsInt32();
                        Vector256<int> upperSecond = Vector256.LoadUnsafe(ref upper, sourceX + (uint)Vector256<float>.Count).AsInt32();
                        SimdUtils.Deinterleave2(upperFirst, upperSecond, out Vector256<int> upperEven, out Vector256<int> upperOdd);
                        Vector256<float> result = Vector256<float>.Zero + (contribution * upperEven.AsSingle());
                        result += contribution * upperOdd.AsSingle();
                        result *= 2F;
                        result.StoreUnsafe(ref destination, (uint)x);
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    for (; x <= pairedWidth - Vector128<float>.Count; x += Vector128<float>.Count)
                    {
                        uint sourceX = (uint)(2 * x);
                        Vector128<int> upperFirst = Vector128.LoadUnsafe(ref upper, sourceX).AsInt32();
                        Vector128<int> upperSecond = Vector128.LoadUnsafe(ref upper, sourceX + (uint)Vector128<float>.Count).AsInt32();
                        SimdUtils.Deinterleave2(upperFirst, upperSecond, out Vector128<int> upperEven, out Vector128<int> upperOdd);
                        Vector128<float> result = Vector128<float>.Zero + (contribution * upperEven.AsSingle());
                        result += contribution * upperOdd.AsSingle();
                        result *= 2F;
                        result.StoreUnsafe(ref destination, (uint)x);
                    }
                }

                for (; x < pairedWidth; x++)
                {
                    int sourceX = 2 * x;
                    float result = 0F + (contribution * Unsafe.Add(ref upper, sourceX));
                    result += contribution * Unsafe.Add(ref upper, sourceX + 1);
                    Unsafe.Add(ref destination, x) = result * 2F;
                }

                if (hasFinalColumn)
                {
                    float result = 0F + (contribution * Unsafe.Add(ref upper, input.XSize - 1));
                    result *= 2F;
                    Unsafe.Add(ref destination, pairedWidth) = result * 2F;
                }
            }
        }
    }

    /// <summary>
    /// Mixes a borrowed lower-resolution difference map into the full-resolution map using nearest-neighbor expansion.
    /// </summary>
    /// <param name="src">The half-resolution difference map.</param>
    /// <param name="w">The lower-resolution contribution weight.</param>
    /// <param name="dest">The full-resolution map, updated in place.</param>
    public static void AddSupersampled2x(JxlPlane<float> src, float w, JxlPlane<float> dest)
    {
        const float heuristicMixingValue = 0.3F;
        ReadOnlySpan<float> sourceSamples = src.GetRow(0);
        Span<float> destinationSamples = dest.GetRow(0);

        for (int y = 0; y < dest.YSize; y++)
        {
            ReadOnlySpan<float> sourceRow = sourceSamples[((y / 2) * src.PixelsPerRow)..];
            Span<float> destinationRow = destinationSamples[(y * dest.PixelsPerRow)..];

            for (int x = 0; x < dest.XSize; x++)
            {
                destinationRow[x] *= 1F - (heuristicMixingValue * w);
                destinationRow[x] += w * sourceRow[x / 2];
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

    public static JxlImage3<float> CreateHeatMapImage(Configuration configuration, JxlPlane<float> distmap, float goodThreshold, float badThreshold)
    {
        // Do not dispose (this is the return value; it is caller's responsibility to dispose this
        // or keep it)
        JxlImage3<float> heatmap = new(configuration, distmap.XSize, distmap.YSize);

        ReadOnlySpan<float> differenceSamples = distmap.GetRow(0);
        Span<float> heatmap0 = heatmap.Plane(0).GetRow(0);
        Span<float> heatmap1 = heatmap.Plane(1).GetRow(0);
        Span<float> heatmap2 = heatmap.Plane(2).GetRow(0);

        for (int y = 0; y < distmap.YSize; y++)
        {
            ReadOnlySpan<float> row_distmap = differenceSamples[(y * distmap.PixelsPerRow)..];
            Span<float> row_h0 = heatmap0[(y * heatmap.Plane(0).PixelsPerRow)..];
            Span<float> row_h1 = heatmap1[(y * heatmap.Plane(1).PixelsPerRow)..];
            Span<float> row_h2 = heatmap2[(y * heatmap.Plane(2).PixelsPerRow)..];

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

    /// <summary>
    /// Computes the perceptual difference map and score, using input storage for full-size comparisons.
    /// </summary>
    /// <param name="configuration">The configuration used for sample allocations.</param>
    /// <param name="rgb0">The reference pixels, which full-size comparison can modify.</param>
    /// <param name="rgb1">The distorted pixels, which full-size comparison can modify.</param>
    /// <param name="parameters">The perceptual comparison parameters.</param>
    /// <param name="diffmap">The caller-owned output plane. Storage is reused when it fits; the plane's identity does not change.</param>
    /// <param name="diffvalue">The comparison score, or zero when comparison fails.</param>
    /// <returns>Whether every comparison stage succeeded.</returns>
    public static bool ButteraugliInterfaceInPlace(
        Configuration configuration,
        JxlImage3<float> rgb0,
        JxlImage3<float> rgb1,
        ButteraugliParameters parameters,
        JxlPlane<float> diffmap,
        out double diffvalue)
    {
        diffvalue = 0;

        int xsize = rgb0.XSize;
        int ysize = rgb0.YSize;

        if (xsize == 0 || ysize == 0)
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
            bool completed = ButteraugliDiffmapSmall(configuration, max, rgb0, rgb1, parameters, out JxlPlane<float> smallDiffmap);

            using (smallDiffmap)
            {
                if (!completed)
                {
                    return false;
                }

                diffvalue = ButteraugliScoreFromDiffmap(smallDiffmap, parameters);

                // Transfer the completed allocation into the caller's plane. The temporary releases the caller's previous allocation.
                diffmap.Swap(smallDiffmap);
                return true;
            }
        }

        // The half-size result must survive the full-size comparison. Each result has its own dimensions and allocation.
        // Smaller images need no second comparison; zero dimensions leave this scoped owner without sample storage.
        bool compareSubsample = xsize >= 15 && ysize >= 15;
        using JxlPlane<float> subDiffmap = new(configuration, compareSubsample ? (xsize + 1) / 2 : 0, compareSubsample ? (ysize + 1) / 2 : 0);

        if (compareSubsample)
        {
            using JxlImage3<float> rgb0Sub = SubSample2x(configuration, rgb0);
            using JxlImage3<float> rgb1Sub = SubSample2x(configuration, rgb1);

            if (!ButteraugliDiffmapInPlace(configuration, rgb0Sub, rgb1Sub, parameters, subDiffmap))
            {
                return false;
            }
        }

        // The raw comparison writes preallocated output. This entry point owns preparation, including an initially empty output plane.
        // Swap only after allocation succeeds, and release the displaced allocation through the scoped replacement.
        if (!diffmap.ShrinkTo(xsize, ysize))
        {
            using JxlPlane<float> outputStorage = new(configuration, xsize, ysize);
            diffmap.Swap(outputStorage);
        }

        if (!ButteraugliDiffmapInPlace(configuration, rgb0, rgb1, parameters, diffmap))
        {
            return false;
        }

        if (compareSubsample)
        {
            AddSupersampled2x(subDiffmap, 0.5f, diffmap);
        }

        diffvalue = ButteraugliScoreFromDiffmap(diffmap, parameters);
        return true;
    }

    /// <summary>
    /// Computes the perceptual difference map and score without changing either input image.
    /// </summary>
    /// <param name="configuration">The configuration used for sample allocations.</param>
    /// <param name="rgb0">The borrowed reference pixels.</param>
    /// <param name="rgb1">The borrowed distorted pixels of the same size.</param>
    /// <param name="parameters">The perceptual comparison parameters.</param>
    /// <param name="diffmap">The caller-owned plane that receives the completed map. Its identity does not change.</param>
    /// <param name="diffValue">The comparison score, or zero when comparison fails.</param>
    /// <returns>Whether every comparison stage succeeded.</returns>
    public static bool ButteraugliInterface(
        Configuration configuration,
        JxlImage3<float> rgb0,
        JxlImage3<float> rgb1,
        ButteraugliParameters parameters,
        JxlPlane<float> diffmap,
        out double diffValue)
    {
        diffValue = 0;

        bool completed = ButteraugliDiffmap(configuration, rgb0, rgb1, parameters, out JxlPlane<float> result);

        using (result)
        {
            if (!completed)
            {
                return false;
            }

            diffValue = ButteraugliScoreFromDiffmap(result, parameters);

            // Keep the caller's plane object and dispose its previous allocation after the storage exchange.
            diffmap.Swap(result);
            return true;
        }
    }

    public static bool ButteraugliInterface(
        Configuration configuration,
        JxlImage3<float> rgb0,
        JxlImage3<float> rgb1,
        float hfAsymmetry,
        float xmul,
        JxlPlane<float> diffmap,
        out double diffValue)
    {
        ButteraugliParameters parameters = new()
        {
            HfAsymmetry = hfAsymmetry,
            XMultiplier = xmul
        };

        return ButteraugliInterface(configuration, rgb0, rgb1, parameters, diffmap, out diffValue);
    }

    /// <summary>
    /// Allocates the perceptual difference map for two borrowed RGB images.
    /// </summary>
    /// <param name="configuration">The configuration used for sample allocations.</param>
    /// <param name="rgb0">The borrowed reference pixels.</param>
    /// <param name="rgb1">The borrowed distorted pixels of the same size.</param>
    /// <param name="parameters">The perceptual comparison parameters.</param>
    /// <param name="diffmap">The output owner transferred on success, or an empty plane on failure.</param>
    /// <returns>Whether every comparison stage succeeded.</returns>
    public static bool ButteraugliDiffmap(
        Configuration configuration,
        JxlImage3<float> rgb0,
        JxlImage3<float> rgb1,
        ButteraugliParameters parameters,
        out JxlPlane<float> diffmap)
    {
        int xsize = rgb0.XSize;
        int ysize = rgb0.YSize;

        if (xsize == 0 || ysize == 0)
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
            // The padded comparison already produced the result. Do not replace it with the small comparator's empty decomposition.
            return ButteraugliDiffmapSmall(configuration, max, rgb0, rgb1, parameters, out diffmap);
        }

        using ButteraugliComparator butteraugli = ButteraugliComparator.Make(configuration, rgb0, parameters);
        diffmap = new(configuration, xsize, ysize);
        bool completed = false;

        try
        {
            completed = butteraugli.Diffmap(configuration, rgb1, diffmap);
            return completed;
        }
        finally
        {
            if (!completed)
            {
                diffmap.Dispose();
            }
        }
    }

    /// <summary>
    /// Compares padded small images and transfers only the original-size region of the resulting difference map.
    /// </summary>
    /// <param name="configuration">The configuration used for sample allocations.</param>
    /// <param name="max">The minimum comparison extent in each dimension.</param>
    /// <param name="rgb0">The borrowed reference pixels.</param>
    /// <param name="rgb1">The borrowed distorted pixels of the same size.</param>
    /// <param name="parameters">The perceptual comparison parameters.</param>
    /// <param name="diffmap">The original-size output owner transferred on success, or an empty plane on failure.</param>
    /// <returns>Whether the padded comparison succeeded.</returns>
    public static bool ButteraugliDiffmapSmall(
        Configuration configuration,
        int max,
        JxlImage3<float> rgb0,
        JxlImage3<float> rgb1,
        ButteraugliParameters parameters,
        out JxlPlane<float> diffmap)
    {
        int xsize = rgb0.XSize;
        int ysize = rgb0.YSize;

        int xborder = xsize < max ? (max - xsize) / 2 : 0;
        int yborder = ysize < max ? (max - ysize) / 2 : 0;
        int xscaled = Math.Max(max, xsize);
        int yscaled = Math.Max(max, ysize);

        using JxlImage3<float> scaled0 = new(configuration, xscaled, yscaled);
        using JxlImage3<float> scaled1 = new(configuration, xscaled, yscaled);

        for (int i = 0; i < 3; ++i)
        {
            ReadOnlySpan<float> source0 = rgb0.Plane(i).GetRow(0);
            ReadOnlySpan<float> source1 = rgb1.Plane(i).GetRow(0);
            Span<float> destination0 = scaled0.Plane(i).GetRow(0);
            Span<float> destination1 = scaled1.Plane(i).GetRow(0);

            for (int y = 0; y < yscaled; y++)
            {
                for (int x = 0; x < xscaled; x++)
                {
                    int x2 = Math.Min(xsize - 1, x > xborder ? x - xborder : 0);
                    int y2 = Math.Min(ysize - 1, y > yborder ? y - yborder : 0);

                    destination0[(y * scaled0.Plane(i).PixelsPerRow) + x] = source0[(y2 * rgb0.Plane(i).PixelsPerRow) + x2];
                    destination1[(y * scaled1.Plane(i).PixelsPerRow) + x] = source1[(y2 * rgb1.Plane(i).PixelsPerRow) + x2];
                }
            }
        }

        bool completed = ButteraugliDiffmap(configuration, scaled0, scaled1, parameters, out JxlPlane<float> diffmapScaled);

        using (diffmapScaled)
        {
            if (!completed)
            {
                diffmap = new();
                return false;
            }

            diffmap = new(configuration, xsize, ysize);
            bool copied = false;

            try
            {
                ReadOnlySpan<float> scaledSamples = diffmapScaled.GetRow(0);
                Span<float> outputSamples = diffmap.GetRow(0);

                for (int y = 0; y < ysize; y++)
                {
                    scaledSamples.Slice(((y + yborder) * diffmapScaled.PixelsPerRow) + xborder, xsize)
                        .CopyTo(outputSamples.Slice(y * diffmap.PixelsPerRow, xsize));
                }

                copied = true;
                return true;
            }
            finally
            {
                if (!copied)
                {
                    diffmap.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Returns the largest logical difference-map sample.
    /// </summary>
    /// <param name="diffmap">The borrowed difference map; row padding is excluded.</param>
    /// <param name="parameters">The comparison parameters retained by the score API.</param>
    /// <returns>The maximum difference-map sample, with a lower bound of zero.</returns>
    public static float ButteraugliScoreFromDiffmap(JxlPlane<float> diffmap, ButteraugliParameters parameters)
    {
        float retval = 0.0f;

        ReadOnlySpan<float> samples = diffmap.GetRow(0);

        for (int y = 0; y < diffmap.YSize; ++y)
        {
            ReadOnlySpan<float> row = samples[(y * diffmap.PixelsPerRow)..];
            for (int x = 0; x < diffmap.XSize; ++x)
            {
                retval = Math.Max(retval, row[x]);
            }
        }

        return retval;
    }

    public static bool MaltaDiffMap(
        JxlPlane<float> lum0,
        JxlPlane<float> lum1,
        float w0Gt1,
        float w0Lt1,
        float norm1,
        JxlPlane<float> diffs,
        JxlImage3<float> blockDiffAc,
        int c)
        => MaltaDiffMap(isLf: false, lum0, lum1, w0Gt1, w0Lt1, norm1, diffs, blockDiffAc.Plane(c));

    public static bool MaltaDiffMapLf(
        JxlPlane<float> lum0,
        JxlPlane<float> lum1,
        float w0Gt1,
        float w0Lt1,
        float norm1,
        JxlPlane<float> diffs,
        JxlImage3<float> blockDiffAc,
        int c)
        => MaltaDiffMap(isLf: true, lum0, lum1, w0Gt1, w0Lt1, norm1, diffs, blockDiffAc.Plane(c));
}
