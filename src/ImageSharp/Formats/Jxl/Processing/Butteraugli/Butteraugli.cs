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
/// Computes perceptual difference maps and scores for linear RGB images.
/// </summary>
internal static partial class Butteraugli
{
    /// <summary>The penalty weight for Y middle-frequency Malta differences.</summary>
    public const double WMfMalta = 37.0819870399D;

    /// <summary>The Y magnitude offset used to normalize middle-frequency Malta differences.</summary>
    public const double Norm1Mf = 130262059.556D;

    /// <summary>The penalty weight for X middle-frequency Malta differences.</summary>
    public const double WMfMaltaX = 8246.75321353D;

    /// <summary>The X magnitude offset used to normalize middle-frequency Malta differences.</summary>
    public const double Norm1MfX = 1009002.70582D;

    /// <summary>The penalty weight for Y high-frequency Malta differences.</summary>
    public const double WHfMalta = 18.7237414387D;

    /// <summary>The Y magnitude offset used to normalize high-frequency Malta differences.</summary>
    public const double Norm1Hf = 4498534.45232D;

    /// <summary>The penalty weight for X high-frequency Malta differences.</summary>
    public const double WHfMaltaX = 6923.99476109D;

    /// <summary>The X magnitude offset used to normalize high-frequency Malta differences.</summary>
    public const double Norm1HfX = 8051.15833247D;

    /// <summary>The penalty weight for Y ultra-high-frequency Malta differences.</summary>
    public const double WUhfMalta = 1.10039032555D;

    /// <summary>The Y magnitude offset used to normalize ultra-high-frequency Malta differences.</summary>
    public const double Norm1Uhf = 71.7800275169D;

    /// <summary>The penalty weight for X ultra-high-frequency Malta differences.</summary>
    public const double WUhfMaltaX = 173.5D;

    /// <summary>The X magnitude offset used to normalize ultra-high-frequency Malta differences.</summary>
    public const double Norm1UhfX = 5.0D;

    private const float IntensityTargetNormalizationHack = 0.79079917404f;

    private const float InternalGoodQualityThreshold =
        17.83f * IntensityTargetNormalizationHack;

    private const float GlobalScale =
        1.0f / InternalGoodQualityThreshold;

    /// <summary>The red contribution to the first perceptual absorbance.</summary>
    private const float Absorbance0Red = 0.29956550340058319F;

    /// <summary>The green contribution to the first perceptual absorbance.</summary>
    private const float Absorbance0Green = 0.63373087833825936F;

    /// <summary>The blue contribution to the first perceptual absorbance.</summary>
    private const float Absorbance0Blue = 0.077705617820981968F;

    /// <summary>The red contribution to the second perceptual absorbance.</summary>
    private const float Absorbance1Red = 0.22158691104574774F;

    /// <summary>The green contribution to the second perceptual absorbance.</summary>
    private const float Absorbance1Green = 0.69391388044116142F;

    /// <summary>The blue contribution to the second perceptual absorbance.</summary>
    private const float Absorbance1Blue = 0.0987313588422F;

    /// <summary>The red and green contributions to the third perceptual absorbance.</summary>
    private const float Absorbance2RedGreen = 0.02F;

    /// <summary>The blue contribution to the third perceptual absorbance.</summary>
    private const float Absorbance2Blue = 0.20480129041026129F;

    /// <summary>The native positive offset and reconstruction floor for the first two absorbances.</summary>
    private const float Absorbance01Bias = 1.7557483643287353F;

    /// <summary>The native positive offset and reconstruction floor for the third absorbance.</summary>
    private const float Absorbance2Bias = 12.226454707163354F;

    /// <summary>The native lower bound for the blurred image's sensitivity response.</summary>
    private const float MinimumOpsinSensitivity = 1e-4F;

    /// <summary>The positive offset inside the perceptual response's logarithm.</summary>
    private const float OpsinGammaInputBias = 9.9710635769299145F;

    /// <summary>The native float product that converts log2 to the scaled natural-log response.</summary>
    private const float OpsinGammaSlope = 19.245013259874995F * JxlMath.InverseLog2E;

    /// <summary>The offset applied after the perceptual logarithm.</summary>
    private const float OpsinGammaOffset = -23.16046239805755F;

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

    // Native ComputeKernel first converts sigma to float, then evaluates its scaler and exponential in double.
    // These fixed coefficients retain the final rounded float bits. Border normalization and Opsin normalization
    // remain separate float operations, in native order.
    /// <summary>
    /// Gets the five-tap Opsin Gaussian with float sigma 1.2 and a radius of two samples.
    /// </summary>
    private static ReadOnlySpan<float> OpsinDynamicsKernel => [0.249352232F, 0.70664829F, 1F, 0.70664829F, 0.249352232F];

    /// <summary>
    /// Gets the 33-tap low-frequency Gaussian with float sigma 7.15593339443 and a radius of 16 samples.
    /// </summary>
    private static ReadOnlySpan<float> LowFrequencyKernel =>
    [
        0.0821145922F, 0.111141749F, 0.147520736F, 0.19202058F, 0.245110229F, 0.306827277F, 0.376656473F, 0.453435808F, 0.535309672F, 0.619745314F,
        0.703623533F, 0.783405066F, 0.855364621F, 0.915872633F, 0.961696029F, 0.99028331F,
        1F,
        0.99028331F, 0.961696029F, 0.915872633F, 0.855364621F, 0.783405066F, 0.703623533F, 0.619745314F, 0.535309672F, 0.453435808F, 0.376656473F,
        0.306827277F, 0.245110229F, 0.19202058F, 0.147520736F, 0.111141749F, 0.0821145922F
    ];

    /// <summary>
    /// Gets the 15-tap high-frequency Gaussian with float sigma 3.22489901262 and a radius of seven samples.
    /// </summary>
    private static ReadOnlySpan<float> HighFrequencyKernel =>
    [
        0.0948199779F, 0.177147314F, 0.300614655F, 0.463368475F, 0.648759365F, 0.825052559F, 0.953060329F,
        1F,
        0.953060329F, 0.825052559F, 0.648759365F, 0.463368475F, 0.300614655F, 0.177147314F, 0.0948199779F
    ];

    /// <summary>
    /// Gets the seven-tap ultra-high-frequency Gaussian with float sigma 1.56416327805 and a radius of three samples.
    /// </summary>
    private static ReadOnlySpan<float> UltraHighFrequencyKernel => [0.158931509F, 0.441552371F, 0.815165043F, 1F, 0.815165043F, 0.441552371F, 0.158931509F];

    /// <summary>
    /// Gets the 13-tap masking Gaussian with float sigma 2.7 and a radius of six samples.
    /// </summary>
    private static ReadOnlySpan<float> MaskingKernel =>
    [
        0.0846579969F, 0.180021748F, 0.333739787F, 0.539407492F, 0.760067225F, 0.933712125F,
        1F,
        0.933712125F, 0.760067225F, 0.539407492F, 0.333739787F, 0.180021748F, 0.0846579969F
    ];

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
        for (int c = 0; c < 2; c++)
        {
            // Keep the original middle-frequency samples in place. This blur writes its result into the prepared HF plane
            // through the existing transposed workspace; separation can then consume both inputs without a copy pass.
            if (!Blur(mf.Plane(c), HighFrequencyKernel, blurTemp, hf[c]))
            {
                return false;
            }

            if (c == 0)
            {
                SeparateFrequencyBands(
                    mf.Plane(c),
                    hf[c],
                    default(TensorPrimitives_.IdentityOperator),
                    new RemoveRangeAroundZeroOperator(0.29F),
                    default(TensorPrimitives_.IdentityOperator));

            }
            else
            {
                SeparateFrequencyBands(
                    mf.Plane(c),
                    hf[c],
                    default(TensorPrimitives_.IdentityOperator),
                    new AmplifyRangeAroundZeroOperator(0.1F),
                    default(TensorPrimitives_.IdentityOperator));

            }
        }

        // B has no separate HF plane. Its complete result remains in the middle-frequency image.
        if (!Blur(mf.Plane(2), HighFrequencyKernel, blurTemp, mf.Plane(2)))
        {
            return false;
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
        for (int c = 0; c < 2; c++)
        {
            if (!Blur(hf[c], UltraHighFrequencyKernel, blurTemp, uhf[c]))
            {
                return false;
            }

            if (c == 0)
            {
                SeparateFrequencyBands(
                    hf[c],
                    uhf[c],
                    default(TensorPrimitives_.IdentityOperator),
                    new RemoveRangeAroundZeroOperator(1.5F),
                    new RemoveRangeAroundZeroOperator(0.04F));

            }
            else
            {
                // Native Y separation clips the blurred HF before subtracting it from the original HF.
                // The retained HF is then scaled and amplified; UHF is clipped and scaled independently.
                SeparateFrequencyBands(
                    hf[c],
                    uhf[c],
                    new MaximumClampOperator(28.4691806922F),
                    default(HighFrequencyYOperator),
                    default(UltraHighFrequencyYOperator));

            }
        }

        return true;
    }

