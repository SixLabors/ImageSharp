// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg.Utils
{
    internal static partial class ReferenceImplementations
    {
        /// <summary>
        /// True accurate FDCT/IDCT implementations. We should test everything against them!
        /// Based on:
        /// https://github.com/keithw/mympeg2enc/blob/master/idct.c#L222
        /// Claiming:
        /// /* reference idct taken from "ieeetest.c"
        ///  * Written by Tom Lane (tgl@cs.cmu.edu).
        ///  * Released to public domain 11/22/93.
        ///  */
        /// </summary>
        internal static class AccurateDCT
        {
            private static readonly double[,] CosLut = InitCosLut();

            public static Block8x8 TransformIDCT(ref Block8x8 block)
            {
                Block8x8F temp = block.AsFloatBlock();
                Block8x8F res0 = TransformIDCT(ref temp);
                return res0.RoundAsInt16Block();
            }

            public static void TransformIDCTInplace(Span<int> span)
            {
                var temp = default(Block8x8);
                temp.LoadFrom(span);
                Block8x8 result = TransformIDCT(ref temp);
                result.CopyTo(span);
            }

            public static Block8x8 TransformFDCT(ref Block8x8 block)
            {
                Block8x8F temp = block.AsFloatBlock();
                Block8x8F res0 = TransformFDCT(ref temp);
                return res0.RoundAsInt16Block();
            }

            public static void TransformFDCTInplace(Span<int> span)
            {
                var temp = default(Block8x8);
                temp.LoadFrom(span);
                Block8x8 result = TransformFDCT(ref temp);
                result.CopyTo(span);
            }

            public static Block8x8F TransformIDCT(ref Block8x8F block)
            {
                int x, y, u, v;
                double tmp, tmp2;
                Block8x8F res = default;

                for (y = 0; y < 8; y++)
                {
                    for (x = 0; x < 8; x++)
                    {
                        tmp = 0.0;
                        for (v = 0; v < 8; v++)
                        {
                            tmp2 = 0.0;
                            for (u = 0; u < 8; u++)
                            {
                                tmp2 += block[(v * 8) + u] * CosLut[x, u];
                            }

                            tmp += CosLut[y, v] * tmp2;
                        }

                        res[(y * 8) + x] = (float)tmp;
                    }
                }

                return res;
            }

            public static Block8x8F TransformFDCT(ref Block8x8F block)
            {
                int x, y, u, v;
                double tmp, tmp2;
                Block8x8F res = default;

                for (v = 0; v < 8; v++)
                {
                    for (u = 0; u < 8; u++)
                    {
                        tmp = 0.0;
                        for (y = 0; y < 8; y++)
                        {
                            tmp2 = 0.0;
                            for (x = 0; x < 8; x++)
                            {
                                tmp2 += block[(y * 8) + x] * CosLut[x, u];
                            }

                            tmp += CosLut[y, v] * tmp2;
                        }

                        res[(v * 8) + u] = (float)tmp;
                    }
                }

                return res;
            }

            private static double[,] InitCosLut()
            {
                double[,] coslu = new double[8, 8];
                int a, b;
                double tmp;

                for (a = 0; a < 8; a++)
                {
                    for (b = 0; b < 8; b++)
                    {
                        tmp = Math.Cos((a + a + 1) * b * (3.14159265358979323846 / 16.0));
                        if (b == 0)
                        {
                            tmp /= Math.Sqrt(2.0);
                        }

                        coslu[a, b] = tmp * 0.5;
                    }
                }

                return coslu;
            }
        }
    }
}
