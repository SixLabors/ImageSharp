// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Performs the inverse Descrete Cosine Transform on each frame component.
    /// </summary>
    internal static class PdfJsIDCT
    {
        private const int DctCos1 = 4017;     // cos(pi/16)
        private const int DctSin1 = 799;      // sin(pi/16)
        private const int DctCos3 = 3406;     // cos(3*pi/16)
        private const int DctSin3 = 2276;     // sin(3*pi/16)
        private const int DctCos6 = 1567;     // cos(6*pi/16)
        private const int DctSin6 = 3784;     // sin(6*pi/16)
        private const int DctSqrt2 = 5793;    // sqrt(2)
        private const int DctSqrt1D2 = 2896;  // sqrt(2) / 2
        private const int MaxJSample = 255;
        private const int CenterJSample = 128;
        private const int RangeCenter = (MaxJSample * 2) + 2;

        // First segment of range limit table: limit[x] = 0 for x < 0
        // allow negative subscripts of simple table
        private const int TableOffset = 2 * (MaxJSample + 1);
        private const int LimitOffset = TableOffset - (RangeCenter - CenterJSample);

        // Each IDCT routine is responsible for range-limiting its results and
        // converting them to unsigned form (0..MaxJSample).  The raw outputs could
        // be quite far out of range if the input data is corrupt, so a bulletproof
        // range-limiting step is required.  We use a mask-and-table-lookup method
        // to do the combined operations quickly, assuming that MaxJSample+1
        // is a power of 2.
        private const int RangeMask = (MaxJSample * 4) + 3; // 2 bits wider than legal samples

        private static readonly byte[] Limit = new byte[5 * (MaxJSample + 1)];

        static PdfJsIDCT()
        {
            // Main part of range limit table: limit[x] = x
            int i;
            for (i = 0; i <= MaxJSample; i++)
            {
                Limit[TableOffset + i] = (byte)i;
            }

            // End of range limit table: Limit[x] = MaxJSample for x > MaxJSample
            for (; i < 3 * (MaxJSample + 1); i++)
            {
                Limit[TableOffset + i] = MaxJSample;
            }
        }

        /// <summary>
        /// A port of Poppler's IDCT method which in turn is taken from:
        /// Christoph Loeffler, Adriaan Ligtenberg, George S. Moschytz,
        /// 'Practical Fast 1-D DCT Algorithms with 11 Multiplications',
        /// IEEE Intl. Conf. on Acoustics, Speech &amp; Signal Processing, 1989, 988-991.
        /// </summary>
        /// <param name="component">The frame component</param>
        /// <param name="blockBufferOffset">The block buffer offset</param>
        /// <param name="computationBuffer">The computational buffer for holding temp values</param>
        /// <param name="quantizationTable">The quantization table</param>
        public static void QuantizeAndInverse(PdfJsFrameComponent component, int blockBufferOffset, ref short computationBuffer, ref short quantizationTable)
        {
            ref short blockDataRef = ref MemoryMarshal.GetReference(component.BlockData.Slice(blockBufferOffset));
            int v0, v1, v2, v3, v4, v5, v6, v7;
            int p0, p1, p2, p3, p4, p5, p6, p7;
            int t;

            // inverse DCT on rows
            for (int row = 0; row < 64; row += 8)
            {
                // gather block data
                p0 = Unsafe.Add(ref blockDataRef, row);
                p1 = Unsafe.Add(ref blockDataRef, row + 1);
                p2 = Unsafe.Add(ref blockDataRef, row + 2);
                p3 = Unsafe.Add(ref blockDataRef, row + 3);
                p4 = Unsafe.Add(ref blockDataRef, row + 4);
                p5 = Unsafe.Add(ref blockDataRef, row + 5);
                p6 = Unsafe.Add(ref blockDataRef, row + 6);
                p7 = Unsafe.Add(ref blockDataRef, row + 7);

                // dequant p0
                p0 *= Unsafe.Add(ref quantizationTable, row);

                // check for all-zero AC coefficients
                if ((p1 | p2 | p3 | p4 | p5 | p6 | p7) == 0)
                {
                    t = ((DctSqrt2 * p0) + 512) >> 10;
                    short st = (short)t;
                    Unsafe.Add(ref computationBuffer, row) = st;
                    Unsafe.Add(ref computationBuffer, row + 1) = st;
                    Unsafe.Add(ref computationBuffer, row + 2) = st;
                    Unsafe.Add(ref computationBuffer, row + 3) = st;
                    Unsafe.Add(ref computationBuffer, row + 4) = st;
                    Unsafe.Add(ref computationBuffer, row + 5) = st;
                    Unsafe.Add(ref computationBuffer, row + 6) = st;
                    Unsafe.Add(ref computationBuffer, row + 7) = st;
                    continue;
                }

                // dequant p1 ... p7
                p1 *= Unsafe.Add(ref quantizationTable, row + 1);
                p2 *= Unsafe.Add(ref quantizationTable, row + 2);
                p3 *= Unsafe.Add(ref quantizationTable, row + 3);
                p4 *= Unsafe.Add(ref quantizationTable, row + 4);
                p5 *= Unsafe.Add(ref quantizationTable, row + 5);
                p6 *= Unsafe.Add(ref quantizationTable, row + 6);
                p7 *= Unsafe.Add(ref quantizationTable, row + 7);

                // stage 4
                v0 = ((DctSqrt2 * p0) + 128) >> 8;
                v1 = ((DctSqrt2 * p4) + 128) >> 8;
                v2 = p2;
                v3 = p6;
                v4 = ((DctSqrt1D2 * (p1 - p7)) + 128) >> 8;
                v7 = ((DctSqrt1D2 * (p1 + p7)) + 128) >> 8;
                v5 = p3 << 4;
                v6 = p5 << 4;

                // stage 3
                v0 = (v0 + v1 + 1) >> 1;
                v1 = v0 - v1;
                t = ((v2 * DctSin6) + (v3 * DctCos6) + 128) >> 8;
                v2 = ((v2 * DctCos6) - (v3 * DctSin6) + 128) >> 8;
                v3 = t;
                v4 = (v4 + v6 + 1) >> 1;
                v6 = v4 - v6;
                v7 = (v7 + v5 + 1) >> 1;
                v5 = v7 - v5;

                // stage 2
                v0 = (v0 + v3 + 1) >> 1;
                v3 = v0 - v3;
                v1 = (v1 + v2 + 1) >> 1;
                v2 = v1 - v2;
                t = ((v4 * DctSin3) + (v7 * DctCos3) + 2048) >> 12;
                v4 = ((v4 * DctCos3) - (v7 * DctSin3) + 2048) >> 12;
                v7 = t;
                t = ((v5 * DctSin1) + (v6 * DctCos1) + 2048) >> 12;
                v5 = ((v5 * DctCos1) - (v6 * DctSin1) + 2048) >> 12;
                v6 = t;

                // stage 1
                Unsafe.Add(ref computationBuffer, row) = (short)(v0 + v7);
                Unsafe.Add(ref computationBuffer, row + 7) = (short)(v0 - v7);
                Unsafe.Add(ref computationBuffer, row + 1) = (short)(v1 + v6);
                Unsafe.Add(ref computationBuffer, row + 6) = (short)(v1 - v6);
                Unsafe.Add(ref computationBuffer, row + 2) = (short)(v2 + v5);
                Unsafe.Add(ref computationBuffer, row + 5) = (short)(v2 - v5);
                Unsafe.Add(ref computationBuffer, row + 3) = (short)(v3 + v4);
                Unsafe.Add(ref computationBuffer, row + 4) = (short)(v3 - v4);
            }

            // inverse DCT on columns
            for (int col = 0; col < 8; ++col)
            {
                p0 = Unsafe.Add(ref computationBuffer, col);
                p1 = Unsafe.Add(ref computationBuffer, col + 8);
                p2 = Unsafe.Add(ref computationBuffer, col + 16);
                p3 = Unsafe.Add(ref computationBuffer, col + 24);
                p4 = Unsafe.Add(ref computationBuffer, col + 32);
                p5 = Unsafe.Add(ref computationBuffer, col + 40);
                p6 = Unsafe.Add(ref computationBuffer, col + 48);
                p7 = Unsafe.Add(ref computationBuffer, col + 56);

                // check for all-zero AC coefficients
                if ((p1 | p2 | p3 | p4 | p5 | p6 | p7) == 0)
                {
                    t = ((DctSqrt2 * p0) + 8192) >> 14;

                    // convert to 8 bit
                    t = (t < -2040) ? 0 : (t >= 2024) ? MaxJSample : (t + 2056) >> 4;
                    short st = (short)t;

                    Unsafe.Add(ref blockDataRef, col) = st;
                    Unsafe.Add(ref blockDataRef, col + 8) = st;
                    Unsafe.Add(ref blockDataRef, col + 16) = st;
                    Unsafe.Add(ref blockDataRef, col + 24) = st;
                    Unsafe.Add(ref blockDataRef, col + 32) = st;
                    Unsafe.Add(ref blockDataRef, col + 40) = st;
                    Unsafe.Add(ref blockDataRef, col + 48) = st;
                    Unsafe.Add(ref blockDataRef, col + 56) = st;
                    continue;
                }

                // stage 4
                v0 = ((DctSqrt2 * p0) + 2048) >> 12;
                v1 = ((DctSqrt2 * p4) + 2048) >> 12;
                v2 = p2;
                v3 = p6;
                v4 = ((DctSqrt1D2 * (p1 - p7)) + 2048) >> 12;
                v7 = ((DctSqrt1D2 * (p1 + p7)) + 2048) >> 12;
                v5 = p3;
                v6 = p5;

                // stage 3
                // Shift v0 by 128.5 << 5 here, so we don't need to shift p0...p7 when
                // converting to UInt8 range later.
                v0 = ((v0 + v1 + 1) >> 1) + 4112;
                v1 = v0 - v1;
                t = ((v2 * DctSin6) + (v3 * DctCos6) + 2048) >> 12;
                v2 = ((v2 * DctCos6) - (v3 * DctSin6) + 2048) >> 12;
                v3 = t;
                v4 = (v4 + v6 + 1) >> 1;
                v6 = v4 - v6;
                v7 = (v7 + v5 + 1) >> 1;
                v5 = v7 - v5;

                // stage 2
                v0 = (v0 + v3 + 1) >> 1;
                v3 = v0 - v3;
                v1 = (v1 + v2 + 1) >> 1;
                v2 = v1 - v2;
                t = ((v4 * DctSin3) + (v7 * DctCos3) + 2048) >> 12;
                v4 = ((v4 * DctCos3) - (v7 * DctSin3) + 2048) >> 12;
                v7 = t;
                t = ((v5 * DctSin1) + (v6 * DctCos1) + 2048) >> 12;
                v5 = ((v5 * DctCos1) - (v6 * DctSin1) + 2048) >> 12;
                v6 = t;

                // stage 1
                p0 = v0 + v7;
                p7 = v0 - v7;
                p1 = v1 + v6;
                p6 = v1 - v6;
                p2 = v2 + v5;
                p5 = v2 - v5;
                p3 = v3 + v4;
                p4 = v3 - v4;

                // convert to 8-bit integers
                p0 = (p0 < 16) ? 0 : (p0 >= 4080) ? MaxJSample : p0 >> 4;
                p1 = (p1 < 16) ? 0 : (p1 >= 4080) ? MaxJSample : p1 >> 4;
                p2 = (p2 < 16) ? 0 : (p2 >= 4080) ? MaxJSample : p2 >> 4;
                p3 = (p3 < 16) ? 0 : (p3 >= 4080) ? MaxJSample : p3 >> 4;
                p4 = (p4 < 16) ? 0 : (p4 >= 4080) ? MaxJSample : p4 >> 4;
                p5 = (p5 < 16) ? 0 : (p5 >= 4080) ? MaxJSample : p5 >> 4;
                p6 = (p6 < 16) ? 0 : (p6 >= 4080) ? MaxJSample : p6 >> 4;
                p7 = (p7 < 16) ? 0 : (p7 >= 4080) ? MaxJSample : p7 >> 4;

                // store block data
                Unsafe.Add(ref blockDataRef, col) = (short)p0;
                Unsafe.Add(ref blockDataRef, col + 8) = (short)p1;
                Unsafe.Add(ref blockDataRef, col + 16) = (short)p2;
                Unsafe.Add(ref blockDataRef, col + 24) = (short)p3;
                Unsafe.Add(ref blockDataRef, col + 32) = (short)p4;
                Unsafe.Add(ref blockDataRef, col + 40) = (short)p5;
                Unsafe.Add(ref blockDataRef, col + 48) = (short)p6;
                Unsafe.Add(ref blockDataRef, col + 56) = (short)p7;
            }
        }
    }
}