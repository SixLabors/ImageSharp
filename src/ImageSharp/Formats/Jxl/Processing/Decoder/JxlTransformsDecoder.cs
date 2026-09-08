// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal static class JxlTransformsDecoder
{
    /// <summary>
    /// Lookup for the inverse AFV transform.
    /// </summary>
    private static readonly float[][] AfvBasis4x4 =
    [
        [
            0.25f,
            0.25f,
            0.25f,
            0.25f,
            0.25f,
            0.25f,
            0.25f,
            0.25f,
            0.25f,
            0.25f,
            0.25f,
            0.25f,
            0.25f,
            0.25f,
            0.25f,
            0.25f,
        ],
        [
            0.876902929799142f,
            0.2206518106944235f,
            -0.10140050393753763f,
            -0.1014005039375375f,
            0.2206518106944236f,
            -0.10140050393753777f,
            -0.10140050393753772f,
            -0.10140050393753763f,
            -0.10140050393753758f,
            -0.10140050393753769f,
            -0.1014005039375375f,
            -0.10140050393753768f,
            -0.10140050393753768f,
            -0.10140050393753759f,
            -0.10140050393753763f,
            -0.10140050393753741f,
        ],
        [
            0.0f,
            0.0f,
            0.40670075830260755f,
            0.44444816619734445f,
            0.0f,
            0.0f,
            0.19574399372042936f,
            0.2929100136981264f,
            -0.40670075830260716f,
            -0.19574399372042872f,
            0.0f,
            0.11379074460448091f,
            -0.44444816619734384f,
            -0.29291001369812636f,
            -0.1137907446044814f,
            0.0f,
        ],
        [
            0.0f,
            0.0f,
            -0.21255748058288748f,
            0.3085497062849767f,
            0.0f,
            0.4706702258572536f,
            -0.1621205195722993f,
            0.0f,
            -0.21255748058287047f,
            -0.16212051957228327f,
            -0.47067022585725277f,
            -0.1464291867126764f,
            0.3085497062849487f,
            0.0f,
            -0.14642918671266536f,
            0.4251149611657548f,
        ],
        [
            0.0f,
            -0.7071067811865474f,
            0.0f,
            0.0f,
            0.7071067811865476f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
        ],
        [
            -0.4105377591765233f,
            0.6235485373547691f,
            -0.06435071657946274f,
            -0.06435071657946266f,
            0.6235485373547694f,
            -0.06435071657946284f,
            -0.0643507165794628f,
            -0.06435071657946274f,
            -0.06435071657946272f,
            -0.06435071657946279f,
            -0.06435071657946266f,
            -0.06435071657946277f,
            -0.06435071657946277f,
            -0.06435071657946273f,
            -0.06435071657946274f,
            -0.0643507165794626f,
        ],
        [
            0.0f,
            0.0f,
            -0.4517556589999482f,
            0.15854503551840063f,
            0.0f,
            -0.04038515160822202f,
            0.0074182263792423875f,
            0.39351034269210167f,
            -0.45175565899994635f,
            0.007418226379244351f,
            0.1107416575309343f,
            0.08298163094882051f,
            0.15854503551839705f,
            0.3935103426921022f,
            0.0829816309488214f,
            -0.45175565899994796f,
        ],
        [
            0.0f,
            0.0f,
            -0.304684750724869f,
            0.5112616136591823f,
            0.0f,
            0.0f,
            -0.290480129728998f,
            -0.06578701549142804f,
            0.304684750724884f,
            0.2904801297290076f,
            0.0f,
            -0.23889773523344604f,
            -0.5112616136592012f,
            0.06578701549142545f,
            0.23889773523345467f,
            0.0f,
        ],
        [
            0.0f,
            0.0f,
            0.3017929516615495f,
            0.25792362796341184f,
            0.0f,
            0.16272340142866204f,
            0.09520022653475037f,
            0.0f,
            0.3017929516615503f,
            0.09520022653475055f,
            -0.16272340142866173f,
            -0.35312385449816297f,
            0.25792362796341295f,
            0.0f,
            -0.3531238544981624f,
            -0.6035859033230976f,
        ],
        [
            0.0f,
            0.0f,
            0.40824829046386274f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            -0.4082482904638628f,
            -0.4082482904638635f,
            0.0f,
            0.0f,
            -0.40824829046386296f,
            0.0f,
            0.4082482904638634f,
            0.408248290463863f,
            0.0f,
        ],
        [
            0.0f,
            0.0f,
            0.1747866975480809f,
            0.0812611176717539f,
            0.0f,
            0.0f,
            -0.3675398009862027f,
            -0.307882213957909f,
            -0.17478669754808135f,
            0.3675398009862011f,
            0.0f,
            0.4826689115059883f,
            -0.08126111767175039f,
            0.30788221395790305f,
            -0.48266891150598584f,
            0.0f,
        ],
        [
            0.0f,
            0.0f,
            -0.21105601049335784f,
            0.18567180916109802f,
            0.0f,
            0.0f,
            0.49215859013738733f,
            -0.38525013709251915f,
            0.21105601049335806f,
            -0.49215859013738905f,
            0.0f,
            0.17419412659916217f,
            -0.18567180916109904f,
            0.3852501370925211f,
            -0.1741941265991621f,
            0.0f,
        ],
        [
            0.0f,
            0.0f,
            -0.14266084808807264f,
            -0.3416446842253372f,
            0.0f,
            0.7367497537172237f,
            0.24627107722075148f,
            -0.08574019035519306f,
            -0.14266084808807344f,
            0.24627107722075137f,
            0.14883399227113567f,
            -0.04768680350229251f,
            -0.3416446842253373f,
            -0.08574019035519267f,
            -0.047686803502292804f,
            -0.14266084808807242f,
        ],
        [
            0.0f,
            0.0f,
            -0.13813540350758585f,
            0.3302282550303788f,
            0.0f,
            0.08755115000587084f,
            -0.07946706605909573f,
            -0.4613374887461511f,
            -0.13813540350758294f,
            -0.07946706605910261f,
            0.49724647109535086f,
            0.12538059448563663f,
            0.3302282550303805f,
            -0.4613374887461554f,
            0.12538059448564315f,
            -0.13813540350758452f,
        ],
        [
            0.0f,
            0.0f,
            -0.17437602599651067f,
            0.0702790691196284f,
            0.0f,
            -0.2921026642334881f,
            0.3623817333531167f,
            0.0f,
            -0.1743760259965108f,
            0.36238173335311646f,
            0.29210266423348785f,
            -0.4326608024727445f,
            0.07027906911962818f,
            0.0f,
            -0.4326608024727457f,
            0.34875205199302267f,
        ],
        [
            0.0f,
            0.0f,
            0.11354987314994337f,
            -0.07417504595810355f,
            0.0f,
            0.19402893032594343f,
            -0.435190496523228f,
            0.21918684838857466f,
            0.11354987314994257f,
            -0.4351904965232251f,
            0.5550443808910661f,
            -0.25468277124066463f,
            -0.07417504595810233f,
            0.2191868483885728f,
            -0.25468277124066413f,
            0.1135498731499429f,
        ],
    ];

    public static void ReinterpretingDct(
        int dctRows,
        int dctCols,
        int lfRows,
        int lfCols,
        int rows,
        int cols,
        Span<float> input,
        int inputStride,
        Span<float> output,
        int outputStride,
        Span<float> block,
        Span<float> scratchSpace)
    {
        JxlDct.ComputeScaledDct(rows, cols, new JxlDctSource(input, inputStride), block, scratchSpace);

        if (rows < cols)
        {
            ReadOnlySpan<float> sp1 = JxlDctScales.GetResampleScales(rows, dctRows);
            ReadOnlySpan<float> sp2 = JxlDctScales.GetResampleScales(cols, dctCols);

            for (int y = 0; y < lfRows; y++)
            {
                for (int x = 0; x < lfCols; x++)
                {
                    output[(y * outputStride) + x] = block[(y * cols) + x] * sp1[y] * sp2[x];
                }
            }
        }
        else
        {
            ReadOnlySpan<float> sp2 = JxlDctScales.GetResampleScales(rows, dctRows);
            ReadOnlySpan<float> sp1 = JxlDctScales.GetResampleScales(cols, dctCols);

            for (int y = 0; y < lfCols; y++)
            {
                for (int x = 0; x < lfRows; x++)
                {
                    output[(y * outputStride) + x] = block[(y * rows) + x] * sp1[y] * sp2[y];
                }
            }
        }
    }

    public static void Idct2TopBlock(int s, Span<float> block, int strideOut, Span<float> output)
    {
        Span<float> temp = stackalloc float[JxlFrameDimensions.DctBlockSize];
        int num2x2 = s >> 1;

        for (int y = 0; y < num2x2; y++)
        {
            int y2 = y << 1;
            int y2Plus1 = y2 | 1; // y2 is guaranteed to be even due to left shift

            for (int x = 0; x < num2x2; x++)
            {
                int x2 = x << 1;
                int x2Plus1 = x2 | 1; // x2 is guaranteed to be even due to left shift
                int n2x2PlusX = num2x2 + x;

                float c00 = block[(y * JxlFrameDimensions.BlockDimensions) + x];
                float c01 = block[(y * JxlFrameDimensions.BlockDimensions) + n2x2PlusX];
                float c10 = block[((y + num2x2) * JxlFrameDimensions.BlockDimensions) + x];
                float c11 = block[((y + num2x2) * JxlFrameDimensions.BlockDimensions) + n2x2PlusX];

                float r00 = c00 + c01 + c10 + c11;
                float r01 = c00 + c01 - c10 - c11;
                float r10 = c00 - c01 + c10 - c11;
                float r11 = c00 - c01 - c10 + c11;

                temp[(y2 * JxlFrameDimensions.BlockDimensions) + x2] = r00;
                temp[(y2 * JxlFrameDimensions.BlockDimensions) + x2Plus1] = r01;
                temp[(y2Plus1 * JxlFrameDimensions.BlockDimensions) + x2] = r10;
                temp[(y2Plus1 * JxlFrameDimensions.BlockDimensions) + x2Plus1] = r11;
            }
        }

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                output[(y * strideOut) + x] = temp[(y * JxlFrameDimensions.BlockDimensions) + x];
            }
        }
    }

    public static void AfvIdct4x4(Span<float> coeffs, Span<float> pixels)
    {
        if (!Vector.IsHardwareAccelerated || Vector<float>.Count > 16)
        {
            // Vector<float>.Count can be > 16 on some CPUs,
            // e.g. some ARM SVE2 processors can reach 2048 bits
            for (int i = 0; i < 16; i++)
            {
                float pixel = 0f;

                for (int j = 0; j < 16; j++)
                {
                    float cf = coeffs[j];
                    float basis = AfvBasis4x4[j][i];
                    pixel = (cf * basis) + pixel;
                }

                pixels[i] = pixel;
            }
        }
        else
        {
            for (int i = 0; i < 16; i += Vector<float>.Count)
            {
                Vector<float> pixel = Vector<float>.Zero;

                for (int j = 0; j < 16; j++)
                {
                    Vector<float> cf = Vector.Create(coeffs[j]);
                    Vector<float> basis = Vector.Create<float>(AfvBasis4x4[j].AsSpan(i));
                    pixel = (cf * basis) + pixel;
                }

                pixel.CopyTo(pixels[i..]);
            }
        }
    }

    public static void AfvTransformToPixels(int afvKind, Span<float> coefficients, Span<float> pixels, int pixelsStride)
    {
        Span<float> scratchSpace = stackalloc float[4 * 8 * 4]; // 128 floats, 512 bytes

        // This is an equivalent of Math.DivRem(afvKind, 2)
        int afvX = afvKind & 1;
        int afvY = afvKind >> 1;

        InlineArray3<float> dcs = default;
        float block00 = coefficients[0];
        float block01 = coefficients[1];
        float block10 = coefficients[8];

        dcs[0] = (block00 + block10 + block01) * 4.0f;
        dcs[1] = block00 + block10 - block01;

        dcs[2] = block00 - block10;
        Span<float> coeff = stackalloc float[4 * 4]; // No need to zero-initialize
        coeff[0] = dcs[0];

        // TODO: unrolling this would lead to big performance benefits
        for (int iy = 0; iy < 4; iy++)
        {
            // Variables so we don't repeat multiplication over and over again
            int iy4 = iy * 4; // iy multiplied by 4
            int iy28 = iy * 2 * 8; // iy multiplied by 2 multiplied by 8

            for (int ix = 0; ix < 4; ix++)
            {
                if (ix == 0 && iy == 0)
                {
                    continue;
                }

                coeff[iy4 + ix] = coefficients[iy28 + (ix * 2)];
            }
        }

        Span<float> block = stackalloc float[4 * 8]; // Don't zero-init
        AfvIdct4x4(coeff, block);

        int afvX4 = afvX * 4;

        for (int iy = 0; iy < 4; iy++)
        {
            // Variables so we don't do repeated arithmetic
            int iyAfvY4 = iy + (afvY * 4);
            int iyAfvY4Stride = iyAfvY4 * pixelsStride;
            int yOffset = afvY == 1 ? 3 - iy : iy;
            int yOffsetMul4 = yOffset * 4;

            for (int ix = 0; ix < 4; ix++)
            {
                pixels[iyAfvY4Stride + afvX4 + ix] = block[yOffsetMul4 + (afvX == 1 ? 3 - ix : ix)];
            }
        }

        // IDCT4x4 in (odd, even) positions.
        block[0] = dcs[1];
        for (int iy = 0; iy < 4; iy++)
        {
            int iy4 = iy * 4;
            int iy28 = iy * 2 * 8;

            for (int ix = 0; ix < 4; ix++)
            {
                if (ix == 0 && iy == 0)
                {
                    continue;
                }

                block[iy4 + ix] = coefficients[iy28 + (ix * 2) + 1];
            }
        }

        JxlDct.ComputeScaledInverseDct(
            4,
            4,
            block,
            new JxlDctOutput(pixels[((afvY * 4 * pixelsStride) + (afvX == 1 ? 0 : 4))..], pixelsStride),
            scratchSpace);

        block[0] = dcs[2];

        for (int iy = 0; iy < 4; iy++)
        {
            int iy8 = iy * 8;

            for (int ix = 0; ix < 8; ix++)
            {
                if (ix == 0 && iy == 0)
                {
                    continue;
                }

                block[iy8 + ix] = coefficients[((1 + (iy * 2)) * 8) + ix];
            }
        }

        JxlDct.ComputeScaledInverseDct(
            4,
            8,
            block,
            new JxlDctOutput(pixels[((afvY == 1 ? 0 : 4) * pixelsStride)..], pixelsStride),
            scratchSpace);
    }

    public static void TransformToPixels(JxlAcStrategyType strategy, Span<float> coefficients, Span<float> pixels, int pixelsStride, Span<float> scratchSpace)
    {
        switch (strategy)
        {
            case JxlAcStrategyType.IDENTITY:
            {
                // Identity matrix-based transform
                InlineArray4<float> dcs = default;

                float block00 = coefficients[0];
                float block01 = coefficients[1];
                float block10 = coefficients[8];
                float block11 = coefficients[9];

                dcs[0] = block00 + block01 + block10 + block11;
                dcs[1] = block00 + block01 - block10 - block11;
                dcs[2] = block00 - block01 + block10 - block11;
                dcs[3] = block00 - block01 - block10 + block11;

                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        float blockDc = dcs[(y * 2) + x];
                        float residualSum = 0;

                        for (int iy = 0; iy < 4; iy++)
                        {
                            for (int ix = 0; ix < 4; ix++)
                            {
                                if (ix == 0 && iy == 0)
                                {
                                    continue;
                                }

                                residualSum += coefficients[((y + (iy * 2)) * 8) + x + (ix * 2)];
                            }
                        }

                        pixels[(((4 * y) + 1) * pixelsStride) + (4 * x) + 1] = blockDc - (residualSum * (1.0f / 16));

                        for (int iy = 0; iy < 4; iy++)
                        {
                            for (int ix = 0; ix < 4; ix++)
                            {
                                if (ix == 1 && iy == 1)
                                {
                                    continue;
                                }

                                pixels[(((y * 4) + iy) * pixelsStride) + (x * 4) + ix] =
                                    coefficients[((y + (iy * 2)) * 8) + x + (ix * 2)] +
                                    pixels[(((4 * y) + 1) * pixelsStride) + (4 * x) + 1];
                            }
                        }

                        pixels[(y * 4 * pixelsStride) + (x * 4)] =
                            coefficients[((y + 2) * 8) + x + 2] +
                            pixels[(((4 * y) + 1) * pixelsStride) + (4 * x) + 1];
                    }
                }

                break;
            }

            case JxlAcStrategyType.DCT8X4:
            {
                // Discrete Cosine Transform on an 8x4 block
                InlineArray2<float> dcs = default;

                float block0 = coefficients[0];
                float block1 = coefficients[8];

                dcs[0] = block0 + block1;
                dcs[1] = block0 - block1;

                Span<float> block = stackalloc float[4 * 8];

                for (int x = 0; x < 2; x++)
                {
                    block[0] = dcs[x];

                    for (int iy = 0; iy < 4; iy++)
                    {
                        for (int ix = 0; ix < 8; ix++)
                        {
                            if (ix == 0 && iy == 0)
                            {
                                continue;
                            }

                            block[(iy * 8) + ix] = coefficients[((x + (iy * 2)) * 8) + ix];
                        }
                    }

                    JxlDct.ComputeScaledInverseDct(
                        8,
                        4,
                        block,
                        new JxlDctOutput(pixels[(x * 4)..], pixelsStride),
                        scratchSpace);
                }

                break;
            }

            case JxlAcStrategyType.DCT4X8:
            {
                // Discrete Cosine Transform on an 4x8 block
                InlineArray2<float> dcs = default;

                float block0 = coefficients[0];
                float block1 = coefficients[8];

                dcs[0] = block0 + block1;
                dcs[1] = block0 - block1;

                Span<float> block = stackalloc float[4 * 8];

                for (int y = 0; y < 2; y++)
                {
                    block[0] = dcs[y];

                    for (int iy = 0; iy < 4; iy++)
                    {
                        for (int ix = 0; ix < 8; ix++)
                        {
                            if (ix == 0 && iy == 0)
                            {
                                continue;
                            }

                            block[(iy * 8) + ix] = coefficients[((y + (iy * 2)) * 8) + ix];
                        }
                    }

                    JxlDct.ComputeScaledInverseDct(
                        4,
                        8,
                        block,
                        new JxlDctOutput(pixels[(y * 4 * pixelsStride)..], pixelsStride),
                        scratchSpace);
                }

                break;
            }

            case JxlAcStrategyType.DCT4X4:
            {
                InlineArray4<float> dcs = default;

                float block00 = coefficients[0];
                float block01 = coefficients[1];
                float block10 = coefficients[8];
                float block11 = coefficients[9];

                dcs[0] = block00 + block01 + block10 + block11;
                dcs[1] = block00 + block01 - block10 - block11;
                dcs[2] = block00 - block01 + block10 - block11;
                dcs[3] = block00 - block01 - block10 + block11;

                Span<float> block = stackalloc float[4 * 4];

                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        block[0] = dcs[(y * 2) + x];

                        for (int iy = 0; iy < 4; iy++)
                        {
                            for (int ix = 0; ix < 4; ix++)
                            {
                                if (ix == 0 && iy == 0)
                                {
                                    continue;
                                }

                                block[(iy * 4) + ix] = coefficients[((y + (iy * 2)) * 8) + x + (ix * 2)];
                            }
                        }

                        JxlDct.ComputeScaledInverseDct(
                            4,
                            4,
                            block,
                            new JxlDctOutput(pixels[((y * 4 * pixelsStride) + (x * 4))..], pixelsStride),
                            scratchSpace);
                    }
                }

                break;
            }

            case JxlAcStrategyType.DCT2X2:
            {
                Span<float> coeffs = stackalloc float[JxlFrameDimensions.DctBlockSize];
                coefficients[..JxlFrameDimensions.DctBlockSize].CopyTo(coeffs);

                Idct2TopBlock(2, coeffs, JxlFrameDimensions.BlockDimensions, coeffs);
                Idct2TopBlock(4, coeffs, JxlFrameDimensions.BlockDimensions, coeffs);
                Idct2TopBlock(8, coeffs, JxlFrameDimensions.BlockDimensions, coeffs);

                for (int y = 0; y < JxlFrameDimensions.BlockDimensions; y++)
                {
                    for (int x = 0; x < JxlFrameDimensions.BlockDimensions; x++)
                    {
                        pixels[(y * pixelsStride) + x] = coeffs[(y * JxlFrameDimensions.BlockDimensions) + x];
                    }
                }

                break;
            }

            case JxlAcStrategyType.DCT16X16:
            {
                JxlDct.ComputeScaledInverseDct(16, 16, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT16X8:
            {
                JxlDct.ComputeScaledInverseDct(16, 8, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT8X16:
            {
                JxlDct.ComputeScaledInverseDct(8, 16, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X8:
            {
                JxlDct.ComputeScaledInverseDct(32, 8, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT8X32:
            {
                JxlDct.ComputeScaledInverseDct(8, 32, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X16:
            {
                JxlDct.ComputeScaledInverseDct(32, 16, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT16X32:
            {
                JxlDct.ComputeScaledInverseDct(16, 32, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X32:
            {
                JxlDct.ComputeScaledInverseDct(32, 32, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT:
            {
                JxlDct.ComputeScaledInverseDct(8, 8, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.AFV0:
            {
                AfvTransformToPixels(0, coefficients, pixels, pixelsStride);
                break;
            }

            case JxlAcStrategyType.AFV1:
            {
                AfvTransformToPixels(1, coefficients, pixels, pixelsStride);
                break;
            }

            case JxlAcStrategyType.AFV2:
            {
                AfvTransformToPixels(2, coefficients, pixels, pixelsStride);
                break;
            }

            case JxlAcStrategyType.AFV3:
            {
                AfvTransformToPixels(3, coefficients, pixels, pixelsStride);
                break;
            }

            case JxlAcStrategyType.DCT64X32:
            {
                JxlDct.ComputeScaledInverseDct(64, 32, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X64:
            {
                JxlDct.ComputeScaledInverseDct(32, 64, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT64X64:
            {
                JxlDct.ComputeScaledInverseDct(64, 64, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT128X64:
            {
                JxlDct.ComputeScaledInverseDct(128, 64, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT64X128:
            {
                JxlDct.ComputeScaledInverseDct(64, 128, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT128X128:
            {
                JxlDct.ComputeScaledInverseDct(128, 128, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT256X128:
            {
                JxlDct.ComputeScaledInverseDct(256, 128, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT128X256:
            {
                JxlDct.ComputeScaledInverseDct(128, 256, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT256X256:
            {
                JxlDct.ComputeScaledInverseDct(256, 256, coefficients, new JxlDctOutput(pixels, pixelsStride), scratchSpace);
                break;
            }
        }
    }

    public static void LowestFrequenciesFromDc(JxlAcStrategyType strategy, Span<float> dc, int dcStride, Span<float> llf, Span<float> scratch)
    {
        Span<float> warmBlock = stackalloc float[4 * 4];
        Span<float> warmScratchSpace = stackalloc float[4 * 4 * 4];

        switch (strategy)
        {
            case JxlAcStrategyType.DCT16X8:
            {
                ReinterpretingDct(
                    2 * JxlFrameDimensions.BlockDimensions,
                    JxlFrameDimensions.BlockDimensions,
                    2,
                    1,
                    2,
                    1,
                    dc,
                    dcStride,
                    llf,
                    2 * JxlFrameDimensions.BlockDimensions,
                    warmBlock,
                    warmScratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT8X16:
            {
                ReinterpretingDct(
                    JxlFrameDimensions.BlockDimensions,
                    2 * JxlFrameDimensions.BlockDimensions,
                    1,
                    2,
                    1,
                    2,
                    dc,
                    dcStride,
                    llf,
                    2 * JxlFrameDimensions.BlockDimensions,
                    warmBlock,
                    warmScratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT16X16:
            {
                ReinterpretingDct(
                    2 * JxlFrameDimensions.BlockDimensions,
                    2 * JxlFrameDimensions.BlockDimensions,
                    2,
                    2,
                    2,
                    2,
                    dc,
                    dcStride,
                    llf,
                    2 * JxlFrameDimensions.BlockDimensions,
                    warmBlock,
                    warmScratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X8:
            {
                ReinterpretingDct(
                    4 * JxlFrameDimensions.BlockDimensions,
                    JxlFrameDimensions.BlockDimensions,
                    4,
                    1,
                    4,
                    1,
                    dc,
                    dcStride,
                    llf,
                    4 * JxlFrameDimensions.BlockDimensions,
                    warmBlock,
                    warmScratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT8X32:
            {
                ReinterpretingDct(
                    JxlFrameDimensions.BlockDimensions,
                    4 * JxlFrameDimensions.BlockDimensions,
                    1,
                    4,
                    1,
                    4,
                    dc,
                    dcStride,
                    llf,
                    4 * JxlFrameDimensions.BlockDimensions,
                    warmBlock,
                    warmScratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X16:
            {
                ReinterpretingDct(
                    4 * JxlFrameDimensions.BlockDimensions,
                    2 * JxlFrameDimensions.BlockDimensions,
                    4,
                    2,
                    4,
                    2,
                    dc,
                    dcStride,
                    llf,
                    4 * JxlFrameDimensions.BlockDimensions,
                    warmBlock,
                    warmScratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT16X32:
            {
                ReinterpretingDct(
                    2 * JxlFrameDimensions.BlockDimensions,
                    4 * JxlFrameDimensions.BlockDimensions,
                    2,
                    4,
                    2,
                    4,
                    dc,
                    dcStride,
                    llf,
                    4 * JxlFrameDimensions.BlockDimensions,
                    warmBlock,
                    warmScratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X32:
            {
                ReinterpretingDct(
                    4 * JxlFrameDimensions.BlockDimensions,
                    4 * JxlFrameDimensions.BlockDimensions,
                    4,
                    4,
                    4,
                    4,
                    dc,
                    dcStride,
                    llf,
                    4 * JxlFrameDimensions.BlockDimensions,
                    warmBlock,
                    warmScratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT64X32:
            {
                ReinterpretingDct(
                    8 * JxlFrameDimensions.BlockDimensions,
                    4 * JxlFrameDimensions.BlockDimensions,
                    8,
                    4,
                    8,
                    4,
                    dc,
                    dcStride,
                    llf,
                    8 * JxlFrameDimensions.BlockDimensions,
                    scratch,
                    scratch[(8 * 4)..]);
                break;
            }

            case JxlAcStrategyType.DCT32X64:
            {
                ReinterpretingDct(
                    4 * JxlFrameDimensions.BlockDimensions,
                    8 * JxlFrameDimensions.BlockDimensions,
                    4,
                    8,
                    4,
                    8,
                    dc,
                    dcStride,
                    llf,
                    8 * JxlFrameDimensions.BlockDimensions,
                    scratch,
                    scratch[(4 * 8)..]);
                break;
            }

            case JxlAcStrategyType.DCT64X64:
            {
                ReinterpretingDct(
                    8 * JxlFrameDimensions.BlockDimensions,
                    8 * JxlFrameDimensions.BlockDimensions,
                    8,
                    8,
                    8,
                    8,
                    dc,
                    dcStride,
                    llf,
                    8 * JxlFrameDimensions.BlockDimensions,
                    scratch,
                    scratch[(8 * 8)..]);
                break;
            }

            case JxlAcStrategyType.DCT128X64:
            {
                ReinterpretingDct(
                    16 * JxlFrameDimensions.BlockDimensions,
                    8 * JxlFrameDimensions.BlockDimensions,
                    16,
                    8,
                    16,
                    8,
                    dc,
                    dcStride,
                    llf,
                    16 * JxlFrameDimensions.BlockDimensions,
                    scratch,
                    scratch[(16 * 8)..]);
                break;
            }

            case JxlAcStrategyType.DCT64X128:
            {
                ReinterpretingDct(
                    8 * JxlFrameDimensions.BlockDimensions,
                    16 * JxlFrameDimensions.BlockDimensions,
                    8,
                    16,
                    8,
                    16,
                    dc,
                    dcStride,
                    llf,
                    16 * JxlFrameDimensions.BlockDimensions,
                    scratch,
                    scratch[(8 * 16)..]);
                break;
            }

            case JxlAcStrategyType.DCT128X128:
            {
                ReinterpretingDct(
                    16 * JxlFrameDimensions.BlockDimensions,
                    16 * JxlFrameDimensions.BlockDimensions,
                    16,
                    16,
                    16,
                    16,
                    dc,
                    dcStride,
                    llf,
                    16 * JxlFrameDimensions.BlockDimensions,
                    scratch,
                    scratch[(16 * 16)..]);
                break;
            }

            case JxlAcStrategyType.DCT256X128:
            {
                ReinterpretingDct(
                    32 * JxlFrameDimensions.BlockDimensions,
                    16 * JxlFrameDimensions.BlockDimensions,
                    32,
                    16,
                    32,
                    16,
                    dc,
                    dcStride,
                    llf,
                    32 * JxlFrameDimensions.BlockDimensions,
                    scratch,
                    scratch[(32 * 16)..]);
                break;
            }

            case JxlAcStrategyType.DCT128X256:
            {
                ReinterpretingDct(
                    16 * JxlFrameDimensions.BlockDimensions,
                    32 * JxlFrameDimensions.BlockDimensions,
                    16,
                    32,
                    16,
                    32,
                    dc,
                    dcStride,
                    llf,
                    32 * JxlFrameDimensions.BlockDimensions,
                    scratch,
                    scratch[(16 * 32)..]);
                break;
            }

            case JxlAcStrategyType.DCT256X256:
            {
                ReinterpretingDct(
                    32 * JxlFrameDimensions.BlockDimensions,
                    32 * JxlFrameDimensions.BlockDimensions,
                    32,
                    32,
                    32,
                    32,
                    dc,
                    dcStride,
                    llf,
                    32 * JxlFrameDimensions.BlockDimensions,
                    scratch,
                    scratch[(32 * 32)..]);
                break;
            }

            case JxlAcStrategyType.DCT:
            case JxlAcStrategyType.DCT2X2:
            case JxlAcStrategyType.DCT4X4:
            case JxlAcStrategyType.DCT4X8:
            case JxlAcStrategyType.DCT8X4:
            case JxlAcStrategyType.AFV0:
            case JxlAcStrategyType.AFV1:
            case JxlAcStrategyType.AFV2:
            case JxlAcStrategyType.AFV3:
            case JxlAcStrategyType.IDENTITY:
                llf[0] = dc[0];
                break;
        }
    }
}
