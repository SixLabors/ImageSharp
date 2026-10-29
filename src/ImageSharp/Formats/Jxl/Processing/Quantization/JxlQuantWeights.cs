// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

internal static class JxlQuantWeights
{
    public const int MaxQuantTableSize = JxlAcStrategy.MaximumCoefficientArea;

    public const int NumPredefinedTables = 1;

    public const int CeilLog2NumPredefinedTables = 0;

    public const int Log2NumQuantModes = 3;

    // The format rejects quantization weights outside this reciprocal-safe interval.
    private const float AlmostZero = 1e-8f;
    private const float MaximumWeight = 1F / AlmostZero;

    /// <summary>
    /// DCT quantizer encoding. (6 distance bands)
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct = JxlQuantizerEncoding.Dct(
        new JxlDctQuantWeightParameters(
            [
                [3150f, 0f, -0.4f, -0.4f, -0.4f, -2f],
                [560f, 0f, -0.3f, -0.3f, -0.3f, -0.3f],
                [512f, -2f, -1f, 0f, -1f, -2f]
            ],
            6));

    /// <summary>
    /// Identity quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Identity = JxlQuantizerEncoding.Identity(
        [
            [280f, 3160f, 3160f],
            [60f, 864f, 864f],
            [18f, 200f, 200f],
        ]);

    /// <summary>
    /// DCT2X2 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct2x2 = JxlQuantizerEncoding.Dct2(
        [
            [3840f, 2560f, 1280f, 640f, 480f, 300f],
            [960f, 640f, 320f, 180f, 140f, 120f],
            [640f, 320f, 128f, 64f, 32f, 16f],
        ]);

    /// <summary>
    /// DCT4X4 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct4x4 = JxlQuantizerEncoding.Dct4(
        new JxlDctQuantWeightParameters(
            [
                [2200, 0, 0, 0],
                [392, 0, 0, 0],
                [112, -0.25f, -0.25f, -0.5f]
            ],
            4),
        [
            [1, 1],
            [1, 1],
            [1, 1]
        ]);

    /// <summary>
    /// DCT16x16 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct16x16 = JxlQuantizerEncoding.Dct(
        new JxlDctQuantWeightParameters(
            [
                [
                    8996.8725711814115328f,
                    -1.3000777393353804f,
                    -0.49424529824571225f,
                    -0.439093774457103443f,
                    -0.6350101832695744f,
                    -0.90177264050827612f,
                    -1.6162099239887414f,
                ],
                [
                    3191.48366296844234752f,
                    -0.67424582104194355f,
                    -0.80745813428471001f,
                    -0.44925837484843441f,
                    -0.35865440981033403f,
                    -0.31322389111877305f,
                    -0.37615025315725483f,
                ],
                [
                    1157.50408145487200256f,
                    -2.0531423165804414f,
                    -1.4f,
                    -0.50687130033378396f,
                    -0.42708730624733904f,
                    -1.4856834539296244f,
                    -4.9209142884401604f,
                ]
            ],
            7));

    /// <summary>
    /// DCT32x32 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct32x32 = JxlQuantizerEncoding.Dct(
        new JxlDctQuantWeightParameters(
            [
                [
                    15718.40830982518931456f,
                    -1.025f,
                    -0.98f,
                    -0.9012f,
                    -0.4f,
                    -0.48819395464f,
                    -0.421064f,
                    -0.27f,
                ],
                [
                    7305.7636810695983104f,
                    -0.8041958212306401f,
                    -0.7633036457487539f,
                    -0.55660379990111464f,
                    -0.49785304658857626f,
                    -0.43699592683512467f,
                    -0.40180866526242109f,
                    -0.27321683125358037f,
                ],
                [
                    3803.53173721215041536f,
                    -3.060733579805728f,
                    -2.0413270132490346f,
                    -2.0235650159727417f,
                    -0.5495389509954993f,
                    -0.4f,
                    -0.4f,
                    -0.3f,
                ]
            ],
            8));

    /// <summary>
    /// DCT8x16 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct8x16 = JxlQuantizerEncoding.Dct(
        new JxlDctQuantWeightParameters(
            [
                [
                    7240.7734393502f,
                    -0.7f,
                    -0.7f,
                    -0.2f,
                    -0.2f,
                    -0.2f,
                    -0.5f,
                ],
                [
                    1448.15468787004f,
                    -0.5f,
                    -0.5f,
                    -0.5f,
                    -0.2f,
                    -0.2f,
                    -0.2f,
                ],
                [
                    506.854140754517f,
                    -1.4f,
                    -0.2f,
                    -0.5f,
                    -0.5f,
                    -1.5f,
                    -3.6f,
                ]
            ],
            7));

    /// <summary>
    /// DCT8x32 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct8x32 = JxlQuantizerEncoding.Dct(
        new JxlDctQuantWeightParameters(
            [
                [
                    16283.2494710648897f,
                    -1.7812845336559429f,
                    -1.6309059012653515f,
                    -1.0382179034313539f,
                    -0.85f,
                    -0.7f,
                    -0.9f,
                    -1.2360638576849587f,
                ],
                [
                    5089.15750884921511936f,
                    -0.320049391452786891f,
                    -0.35362849922161446f,
                    -0.30340000000000003f,
                    -0.61f,
                    -0.5f,
                    -0.5f,
                    -0.6f,
                ],
                [
                    3397.77603275308720128f,
                    -0.321327362693153371f,
                    -0.34507619223117997f,
                    -0.70340000000000003f,
                    -0.9f,
                    -1.0f,
                    -1.0f,
                    -1.1754605576265209f,
                ]
            ],
            8));

    /// <summary>
    /// DCT16x32 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct16x32 = JxlQuantizerEncoding.Dct(
        new JxlDctQuantWeightParameters(
            [
                [
                    13844.97076442300573f,
                    -0.97113799999999995f,
                    -0.658f,
                    -0.42026f,
                    -0.22712f,
                    -0.2206f,
                    -0.226f,
                    -0.6f,
                ],
                [
                    4798.964084220744293f,
                    -0.61125308982767057f,
                    -0.83770786552491361f,
                    -0.79014862079498627f,
                    -0.2692727459704829f,
                    -0.38272769465388551f,
                    -0.22924222653091453f,
                    -0.20719098826199578f,
                ],
                [
                    1807.236946760964614f,
                    -1.2f,
                    -1.2f,
                    -0.7f,
                    -0.7f,
                    -0.7f,
                    -0.4f,
                    -0.5f,
                ]
            ],
            8));

    /// <summary>
    /// DCT4x8 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct4x8 = JxlQuantizerEncoding.Dct4x8(
        new JxlDctQuantWeightParameters(
            [
                [
                    2198.050556016380522f,
                    -0.96269623020744692f,
                    -0.76194253026666783f,
                    -0.6551140670773547f
                ],
                [
                    764.3655248643528689f,
                    -0.92630200888366945f,
                    -0.9675229603596517f,
                    -0.27845290869168118f
                ],
                [
                    527.107573587542228f,
                    -1.4594385811273854f,
                    -1.450082094097871593f,
                    -1.5843722511996204f
                ]
            ],
            4),
        [1, 1, 1]);

    /// <summary>
    /// AFV quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Afv = JxlQuantizerEncoding.Afv(
        Dct4x8.DctParameters!,
        Dct4x4.DctParameters!,
        [
            [3072, 3072, 256, 256, 256, 414, 0, 0, 0],
            [1024, 1024, 50, 50, 50, 58, 0, 0, 0],
            [384, 384, 12, 12, 12, 22, -0.25f, -0.25f, -0.25f]
        ]);

    /// <summary>
    /// DCT64x64 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct64x64 = JxlQuantizerEncoding.Dct(
        new JxlDctQuantWeightParameters(
            [
                [
                    0.9f * 26629.073922049845f,
                    -1.025f,
                    -0.78f,
                    -0.65012f,
                    -0.19041574084286472f,
                    -0.20819395464f,
                    -0.421064f,
                    -0.32733845535848671f,
                ],
                [
                    0.9f * 9311.3238710010046f,
                    -0.3041958212306401f,
                    -0.3633036457487539f,
                    -0.35660379990111464f,
                    -0.3443074455424403f,
                    -0.33699592683512467f,
                    -0.30180866526242109f,
                    -0.27321683125358037f,
                ],
                [
                    0.9f * 4992.2486445538634f,
                    -1.2f,
                    -1.2f,
                    -0.8f,
                    -0.7f,
                    -0.7f,
                    -0.4f,
                    -0.5f,
                ]
            ],
            8));

    /// <summary>
    /// DCT32x64 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct32x64 = JxlQuantizerEncoding.Dct(
        new JxlDctQuantWeightParameters(
            [
                [
                    0.65f * 23629.073922049845f,
                    -1.025f,
                    -0.78f,
                    -0.65012f,
                    -0.19041574084286472f,
                    -0.20819395464f,
                    -0.421064f,
                    -0.32733845535848671f,
                ],
                [
                    0.65f * 8611.3238710010046f,
                    -0.3041958212306401f,
                    -0.3633036457487539f,
                    -0.35660379990111464f,
                    -0.3443074455424403f,
                    -0.33699592683512467f,
                    -0.30180866526242109f,
                    -0.27321683125358037f,
                ],
                [
                    0.65f * 4492.2486445538634f,
                    -1.2f,
                    -1.2f,
                    -0.8f,
                    -0.7f,
                    -0.7f,
                    -0.4f,
                    -0.5f,
                ]
            ],
            8));

    /// <summary>
    /// DCT128x128 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct128x128 = JxlQuantizerEncoding.Dct(
        new JxlDctQuantWeightParameters(
            [
                [
                    1.8f * 26629.073922049845f,
                    -1.025f,
                    -0.78f,
                    -0.65012f,
                    -0.19041574084286472f,
                    -0.20819395464f,
                    -0.421064f,
                    -0.32733845535848671f,
                ],
                [
                    1.8f * 9311.3238710010046f,
                    -0.3041958212306401f,
                    -0.3633036457487539f,
                    -0.35660379990111464f,
                    -0.3443074455424403f,
                    -0.33699592683512467f,
                    -0.30180866526242109f,
                    -0.27321683125358037f,
                ],
                [
                    1.8f * 4992.2486445538634f,
                    -1.2f,
                    -1.2f,
                    -0.8f,
                    -0.7f,
                    -0.7f,
                    -0.4f,
                    -0.5f,
                ]
            ],
            8));

    /// <summary>
    /// DCT64x128 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct64x128 = JxlQuantizerEncoding.Dct(
        new JxlDctQuantWeightParameters(
            [
                [
                    1.3f * 23629.073922049845f,
                    -1.025f,
                    -0.78f,
                    -0.65012f,
                    -0.19041574084286472f,
                    -0.20819395464f,
                    -0.421064f,
                    -0.32733845535848671f,
                ],
                [
                    1.3f * 8611.3238710010046f,
                    -0.3041958212306401f,
                    -0.3633036457487539f,
                    -0.35660379990111464f,
                    -0.3443074455424403f,
                    -0.33699592683512467f,
                    -0.30180866526242109f,
                    -0.27321683125358037f,
                ],
                [
                    1.3f * 4492.2486445538634f,
                    -1.2f,
                    -1.2f,
                    -0.8f,
                    -0.7f,
                    -0.7f,
                    -0.4f,
                    -0.5f,
                ]
            ],
            8));

    /// <summary>
    /// DCT256x256 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct256x256 = JxlQuantizerEncoding.Dct(
        new JxlDctQuantWeightParameters(
            [
                [
                    3.6f * 26629.073922049845f,
                    -1.025f,
                    -0.78f,
                    -0.65012f,
                    -0.19041574084286472f,
                    -0.20819395464f,
                    -0.421064f,
                    -0.32733845535848671f,
                ],
                [
                    3.6f * 9311.3238710010046f,
                    -0.3041958212306401f,
                    -0.3633036457487539f,
                    -0.35660379990111464f,
                    -0.3443074455424403f,
                    -0.33699592683512467f,
                    -0.30180866526242109f,
                    -0.27321683125358037f,
                ],
                [
                    3.6f * 4992.2486445538634f,
                    -1.2f,
                    -1.2f,
                    -0.8f,
                    -0.7f,
                    -0.7f,
                    -0.4f,
                    -0.5f,
                ]
            ],
            8));

    /// <summary>
    /// DCT128x256 quantizer encoding.
    /// </summary>
    public static readonly JxlQuantizerEncoding Dct128x256 = JxlQuantizerEncoding.Dct(
        new JxlDctQuantWeightParameters(
            [
                [
                    2.6f * 23629.073922049845f,
                    -1.025f,
                    -0.78f,
                    -0.65012f,
                    -0.19041574084286472f,
                    -0.20819395464f,
                    -0.421064f,
                    -0.32733845535848671f,
                ],
                [
                    2.6f * 8611.3238710010046f,
                    -0.3041958212306401f,
                    -0.3633036457487539f,
                    -0.35660379990111464f,
                    -0.3443074455424403f,
                    -0.33699592683512467f,
                    -0.30180866526242109f,
                    -0.27321683125358037f,
                ],
                [
                    2.6f * 4492.2486445538634f,
                    -1.2f,
                    -1.2f,
                    -0.8f,
                    -0.7f,
                    -0.7f,
                    -0.4f,
                    -0.5f,
                ]
            ],
            8));

    private static ReadOnlySpan<float> AfvFrequencies =>
    [
        0xBAD,
        0xBAD,
        0.8517778890324296f,
        5.37778436506804f,
        0xBAD,
        0xBAD,
        4.734747904497923f,
        5.449245381693219f,
        1.6598270267479331f,
        4,
        7.275749096817861f,
        10.423227632456525f,
        2.662932286148962f,
        7.630657783650829f,
        8.962388608184032f,
        12.97166202570235f,
    ];

    public static void GetQuantWeightsDCT2(float[][] dct2Weights, Span<float> weights)
    {
        for (int c = 0; c < 3; c++)
        {
            int start = c * 64;

            weights[start] = 0xBAD;
            weights[start + 1] = weights[start + 8] = dct2Weights[c][0];
            weights[start + 9] = dct2Weights[c][1];

            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    weights[start + (y * 8) + x + 2] = dct2Weights[c][2];
                    weights[start + ((y + 2) * 8) + x] = dct2Weights[c][2];
                }
            }

            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    weights[start + ((y + 2) * 8) + x + 2] = dct2Weights[c][3];
                }
            }

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    weights[start + (y * 8) + x + 4] = dct2Weights[c][4];
                    weights[start + ((y + 4) * 8) + x] = dct2Weights[c][4];
                }
            }

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    weights[start + ((y + 4) * 8) + x + 4] = dct2Weights[c][5];
                }
            }
        }
    }

    public static void GetQuantWeightsIdentity(float[][] idWeights, Span<float> weights)
    {
        for (int c = 0; c < 3; c++)
        {
            int c64 = 64 * c;

            for (int i = 0; i < 64; i++)
            {
                weights[c64 + i] = idWeights[c][0];
            }

            weights[c64 + 1] = idWeights[c][1];
            weights[c64 + 8] = idWeights[c][1];
            weights[c64 + 9] = idWeights[c][2];
        }
    }

    /// <summary>
    /// Interpolates geometrically between adjacent quantization weights.
    /// </summary>
    /// <param name="scaledPosition">The position in sample intervals, below sampleValues.Length minus one.</param>
    /// <param name="sampleValues">The ordered sample values.</param>
    /// <param name="logRatios">The precomputed base-two logarithms of adjacent sample ratios.</param>
    /// <returns>The interpolated weight.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Interpolate(float scaledPosition, ReadOnlySpan<float> sampleValues, ReadOnlySpan<float> logRatios)
    {
        int sampleIndex = (int)scaledPosition;
        float a = sampleValues[sampleIndex];
        float fraction = scaledPosition - sampleIndex;
        float logarithm = logRatios[sampleIndex];
        return fraction == 0F || logarithm == 0F ? a : a * float.Exp2(logarithm * fraction);
    }

    /// <summary>
    /// Converts a signed distance-band delta to its positive multiplicative factor.
    /// </summary>
    /// <param name="v">The encoded delta.</param>
    /// <returns>One plus a positive delta, or the reciprocal of one minus a nonpositive delta.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Mult(float v) => v > 0f ? 1.0f + v : 1.0f / (1.0f - v);

    /// <summary>
    /// Interpolates four quantization weights from sample-space positions.
    /// </summary>
    /// <param name="scaledPositions">Nonnegative sample-space positions below sampleValues.Length minus one.</param>
    /// <param name="sampleValues">The ordered sample values.</param>
    /// <param name="logRatios">The precomputed base-two logarithms of adjacent sample ratios.</param>
    /// <returns>The four interpolated weights.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> Interpolate(Vector128<float> scaledPositions, ReadOnlySpan<float> sampleValues, ReadOnlySpan<float> logRatios)
    {
        // Positions are nonnegative and below the final band. Native truncation cannot produce an out-of-range gather index.
        Vector128<int> idx = Vector128.ConvertToInt32Native(scaledPositions);
        Vector128<float> frac = scaledPositions - Vector128.ConvertToSingle(idx);
        Vector128<float> a = Vector128_.Gather(ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(sampleValues)), idx).AsSingle();
        Vector128<float> logarithm = Vector128_.Gather(ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(logRatios)), idx).AsSingle();
        Vector128<float> exact = Vector128.Equals(frac, Vector128<float>.Zero) | Vector128.Equals(logarithm, Vector128<float>.Zero);

        // Each lane uses its own interval and exponent. Shared Exp2 is approximate; restore exact interval starts and constant intervals.
        return Vector128.ConditionalSelect(exact, a, a * Vector128_.Exp2(logarithm * frac));
    }

    /// <summary>
    /// Interpolates eight quantization weights from sample-space positions.
    /// </summary>
    /// <param name="scaledPositions">Nonnegative sample-space positions below sampleValues.Length minus one.</param>
    /// <param name="sampleValues">The ordered sample values.</param>
    /// <param name="logRatios">The precomputed base-two logarithms of adjacent sample ratios.</param>
    /// <returns>The eight interpolated weights.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Interpolate(Vector256<float> scaledPositions, ReadOnlySpan<float> sampleValues, ReadOnlySpan<float> logRatios)
    {
        Vector256<int> idx = Vector256.ConvertToInt32Native(scaledPositions);
        Vector256<float> frac = scaledPositions - Vector256.ConvertToSingle(idx);
        Vector256<float> a = Vector256_.Gather(ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(sampleValues)), idx).AsSingle();
        Vector256<float> logarithm = Vector256_.Gather(ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(logRatios)), idx).AsSingle();
        Vector256<float> exact = Vector256.Equals(frac, Vector256<float>.Zero) | Vector256.Equals(logarithm, Vector256<float>.Zero);
        return Vector256.ConditionalSelect(exact, a, a * Vector256_.Exp2(logarithm * frac));
    }

    /// <summary>
    /// Interpolates sixteen quantization weights from sample-space positions on a supported SIMD target.
    /// </summary>
    /// <param name="scaledPositions">Nonnegative sample-space positions below sampleValues.Length minus one.</param>
    /// <param name="sampleValues">The ordered sample values.</param>
    /// <param name="logRatios">The precomputed base-two logarithms of adjacent sample ratios.</param>
    /// <returns>The sixteen interpolated weights.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Interpolate(Vector512<float> scaledPositions, ReadOnlySpan<float> sampleValues, ReadOnlySpan<float> logRatios)
    {
        Vector512<int> idx = Vector512.ConvertToInt32Native(scaledPositions);
        Vector512<float> frac = scaledPositions - Vector512.ConvertToSingle(idx);
        Vector512<float> a = Vector512_.Gather(ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(sampleValues)), idx).AsSingle();
        Vector512<float> logarithm = Vector512_.Gather(ref Unsafe.As<float, int>(ref MemoryMarshal.GetReference(logRatios)), idx).AsSingle();
        Vector512<float> exact = Vector512.Equals(frac, Vector512<float>.Zero) | Vector512.Equals(logarithm, Vector512<float>.Zero);
        return Vector512.ConditionalSelect(exact, a, a * Vector512_.Exp2(logarithm * frac));
    }

    /// <summary>
    /// Precomputes the logarithm used by every position in each geometric interval.
    /// </summary>
    /// <param name="bands">The positive cumulative band values.</param>
    /// <param name="logRatios">Storage for bands.Length minus one adjacent logarithms.</param>
    private static void PrepareLogRatios(ReadOnlySpan<float> bands, Span<float> logRatios)
    {
        for (int i = 0; i < logRatios.Length; i++)
        {
            logRatios[i] = bands[i + 1] / bands[i];
        }

        TensorPrimitives.Log2(logRatios, logRatios);
    }

    /// <summary>
    /// Builds quantization weights from the distance bands for each color channel.
    /// </summary>
    /// <param name="rowCount">The number of coefficient rows.</param>
    /// <param name="columnCount">The number of coefficients per row.</param>
    /// <param name="distanceBands">The distance-band parameters for each channel.</param>
    /// <param name="bandCount">The number of bands per channel.</param>
    /// <param name="output">The destination weights, in channel, row, and column order.</param>
    /// <returns>Whether the cumulative bands satisfy the native minimum weight.</returns>
    public static bool GetQuantWeights(int rowCount, int columnCount, float[][] distanceBands, int bandCount, Span<float> output)
    {
        Span<float> bands = stackalloc float[JxlQuantizerConstants.MaxDistanceBands];
        Span<float> logStorage = stackalloc float[JxlQuantizerConstants.MaxDistanceBands - 1];
        Span<float> logRatios = logStorage[..(bandCount - 1)];
        int channelSize = rowCount * columnCount;
        float positionScale = (bandCount - 1) / (JxlDctScales.Sqrt2 + 1e-6f);
        float columnStep = positionScale / (columnCount - 1);
        float rowStep = positionScale / (rowCount - 1);

        for (int c = 0; c < 3; c++)
        {
            bands[0] = distanceBands[c][0];

            if (bands[0] < AlmostZero)
            {
                return false;
            }

            for (int i = 1; i < bandCount; i++)
            {
                bands[i] = bands[i - 1] * Mult(distanceBands[c][i]);

                if (bands[i] < AlmostZero)
                {
                    return false;
                }
            }

            Span<float> channel = output.Slice(c * channelSize, channelSize);
            if (bandCount == 1)
            {
                channel.Fill(bands[0]);
                continue;
            }

            // These two bounded stack tables serve every row of this channel. No per-coefficient logarithm or division is needed.
            // The epsilon in positionScale keeps distances below bandCount-1, so truncation selects an existing adjacent interval.
            // Interior values use shared Exp2 precision, which can differ from libjxl's separate FastPowf approximation.
            ReadOnlySpan<float> sampleValues = bands[..bandCount];
            PrepareLogRatios(sampleValues, logRatios);

            for (int y = 0; y < rowCount; y++)
            {
                float dy = y * rowStep;
                float dy2 = dy * dy;
                Span<float> row = channel.Slice(y * columnCount, columnCount);
                ref float destination = ref MemoryMarshal.GetReference(row);
                int x = 0;

                // Lanes hold consecutive coefficient columns. Each store covers only complete registers within this logical row.
                if (Vector512.IsHardwareAccelerated)
                {
                    for (; x <= columnCount - Vector512<float>.Count; x += Vector512<float>.Count)
                    {
                        Vector512<float> dx = (Vector512<float>.Indices + Vector512.Create((float)x)) * Vector512.Create(columnStep);
                        Vector512<float> distance = Vector512.Sqrt(Vector512.MultiplyAddEstimate(dx, dx, Vector512.Create(dy2)));
                        Interpolate(distance, sampleValues, logRatios).StoreUnsafe(ref destination, (uint)x);
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    for (; x <= columnCount - Vector256<float>.Count; x += Vector256<float>.Count)
                    {
                        Vector256<float> dx = (Vector256<float>.Indices + Vector256.Create((float)x)) * Vector256.Create(columnStep);
                        Vector256<float> distance = Vector256.Sqrt(Vector256.MultiplyAddEstimate(dx, dx, Vector256.Create(dy2)));
                        Interpolate(distance, sampleValues, logRatios).StoreUnsafe(ref destination, (uint)x);
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    for (; x <= columnCount - Vector128<float>.Count; x += Vector128<float>.Count)
                    {
                        Vector128<float> dx = (Vector128<float>.Indices + Vector128.Create((float)x)) * Vector128.Create(columnStep);
                        Vector128<float> distance = Vector128.Sqrt(Vector128.MultiplyAddEstimate(dx, dx, Vector128.Create(dy2)));
                        Interpolate(distance, sampleValues, logRatios).StoreUnsafe(ref destination, (uint)x);
                    }
                }

                for (; x < columnCount; x++)
                {
                    float dx = x * columnStep;
                    float distance = MathF.Sqrt(float.MultiplyAddEstimate(dx, dx, dy2));
                    row[x] = Interpolate(distance, sampleValues, logRatios);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Builds dequantization weights and their reciprocals for one transform in the caller's final storage.
    /// </summary>
    /// <param name="encoding">The quantization weight parameters.</param>
    /// <param name="quantizationTable">The destination dequantization weights. Each entry is the reciprocal of the generated weight.</param>
    /// <param name="inverseQuantizationTable">The destination quantization weights. Low-frequency entries are cleared after preparation.</param>
    /// <param name="kind">The transform table kind.</param>
    /// <param name="writeOffset">The starting destination offset, advanced past the written weights.</param>
    /// <returns>Whether the encoding supports the selected transform.</returns>
    public static bool ComputeQuantTable(
        JxlQuantizerEncoding encoding,
        Span<float> quantizationTable,
        Span<float> inverseQuantizationTable,
        JxlQuantTable kind,
        ref int writeOffset)
    {
        const int n = JxlFrameDimensions.BlockDimensions;

        int quantSizeTable = (int)kind;
        int wrows = 8 * JxlDequantMatrices.RequiredSizeX[quantSizeTable];
        int wcols = 8 * JxlDequantMatrices.RequiredSizeY[quantSizeTable];
        int num = wrows * wcols;

        // Generate all three channel planes directly in their final allocation. The largest transform has 196,608 entries.
        // Keeping this view avoids a proportional stack allocation and subsequent copies of the complete table.
        Span<float> weights = inverseQuantizationTable.Slice(writeOffset, 3 * num);

        switch (encoding.Mode)
        {
            case JxlQuantMode.Library:
            {
                // Library and copy quant encoding should get replaced by the actual
                // parameters by the caller.
                return false;
            }

            case JxlQuantMode.Id:
            {
                if (num != JxlFrameDimensions.DctBlockSize)
                {
                    return false;
                }

                GetQuantWeightsIdentity(encoding.IdWeights!, weights);
                break;
            }

            case JxlQuantMode.Dct2:
            {
                if (num != JxlFrameDimensions.DctBlockSize)
                {
                    return false;
                }

                GetQuantWeightsDCT2(encoding.Dct2Weights!, weights);
                break;
            }

            case JxlQuantMode.Dct4:
            {
                if (num != JxlFrameDimensions.DctBlockSize)
                {
                    return false;
                }

                Span<float> weights4x4 = stackalloc float[3 * 4 * 4];

                // Always use 4x4 GetQuantWeights for DCT4 quantization tables.
                if (!GetQuantWeights(4, 4, encoding.DctParameters!.DistanceBands, encoding.DctParameters.NumDistanceBands, weights4x4))
                {
                    return false;
                }

                for (int c = 0; c < 3; c++)
                {
                    for (int y = 0; y < JxlFrameDimensions.BlockDimensions; y++)
                    {
                        for (int x = 0; x < JxlFrameDimensions.BlockDimensions; x++)
                        {
                            weights[(c * num) + (y * JxlFrameDimensions.BlockDimensions) + x] = weights4x4[(c * 16) + ((y / 2) * 4) + (x / 2)];
                        }
                    }

                    weights[(c * num) + 1] /= encoding.Dct4Multipliers![c][0];
                    weights[(c * num) + n] /= encoding.Dct4Multipliers[c][0];
                    weights[(c * num) + n + 1] /= encoding.Dct4Multipliers[c][1];
                }

                break;
            }

            case JxlQuantMode.Dct4x8:
            {
                if (num != JxlFrameDimensions.DctBlockSize)
                {
                    return false;
                }

                Span<float> weights4x8 = stackalloc float[3 * 4 * 8];

                if (!GetQuantWeights(4, 8, encoding.DctParameters!.DistanceBands, encoding.DctParameters.NumDistanceBands, weights4x8))
                {
                    return false;
                }

                for (int c = 0; c < 3; c++)
                {
                    for (int y = 0; y < JxlFrameDimensions.BlockDimensions; y++)
                    {
                        for (int x = 0; x < JxlFrameDimensions.BlockDimensions; x++)
                        {
                            weights[(c * num) + (y * JxlFrameDimensions.BlockDimensions) + x] = weights4x8[(c * 32) + ((y / 2) * 8) + x];
                        }
                    }

                    weights[(c * num) + n] /= encoding.Dct4x8Multipliers![c];
                }

                break;
            }

            case JxlQuantMode.Dct:
            {
                if (!GetQuantWeights(wrows, wcols, encoding.DctParameters!.DistanceBands, encoding.DctParameters.NumDistanceBands, weights))
                {
                    return false;
                }

                break;
            }

            case JxlQuantMode.Raw:
            {
                if (encoding.QuantizationTable is null || encoding.QuantizationTable.Length != 3 * num)
                {
                    throw new InvalidOperationException("Invalid raw quantizer table encoding");
                }

                Span<int> qtable = encoding.QuantizationTable.AsSpan();

                for (int i = 0; i < 3 * num; i++)
                {
                    weights[i] = 1f / (encoding.QuantizationTableDenominator * qtable[i]);
                }

                break;
            }

            case JxlQuantMode.Afv:
            {
                Span<float> weights4x8 = stackalloc float[3 * 4 * 8];

                if (!GetQuantWeights(4, 8, encoding.DctParameters!.DistanceBands, encoding.DctParameters.NumDistanceBands, weights4x8))
                {
                    return false;
                }

                Span<float> weights4x4 = stackalloc float[3 * 4 * 4];

                if (!GetQuantWeights(4, 4, encoding.DctParametersAfv4x4!.DistanceBands, encoding.DctParametersAfv4x4.NumDistanceBands, weights4x4))
                {
                    return false;
                }

                const float lo = 0.8517778890324296f;
                const float hi = 12.97166202570235f - lo + 1e-6f;

                Span<float> bands = [0, 0, 0, 0];
                Span<float> logRatios = stackalloc float[3];

                for (int c = 0; c < 3; c++)
                {
                    bands[0] = encoding.AfvWeights![c][5];

                    if (bands[0] < AlmostZero)
                    {
                        throw new InvalidOperationException("Invalid AFV bands");
                    }

                    for (int i = 1; i < 4; i++)
                    {
                        bands[i] = bands[i - 1] * Mult(encoding.AfvWeights[c][i + 5]);

                        if (bands[i] < AlmostZero)
                        {
                            throw new InvalidOperationException("Invalid AFV bands");
                        }
                    }

                    PrepareLogRatios(bands, logRatios);

                    int start = c * 64;

                    void SetWeight(int x, int y, float value, Span<float> weights) => weights[start + (y * 8) + x] = value;

                    weights[start] = 1;

                    SetWeight(0, 1, encoding.AfvWeights[c][0], weights);
                    SetWeight(1, 0, encoding.AfvWeights[c][1], weights);
                    SetWeight(0, 2, encoding.AfvWeights[c][2], weights);
                    SetWeight(2, 0, encoding.AfvWeights[c][3], weights);
                    SetWeight(2, 2, encoding.AfvWeights[c][4], weights);

                    // All other AFV weights.
                    for (int y = 0; y < 4; y++)
                    {
                        for (int x = 0; x < 4; x++)
                        {
                            if (x < 2 && y < 2)
                            {
                                continue;
                            }

                            float position = (AfvFrequencies[(y * 4) + x] - lo) * 3 / hi;
                            float interpolatedVal = Interpolate(position, bands, logRatios);

                            SetWeight(2 * x, 2 * y, interpolatedVal, weights);
                        }
                    }

                    // Put 4x8 weights in odd rows, except (1, 0).
                    for (int y = 0; y < JxlFrameDimensions.BlockDimensions / 2; y++)
                    {
                        for (int x = 0; x < JxlFrameDimensions.BlockDimensions; x++)
                        {
                            if (x == 0 && y == 0)
                            {
                                continue;
                            }

                            weights[(c * num) + (((2 * y) + 1) * JxlFrameDimensions.BlockDimensions) + x] = weights4x8[(c * 32) + (y * 8) + x];
                        }
                    }

                    // Put 4x4 weights in even rows / odd columns, except (0, 1).
                    for (int y = 0; y < JxlFrameDimensions.BlockDimensions / 2; y++)
                    {
                        for (int x = 0; x < JxlFrameDimensions.BlockDimensions / 2; x++)
                        {
                            if (x == 0 && y == 0)
                            {
                                continue;
                            }

                            weights[(c * num) + ((2 * y) * JxlFrameDimensions.BlockDimensions) + (2 * x) + 1] = weights4x4[(c * 16) + (y * 4) + x];
                        }
                    }
                }

                break;
            }

            default:
                return false;
        }

        int prevPos = writeOffset;

        // libjxl quant_weights.cc validates this interval before division. Validate and invert together;
        // no temporary table, fill with ones, or extra validation pass is required. Each lane owns one coefficient.
        TensorPrimitives_.Invoke(weights, quantizationTable.Slice(writeOffset, weights.Length), default(ReciprocalWeightOperator));

        writeOffset += 3 * num;

        int xs = JxlDequantMatrices.RequiredSizeX[quantSizeTable];
        int ys = JxlDequantMatrices.RequiredSizeY[quantSizeTable];

        JxlForwardCoefficientOrder.CoefficientLayout(ref ys, ref xs);

        for (int c = 0; c < 3; c++)
        {
            for (int y = 0; y < ys; y++)
            {
                for (int x = 0; x < xs; x++)
                {
                    inverseQuantizationTable[prevPos + (c * ys * xs * JxlFrameDimensions.DctBlockSize) + (y * JxlFrameDimensions.BlockDimensions * xs) + x] = 0;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Validates generated weights and returns their reciprocals. Preparation rejects invalid weights instead of clipping them.
    /// </summary>
    private readonly struct ReciprocalWeightOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value)
        {
            // Ordered comparisons also reject NaN. Positive finite weights in [1e-8,1e8) have finite positive reciprocals.
            if (!(value >= AlmostZero && value < MaximumWeight))
            {
                throw new InvalidImageContentException("The quantization weight is outside the supported range.");
            }

            return 1F / value;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            if (!(Vector128.GreaterThanOrEqualAll(value, Vector128.Create(AlmostZero)) && Vector128.LessThanAll(value, Vector128.Create(MaximumWeight))))
            {
                throw new InvalidImageContentException("The quantization weight is outside the supported range.");
            }

            return Vector128<float>.One / value;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            if (!(Vector256.GreaterThanOrEqualAll(value, Vector256.Create(AlmostZero)) && Vector256.LessThanAll(value, Vector256.Create(MaximumWeight))))
            {
                throw new InvalidImageContentException("The quantization weight is outside the supported range.");
            }

            return Vector256<float>.One / value;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            if (!(Vector512.GreaterThanOrEqualAll(value, Vector512.Create(AlmostZero)) && Vector512.LessThanAll(value, Vector512.Create(MaximumWeight))))
            {
                throw new InvalidImageContentException("The quantization weight is outside the supported range.");
            }

            return Vector512<float>.One / value;
        }
    }

    public static bool DecodeDctParameters(JxlBitReader reader, JxlDctQuantWeightParameters parameters)
    {
        parameters.NumDistanceBands = (int)reader.ReadBits32(JxlQuantizerConstants.Log2MaxDistanceBands) + 1;

        for (int c = 0; c < 3; c++)
        {
            for (int i = 0; i < parameters.NumDistanceBands; i++)
            {
                if (!JxlF16Coder.Read(reader, ref parameters.DistanceBands[c][i]))
                {
                    return false;
                }
            }

            if (parameters.DistanceBands[c][0] < AlmostZero)
            {
                throw new InvalidOperationException("Distance band seed is too small");
            }

            parameters.DistanceBands[c][0] *= 64f;
        }

        return true;
    }

    /// <summary>
    /// Reads the quantization weight parameters for one table.
    /// </summary>
    /// <param name="configuration">The configuration used by the modular decoder.</param>
    /// <param name="reader">The source bit reader.</param>
    /// <param name="encoding">The destination quantization parameters.</param>
    /// <param name="requiredSizeX">The required horizontal extent in blocks.</param>
    /// <param name="requiredSizeY">The required vertical extent in blocks.</param>
    /// <param name="quantizationTableIndex">The index that identifies the table in the modular stream.</param>
    /// <param name="modularFrameDecoder">The decoder for raw quantization tables.</param>
    /// <returns>Whether the parameters were read successfully.</returns>
    public static bool Decode(
        Configuration configuration,
        JxlBitReader reader,
        JxlQuantizerEncoding encoding,
        int requiredSizeX,
        int requiredSizeY,
        int quantizationTableIndex,
        JxlModularFrameDecoder modularFrameDecoder)
    {
        int requiredSize = requiredSizeX * requiredSizeY;

        requiredSizeX *= JxlFrameDimensions.BlockDimensions;
        requiredSizeY *= JxlFrameDimensions.BlockDimensions;

        int mode = (int)reader.ReadBits32(JxlQuantizerConstants.Log2NumQuantModes);

        switch ((JxlQuantMode)mode)
        {
            case JxlQuantMode.Library:
            {
                encoding.Predefined = (byte)reader.ReadBits32(CeilLog2NumPredefinedTables);

                if (encoding.Predefined >= NumPredefinedTables)
                {
                    throw new InvalidOperationException("Invalid predefined table");
                }

                break;
            }

            case JxlQuantMode.Id:
            {
                if (requiredSize != 1)
                {
                    throw new InvalidOperationException("Invalid mode");
                }

                for (int c = 0; c < 3; c++)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (!JxlF16Coder.Read(reader, ref encoding.IdWeights![c][i]))
                        {
                            return false;
                        }

                        if (Math.Abs(encoding.IdWeights[c][i]) < AlmostZero)
                        {
                            throw new InvalidOperationException("ID Quantizer is too small");
                        }

                        encoding.IdWeights[c][i] *= 64;
                    }
                }

                break;
            }

            case JxlQuantMode.Dct2:
            {
                if (requiredSize != 1)
                {
                    throw new InvalidOperationException("Invalid mode");
                }

                for (int c = 0; c < 3; c++)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        if (!JxlF16Coder.Read(reader, ref encoding.Dct2Weights![c][i]))
                        {
                            return false;
                        }

                        if (Math.Abs(encoding.Dct2Weights[c][i]) < AlmostZero)
                        {
                            throw new InvalidOperationException("Quantizer is too small");
                        }

                        encoding.Dct2Weights[c][i] *= 64;
                    }
                }

                break;
            }

            case JxlQuantMode.Dct4x8:
            {
                if (requiredSize != 1)
                {
                    throw new InvalidOperationException("Invalid mode");
                }

                for (int c = 0; c < 3; c++)
                {
                    if (!JxlF16Coder.Read(reader, ref encoding.Dct4x8Multipliers![c]))
                    {
                        return false;
                    }

                    if (Math.Abs(encoding.Dct4x8Multipliers[c]) < AlmostZero)
                    {
                        throw new InvalidOperationException("DCT4X8 multiplier is too small");
                    }
                }

                if (!DecodeDctParameters(reader, encoding.DctParameters!))
                {
                    return false;
                }

                break;
            }

            case JxlQuantMode.Dct4:
            {
                if (requiredSize != 1)
                {
                    throw new InvalidOperationException("Invalid mode");
                }

                for (int c = 0; c < 3; c++)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        if (!JxlF16Coder.Read(reader, ref encoding.Dct4Multipliers![c][i]))
                        {
                            return false;
                        }

                        if (Math.Abs(encoding.Dct4Multipliers[c][i]) < AlmostZero)
                        {
                            throw new InvalidOperationException("DCT4 multiplier is too small");
                        }
                    }
                }

                if (!DecodeDctParameters(reader, encoding.DctParameters!))
                {
                    return false;
                }

                break;
            }

            case JxlQuantMode.Afv:
            {
                if (requiredSize != 1)
                {
                    throw new InvalidOperationException("Invalid mode");
                }

                for (int c = 0; c < 3; c++)
                {
                    for (int i = 0; i < 9; i++)
                    {
                        if (!JxlF16Coder.Read(reader, ref encoding.AfvWeights![c][i]))
                        {
                            return false;
                        }
                    }

                    for (int i = 0; i < 6; i++)
                    {
                        encoding.AfvWeights![c][i] *= 64;
                    }
                }

                if (!DecodeDctParameters(reader, encoding.DctParameters!))
                {
                    return false;
                }

                if (!DecodeDctParameters(reader, encoding.DctParametersAfv4x4!))
                {
                    return false;
                }

                break;
            }

            case JxlQuantMode.Dct:
            {
                if (!DecodeDctParameters(reader, encoding.DctParameters!))
                {
                    return false;
                }

                break;
            }

            case JxlQuantMode.Raw:
            {
                // Set mode early, to avoid mem-leak.
                encoding.Mode = JxlQuantMode.Raw;

                if (!JxlModularFrameDecoder.DecodeQuantTable(
                    configuration,
                    requiredSizeX,
                    requiredSizeY,
                    reader,
                    encoding,
                    quantizationTableIndex,
                    modularFrameDecoder))
                {
                    return false;
                }

                break;
            }

            default:
                throw new InvalidOperationException("Invalid quant table encoding");
        }

        encoding.Mode = (JxlQuantMode)mode;
        return true;
    }
}