    /// <summary>
    /// Separates original and blurred samples into two bands using the selected component equations.
    /// </summary>
    /// <typeparam name="TBeforeSubtraction">The operation applied to blurred samples before residual subtraction.</typeparam>
    /// <typeparam name="TLowFrequency">The operation applied to the subtraction base for the retained lower-frequency band.</typeparam>
    /// <typeparam name="THighFrequency">The operation applied to residual samples for the higher-frequency band.</typeparam>
    /// <param name="original">The original samples, replaced with the lower-frequency result.</param>
    /// <param name="blurred">The distinct blurred samples, replaced with the higher-frequency result.</param>
    /// <param name="beforeSubtraction">The selected operation for the subtraction base.</param>
    /// <param name="lowFrequency">The selected lower-frequency equation.</param>
    /// <param name="highFrequency">The selected higher-frequency equation.</param>
    private static void SeparateFrequencyBands<TBeforeSubtraction, TLowFrequency, THighFrequency>(
        JxlPlane<float> original,
        JxlPlane<float> blurred,
        TBeforeSubtraction beforeSubtraction,
        TLowFrequency lowFrequency,
        THighFrequency highFrequency)
        where TBeforeSubtraction : struct, TensorPrimitives_.IUnaryOperator
        where TLowFrequency : struct, TensorPrimitives_.IUnaryOperator
        where THighFrequency : struct, TensorPrimitives_.IUnaryOperator
    {
        Span<float> originalSamples = original.GetRow(0);
        Span<float> blurredSamples = blurred.GetRow(0);
        int originalStride = original.PixelsPerRow;
        int blurredStride = blurred.PixelsPerRow;
        int width = original.XSize;

        for (int y = 0; y < original.YSize; y++)
        {
            ref float originalRow = ref MemoryMarshal.GetReference(originalSamples.Slice(y * originalStride, width));
            ref float blurredRow = ref MemoryMarshal.GetReference(blurredSamples.Slice(y * blurredStride, width));
            int x = 0;

            // Corresponding lanes contain one original sample and its blurred base. Load both before either store.
            // Closed operator types select each channel's equations outside traversal; no channel or stage flag enters
            // these loops. Complete registers and scalar tails touch only logical samples, regardless of physical stride.
            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<float>.Count; x += Vector512<float>.Count)
                {
                    Vector512<float> source = Vector512.LoadUnsafe(ref originalRow, (uint)x);
                    Vector512<float> baseFrequency = beforeSubtraction.Invoke(Vector512.LoadUnsafe(ref blurredRow, (uint)x));
                    Vector512<float> residual = source - baseFrequency;
                    lowFrequency.Invoke(baseFrequency).StoreUnsafe(ref originalRow, (uint)x);
                    highFrequency.Invoke(residual).StoreUnsafe(ref blurredRow, (uint)x);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<float>.Count; x += Vector256<float>.Count)
                {
                    Vector256<float> source = Vector256.LoadUnsafe(ref originalRow, (uint)x);
                    Vector256<float> baseFrequency = beforeSubtraction.Invoke(Vector256.LoadUnsafe(ref blurredRow, (uint)x));
                    Vector256<float> residual = source - baseFrequency;
                    lowFrequency.Invoke(baseFrequency).StoreUnsafe(ref originalRow, (uint)x);
                    highFrequency.Invoke(residual).StoreUnsafe(ref blurredRow, (uint)x);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<float>.Count; x += Vector128<float>.Count)
                {
                    Vector128<float> source = Vector128.LoadUnsafe(ref originalRow, (uint)x);
                    Vector128<float> baseFrequency = beforeSubtraction.Invoke(Vector128.LoadUnsafe(ref blurredRow, (uint)x));
                    Vector128<float> residual = source - baseFrequency;
                    lowFrequency.Invoke(baseFrequency).StoreUnsafe(ref originalRow, (uint)x);
                    highFrequency.Invoke(residual).StoreUnsafe(ref blurredRow, (uint)x);
                }
            }

