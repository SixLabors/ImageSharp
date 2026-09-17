// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal static class JxlTransformsEncoder
{
    private const int MaxBlocks = 32;

    /// <summary>
    /// Lookup for forward AFV DCT 4x4.
    /// </summary>
    private static readonly float[][] AfvBasisTranspose4x4 =
    [
        [
            0.2500000000000000f,
            0.8769029297991420f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            -0.4105377591765233f,
            0.0000000000000000f,
              0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
        ],
        [
            0.2500000000000000f,
            0.2206518106944235f,
            0.0000000000000000f,
            0.0000000000000000f,
            -0.7071067811865474f,
            0.6235485373547691f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
        ],
        [
            0.2500000000000000f,
            -0.1014005039375376f,
            0.4067007583026075f,
            -0.2125574805828875f,
            0.0000000000000000f,
            -0.0643507165794627f,
            -0.4517556589999482f,
            -0.3046847507248690f,
            0.3017929516615495f,
            0.4082482904638627f,
            0.1747866975480809f,
            -0.2110560104933578f,
            -0.1426608480880726f,
            -0.1381354035075859f,
            -0.1743760259965107f,
            0.1135498731499434f,
        ],
        [
            0.2500000000000000f,
            -0.1014005039375375f,
            0.4444481661973445f,
            0.3085497062849767f,
            0.0000000000000000f,
            -0.0643507165794627f,
            0.1585450355184006f,
            0.5112616136591823f,
            0.2579236279634118f,
            0.0000000000000000f,
            0.0812611176717539f,
            0.1856718091610980f,
            -0.3416446842253372f,
            0.3302282550303788f,
            0.0702790691196284f,
            -0.0741750459581035f,
        ],
        [
            0.2500000000000000f,
            0.2206518106944236f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.7071067811865476f,
            0.6235485373547694f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
        ],
        [
            0.2500000000000000f,
            -0.1014005039375378f,
            0.0000000000000000f,
            0.4706702258572536f,
            0.0000000000000000f,
            -0.0643507165794628f,
            -0.0403851516082220f,
            0.0000000000000000f,
            0.1627234014286620f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.7367497537172237f,
            0.0875511500058708f,
            -0.2921026642334881f,
            0.1940289303259434f,
        ],
        [
            0.2500000000000000f,
            -0.1014005039375377f,
            0.1957439937204294f,
            -0.1621205195722993f,
            0.0000000000000000f,
            -0.0643507165794628f,
            0.0074182263792424f,
            -0.2904801297289980f,
            0.0952002265347504f,
            0.0000000000000000f,
            -0.3675398009862027f,
            0.4921585901373873f,
            0.2462710772207515f,
            -0.0794670660590957f,
            0.3623817333531167f,
            -0.4351904965232280f,
        ],
        [
            0.2500000000000000f,
            -0.1014005039375376f,
            0.2929100136981264f,
            0.0000000000000000f,
            0.0000000000000000f,
            -0.0643507165794627f,
            0.3935103426921017f,
            -0.0657870154914280f,
            0.0000000000000000f,
            -0.4082482904638628f,
            -0.3078822139579090f,
            -0.3852501370925192f,
            -0.0857401903551931f,
            -0.4613374887461511f,
            0.0000000000000000f,
            0.2191868483885747f,
        ],
        [
            0.2500000000000000f,
            -0.1014005039375376f,
            -0.4067007583026072f,
            -0.2125574805828705f,
            0.0000000000000000f,
            -0.0643507165794627f,
            -0.4517556589999464f,
            0.3046847507248840f,
            0.3017929516615503f,
            -0.4082482904638635f,
            -0.1747866975480813f,
            0.2110560104933581f,
            -0.1426608480880734f,
            -0.1381354035075829f,
            -0.1743760259965108f,
            0.1135498731499426f,
        ],
        [
            0.2500000000000000f,
            -0.1014005039375377f,
            -0.1957439937204287f,
            -0.1621205195722833f,
            0.0000000000000000f,
            -0.0643507165794628f,
            0.0074182263792444f,
            0.2904801297290076f,
            0.0952002265347505f,
            0.0000000000000000f,
            0.3675398009862011f,
            -0.4921585901373891f,
            0.2462710772207514f,
            -0.0794670660591026f,
            0.3623817333531165f,
            -0.4351904965232251f,
        ],
        [
            0.2500000000000000f,
            -0.1014005039375375f,
            0.0000000000000000f,
            -0.4706702258572528f,
            0.0000000000000000f,
            -0.0643507165794627f,
            0.1107416575309343f,
            0.0000000000000000f,
            -0.1627234014286617f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.1488339922711357f,
            0.4972464710953509f,
            0.2921026642334879f,
            0.5550443808910661f,
        ],
        [
            0.2500000000000000f,
            -0.1014005039375377f,
            0.1137907446044809f,
            -0.1464291867126764f,
            0.0000000000000000f,
            -0.0643507165794628f,
            0.0829816309488205f,
            -0.2388977352334460f,
            -0.3531238544981630f,
            -0.4082482904638630f,
            0.4826689115059883f,
            0.1741941265991622f,
            -0.0476868035022925f,
            0.1253805944856366f,
            -0.4326608024727445f,
            -0.2546827712406646f,
        ],
        [
            0.2500000000000000f,
            -0.1014005039375377f,
            -0.4444481661973438f,
            0.3085497062849487f,
            0.0000000000000000f,
            -0.0643507165794628f,
            0.1585450355183970f,
            -0.5112616136592012f,
            0.2579236279634129f,
            0.0000000000000000f,
            -0.0812611176717504f,
            -0.1856718091610990f,
            -0.3416446842253373f,
            0.3302282550303805f,
            0.0702790691196282f,
            -0.0741750459581023f,
        ],
        [
            0.2500000000000000f,
            -0.1014005039375376f,
            -0.2929100136981264f,
            0.0000000000000000f,
            0.0000000000000000f,
            -0.0643507165794627f,
            0.3935103426921022f,
            0.0657870154914254f,
            0.0000000000000000f,
            0.4082482904638634f,
            0.3078822139579031f,
            0.3852501370925211f,
            -0.0857401903551927f,
            -0.4613374887461554f,
            0.0000000000000000f,
            0.2191868483885728f,
        ],
        [
            0.2500000000000000f,
            -0.1014005039375376f,
            -0.1137907446044814f,
            -0.1464291867126654f,
            0.0000000000000000f,
            -0.0643507165794627f,
            0.0829816309488214f,
            0.2388977352334547f,
            -0.3531238544981624f,
            0.4082482904638630f,
            -0.4826689115059858f,
            -0.1741941265991621f,
            -0.0476868035022928f,
            0.1253805944856431f,
            -0.4326608024727457f,
            -0.2546827712406641f,
        ],
        [
            0.2500000000000000f,
            -0.1014005039375374f,
            0.0000000000000000f,
            0.4251149611657548f,
            0.0000000000000000f,
            -0.0643507165794626f,
            -0.4517556589999480f,
            0.0000000000000000f,
            -0.6035859033230976f,
            0.0000000000000000f,
            0.0000000000000000f,
            0.0000000000000000f,
            -0.1426608480880724f,
            -0.1381354035075845f,
            0.3487520519930227f,
            0.1135498731499429f,
      ],
    ];

    public static void ReinterpretingInverseDct(
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
        Span<float> scratch)
    {
        if (rows < cols)
        {
            ReadOnlySpan<float> sp1 = JxlDctScales.GetResampleScales(dctRows, rows);
            ReadOnlySpan<float> sp2 = JxlDctScales.GetResampleScales(dctCols, cols);

            for (int y = 0; y < lfRows; y++)
            {
                int yCols = y * cols;
                int yInputStride = y * inputStride;

                for (int x = 0; x < lfCols; x++)
                {
                    scratch[yCols + x] = input[yInputStride + x] * sp1[y] * sp2[y];
                }
            }
        }
        else
        {
            ReadOnlySpan<float> sp2 = JxlDctScales.GetResampleScales(dctRows, rows);
            ReadOnlySpan<float> sp1 = JxlDctScales.GetResampleScales(dctCols, cols);

            for (int y = 0; y < lfCols; y++)
            {
                int yRows = y * rows;
                int yInputStride = y * inputStride;

                for (int x = 0; x < lfRows; x++)
                {
                    scratch[yRows + x] = input[yInputStride + x] * sp1[y] * sp2[y];
                }
            }
        }

        Span<float> scratchSpace = scratch[(MaxBlocks * MaxBlocks)..];
        JxlDct.ComputeScaledInverseDct(rows, cols, scratch, new JxlDctOutput(output, outputStride), scratchSpace);
    }

    public static void Dct2TopBlock(int s, Span<float> block, int stride, Span<float> output)
    {
        Span<float> temp = stackalloc float[JxlFrameDimensions.DctBlockSize];
        int num2x2 = s >>> 1; // Logical right shift (no sign-extend)

        for (int y = 0; y < num2x2; y++)
        {
            // TODO: convert repeated arithmetic (e.g. y * DctBlockSize)
            // into variables?
            for (int x = 0; x < num2x2; x++)
            {
                float c00 = block[(y * 2 * stride) + (x * 2)];
                float c01 = block[(y * 2 * stride) + (x * 2) + 1];
                float c10 = block[(((y * 2) + 1) * stride) + (x * 2)];
                float c11 = block[(((y * 2) + 1) * stride) + (x * 2) + 1];

                float r00 = (c00 + c01) + (c10 + c11);
                float r01 = (c00 + c01) - (c10 - c11);
                float r10 = (c00 - c01) + (c10 - c11);
                float r11 = (c00 - c01) - (c10 + c11);

                r00 *= 0.25f;
                r01 *= 0.25f;
                r10 *= 0.25f;
                r11 *= 0.25f;

                temp[(y * JxlFrameDimensions.DctBlockSize) + x] = r00;
                temp[(y * JxlFrameDimensions.DctBlockSize) + num2x2 + x] = r01;
                temp[((y + num2x2) * JxlFrameDimensions.DctBlockSize) + x] = r10;
                temp[((y + num2x2) * JxlFrameDimensions.DctBlockSize) + num2x2 + x] = r11;
            }
        }

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                output[(y * JxlFrameDimensions.BlockDimensions) + x] = temp[(y * JxlFrameDimensions.BlockDimensions) + x];
            }
        }
    }

    public static void AfvDct4x4(Span<float> coeffs, Span<float> pixels)
    {
        if (!Vector.IsHardwareAccelerated || Vector<float>.Count > 16)
        {
            // Vector<float>.Count can be > 16 on some CPUs,
            // e.g. some ARM SVE2 processors can reach 2048 bits
            for (int i = 0; i < 16; i++)
            {
                float scalar = 0f;

                for (int j = 0; j < 16; j++)
                {
                    float px = pixels[j];
                    float basis = AfvBasisTranspose4x4[j][i];
                    scalar = (px * basis) + scalar;
                }

                coeffs[i] = scalar;
            }
        }
        else
        {
            for (int i = 0; i < 16; i += Vector<float>.Count)
            {
                Vector<float> scalar = Vector<float>.Zero;

                for (int j = 0; j < 16; j++)
                {
                    Vector<float> px = Vector.Create(pixels[j]);
                    Vector<float> basis = Vector.Create<float>(AfvBasisTranspose4x4[j].AsSpan(i));
                    scalar = (px * basis) + scalar;
                }

                scalar.CopyTo(pixels[i..]);
            }
        }
    }

    private static void AfvTransformFromPixels(int afvKind, Span<float> pixels, int pixelsStride, Span<float> coefficients)
    {
        Span<float> scratchSpace = stackalloc float[4 * 8 * 5];

        // Equivalent of Math.DivRem(afvKind, 2)
        int afvX = afvKind & 1;
        int afvY = afvKind >>> 1;

        Span<float> block = stackalloc float[4 * 8];
        block.Clear();

        for (int iy = 0; iy < 4; iy++)
        {
            for (int ix = 0; ix < 4; ix++)
            {
                block[((afvY == 1 ? 3 - iy : iy) * 4) + (afvX == 1 ? 3 - ix : ix)] = pixels[((iy + (4 * afvY)) * pixelsStride) + ix + (4 * afvX)];
            }
        }

        Span<float> coeff = stackalloc float[4 * 4];
        AfvDct4x4(block, coeff);

        for (int iy = 0; iy < 4; iy++)
        {
            for (int ix = 0; ix < 4; ix++)
            {
                coefficients[(iy * 2 * 8) + (ix * 2)] = coeff[(iy * 4) + ix];
            }
        }

        JxlDct.ComputeScaledDct(
            4,
            4,
            new JxlDctSource(pixels[((afvY * 4 * pixelsStride) + (afvX == 1 ? 0 : 4))..], pixelsStride),
            block,
            scratchSpace);

        for (int iy = 0; iy < 4; iy++)
        {
            for (int ix = 0; ix < 8; ix++)
            {
                coefficients[(iy * 2 * 8) + (ix * 2) + 1] = block[(iy * 4) + ix];
            }
        }

        JxlDct.ComputeScaledDct(
            4,
            8,
            new JxlDctSource(pixels[((afvY == 1 ? 0 : 4) * pixelsStride)..], pixelsStride),
            block,
            scratchSpace);

        for (int iy = 0; iy < 4; iy++)
        {
            for (int ix = 0; ix < 8; ix++)
            {
                coefficients[((1 + (iy * 2)) * 8) + ix] = block[(iy * 8) + ix];
            }
        }

        float block00 = coefficients[0] * 0.25f;
        float block01 = coefficients[1];
        float block10 = coefficients[8];
        coefficients[0] = (block00 + block01 + (2 * block10)) * 0.25f;
        coefficients[1] = (block00 - block01) * 0.5f;
        coefficients[8] = (block00 + block01 - (2 * block10)) * 0.25f;
    }

    public static void TransformFromPixels(JxlAcStrategyType strategy, Span<float> pixels, int pixelsStride, Span<float> coefficients, Span<float> scratchSpace)
    {
        switch (strategy)
        {
            case JxlAcStrategyType.IDENTITY:
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        float blockDc = 0;

                        for (int iy = 0; iy < 4; iy++)
                        {
                            for (int ix = 0; ix < 4; ix++)
                            {
                                blockDc += pixels[(((y * 4) + iy) * pixelsStride) + (x * 4) + ix];
                            }
                        }

                        blockDc *= 1.0f / 16;

                        for (int iy = 0; iy < 4; iy++)
                        {
                            for (int ix = 0; ix < 4; ix++)
                            {
                                if (ix == 1 && iy == 1)
                                {
                                    continue;
                                }

                                coefficients[((y + (iy * 2)) * 8) + x + (ix * 2)] =
                                    pixels[(((y * 4) + iy) * pixelsStride) + (x * 4) + ix] -
                                    pixels[(((y * 4) + 1) * pixelsStride) + (x * 4) + 1];
                            }
                        }

                        coefficients[((y + 2) * 8) + x + 2] = coefficients[(y * 8) + x];
                        coefficients[(y * 8) + x] = blockDc;
                    }
                }

                float block00 = coefficients[0];
                float block01 = coefficients[1];
                float block10 = coefficients[8];
                float block11 = coefficients[9];

                coefficients[0] = ((block00 + block01) + (block10 + block11)) * 0.25f;
                coefficients[1] = ((block00 + block01) - (block10 - block11)) * 0.25f;
                coefficients[8] = ((block00 - block01) + (block10 - block11)) * 0.25f;
                coefficients[9] = ((block00 - block01) - (block10 + block11)) * 0.25f;

                break;
            }

            case JxlAcStrategyType.DCT8X4:
            {
                Span<float> block = stackalloc float[4 * 8];

                for (int x = 0; x < 2; x++)
                {
                    JxlDct.ComputeScaledDct(
                        8,
                        4,
                        new JxlDctSource(pixels[(x * 4)..], pixelsStride),
                        block,
                        scratchSpace);

                    for (int iy = 0; iy < 4; iy++)
                    {
                        for (int ix = 0; ix < 8; ix++)
                        {
                            // Store transposed.
                            coefficients[((x + (iy * 2)) * 8) + ix] = block[(iy * 8) + ix];
                        }
                    }
                }

                float block0 = coefficients[0];
                float block1 = coefficients[8];

                coefficients[0] = (block0 + block1) * 0.5f;
                coefficients[8] = (block0 - block1) * 0.5f;

                break;
            }

            case JxlAcStrategyType.DCT4X8:
            {
                Span<float> block = stackalloc float[4 * 8];

                for (int y = 0; y < 2; y++)
                {
                    JxlDct.ComputeScaledDct(
                        4,
                        8,
                        new JxlDctSource(pixels[(y * 4 * pixelsStride)..], pixelsStride),
                        block,
                        scratchSpace);

                    for (int iy = 0; iy < 4; iy++)
                    {
                        for (int ix = 0; ix < 8; ix++)
                        {
                            coefficients[((y + (iy * 2)) * 8) + ix] = block[(iy * 8) + ix];
                        }
                    }
                }

                float block0 = coefficients[0];
                float block1 = coefficients[8];

                coefficients[0] = (block0 + block1) * 0.5f;
                coefficients[8] = (block0 - block1) * 0.5f;

                break;
            }

            case JxlAcStrategyType.DCT4X4:
            {
                Span<float> block = stackalloc float[4 * 4];

                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        JxlDct.ComputeScaledDct(
                            4,
                            4,
                            new JxlDctSource(pixels[((y * 4 * pixelsStride) + (x * 4))..], pixelsStride),
                            block,
                            scratchSpace);

                        for (int iy = 0; iy < 4; iy++)
                        {
                            for (int ix = 0; ix < 4; ix++)
                            {
                                coefficients[((y + (iy * 2)) * 8) + x + (ix * 2)] = block[(iy * 4) + ix];
                            }
                        }
                    }
                }

                float block00 = coefficients[0];
                float block01 = coefficients[1];
                float block10 = coefficients[8];
                float block11 = coefficients[9];

                coefficients[0] = ((block00 + block01) + (block10 + block11)) * 0.25f;
                coefficients[1] = ((block00 + block01) - (block10 - block11)) * 0.25f;
                coefficients[8] = ((block00 - block01) + (block10 - block11)) * 0.25f;
                coefficients[9] = ((block00 - block01) - (block10 + block11)) * 0.25f;

                break;
            }

            case JxlAcStrategyType.DCT2X2:
            {
                Dct2TopBlock(8, pixels, pixelsStride, coefficients);
                Dct2TopBlock(4, coefficients, JxlFrameDimensions.BlockDimensions, coefficients);
                Dct2TopBlock(2, coefficients, JxlFrameDimensions.BlockDimensions, coefficients);

                break;
            }

            case JxlAcStrategyType.DCT16X16:
            {
                JxlDct.ComputeScaledDct(
                    16,
                    16,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT16X8:
            {
                JxlDct.ComputeScaledDct(
                    16,
                    8,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT8X16:
            {
                JxlDct.ComputeScaledDct(
                    8,
                    16,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X8:
            {
                JxlDct.ComputeScaledDct(
                    32,
                    8,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT8X32:
            {
                JxlDct.ComputeScaledDct(
                    8,
                    32,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X16:
            {
                JxlDct.ComputeScaledDct(
                    32,
                    16,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT16X32:
            {
                JxlDct.ComputeScaledDct(
                    16,
                    32,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X32:
            {
                JxlDct.ComputeScaledDct(
                    32,
                    32,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT:
            {
                JxlDct.ComputeScaledDct(
                    8,
                    8,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.AFV0:
            {
                AfvTransformFromPixels(0, pixels, pixelsStride, coefficients);
                break;
            }

            case JxlAcStrategyType.AFV1:
            {
                AfvTransformFromPixels(1, pixels, pixelsStride, coefficients);
                break;
            }

            case JxlAcStrategyType.AFV2:
            {
                AfvTransformFromPixels(2, pixels, pixelsStride, coefficients);
                break;
            }

            case JxlAcStrategyType.AFV3:
            {
                AfvTransformFromPixels(3, pixels, pixelsStride, coefficients);
                break;
            }

            case JxlAcStrategyType.DCT64X64:
            {
                JxlDct.ComputeScaledDct(
                    64,
                    64,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT64X32:
            {
                JxlDct.ComputeScaledDct(
                    64,
                    32,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X64:
            {
                JxlDct.ComputeScaledDct(
                    32,
                    64,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT128X128:
            {
                JxlDct.ComputeScaledDct(
                    128,
                    128,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT128X64:
            {
                JxlDct.ComputeScaledDct(
                    128,
                    64,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT64X128:
            {
                JxlDct.ComputeScaledDct(
                    64,
                    128,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT256X256:
            {
                JxlDct.ComputeScaledDct(
                    256,
                    256,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT256X128:
            {
                JxlDct.ComputeScaledDct(
                    256,
                    128,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT128X256:
            {
                JxlDct.ComputeScaledDct(
                    128,
                    256,
                    new JxlDctSource(pixels, pixelsStride),
                    coefficients,
                    scratchSpace);
                break;
            }
        }
    }

    public static void DcFromLowestFrequencies(JxlAcStrategyType strategy, Span<float> block, Span<float> dc, int dcStride, Span<float> scratchSpace)
    {
        switch (strategy)
        {
            case JxlAcStrategyType.DCT16X8:
            {
                ReinterpretingInverseDct(
                    2 * JxlFrameDimensions.BlockDimensions,
                    JxlFrameDimensions.BlockDimensions,
                    2,
                    1,
                    2,
                    1,
                    block,
                    2 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT8X16:
            {
                ReinterpretingInverseDct(
                    JxlFrameDimensions.BlockDimensions,
                    2 * JxlFrameDimensions.BlockDimensions,
                    1,
                    2,
                    1,
                    2,
                    block,
                    2 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT16X16:
            {
                ReinterpretingInverseDct(
                    2 * JxlFrameDimensions.BlockDimensions,
                    2 * JxlFrameDimensions.BlockDimensions,
                    2,
                    2,
                    2,
                    2,
                    block,
                    2 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X8:
            {
                ReinterpretingInverseDct(
                    4 * JxlFrameDimensions.BlockDimensions,
                    JxlFrameDimensions.BlockDimensions,
                    4,
                    1,
                    4,
                    1,
                    block,
                    4 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT8X32:
            {
                ReinterpretingInverseDct(
                    JxlFrameDimensions.BlockDimensions,
                    4 * JxlFrameDimensions.BlockDimensions,
                    1,
                    4,
                    1,
                    4,
                    block,
                    4 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X16:
            {
                ReinterpretingInverseDct(
                    4 * JxlFrameDimensions.BlockDimensions,
                    2 * JxlFrameDimensions.BlockDimensions,
                    4,
                    2,
                    4,
                    2,
                    block,
                    4 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT16X32:
            {
                ReinterpretingInverseDct(
                    2 * JxlFrameDimensions.BlockDimensions,
                    4 * JxlFrameDimensions.BlockDimensions,
                    2,
                    4,
                    2,
                    4,
                    block,
                    4 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X32:
            {
                ReinterpretingInverseDct(
                    4 * JxlFrameDimensions.BlockDimensions,
                    4 * JxlFrameDimensions.BlockDimensions,
                    4,
                    4,
                    4,
                    4,
                    block,
                    4 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT64X32:
            {
                ReinterpretingInverseDct(
                    8 * JxlFrameDimensions.BlockDimensions,
                    4 * JxlFrameDimensions.BlockDimensions,
                    8,
                    4,
                    8,
                    4,
                    block,
                    8 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT32X64:
            {
                ReinterpretingInverseDct(
                    4 * JxlFrameDimensions.BlockDimensions,
                    8 * JxlFrameDimensions.BlockDimensions,
                    4,
                    8,
                    4,
                    8,
                    block,
                    8 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT64X64:
            {
                ReinterpretingInverseDct(
                    8 * JxlFrameDimensions.BlockDimensions,
                    8 * JxlFrameDimensions.BlockDimensions,
                    8,
                    8,
                    8,
                    8,
                    block,
                    8 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT128X64:
            {
                ReinterpretingInverseDct(
                    16 * JxlFrameDimensions.BlockDimensions,
                    8 * JxlFrameDimensions.BlockDimensions,
                    16,
                    8,
                    16,
                    8,
                    block,
                    16 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT64X128:
            {
                ReinterpretingInverseDct(
                    8 * JxlFrameDimensions.BlockDimensions,
                    16 * JxlFrameDimensions.BlockDimensions,
                    8,
                    16,
                    8,
                    16,
                    block,
                    16 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT128X128:
            {
                ReinterpretingInverseDct(
                    16 * JxlFrameDimensions.BlockDimensions,
                    16 * JxlFrameDimensions.BlockDimensions,
                    16,
                    16,
                    16,
                    16,
                    block,
                    16 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT256X128:
            {
                ReinterpretingInverseDct(
                    32 * JxlFrameDimensions.BlockDimensions,
                    16 * JxlFrameDimensions.BlockDimensions,
                    32,
                    16,
                    32,
                    16,
                    block,
                    32 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT128X256:
            {
                ReinterpretingInverseDct(
                    16 * JxlFrameDimensions.BlockDimensions,
                    32 * JxlFrameDimensions.BlockDimensions,
                    16,
                    32,
                    16,
                    32,
                    block,
                    32 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
                break;
            }

            case JxlAcStrategyType.DCT256X256:
            {
                ReinterpretingInverseDct(
                    32 * JxlFrameDimensions.BlockDimensions,
                    32 * JxlFrameDimensions.BlockDimensions,
                    32,
                    32,
                    32,
                    32,
                    block,
                    32 * JxlFrameDimensions.BlockDimensions,
                    dc,
                    dcStride,
                    scratchSpace);
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
                dc[0] = block[0];
                break;
        }
    }
}
