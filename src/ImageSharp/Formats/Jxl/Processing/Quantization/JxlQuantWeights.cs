// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

internal static class JxlQuantWeights
{
    public const int MaxQuantTableSize = JxlAcStrategy.MaximumCoefficientArea;

    public const int NumPredefinedTables = 1;

    public const int CeilLog2NumPredefinedTables = 0;

    public const int Log2NumQuantModes = 3;

    private const float AlmostZero = 1e-8f;

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
            7));

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

    private static Vector128<float> Gather(ReadOnlySpan<float> data, Vector128<int> indices)
    {
        Vector128<float> result = Vector128<float>.Zero;

        for (int i = 0; i < 4; i++)
        {
            result = result.WithElement(i, data[indices[i]]);
        }

        return result;
    }

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

    public static float Interpolate(float pos, float max, Span<float> array, int len)
    {
        float scaledPos = pos * (len - 1) / max;
        int idx = (int)scaledPos;
        float a = array[idx];
        float b = array[idx + 1];
        return a * MathF.Pow(b / a, scaledPos - idx);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Mult(float v) => v > 0f
        ? 1.0f + v
        : 1.0f / (1.0f - v);

    public static Vector128<float> InterpolateVec(Vector128<float> scaledPos, ReadOnlySpan<float> array)
    {
        Vector128<int> idx = Vector128.ConvertToInt32(scaledPos);
        Vector128<float> frac = scaledPos - Vector128.ConvertToSingle(idx);

        Vector128<float> a = Gather(array, idx);
        Vector128<float> b = Gather(array[1..], idx);

        return a * JxlSimdUtils.FastPowf(b / a, frac);
    }

    public static bool GetQuantWeights(
        int rows,
        int cols,
        float[][] distanceBands,
        int numBands,
        Span<float> output)
    {
        Span<float> bands = stackalloc float[JxlQuantizerConstants.MaxDistanceBands];

        for (int c = 0; c < 3; c++)
        {
            bands[0] = distanceBands[c][0];

            if (bands[0] < AlmostZero)
            {
                return false;
            }

            for (int i = 1; i < numBands; i++)
            {
                bands[i] = bands[i - 1] * distanceBands[c][i];

                if (bands[i] < AlmostZero)
                {
                    return false;
                }
            }

            float scale = (numBands - 1) / (JxlDctScales.Sqrt2 + 1e-6f);
            float rcpCol = scale / (cols - 1);
            float rcpRow = scale / (rows - 1);

            for (int y = 0; y < rows; y++)
            {
                float dy = y * rcpRow;
                float dy2 = dy * dy;

                for (int x = 0; x < cols; x += 4)
                {
                    Vector128<float> dx =
                        Vector128.Create((float)x, x + 1, x + 2, x + 3)
                        * Vector128.Create(rcpCol);

                    Vector128<float> scaledDistance =
                        Vector128.Sqrt(
                            (dx * dx) + Vector128.Create(dy2));

                    Vector128<float> weight =
                        numBands == 1
                            ? Vector128.Create(bands[0])
                            : InterpolateVec(scaledDistance, bands);

                    weight.CopyTo(output[((c * cols * rows) + (y * cols) + x)..]);
                }
            }
        }

        return true;
    }

    public static bool ComputeQuantTable(JxlQuantizerEncoding encoding, Span<float> table, Span<float> inverseTable, int tableNum, JxlQuantTable kind, ref int pos)
    {
        const int n = JxlFrameDimensions.BlockDimensions;

        int quantSizeTable = (int)kind;
        int wrows = 8 * JxlDequantMatrices.RequiredSizeX[quantSizeTable];
        int wcols = 8 * JxlDequantMatrices.RequiredSizeY[quantSizeTable];
        int num = wrows * wcols;

        Span<float> weights = stackalloc float[3 * num];

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

                            float interpolatedVal = Interpolate(AfvFrequencies[(y * 4) + x] - lo, hi, bands, 4);

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
        }

        int prevPos = pos;

        // Don't zero-init
        Span<float> invVal = stackalloc float[64];
        Span<float> val = stackalloc float[64];

        for (int i = 0; i < num * 3; i += 64)
        {
            weights.Slice(i, 64).CopyTo(invVal);

            // TODO: there's an unlikely check right here in
            // reference:
            //    if (JXL_UNLIKELY(!AllFalse(d, Ge(inv_val, Set(d, 1.0f / kAlmostZero))) ||
            //             !AllFalse(d, Lt(inv_val, Set(d, kAlmostZero)))))
            //    {
            //      throw new InvalidOperationException("Invalid quantization table");
            //    }
            // should we trade performance for an unlikely check?
            val.Fill(1.0f);
            TensorPrimitives.Divide(val, invVal, val);

            val.CopyTo(table.Slice(pos + i, 64));
            invVal.CopyTo(inverseTable.Slice(pos + i, 64));
        }

        pos += 3 * num;

        int xs = JxlDequantMatrices.RequiredSizeX[quantSizeTable];
        int ys = JxlDequantMatrices.RequiredSizeY[quantSizeTable];

        JxlForwardCoefficientOrder.CoefficientLayout(ref ys, ref xs);

        for (int c = 0; c < 3; c++)
        {
            for (int y = 0; y < ys; y++)
            {
                for (int x = 0; x < xs; x++)
                {
                    inverseTable[prevPos + (c * ys * xs * JxlFrameDimensions.DctBlockSize) + (y * JxlFrameDimensions.BlockDimensions * xs) + x] = 0;
                }
            }
        }

        return true;
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

    public static bool Decode(Configuration configuration, JxlBitReader br, JxlQuantizerEncoding encoding, int requiredSizeX, int requiredSizeY, int idx, JxlModularFrameDecoder modularFrameDecoder)
    {
        int requiredSize = requiredSizeX * requiredSizeY;

        requiredSizeX *= JxlFrameDimensions.BlockDimensions;
        requiredSizeY *= JxlFrameDimensions.BlockDimensions;

        int mode = (int)br.ReadBits32(JxlQuantizerConstants.Log2NumQuantModes);

        switch ((JxlQuantMode)mode)
        {
            case JxlQuantMode.Library:
            {
                encoding.Predefined = (byte)br.ReadBits32(CeilLog2NumPredefinedTables);

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
                        if (!JxlF16Coder.Read(br, ref encoding.IdWeights![c][i]))
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
                        if (!JxlF16Coder.Read(br, ref encoding.Dct2Weights![c][i]))
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
                    if (!JxlF16Coder.Read(br, ref encoding.Dct4x8Multipliers![c]))
                    {
                        return false;
                    }

                    if (Math.Abs(encoding.Dct4x8Multipliers[c]) < AlmostZero)
                    {
                        throw new InvalidOperationException("DCT4X8 multiplier is too small");
                    }
                }

                if (!DecodeDctParameters(br, encoding.DctParameters!))
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
                        if (!JxlF16Coder.Read(br, ref encoding.Dct4Multipliers![c][i]))
                        {
                            return false;
                        }

                        if (Math.Abs(encoding.Dct4Multipliers[c][i]) < AlmostZero)
                        {
                            throw new InvalidOperationException("DCT4 multiplier is too small");
                        }
                    }
                }

                if (!DecodeDctParameters(br, encoding.DctParameters!))
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
                        if (!JxlF16Coder.Read(br, ref encoding.AfvWeights![c][i]))
                        {
                            return false;
                        }
                    }

                    for (int i = 0; i < 6; i++)
                    {
                        encoding.AfvWeights![c][i] *= 64;
                    }
                }

                if (!DecodeDctParameters(br, encoding.DctParameters!))
                {
                    return false;
                }

                if (!DecodeDctParameters(br, encoding.DctParametersAfv4x4!))
                {
                    return false;
                }

                break;
            }

            case JxlQuantMode.Dct:
            {
                if (!DecodeDctParameters(br, encoding.DctParameters!))
                {
                    return false;
                }

                break;
            }

            case JxlQuantMode.Raw:
            {
                // Set mode early, to avoid mem-leak.
                encoding.Mode = JxlQuantMode.Raw;

                if (!JxlModularFrameDecoder.DecodeQuantTable(configuration, requiredSizeX, requiredSizeY, br, encoding, idx, modularFrameDecoder))
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