            for (; x < width; x++)
            {
                float source = Unsafe.Add(ref originalRow, x);
                float baseFrequency = beforeSubtraction.Invoke(Unsafe.Add(ref blurredRow, x));
                float residual = source - baseFrequency;
                Unsafe.Add(ref originalRow, x) = lowFrequency.Invoke(baseFrequency);
                Unsafe.Add(ref blurredRow, x) = highFrequency.Invoke(residual);
            }
        }
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

    /// <summary>
    /// Accumulates a Malta difference with the selected frequency range's normalization and neighborhood.
    /// </summary>
    /// <param name="isLf">Whether to use the low-frequency Malta equation.</param>
    /// <param name="lum0">The borrowed reference band, at least eight samples wide and eight rows high.</param>
    /// <param name="lum1">The equally sized borrowed distorted band.</param>
    /// <param name="w0Gt1">The primary symmetric-difference weight, retained in double until operation setup.</param>
    /// <param name="w0Lt1">The secondary magnitude-penalty weight, retained in double until operation setup.</param>
    /// <param name="norm1">The magnitude normalization, retained in double until operation setup.</param>
    /// <param name="diffs">The equally sized prepared-difference workspace, separate from the input and AC planes.</param>
    /// <param name="blockDiffAc">The equally sized existing AC accumulation plane.</param>
    /// <returns>Whether input and workspace dimensions match.</returns>
    public static bool MaltaDiffMap(
        bool isLf,
        JxlPlane<float> lum0,
        JxlPlane<float> lum1,
        double w0Gt1,
        double w0Lt1,
        double norm1,
        JxlPlane<float> diffs,
        JxlPlane<float> blockDiffAc)
        => isLf
            ? MaltaDiffMapLf(lum0, lum1, w0Gt1, w0Lt1, norm1, diffs, blockDiffAc)
            : MaltaDiffMap(lum0, lum1, w0Gt1, w0Lt1, norm1, diffs, blockDiffAc);

    /// <summary>
    /// Prepares float differences and accumulates the selected Malta neighborhood without changing either input band.
    /// </summary>
    /// <param name="isLf">Whether to use the low-frequency ray samples.</param>
    /// <param name="lum0">The borrowed reference band, at least eight samples wide and eight rows high.</param>
    /// <param name="lum1">The equally sized borrowed distorted band.</param>
    /// <param name="w0Gt1">The primary symmetric-difference weight.</param>
    /// <param name="w0Lt1">The secondary reference-magnitude penalty weight.</param>
    /// <param name="norm1">The native magnitude normalization.</param>
    /// <param name="effectiveHalfLength">The native effective ray half-length used to normalize the penalty.</param>
    /// <param name="multiplier">The frequency-specific scale used when preparing the penalty coefficients.</param>
    /// <param name="diffs">The equally sized workspace, separate from the input and AC planes and replaced with prepared float differences.</param>
    /// <param name="blockDiffAc">The equally sized existing AC accumulation, updated only at logical samples.</param>
    /// <returns>Whether the input and workspace dimensions match.</returns>
    public static bool MaltaDiffMap(
        bool isLf,
        JxlPlane<float> lum0,
        JxlPlane<float> lum1,
        double w0Gt1,
        double w0Lt1,
        double norm1,
        double effectiveHalfLength,
        double multiplier,
        JxlPlane<float> diffs,
        JxlPlane<float> blockDiffAc)
    {
        if (!JxlImageOperations.SameSize(lum0, lum1) || !JxlImageOperations.SameSize(lum0, diffs))
        {
            return false;
        }

        int width = lum0.XSize;
        int height = lum0.YSize;
        ReadOnlySpan<float> samples0 = lum0.GetRow(0);
        ReadOnlySpan<float> samples1 = lum1.GetRow(0);
        Span<float> differenceSamples = diffs.GetRow(0);
        Span<float> outputSamples = blockDiffAc.GetRow(0);
        MaltaDifferenceOperator operation = new(w0Gt1, w0Lt1, norm1, effectiveHalfLength, multiplier);

        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<float> row0 = samples0.Slice(y * lum0.PixelsPerRow, width);
            ReadOnlySpan<float> row1 = samples1.Slice(y * lum1.PixelsPerRow, width);
            Span<float> differenceRow = differenceSamples.Slice(y * diffs.PixelsPerRow, width);
            TensorPrimitives_.Invoke(row0, row1, differenceRow, operation);
        }

        // Small-image comparison expands inputs to at least 8 by 8 before reaching Malta. Every ray has a four-sample
        // halo. Reuse one scalar 9-by-9 border block; SIMD never reads a halo from physical allocator padding.
        Span<float> border = stackalloc float[81];
        if (isLf)
        {
            AccumulateMaltaLf(differenceSamples, width, height, diffs.PixelsPerRow, outputSamples, blockDiffAc.PixelsPerRow, border);
        }
        else
        {
            AccumulateMalta(differenceSamples, width, height, diffs.PixelsPerRow, outputSamples, blockDiffAc.PixelsPerRow, border);
        }

        return true;
    }

    /// <summary>
    /// Accumulates full Malta differences with the native high-frequency scale and effective ray length.
    /// </summary>
    /// <param name="lum0">The borrowed reference band, at least eight samples wide and eight rows high.</param>
    /// <param name="lum1">The equally sized borrowed distorted band.</param>
    /// <param name="w0Gt1">The primary symmetric-difference weight.</param>
    /// <param name="w0Lt1">The secondary reference-magnitude penalty weight.</param>
    /// <param name="norm1">The magnitude normalization.</param>
    /// <param name="diffs">The equally sized difference workspace, separate from the input and AC planes.</param>
    /// <param name="blockDiffAc">The equally sized existing AC accumulation.</param>
    /// <returns>Whether the input and workspace dimensions match.</returns>
    public static bool MaltaDiffMap(
        JxlPlane<float> lum0,
        JxlPlane<float> lum1,
        double w0Gt1,
        double w0Lt1,
        double norm1,
        JxlPlane<float> diffs,
        JxlPlane<float> blockDiffAc)
        => MaltaDiffMap(false, lum0, lum1, w0Gt1, w0Lt1, norm1, 3.75D, 0.39905817637D, diffs, blockDiffAc);

    /// <summary>
    /// Accumulates sparse low-frequency Malta differences with the native scale and effective ray length.
    /// </summary>
    /// <param name="lum0">The borrowed reference band, at least eight samples wide and eight rows high.</param>
    /// <param name="lum1">The equally sized borrowed distorted band.</param>
    /// <param name="w0Gt1">The primary symmetric-difference weight.</param>
    /// <param name="w0Lt1">The secondary reference-magnitude penalty weight.</param>
    /// <param name="norm1">The magnitude normalization.</param>
    /// <param name="diffs">The equally sized difference workspace, separate from the input and AC planes.</param>
    /// <param name="blockDiffAc">The equally sized existing AC accumulation.</param>
    /// <returns>Whether the input and workspace dimensions match.</returns>
    public static bool MaltaDiffMapLf(
        JxlPlane<float> lum0,
        JxlPlane<float> lum1,
        double w0Gt1,
        double w0Lt1,
        double norm1,
        JxlPlane<float> diffs,
        JxlPlane<float> blockDiffAc)
        => MaltaDiffMap(true, lum0, lum1, w0Gt1, w0Lt1, norm1, 3.75D, 0.611612573796D, diffs, blockDiffAc);

    /// <summary>
    /// Copies a center's valid neighbors into the reused zero-extended scalar Malta block.
    /// </summary>
    /// <param name="differences">The borrowed complete difference plane at its logical origin.</param>
    /// <param name="width">The logical source width.</param>
    /// <param name="height">The logical source height.</param>
    /// <param name="stride">The physical source stride in samples.</param>
    /// <param name="x">The valid center column.</param>
    /// <param name="y">The valid center row.</param>
    /// <param name="border">The 81-sample block, cleared before valid source rows are copied into it.</param>
    private static void PrepareMaltaBorder(ReadOnlySpan<float> differences, int width, int height, int stride, int x, int y, Span<float> border)
    {
        border.Clear();
        int firstX = Math.Max(0, x - 4);
        int lastX = Math.Min(width, x + 5);
        int copyWidth = lastX - firstX;
        int destinationX = firstX - (x - 4);

        for (int dy = -4; dy <= 4; dy++)
        {
            int sourceY = y + dy;
            if ((uint)sourceY < (uint)height)
            {
                differences.Slice((sourceY * stride) + firstX, copyWidth).CopyTo(border.Slice(((dy + 4) * 9) + destinationX, copyWidth));
            }
        }
    }

    /// <summary>
    /// Combines X and Y high-frequency bands into the perceptual masking response.
    /// </summary>
    /// <param name="hf">The borrowed high-frequency X and Y planes.</param>
    /// <param name="uhf">The borrowed ultra-high-frequency X and Y planes.</param>
    /// <param name="output">The distinct preallocated masking output, with the same dimensions as the four input bands.</param>
    public static void CombineChannelsForMasking(InlineArray2<JxlPlane<float>> hf, InlineArray2<JxlPlane<float>> uhf, JxlPlane<float> output)
    {
        ReadOnlySpan<float> hfX = hf[0].GetRow(0);
        ReadOnlySpan<float> hfY = hf[1].GetRow(0);
        ReadOnlySpan<float> uhfX = uhf[0].GetRow(0);
        ReadOnlySpan<float> uhfY = uhf[1].GetRow(0);
        Span<float> outputSamples = output.GetRow(0);
        int hfXStride = hf[0].PixelsPerRow;
        int hfYStride = hf[1].PixelsPerRow;
        int uhfXStride = uhf[0].PixelsPerRow;
        int uhfYStride = uhf[1].PixelsPerRow;
        int width = output.XSize;
        MaskingMagnitudeOperator operation = default;

        for (int y = 0; y < output.YSize; y++)
        {
            ref float rowHfX = ref MemoryMarshal.GetReference(hfX.Slice(y * hfXStride, width));
            ref float rowHfY = ref MemoryMarshal.GetReference(hfY.Slice(y * hfYStride, width));
            ref float rowUhfX = ref MemoryMarshal.GetReference(uhfX.Slice(y * uhfXStride, width));
            ref float rowUhfY = ref MemoryMarshal.GetReference(uhfY.Slice(y * uhfYStride, width));
            ref float result = ref MemoryMarshal.GetReference(outputSamples.Slice(y * output.PixelsPerRow, width));
            int x = 0;

            // The four planes remain separate. Sum the X bands in registers, then use the shared ternary operator
            // for the native magnitude equation. Each lane writes one logical output sample without an intermediate pass.
            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<float>.Count; x += Vector512<float>.Count)
                {
                    Vector512<float> xSum = Vector512.LoadUnsafe(ref rowUhfX, (uint)x) + Vector512.LoadUnsafe(ref rowHfX, (uint)x);
                    Vector512<float> yUhf = Vector512.LoadUnsafe(ref rowUhfY, (uint)x);
                    Vector512<float> yHf = Vector512.LoadUnsafe(ref rowHfY, (uint)x);
                    operation.Invoke(xSum, yUhf, yHf).StoreUnsafe(ref result, (uint)x);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<float>.Count; x += Vector256<float>.Count)
                {
                    Vector256<float> xSum = Vector256.LoadUnsafe(ref rowUhfX, (uint)x) + Vector256.LoadUnsafe(ref rowHfX, (uint)x);
                    Vector256<float> yUhf = Vector256.LoadUnsafe(ref rowUhfY, (uint)x);
                    Vector256<float> yHf = Vector256.LoadUnsafe(ref rowHfY, (uint)x);
                    operation.Invoke(xSum, yUhf, yHf).StoreUnsafe(ref result, (uint)x);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<float>.Count; x += Vector128<float>.Count)
                {
                    Vector128<float> xSum = Vector128.LoadUnsafe(ref rowUhfX, (uint)x) + Vector128.LoadUnsafe(ref rowHfX, (uint)x);
                    Vector128<float> yUhf = Vector128.LoadUnsafe(ref rowUhfY, (uint)x);
                    Vector128<float> yHf = Vector128.LoadUnsafe(ref rowHfY, (uint)x);
                    operation.Invoke(xSum, yUhf, yHf).StoreUnsafe(ref result, (uint)x);
                }
            }

            for (; x < width; x++)
            {
                float xSum = Unsafe.Add(ref rowUhfX, x) + Unsafe.Add(ref rowHfX, x);
                Unsafe.Add(ref result, x) = operation.Invoke(xSum, Unsafe.Add(ref rowUhfY, x), Unsafe.Add(ref rowHfY, x));
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
    /// Prepares Malta differences using float samples and coefficients normalized once during setup.
    /// </summary>
    /// <param name="w0Gt1">The native primary symmetric-difference weight, before narrowing.</param>
    /// <param name="w0Lt1">The native secondary magnitude-penalty weight, before narrowing.</param>
    /// <param name="norm1">The native magnitude normalization, before narrowing.</param>
    /// <param name="effectiveHalfLength">The native effective ray half-length.</param>
    /// <param name="multiplier">The native frequency-specific scale.</param>
    private readonly struct MaltaDifferenceOperator(
        double w0Gt1,
        double w0Lt1,
        double norm1,
        double effectiveHalfLength,
        double multiplier) : TensorPrimitives_.IBinaryOperator
    {
        /// <summary>The lower secondary-response boundary as a fraction of the reference magnitude.</summary>
        private const float LowerFraction = 0.55F;

        /// <summary>The upper secondary-response boundary as a fraction of the reference magnitude.</summary>
        private const float UpperFraction = 1.05F;

        /// <summary>The native magnitude offset rounded once for the float denominator.</summary>
        private readonly float normalization = (float)norm1;

        /// <summary>The primary coefficient, computed in native double precision once and then rounded to float.</summary>
        private readonly float firstCoefficient = (float)(multiplier * Math.Sqrt(0.5F * w0Gt1) / ((effectiveHalfLength * 2D) + 1D) * norm1);

        /// <summary>The secondary coefficient. Native promotes its already-rounded 0.33F weight before the double product.</summary>
        private readonly float secondCoefficient = (float)(multiplier * Math.Sqrt(0.33F * w0Lt1) / ((effectiveHalfLength * 2D) + 1D) * norm1);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float first, float second)
        {
            float magnitude = MathF.Abs(first);
            float denominator = this.normalization + (0.5F * (magnitude + MathF.Abs(second)));
            float result = (this.firstCoefficient / denominator) * (first - second);
            float secondaryScale = this.secondCoefficient / denominator;
            bool negative = first < 0F;
            float actual = negative ? -second : second;
            float lower = LowerFraction * magnitude;
            float upper = UpperFraction * magnitude;
            float response;
            if (actual < lower)
            {
                response = lower - actual;
            }
            else if (actual > upper)
            {
                response = upper - actual;
            }
            else
            {
                return result;
            }

            // Native evaluates the secondary threshold and impact in double for each sample. This comparison retains
            // float pixel arithmetic instead. Scalar and SIMD use the same rounded fractions and one final multiply-add;
            // they follow the native objective but do not claim its double-secondary rounding bits.
            return float.MultiplyAddEstimate(secondaryScale, negative ? -response : response, result);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second)
        {
            Vector128<float> magnitude = Vector128.Abs(first);
            Vector128<float> denominator = Vector128.Create(this.normalization) + (Vector128.Create(0.5F) * (magnitude + Vector128.Abs(second)));
            Vector128<float> result = (Vector128.Create(this.firstCoefficient) / denominator) * (first - second);
            Vector128<float> secondaryScale = Vector128.Create(this.secondCoefficient) / denominator;
            Vector128<float> negative = Vector128.LessThan(first, Vector128<float>.Zero);
            Vector128<float> actual = Vector128.ConditionalSelect(negative, -second, second);
            Vector128<float> lower = Vector128.Create(LowerFraction) * magnitude;
            Vector128<float> upper = Vector128.Create(UpperFraction) * magnitude;
            Vector128<float> below = Vector128.LessThan(actual, lower);
            Vector128<float> above = Vector128.GreaterThan(actual, upper);
            Vector128<float> response = Vector128.ConditionalSelect(below, lower - actual, upper - actual);
            response = Vector128.ConditionalSelect(negative, -response, response);
            Vector128<float> corrected = Vector128.MultiplyAddEstimate(secondaryScale, response, result);

            // Inactive lanes retain the original result, including its zero sign. Each active lane selects exactly one
            // half-open secondary objective; reflecting negative references gives the scalar sign rules without branching.
            return Vector128.ConditionalSelect(Vector128.BitwiseOr(below, above), corrected, result);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second)
        {
            Vector256<float> magnitude = Vector256.Abs(first);
            Vector256<float> denominator = Vector256.Create(this.normalization) + (Vector256.Create(0.5F) * (magnitude + Vector256.Abs(second)));
            Vector256<float> result = (Vector256.Create(this.firstCoefficient) / denominator) * (first - second);
            Vector256<float> secondaryScale = Vector256.Create(this.secondCoefficient) / denominator;
            Vector256<float> negative = Vector256.LessThan(first, Vector256<float>.Zero);
            Vector256<float> actual = Vector256.ConditionalSelect(negative, -second, second);
            Vector256<float> lower = Vector256.Create(LowerFraction) * magnitude;
            Vector256<float> upper = Vector256.Create(UpperFraction) * magnitude;
            Vector256<float> below = Vector256.LessThan(actual, lower);
            Vector256<float> above = Vector256.GreaterThan(actual, upper);
            Vector256<float> response = Vector256.ConditionalSelect(below, lower - actual, upper - actual);
            response = Vector256.ConditionalSelect(negative, -response, response);
            Vector256<float> corrected = Vector256.MultiplyAddEstimate(secondaryScale, response, result);

            // Inactive lanes retain the original result, including its zero sign. Each active lane selects exactly one
            // half-open secondary objective; reflecting negative references gives the scalar sign rules without branching.
            return Vector256.ConditionalSelect(Vector256.BitwiseOr(below, above), corrected, result);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second)
        {
            Vector512<float> magnitude = Vector512.Abs(first);
            Vector512<float> denominator = Vector512.Create(this.normalization) + (Vector512.Create(0.5F) * (magnitude + Vector512.Abs(second)));
            Vector512<float> result = (Vector512.Create(this.firstCoefficient) / denominator) * (first - second);
            Vector512<float> secondaryScale = Vector512.Create(this.secondCoefficient) / denominator;
            Vector512<float> negative = Vector512.LessThan(first, Vector512<float>.Zero);
            Vector512<float> actual = Vector512.ConditionalSelect(negative, -second, second);
            Vector512<float> lower = Vector512.Create(LowerFraction) * magnitude;
            Vector512<float> upper = Vector512.Create(UpperFraction) * magnitude;
            Vector512<float> below = Vector512.LessThan(actual, lower);
            Vector512<float> above = Vector512.GreaterThan(actual, upper);
            Vector512<float> response = Vector512.ConditionalSelect(below, lower - actual, upper - actual);
            response = Vector512.ConditionalSelect(negative, -response, response);
            Vector512<float> corrected = Vector512.MultiplyAddEstimate(secondaryScale, response, result);

            // Inactive lanes retain the original result, including its zero sign. Each active lane selects exactly one
            // half-open secondary objective; reflecting negative references gives the scalar sign rules without branching.
            return Vector512.ConditionalSelect(Vector512.BitwiseOr(below, above), corrected, result);
        }
    }

    /// <summary>
    /// Combines summed X bands and the two Y bands into the native masking magnitude.
    /// </summary>
    private readonly struct MaskingMagnitudeOperator : TensorPrimitives_.ITernaryOperator
    {
        /// <summary>The weight for the sum of X high and ultra-high frequencies.</summary>
        private const float XMultiplier = 2.5F;

        /// <summary>The weight applied separately to each Y frequency band.</summary>
        private const float YMultiplier = 0.4F;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float first, float second, float third)
        {
            float x = first * XMultiplier;
            float y = (second * YMultiplier) + (third * YMultiplier);

            return MathF.Sqrt((x * x) + (y * y));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second, Vector128<float> third)
        {
            Vector128<float> x = first * Vector128.Create(XMultiplier);
            Vector128<float> y = (second * Vector128.Create(YMultiplier)) + (third * Vector128.Create(YMultiplier));

            return Vector128.Sqrt((x * x) + (y * y));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second, Vector256<float> third)
        {
            Vector256<float> x = first * Vector256.Create(XMultiplier);
            Vector256<float> y = (second * Vector256.Create(YMultiplier)) + (third * Vector256.Create(YMultiplier));

            return Vector256.Sqrt((x * x) + (y * y));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second, Vector512<float> third)
        {
            Vector512<float> x = first * Vector512.Create(XMultiplier);
            Vector512<float> y = (second * Vector512.Create(YMultiplier)) + (third * Vector512.Create(YMultiplier));

            return Vector512.Sqrt((x * x) + (y * y));
        }
    }

    /// <summary>
    /// Removes the interval around zero from independent frequency samples.
    /// </summary>
    /// <param name="width">The nonnegative half-width of the central interval.</param>
    private readonly struct RemoveRangeAroundZeroOperator(float width) : TensorPrimitives_.IUnaryOperator
    {
        /// <summary>The half-width used by the native frequency equation.</summary>
        private readonly float width = width;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
            => value > this.width ? value - this.width : value < -this.width ? value + this.width : 0F;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            Vector128<float> width = Vector128.Create(this.width);

            return Vector128.ConditionalSelect(
                Vector128.GreaterThan(value, width),
                value - width,
                Vector128.ConditionalSelect(Vector128.LessThan(value, -width), value + width, Vector128<float>.Zero));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            Vector256<float> width = Vector256.Create(this.width);

            return Vector256.ConditionalSelect(
                Vector256.GreaterThan(value, width),
                value - width,
                Vector256.ConditionalSelect(Vector256.LessThan(value, -width), value + width, Vector256<float>.Zero));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            Vector512<float> width = Vector512.Create(this.width);

            return Vector512.ConditionalSelect(
                Vector512.GreaterThan(value, width),
                value - width,
                Vector512.ConditionalSelect(Vector512.LessThan(value, -width), value + width, Vector512<float>.Zero));
        }
    }

    /// <summary>
    /// Doubles samples inside the central interval and offsets samples outside it.
    /// </summary>
    /// <param name="width">The nonnegative half-width of the central interval.</param>
    private readonly struct AmplifyRangeAroundZeroOperator(float width) : TensorPrimitives_.IUnaryOperator
    {
        /// <summary>The half-width used by the native frequency equation.</summary>
        private readonly float width = width;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
            => value > this.width ? value + this.width : value < -this.width ? value - this.width : value + value;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            Vector128<float> width = Vector128.Create(this.width);

            return Vector128.ConditionalSelect(
                Vector128.GreaterThan(value, width),
                value + width,
                Vector128.ConditionalSelect(Vector128.LessThan(value, -width), value - width, value + value));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            Vector256<float> width = Vector256.Create(this.width);

            return Vector256.ConditionalSelect(
                Vector256.GreaterThan(value, width),
                value + width,
                Vector256.ConditionalSelect(Vector256.LessThan(value, -width), value - width, value + value));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            Vector512<float> width = Vector512.Create(this.width);

            return Vector512.ConditionalSelect(
                Vector512.GreaterThan(value, width),
                value + width,
                Vector512.ConditionalSelect(Vector512.LessThan(value, -width), value - width, value + value));
        }
    }

    /// <summary>
    /// Reduces the slope beyond positive and negative frequency thresholds using the native multiply-add order.
    /// </summary>
    /// <param name="maximum">The positive magnitude at which the slope changes.</param>
    private readonly struct MaximumClampOperator(float maximum) : TensorPrimitives_.IUnaryOperator
    {
        /// <summary>The slope retained outside the central interval.</summary>
        private const float Multiplier = 0.724216145665F;

        /// <summary>The magnitude at which the native response changes slope.</summary>
        private readonly float maximum = maximum;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
        {
            if (value >= this.maximum)
            {
                return float.MultiplyAddEstimate(value - this.maximum, Multiplier, this.maximum);
            }

            if (value < -this.maximum)
            {
                return float.MultiplyAddEstimate(value + this.maximum, Multiplier, -this.maximum);
            }

            return value;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            Vector128<float> maximum = Vector128.Create(this.maximum);
            Vector128<float> positive = Vector128.MultiplyAddEstimate(value - maximum, Vector128.Create(Multiplier), maximum);
            Vector128<float> negative = Vector128.MultiplyAddEstimate(value + maximum, Vector128.Create(Multiplier), -maximum);
            Vector128<float> positiveOrValue = Vector128.ConditionalSelect(Vector128.GreaterThanOrEqual(value, maximum), positive, value);

            return Vector128.ConditionalSelect(Vector128.LessThan(value, -maximum), negative, positiveOrValue);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            Vector256<float> maximum = Vector256.Create(this.maximum);
            Vector256<float> positive = Vector256.MultiplyAddEstimate(value - maximum, Vector256.Create(Multiplier), maximum);
            Vector256<float> negative = Vector256.MultiplyAddEstimate(value + maximum, Vector256.Create(Multiplier), -maximum);
            Vector256<float> positiveOrValue = Vector256.ConditionalSelect(Vector256.GreaterThanOrEqual(value, maximum), positive, value);

            return Vector256.ConditionalSelect(Vector256.LessThan(value, -maximum), negative, positiveOrValue);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            Vector512<float> maximum = Vector512.Create(this.maximum);
            Vector512<float> positive = Vector512.MultiplyAddEstimate(value - maximum, Vector512.Create(Multiplier), maximum);
            Vector512<float> negative = Vector512.MultiplyAddEstimate(value + maximum, Vector512.Create(Multiplier), -maximum);
            Vector512<float> positiveOrValue = Vector512.ConditionalSelect(Vector512.GreaterThanOrEqual(value, maximum), positive, value);

            return Vector512.ConditionalSelect(Vector512.LessThan(value, -maximum), negative, positiveOrValue);
        }
    }

    /// <summary>
    /// Scales retained Y high frequencies before amplifying their central interval.
    /// </summary>
    private readonly struct HighFrequencyYOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <summary>The native scale applied before central amplification.</summary>
        private const float Multiplier = 2.155F;

        /// <summary>The central half-width after scaling.</summary>
        private const float Width = 0.132F;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value) => new AmplifyRangeAroundZeroOperator(Width).Invoke(value * Multiplier);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value) => new AmplifyRangeAroundZeroOperator(Width).Invoke(value * Multiplier);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value) => new AmplifyRangeAroundZeroOperator(Width).Invoke(value * Multiplier);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value) => new AmplifyRangeAroundZeroOperator(Width).Invoke(value * Multiplier);
    }

    /// <summary>
    /// Reduces large Y ultra-high-frequency responses before applying their native scale.
    /// </summary>
    private readonly struct UltraHighFrequencyYOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <summary>The magnitude where the unscaled response changes slope.</summary>
        private const float Maximum = 5.19175294647F;

        /// <summary>The native scale applied after the response changes slope.</summary>
        private const float Multiplier = 2.69313763794F;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value) => new MaximumClampOperator(Maximum).Invoke(value) * Multiplier;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value) => new MaximumClampOperator(Maximum).Invoke(value) * Multiplier;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value) => new MaximumClampOperator(Maximum).Invoke(value) * Multiplier;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value) => new MaximumClampOperator(Maximum).Invoke(value) * Multiplier;
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

    /// <summary>Transforms borrowed RGB samples with the prepared comparison blur and output storage.</summary>
    /// <param name="configuration">The comparison configuration.</param>
    /// <param name="rgb">The borrowed RGB image. It can also supply the output storage.</param>
    /// <param name="parameters">The perceptual comparison parameters, including the intensity of one input unit in nits.</param>
    /// <param name="blurred">The distinct preallocated three-plane blur workspace.</param>
    /// <param name="blurTemp">The prepared transposed blur workspace.</param>
    /// <param name="xyb">The preallocated transformed output, with the same logical dimensions as the input.</param>
    /// <returns>Whether all blur stages completed.</returns>
    public static bool OpsinDynamicsImage(
        Configuration configuration,
        JxlImage3<float> rgb,
        in ButteraugliParameters parameters,
        JxlImage3<float> blurred,
        ButteraugliBlurTemp blurTemp,
        JxlImage3<float> xyb)
    {
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

        // Blur finishes before any source samples are replaced. All nine row views borrow existing owners.
        // Retained planes can have different physical strides despite equal logical dimensions.
        ReadOnlySpan<float> source0 = rgb.PlaneRow(0, 0);
        ReadOnlySpan<float> source1 = rgb.PlaneRow(1, 0);
        ReadOnlySpan<float> source2 = rgb.PlaneRow(2, 0);
        ReadOnlySpan<float> blurred0 = blurred.PlaneRow(0, 0);
        ReadOnlySpan<float> blurred1 = blurred.PlaneRow(1, 0);
        ReadOnlySpan<float> blurred2 = blurred.PlaneRow(2, 0);
        Span<float> destination0 = xyb.PlaneRow(0, 0);
        Span<float> destination1 = xyb.PlaneRow(1, 0);
        Span<float> destination2 = xyb.PlaneRow(2, 0);
        int sourceStride0 = rgb.Plane(0).PixelsPerRow;
        int sourceStride1 = rgb.Plane(1).PixelsPerRow;
        int sourceStride2 = rgb.Plane(2).PixelsPerRow;
        int blurredStride0 = blurred.Plane(0).PixelsPerRow;
        int blurredStride1 = blurred.Plane(1).PixelsPerRow;
        int blurredStride2 = blurred.Plane(2).PixelsPerRow;
        int destinationStride0 = xyb.Plane(0).PixelsPerRow;
        int destinationStride1 = xyb.Plane(1).PixelsPerRow;
        int destinationStride2 = xyb.Plane(2).PixelsPerRow;
        int width = rgb.XSize;
        float intensity = parameters.IntensityTarget;

        for (int y = 0; y < rgb.YSize; y++)
        {
            ReadOnlySpan<float> row0 = source0.Slice(y * sourceStride0, width);
            ReadOnlySpan<float> row1 = source1.Slice(y * sourceStride1, width);
            ReadOnlySpan<float> row2 = source2.Slice(y * sourceStride2, width);
            ReadOnlySpan<float> blur0 = blurred0.Slice(y * blurredStride0, width);
            ReadOnlySpan<float> blur1 = blurred1.Slice(y * blurredStride1, width);
            ReadOnlySpan<float> blur2 = blurred2.Slice(y * blurredStride2, width);
            Span<float> output0 = destination0.Slice(y * destinationStride0, width);
            Span<float> output1 = destination1.Slice(y * destinationStride1, width);
            Span<float> output2 = destination2.Slice(y * destinationStride2, width);
            ref float input0 = ref MemoryMarshal.GetReference(row0);
            ref float input1 = ref MemoryMarshal.GetReference(row1);
            ref float input2 = ref MemoryMarshal.GetReference(row2);
            ref float smoothed0 = ref MemoryMarshal.GetReference(blur0);
            ref float smoothed1 = ref MemoryMarshal.GetReference(blur1);
            ref float smoothed2 = ref MemoryMarshal.GetReference(blur2);
            ref float result0 = ref MemoryMarshal.GetReference(output0);
            ref float result1 = ref MemoryMarshal.GetReference(output1);
            ref float result2 = ref MemoryMarshal.GetReference(output2);
            int x = 0;

            // One register holds one component from consecutive pixels. Each block reads all six inputs before any output store.
            // Only full registers enter each loop. Narrower widths and the scalar remainder never access logical row padding.
            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<float> multiplier = Vector512.Create(intensity);
                for (; x <= width - Vector512<float>.Count; x += Vector512<float>.Count)
                {
                    OpsinDynamics(
                        Vector512.LoadUnsafe(ref input0, (uint)x),
                        Vector512.LoadUnsafe(ref input1, (uint)x),
                        Vector512.LoadUnsafe(ref input2, (uint)x),
                        Vector512.LoadUnsafe(ref smoothed0, (uint)x),
                        Vector512.LoadUnsafe(ref smoothed1, (uint)x),
                        Vector512.LoadUnsafe(ref smoothed2, (uint)x),
                        multiplier,
                        out Vector512<float> value0,
                        out Vector512<float> value1,
                        out Vector512<float> value2);

                    value0.StoreUnsafe(ref result0, (uint)x);
                    value1.StoreUnsafe(ref result1, (uint)x);
                    value2.StoreUnsafe(ref result2, (uint)x);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<float> multiplier = Vector256.Create(intensity);
                for (; x <= width - Vector256<float>.Count; x += Vector256<float>.Count)
                {
                    OpsinDynamics(
                        Vector256.LoadUnsafe(ref input0, (uint)x),
                        Vector256.LoadUnsafe(ref input1, (uint)x),
                        Vector256.LoadUnsafe(ref input2, (uint)x),
                        Vector256.LoadUnsafe(ref smoothed0, (uint)x),
                        Vector256.LoadUnsafe(ref smoothed1, (uint)x),
                        Vector256.LoadUnsafe(ref smoothed2, (uint)x),
                        multiplier,
                        out Vector256<float> value0,
                        out Vector256<float> value1,
                        out Vector256<float> value2);

                    value0.StoreUnsafe(ref result0, (uint)x);
                    value1.StoreUnsafe(ref result1, (uint)x);
                    value2.StoreUnsafe(ref result2, (uint)x);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<float> multiplier = Vector128.Create(intensity);
                for (; x <= width - Vector128<float>.Count; x += Vector128<float>.Count)
                {
                    OpsinDynamics(
                        Vector128.LoadUnsafe(ref input0, (uint)x),
                        Vector128.LoadUnsafe(ref input1, (uint)x),
                        Vector128.LoadUnsafe(ref input2, (uint)x),
                        Vector128.LoadUnsafe(ref smoothed0, (uint)x),
                        Vector128.LoadUnsafe(ref smoothed1, (uint)x),
                        Vector128.LoadUnsafe(ref smoothed2, (uint)x),
                        multiplier,
                        out Vector128<float> value0,
                        out Vector128<float> value1,
                        out Vector128<float> value2);

                    value0.StoreUnsafe(ref result0, (uint)x);
                    value1.StoreUnsafe(ref result1, (uint)x);
                    value2.StoreUnsafe(ref result2, (uint)x);
                }
            }

            for (; x < width; x++)
            {
                OpsinDynamics(
                    row0[x],
                    row1[x],
                    row2[x],
                    blur0[x],
                    blur1[x],
                    blur2[x],
                    intensity,
                    out float value0,
                    out float value1,
                    out float value2);

                output0[x] = value0;
                output1[x] = value1;
                output2[x] = value2;
            }
        }

        return true;
    }

    /// <summary>Adapts one RGB pixel using its blurred sensitivity in the native arithmetic order.</summary>
    /// <param name="r">The red input samples.</param>
    /// <param name="g">The green input samples.</param>
    /// <param name="b">The blue input samples.</param>
    /// <param name="blurredR">The blurred red samples.</param>
    /// <param name="blurredG">The blurred green samples.</param>
    /// <param name="blurredB">The blurred blue samples.</param>
    /// <param name="intensity">The intensity multiplier applied before both absorbance calculations.</param>
    /// <param name="x">The perceptual X result.</param>
    /// <param name="y">The perceptual Y result.</param>
    /// <param name="blue">The perceptual B result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpsinDynamics(
        float r,
        float g,
        float b,
        float blurredR,
        float blurredG,
        float blurredB,
        float intensity,
        out float x,
        out float y,
        out float blue)
    {
        OpsinAbsorbance(blurredR * intensity, blurredG * intensity, blurredB * intensity, out float pre0, out float pre1, out float pre2);

        // The blurred absorbance floor keeps Gamma's input and its sensitivity divisor positive.
        pre0 = MathF.Max(pre0, Absorbance01Bias);
        pre1 = MathF.Max(pre1, Absorbance01Bias);
        pre2 = MathF.Max(pre2, Absorbance2Bias);
        float sensitivity0 = MathF.Max(Gamma(pre0) / pre0, MinimumOpsinSensitivity);
        float sensitivity1 = MathF.Max(Gamma(pre1) / pre1, MinimumOpsinSensitivity);
        float sensitivity2 = MathF.Max(Gamma(pre2) / pre2, MinimumOpsinSensitivity);

        OpsinAbsorbance(r * intensity, g * intensity, b * intensity, out float current0, out float current1, out float current2);
        current0 = MathF.Max(current0 * sensitivity0, Absorbance01Bias);
        current1 = MathF.Max(current1 * sensitivity1, Absorbance01Bias);
        current2 = MathF.Max(current2 * sensitivity2, Absorbance2Bias);
        x = current0 - current1;
        y = current0 + current1;
        blue = current2;
    }

    /// <summary>Mixes one RGB sample into unclamped perceptual absorbance.</summary>
    /// <param name="r">The intensity-scaled red components.</param>
    /// <param name="g">The intensity-scaled green components.</param>
    /// <param name="b">The intensity-scaled blue components.</param>
    /// <param name="first">The first absorbance.</param>
    /// <param name="second">The second absorbance.</param>
    /// <param name="third">The third absorbance.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpsinAbsorbance(float r, float g, float b, out float first, out float second, out float third)
    {
        // Preserve B+bias, then G, then R as nested multiply-add estimates. Clamp policy belongs to the adaptation stage.
        first = float.MultiplyAddEstimate(Absorbance0Blue, b, Absorbance01Bias);
        first = float.MultiplyAddEstimate(Absorbance0Green, g, first);
        first = float.MultiplyAddEstimate(Absorbance0Red, r, first);

        second = float.MultiplyAddEstimate(Absorbance1Blue, b, Absorbance01Bias);
        second = float.MultiplyAddEstimate(Absorbance1Green, g, second);
        second = float.MultiplyAddEstimate(Absorbance1Red, r, second);

        third = float.MultiplyAddEstimate(Absorbance2Blue, b, Absorbance2Bias);
        third = float.MultiplyAddEstimate(Absorbance2RedGreen, g, third);
        third = float.MultiplyAddEstimate(Absorbance2RedGreen, r, third);
    }

    /// <summary>Evaluates the perceptual logarithmic response for one positive absorbance.</summary>
    /// <param name="value">The absorbance after its native positive floor.</param>
    /// <returns>The response using the existing .NET log2 policy and the native final multiply-add order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Gamma(float value)
        => float.MultiplyAddEstimate(OpsinGammaSlope, MathF.Log2(value + OpsinGammaInputBias), OpsinGammaOffset);

    /// <summary>Adapts 4 independent RGB pixels using their blurred sensitivities, without changing pixel lane order.</summary>
    /// <param name="r">The red input samples.</param>
    /// <param name="g">The green input samples.</param>
    /// <param name="b">The blue input samples.</param>
    /// <param name="blurredR">The blurred red samples.</param>
    /// <param name="blurredG">The blurred green samples.</param>
    /// <param name="blurredB">The blurred blue samples.</param>
    /// <param name="intensity">The intensity multiplier applied before both absorbance calculations.</param>
    /// <param name="x">The perceptual X result.</param>
    /// <param name="y">The perceptual Y result.</param>
    /// <param name="blue">The perceptual B result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpsinDynamics(
        Vector128<float> r,
        Vector128<float> g,
        Vector128<float> b,
        Vector128<float> blurredR,
        Vector128<float> blurredG,
        Vector128<float> blurredB,
        Vector128<float> intensity,
        out Vector128<float> x,
        out Vector128<float> y,
        out Vector128<float> blue)
    {
        OpsinAbsorbance(
            blurredR * intensity,
            blurredG * intensity,
            blurredB * intensity,
            out Vector128<float> pre0,
            out Vector128<float> pre1,
            out Vector128<float> pre2);

        // The blurred absorbance floor keeps Gamma's input and its sensitivity divisor positive.
        pre0 = Vector128.Max(pre0, Vector128.Create(Absorbance01Bias));
        pre1 = Vector128.Max(pre1, Vector128.Create(Absorbance01Bias));
        pre2 = Vector128.Max(pre2, Vector128.Create(Absorbance2Bias));
        Vector128<float> sensitivity0 = Vector128.Max(Gamma(pre0) / pre0, Vector128.Create(MinimumOpsinSensitivity));
        Vector128<float> sensitivity1 = Vector128.Max(Gamma(pre1) / pre1, Vector128.Create(MinimumOpsinSensitivity));
        Vector128<float> sensitivity2 = Vector128.Max(Gamma(pre2) / pre2, Vector128.Create(MinimumOpsinSensitivity));

        OpsinAbsorbance(
            r * intensity,
            g * intensity,
            b * intensity,
            out Vector128<float> current0,
            out Vector128<float> current1,
            out Vector128<float> current2);

        current0 = Vector128.Max(current0 * sensitivity0, Vector128.Create(Absorbance01Bias));
        current1 = Vector128.Max(current1 * sensitivity1, Vector128.Create(Absorbance01Bias));
        current2 = Vector128.Max(current2 * sensitivity2, Vector128.Create(Absorbance2Bias));
        x = current0 - current1;
        y = current0 + current1;
        blue = current2;
    }

    /// <summary>Mixes 4 independent RGB samples into unclamped perceptual absorbance.</summary>
    /// <param name="r">The intensity-scaled red components.</param>
    /// <param name="g">The intensity-scaled green components.</param>
    /// <param name="b">The intensity-scaled blue components.</param>
    /// <param name="first">The first absorbance.</param>
    /// <param name="second">The second absorbance.</param>
    /// <param name="third">The third absorbance.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpsinAbsorbance(
        Vector128<float> r,
        Vector128<float> g,
        Vector128<float> b,
        out Vector128<float> first,
        out Vector128<float> second,
        out Vector128<float> third)
    {
        // Preserve B+bias, then G, then R as nested multiply-add estimates. Clamp policy belongs to the adaptation stage.
        first = Vector128.MultiplyAddEstimate(Vector128.Create(Absorbance0Blue), b, Vector128.Create(Absorbance01Bias));
        first = Vector128.MultiplyAddEstimate(Vector128.Create(Absorbance0Green), g, first);
        first = Vector128.MultiplyAddEstimate(Vector128.Create(Absorbance0Red), r, first);

        second = Vector128.MultiplyAddEstimate(Vector128.Create(Absorbance1Blue), b, Vector128.Create(Absorbance01Bias));
        second = Vector128.MultiplyAddEstimate(Vector128.Create(Absorbance1Green), g, second);
        second = Vector128.MultiplyAddEstimate(Vector128.Create(Absorbance1Red), r, second);

        third = Vector128.MultiplyAddEstimate(Vector128.Create(Absorbance2Blue), b, Vector128.Create(Absorbance2Bias));
        third = Vector128.MultiplyAddEstimate(Vector128.Create(Absorbance2RedGreen), g, third);
        third = Vector128.MultiplyAddEstimate(Vector128.Create(Absorbance2RedGreen), r, third);
    }

    /// <summary>Evaluates the perceptual logarithmic response for 4 positive absorbance lanes.</summary>
    /// <param name="value">The absorbance after its native positive floor.</param>
    /// <returns>The response using the existing .NET log2 policy and the native final multiply-add order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> Gamma(Vector128<float> value)
    {
        Vector128<float> logarithm = Vector128.Log2(value + Vector128.Create(OpsinGammaInputBias));
        return Vector128.MultiplyAddEstimate(Vector128.Create(OpsinGammaSlope), logarithm, Vector128.Create(OpsinGammaOffset));
    }

    /// <summary>Adapts 8 independent RGB pixels using their blurred sensitivities, without changing pixel lane order.</summary>
    /// <param name="r">The red input samples.</param>
    /// <param name="g">The green input samples.</param>
    /// <param name="b">The blue input samples.</param>
    /// <param name="blurredR">The blurred red samples.</param>
    /// <param name="blurredG">The blurred green samples.</param>
    /// <param name="blurredB">The blurred blue samples.</param>
    /// <param name="intensity">The intensity multiplier applied before both absorbance calculations.</param>
    /// <param name="x">The perceptual X result.</param>
    /// <param name="y">The perceptual Y result.</param>
    /// <param name="blue">The perceptual B result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpsinDynamics(
        Vector256<float> r,
        Vector256<float> g,
        Vector256<float> b,
        Vector256<float> blurredR,
        Vector256<float> blurredG,
        Vector256<float> blurredB,
        Vector256<float> intensity,
        out Vector256<float> x,
        out Vector256<float> y,
        out Vector256<float> blue)
    {
        OpsinAbsorbance(
            blurredR * intensity,
            blurredG * intensity,
            blurredB * intensity,
            out Vector256<float> pre0,
            out Vector256<float> pre1,
            out Vector256<float> pre2);

        // The blurred absorbance floor keeps Gamma's input and its sensitivity divisor positive.
        pre0 = Vector256.Max(pre0, Vector256.Create(Absorbance01Bias));
        pre1 = Vector256.Max(pre1, Vector256.Create(Absorbance01Bias));
        pre2 = Vector256.Max(pre2, Vector256.Create(Absorbance2Bias));
        Vector256<float> sensitivity0 = Vector256.Max(Gamma(pre0) / pre0, Vector256.Create(MinimumOpsinSensitivity));
        Vector256<float> sensitivity1 = Vector256.Max(Gamma(pre1) / pre1, Vector256.Create(MinimumOpsinSensitivity));
        Vector256<float> sensitivity2 = Vector256.Max(Gamma(pre2) / pre2, Vector256.Create(MinimumOpsinSensitivity));

        OpsinAbsorbance(
            r * intensity,
            g * intensity,
            b * intensity,
            out Vector256<float> current0,
            out Vector256<float> current1,
            out Vector256<float> current2);

        current0 = Vector256.Max(current0 * sensitivity0, Vector256.Create(Absorbance01Bias));
        current1 = Vector256.Max(current1 * sensitivity1, Vector256.Create(Absorbance01Bias));
        current2 = Vector256.Max(current2 * sensitivity2, Vector256.Create(Absorbance2Bias));
        x = current0 - current1;
        y = current0 + current1;
        blue = current2;
    }

    /// <summary>Mixes 8 independent RGB samples into unclamped perceptual absorbance.</summary>
    /// <param name="r">The intensity-scaled red components.</param>
    /// <param name="g">The intensity-scaled green components.</param>
    /// <param name="b">The intensity-scaled blue components.</param>
    /// <param name="first">The first absorbance.</param>
    /// <param name="second">The second absorbance.</param>
    /// <param name="third">The third absorbance.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpsinAbsorbance(
        Vector256<float> r,
        Vector256<float> g,
        Vector256<float> b,
        out Vector256<float> first,
        out Vector256<float> second,
        out Vector256<float> third)
    {
        // Preserve B+bias, then G, then R as nested multiply-add estimates. Clamp policy belongs to the adaptation stage.
        first = Vector256.MultiplyAddEstimate(Vector256.Create(Absorbance0Blue), b, Vector256.Create(Absorbance01Bias));
        first = Vector256.MultiplyAddEstimate(Vector256.Create(Absorbance0Green), g, first);
        first = Vector256.MultiplyAddEstimate(Vector256.Create(Absorbance0Red), r, first);

        second = Vector256.MultiplyAddEstimate(Vector256.Create(Absorbance1Blue), b, Vector256.Create(Absorbance01Bias));
        second = Vector256.MultiplyAddEstimate(Vector256.Create(Absorbance1Green), g, second);
        second = Vector256.MultiplyAddEstimate(Vector256.Create(Absorbance1Red), r, second);

        third = Vector256.MultiplyAddEstimate(Vector256.Create(Absorbance2Blue), b, Vector256.Create(Absorbance2Bias));
        third = Vector256.MultiplyAddEstimate(Vector256.Create(Absorbance2RedGreen), g, third);
        third = Vector256.MultiplyAddEstimate(Vector256.Create(Absorbance2RedGreen), r, third);
    }

    /// <summary>Evaluates the perceptual logarithmic response for 8 positive absorbance lanes.</summary>
    /// <param name="value">The absorbance after its native positive floor.</param>
    /// <returns>The response using the existing .NET log2 policy and the native final multiply-add order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> Gamma(Vector256<float> value)
    {
        Vector256<float> logarithm = Vector256.Log2(value + Vector256.Create(OpsinGammaInputBias));
        return Vector256.MultiplyAddEstimate(Vector256.Create(OpsinGammaSlope), logarithm, Vector256.Create(OpsinGammaOffset));
    }

    /// <summary>Adapts 16 independent RGB pixels using their blurred sensitivities, without changing pixel lane order.</summary>
    /// <param name="r">The red input samples.</param>
    /// <param name="g">The green input samples.</param>
    /// <param name="b">The blue input samples.</param>
    /// <param name="blurredR">The blurred red samples.</param>
    /// <param name="blurredG">The blurred green samples.</param>
    /// <param name="blurredB">The blurred blue samples.</param>
    /// <param name="intensity">The intensity multiplier applied before both absorbance calculations.</param>
    /// <param name="x">The perceptual X result.</param>
    /// <param name="y">The perceptual Y result.</param>
    /// <param name="blue">The perceptual B result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpsinDynamics(
        Vector512<float> r,
        Vector512<float> g,
        Vector512<float> b,
        Vector512<float> blurredR,
        Vector512<float> blurredG,
        Vector512<float> blurredB,
        Vector512<float> intensity,
        out Vector512<float> x,
        out Vector512<float> y,
        out Vector512<float> blue)
    {
        OpsinAbsorbance(
            blurredR * intensity,
            blurredG * intensity,
            blurredB * intensity,
            out Vector512<float> pre0,
            out Vector512<float> pre1,
            out Vector512<float> pre2);

        // The blurred absorbance floor keeps Gamma's input and its sensitivity divisor positive.
        pre0 = Vector512.Max(pre0, Vector512.Create(Absorbance01Bias));
        pre1 = Vector512.Max(pre1, Vector512.Create(Absorbance01Bias));
        pre2 = Vector512.Max(pre2, Vector512.Create(Absorbance2Bias));
        Vector512<float> sensitivity0 = Vector512.Max(Gamma(pre0) / pre0, Vector512.Create(MinimumOpsinSensitivity));
        Vector512<float> sensitivity1 = Vector512.Max(Gamma(pre1) / pre1, Vector512.Create(MinimumOpsinSensitivity));
        Vector512<float> sensitivity2 = Vector512.Max(Gamma(pre2) / pre2, Vector512.Create(MinimumOpsinSensitivity));

        OpsinAbsorbance(
            r * intensity,
            g * intensity,
            b * intensity,
            out Vector512<float> current0,
            out Vector512<float> current1,
            out Vector512<float> current2);

        current0 = Vector512.Max(current0 * sensitivity0, Vector512.Create(Absorbance01Bias));
        current1 = Vector512.Max(current1 * sensitivity1, Vector512.Create(Absorbance01Bias));
        current2 = Vector512.Max(current2 * sensitivity2, Vector512.Create(Absorbance2Bias));
        x = current0 - current1;
        y = current0 + current1;
        blue = current2;
    }

    /// <summary>Mixes 16 independent RGB samples into unclamped perceptual absorbance.</summary>
    /// <param name="r">The intensity-scaled red components.</param>
    /// <param name="g">The intensity-scaled green components.</param>
    /// <param name="b">The intensity-scaled blue components.</param>
    /// <param name="first">The first absorbance.</param>
    /// <param name="second">The second absorbance.</param>
    /// <param name="third">The third absorbance.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpsinAbsorbance(
        Vector512<float> r,
        Vector512<float> g,
        Vector512<float> b,
        out Vector512<float> first,
        out Vector512<float> second,
        out Vector512<float> third)
    {
        // Preserve B+bias, then G, then R as nested multiply-add estimates. Clamp policy belongs to the adaptation stage.
        first = Vector512.MultiplyAddEstimate(Vector512.Create(Absorbance0Blue), b, Vector512.Create(Absorbance01Bias));
        first = Vector512.MultiplyAddEstimate(Vector512.Create(Absorbance0Green), g, first);
        first = Vector512.MultiplyAddEstimate(Vector512.Create(Absorbance0Red), r, first);

        second = Vector512.MultiplyAddEstimate(Vector512.Create(Absorbance1Blue), b, Vector512.Create(Absorbance01Bias));
        second = Vector512.MultiplyAddEstimate(Vector512.Create(Absorbance1Green), g, second);
        second = Vector512.MultiplyAddEstimate(Vector512.Create(Absorbance1Red), r, second);

        third = Vector512.MultiplyAddEstimate(Vector512.Create(Absorbance2Blue), b, Vector512.Create(Absorbance2Bias));
        third = Vector512.MultiplyAddEstimate(Vector512.Create(Absorbance2RedGreen), g, third);
        third = Vector512.MultiplyAddEstimate(Vector512.Create(Absorbance2RedGreen), r, third);
    }

    /// <summary>Evaluates the perceptual logarithmic response for 16 positive absorbance lanes.</summary>
    /// <param name="value">The absorbance after its native positive floor.</param>
    /// <returns>The response using the existing .NET log2 policy and the native final multiply-add order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> Gamma(Vector512<float> value)
    {
        Vector512<float> logarithm = Vector512.Log2(value + Vector512.Create(OpsinGammaInputBias));
        return Vector512.MultiplyAddEstimate(Vector512.Create(OpsinGammaSlope), logarithm, Vector512.Create(OpsinGammaOffset));
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

    /// <summary>
    /// Accumulates full Malta differences into one component of an existing AC image.
    /// </summary>
    /// <param name="lum0">The borrowed reference band, at least eight samples wide and eight rows high.</param>
    /// <param name="lum1">The equally sized borrowed distorted band.</param>
    /// <param name="w0Gt1">The primary symmetric-difference weight, retained until operation setup.</param>
    /// <param name="w0Lt1">The secondary magnitude-penalty weight, retained until operation setup.</param>
    /// <param name="norm1">The native magnitude normalization, retained until operation setup.</param>
    /// <param name="diffs">The equally sized prepared-difference workspace, separate from the input and AC planes.</param>
    /// <param name="blockDiffAc">The equally sized existing AC accumulation image.</param>
    /// <param name="c">The accumulation component, in X, Y, B order.</param>
    /// <returns>Whether the input and workspace dimensions match.</returns>
    public static bool MaltaDiffMap(
        JxlPlane<float> lum0,
        JxlPlane<float> lum1,
        double w0Gt1,
        double w0Lt1,
        double norm1,
        JxlPlane<float> diffs,
        JxlImage3<float> blockDiffAc,
        int c)
        => MaltaDiffMap(isLf: false, lum0, lum1, w0Gt1, w0Lt1, norm1, diffs, blockDiffAc.Plane(c));

    /// <summary>
    /// Accumulates low-frequency Malta differences into one component of an existing AC image.
    /// </summary>
    /// <param name="lum0">The borrowed reference band, at least eight samples wide and eight rows high.</param>
    /// <param name="lum1">The equally sized borrowed distorted band.</param>
    /// <param name="w0Gt1">The primary symmetric-difference weight, retained until operation setup.</param>
    /// <param name="w0Lt1">The secondary magnitude-penalty weight, retained until operation setup.</param>
    /// <param name="norm1">The native magnitude normalization, retained until operation setup.</param>
    /// <param name="diffs">The equally sized prepared-difference workspace, separate from the input and AC planes.</param>
    /// <param name="blockDiffAc">The equally sized existing AC accumulation image.</param>
    /// <param name="c">The accumulation component, in X, Y, B order.</param>
    /// <returns>Whether the input and workspace dimensions match.</returns>
    public static bool MaltaDiffMapLf(
        JxlPlane<float> lum0,
        JxlPlane<float> lum1,
        double w0Gt1,
        double w0Lt1,
        double norm1,
        JxlPlane<float> diffs,
        JxlImage3<float> blockDiffAc,
        int c)
        => MaltaDiffMap(isLf: true, lum0, lum1, w0Gt1, w0Lt1, norm1, diffs, blockDiffAc.Plane(c));
}
