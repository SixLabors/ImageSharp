﻿namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using ImageSharp.Memory;

    /// <summary>
    /// Performa the invers
    /// </summary>
    internal static class IDCT
    {
        private const int DctCos1 = 4017;   // cos(pi/16)
        private const int DctSin1 = 799;   // sin(pi/16)
        private const int DctCos3 = 3406;   // cos(3*pi/16)
        private const int DctSin3 = 2276;   // sin(3*pi/16)
        private const int DctCos6 = 1567;   // cos(6*pi/16)
        private const int DctSin6 = 3784;   // sin(6*pi/16)
        private const int DctSqrt2 = 5793;   // sqrt(2)
        private const int DctSqrt1D2 = 2896;  // sqrt(2) / 2

#pragma warning disable SA1310 // Field names must not contain underscore
        private const int FIX_1_082392200 = 277;        /* FIX(1.082392200) */
        private const int FIX_1_414213562 = 362;        /* FIX(1.414213562) */
        private const int FIX_1_847759065 = 473;        /* FIX(1.847759065) */
        private const int FIX_2_613125930 = 669;        /* FIX(2.613125930) */
#pragma warning restore SA1310 // Field names must not contain underscore

        private const int ConstBits = 8;
        private const int Pass1Bits = 2; // Factional bits in scale factors
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
        // is a power of 2.  See the comments with prepare_range_limit_table for more info.
        private const int RangeMask = (MaxJSample * 4) + 3; // 2 bits wider than legal samples

        // Precomputed values scaled up by 14 bits
        private static readonly short[] Aanscales =
        {
            16384, 22725, 21407, 19266, 16384, 12873, 8867, 4520, 22725, 31521, 29692, 26722, 22725, 17855,
            12299, 6270, 21407, 29692, 27969, 25172, 21407, 16819, 11585,
            5906, 19266, 26722, 25172, 22654, 19266, 15137, 10426, 5315,
            16384, 22725, 21407, 19266, 16384, 12873, 8867, 4520, 12873,
            17855, 16819, 15137, 12873, 10114, 6967, 3552, 8867, 12299,
            11585, 10426, 8867, 6967, 4799, 2446, 4520, 6270, 5906, 5315,
            4520, 3552, 2446, 1247
        };

        private static readonly byte[] Limit = new byte[5 * (MaxJSample + 1)];

        static IDCT()
        {
            // Main part of range limit table: limit[x] = x
            int i;
            for (i = 0; i <= MaxJSample; i++)
            {
                Limit[TableOffset + i] = (byte)i;
            }

            // End of range limit table: limit[x] = MaxJSample for x > MaxJSample
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
        /// <param name="quantizationTables">The quantization tables</param>
        /// <param name="component">The fram component</param>
        /// <param name="blockBufferOffset">The block buffer offset</param>
        /// <param name="computationBuffer">The computational buffer for holding temp values</param>
        public static void QuantizeAndInverse(QuantizationTables quantizationTables, ref FrameComponent component, int blockBufferOffset, Buffer<short> computationBuffer)
        {
            Span<short> qt = quantizationTables.Tables.GetRowSpan(component.QuantizationIdentifier);
            Span<short> blockData = component.BlockData.Slice(blockBufferOffset);
            Span<short> computationBufferSpan = computationBuffer;
            int v0, v1, v2, v3, v4, v5, v6, v7;
            int p0, p1, p2, p3, p4, p5, p6, p7;
            int t;

            // inverse DCT on rows
            for (int row = 0; row < 64; row += 8)
            {
                // gather block data
                p0 = blockData[row];
                p1 = blockData[row + 1];
                p2 = blockData[row + 2];
                p3 = blockData[row + 3];
                p4 = blockData[row + 4];
                p5 = blockData[row + 5];
                p6 = blockData[row + 6];
                p7 = blockData[row + 7];

                // dequant p0
                p0 *= qt[row];

                // check for all-zero AC coefficients
                if ((p1 | p2 | p3 | p4 | p5 | p6 | p7) == 0)
                {
                    t = ((DctSqrt2 * p0) + 512) >> 10;
                    short st = (short)t;
                    computationBufferSpan[row] = st;
                    computationBufferSpan[row + 1] = st;
                    computationBufferSpan[row + 2] = st;
                    computationBufferSpan[row + 3] = st;
                    computationBufferSpan[row + 4] = st;
                    computationBufferSpan[row + 5] = st;
                    computationBufferSpan[row + 6] = st;
                    computationBufferSpan[row + 7] = st;
                    continue;
                }

                // dequant p1 ... p7
                p1 *= qt[row + 1];
                p2 *= qt[row + 2];
                p3 *= qt[row + 3];
                p4 *= qt[row + 4];
                p5 *= qt[row + 5];
                p6 *= qt[row + 6];
                p7 *= qt[row + 7];

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
                computationBufferSpan[row] = (short)(v0 + v7);
                computationBufferSpan[row + 7] = (short)(v0 - v7);
                computationBufferSpan[row + 1] = (short)(v1 + v6);
                computationBufferSpan[row + 6] = (short)(v1 - v6);
                computationBufferSpan[row + 2] = (short)(v2 + v5);
                computationBufferSpan[row + 5] = (short)(v2 - v5);
                computationBufferSpan[row + 3] = (short)(v3 + v4);
                computationBufferSpan[row + 4] = (short)(v3 - v4);
            }

            // inverse DCT on columns
            for (int col = 0; col < 8; ++col)
            {
                p0 = computationBufferSpan[col];
                p1 = computationBufferSpan[col + 8];
                p2 = computationBufferSpan[col + 16];
                p3 = computationBufferSpan[col + 24];
                p4 = computationBufferSpan[col + 32];
                p5 = computationBufferSpan[col + 40];
                p6 = computationBufferSpan[col + 48];
                p7 = computationBufferSpan[col + 56];

                // check for all-zero AC coefficients
                if ((p1 | p2 | p3 | p4 | p5 | p6 | p7) == 0)
                {
                    t = ((DctSqrt2 * p0) + 8192) >> 14;

                    // convert to 8 bit
                    t = (t < -2040) ? 0 : (t >= 2024) ? MaxJSample : (t + 2056) >> 4;
                    short st = (short)t;

                    blockData[col] = st;
                    blockData[col + 8] = st;
                    blockData[col + 16] = st;
                    blockData[col + 24] = st;
                    blockData[col + 32] = st;
                    blockData[col + 40] = st;
                    blockData[col + 48] = st;
                    blockData[col + 56] = st;
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
                blockData[col] = (short)p0;
                blockData[col + 8] = (short)p1;
                blockData[col + 16] = (short)p2;
                blockData[col + 24] = (short)p3;
                blockData[col + 32] = (short)p4;
                blockData[col + 40] = (short)p5;
                blockData[col + 48] = (short)p6;
                blockData[col + 56] = (short)p7;
            }
        }

        /// <summary>
        /// A port of <see href="https://github.com/libjpeg-turbo/libjpeg-turbo/blob/master/jidctfst.c#L171"/>
        /// A 2-D IDCT can be done by 1-D IDCT on each column followed by 1-D IDCT
        /// on each row(or vice versa, but it's more convenient to emit a row at
        /// a time).  Direct algorithms are also available, but they are much more
        /// complex and seem not to be any faster when reduced to code.
        ///
        /// This implementation is based on Arai, Agui, and Nakajima's algorithm for
        /// scaled DCT.Their original paper (Trans.IEICE E-71(11):1095) is in
        /// Japanese, but the algorithm is described in the Pennebaker &amp; Mitchell
        /// JPEG textbook(see REFERENCES section in file README.ijg).  The following
        /// code is based directly on figure 4-8 in P&amp;M.
        /// While an 8-point DCT cannot be done in less than 11 multiplies, it is
        /// possible to arrange the computation so that many of the multiplies are
        /// simple scalings of the final outputs.These multiplies can then be
        /// folded into the multiplications or divisions by the JPEG quantization
        /// table entries.  The AA&amp;N method leaves only 5 multiplies and 29 adds
        /// to be done in the DCT itself.
        /// The primary disadvantage of this method is that with fixed-point math,
        /// accuracy is lost due to imprecise representation of the scaled
        /// quantization values.The smaller the quantization table entry, the less
        /// precise the scaled value, so this implementation does worse with high -
        /// quality - setting files than with low - quality ones.
        /// </summary>
        /// <param name="quantizationTables">The quantization tables</param>
        /// <param name="component">The fram component</param>
        /// <param name="blockBufferOffset">The block buffer offset</param>
        /// <param name="computationBuffer">The computational buffer for holding temp values</param>
        public static void QuantizeAndInverseAlt(
            QuantizationTables quantizationTables,
            ref FrameComponent component,
            int blockBufferOffset,
            Buffer<short> computationBuffer)
        {
            Span<short> qt = quantizationTables.Tables.GetRowSpan(component.QuantizationIdentifier);
            Span<short> blockData = component.BlockData.Slice(blockBufferOffset);
            Span<short> computationBufferSpan = computationBuffer;

            // For AA&N IDCT method, multiplier are equal to quantization
            // coefficients scaled by scalefactor[row]*scalefactor[col], where
            //   scalefactor[0] = 1
            //   scalefactor[k] = cos(k*PI/16) * sqrt(2)    for k=1..7
            // For integer operation, the multiplier table is to be scaled by 14.
            using (var multiplier = new Buffer<short>(64))
            {
                Span<short> multiplierSpan = multiplier;
                for (int i = 0; i < 64; i++)
                {
                    multiplierSpan[i] = (short)Descale(qt[i] * Aanscales[i], 14 - Pass1Bits);
                }

                int p0, p1, p2, p3, p4, p5, p6, p7;

                int coefBlockIndex = 0;
                int workspaceIndex = 0;
                int quantTableIndex = 0;
                for (int col = 8; col > 0; col--)
                {
                    // Gather block data
                    p0 = blockData[coefBlockIndex];
                    p1 = blockData[coefBlockIndex + 8];
                    p2 = blockData[coefBlockIndex + 16];
                    p3 = blockData[coefBlockIndex + 24];
                    p4 = blockData[coefBlockIndex + 32];
                    p5 = blockData[coefBlockIndex + 40];
                    p6 = blockData[coefBlockIndex + 48];
                    p7 = blockData[coefBlockIndex + 56];

                    int tmp0 = p0 * multiplierSpan[quantTableIndex];

                    // Due to quantization, we will usually find that many of the input
                    // coefficients are zero, especially the AC terms.  We can exploit this
                    // by short-circuiting the IDCT calculation for any column in which all
                    // the AC terms are zero.  In that case each output is equal to the
                    // DC coefficient (with scale factor as needed).
                    // With typical images and quantization tables, half or more of the
                    // column DCT calculations can be simplified this way.
                    if ((p1 | p2 | p3 | p4 | p5 | p6 | p7) == 0)
                    {
                        short dcval = (short)tmp0;

                        computationBufferSpan[workspaceIndex] = dcval;
                        computationBufferSpan[workspaceIndex + 8] = dcval;
                        computationBufferSpan[workspaceIndex + 16] = dcval;
                        computationBufferSpan[workspaceIndex + 24] = dcval;
                        computationBufferSpan[workspaceIndex + 32] = dcval;
                        computationBufferSpan[workspaceIndex + 40] = dcval;
                        computationBufferSpan[workspaceIndex + 48] = dcval;
                        computationBufferSpan[workspaceIndex + 56] = dcval;

                        coefBlockIndex++;
                        quantTableIndex++;
                        workspaceIndex++;
                        continue;
                    }

                    // Even part
                    int tmp1 = p2 * multiplierSpan[quantTableIndex + 16];
                    int tmp2 = p4 * multiplierSpan[quantTableIndex + 32];
                    int tmp3 = p6 * multiplierSpan[quantTableIndex + 48];

                    int tmp10 = tmp0 + tmp2; // Phase 3
                    int tmp11 = tmp0 - tmp2;

                    int tmp13 = tmp1 + tmp3; // Phases 5-3
                    int tmp12 = Multiply(tmp1 - tmp3, FIX_1_414213562) - tmp13; // 2*c4

                    tmp0 = tmp10 + tmp13; // Phase 2
                    tmp3 = tmp10 - tmp13;
                    tmp1 = tmp11 + tmp12;
                    tmp2 = tmp11 - tmp12;

                    // Odd Part
                    int tmp4 = p1 * multiplierSpan[quantTableIndex + 8];
                    int tmp5 = p3 * multiplierSpan[quantTableIndex + 24];
                    int tmp6 = p5 * multiplierSpan[quantTableIndex + 40];
                    int tmp7 = p7 * multiplierSpan[quantTableIndex + 56];

                    int z13 = tmp6 + tmp5; // Phase 6
                    int z10 = tmp6 - tmp5;
                    int z11 = tmp4 + tmp7;
                    int z12 = tmp4 - tmp7;

                    tmp7 = z11 + z13; // Phase 5
                    tmp11 = Multiply(z11 - z13, FIX_1_414213562); // 2*c4

                    int z5 = Multiply(z10 + z12, FIX_1_847759065); // 2*c2
                    tmp10 = z5 - Multiply(z12, FIX_1_082392200); // 2*(c2-c6)
                    tmp12 = z5 - Multiply(z10, FIX_2_613125930); // 2*(c2+c6)

                    tmp6 = tmp12 - tmp7; // Phase 2
                    tmp5 = tmp11 - tmp6;
                    tmp4 = tmp10 - tmp5;

                    computationBufferSpan[workspaceIndex] = (short)(tmp0 + tmp7);
                    computationBufferSpan[workspaceIndex + 56] = (short)(tmp0 - tmp7);
                    computationBufferSpan[workspaceIndex + 8] = (short)(tmp1 + tmp6);
                    computationBufferSpan[workspaceIndex + 48] = (short)(tmp1 - tmp6);
                    computationBufferSpan[workspaceIndex + 16] = (short)(tmp2 + tmp5);
                    computationBufferSpan[workspaceIndex + 40] = (short)(tmp2 - tmp5);
                    computationBufferSpan[workspaceIndex + 24] = (short)(tmp3 + tmp4);
                    computationBufferSpan[workspaceIndex + 32] = (short)(tmp3 - tmp4);

                    coefBlockIndex++;
                    quantTableIndex++;
                    workspaceIndex++;
                }

                // Pass 2: process rows from work array, store into output array.
                // Note that we must descale the results by a factor of 8 == 2**3,
                // and also undo the pass 1 bits scaling.
                for (int row = 0; row < 64; row += 8)
                {
                    p1 = computationBufferSpan[row + 1];
                    p2 = computationBufferSpan[row + 2];
                    p3 = computationBufferSpan[row + 3];
                    p4 = computationBufferSpan[row + 4];
                    p5 = computationBufferSpan[row + 5];
                    p6 = computationBufferSpan[row + 6];
                    p7 = computationBufferSpan[row + 7];

                    // Add range center and fudge factor for final descale and range-limit.
                    int z5 = computationBufferSpan[row] + (RangeCenter << (Pass1Bits + 3)) + (1 << (Pass1Bits + 2));

                    // Check for all-zero AC coefficients
                    if ((p1 | p2 | p3 | p4 | p5 | p6 | p7) == 0)
                    {
                        byte dcval = DoLimit(RightShift(z5, Pass1Bits + 3) & RangeMask);

                        blockData[row] = dcval;
                        blockData[row + 1] = dcval;
                        blockData[row + 2] = dcval;
                        blockData[row + 3] = dcval;
                        blockData[row + 4] = dcval;
                        blockData[row + 5] = dcval;
                        blockData[row + 6] = dcval;
                        blockData[row + 7] = dcval;

                        continue;
                    }

                    // Even part
                    int tmp10 = z5 + p4;
                    int tmp11 = z5 - p4;

                    int tmp13 = p2 + p6;
                    int tmp12 = Multiply(p2 - p6, FIX_1_414213562) - tmp13; /* 2*c4 */

                    int tmp0 = tmp10 + tmp13;
                    int tmp3 = tmp10 - tmp13;
                    int tmp1 = tmp11 + tmp12;
                    int tmp2 = tmp11 - tmp12;

                    // Odd part
                    int z13 = p5 + p3;
                    int z10 = p5 - p3;
                    int z11 = p1 + p7;
                    int z12 = p1 - p7;

                    int tmp7 = z11 + z13; // Phase 5
                    tmp11 = Multiply(z11 - z13, FIX_1_414213562); // 2*c4

                    z5 = Multiply(z10 + z12, FIX_1_847759065); // 2*c2
                    tmp10 = z5 - Multiply(z12, FIX_1_082392200); // 2*(c2-c6)
                    tmp12 = z5 - Multiply(z10, FIX_2_613125930); // 2*(c2+c6)

                    int tmp6 = tmp12 - tmp7; // Phase 2
                    int tmp5 = tmp11 - tmp6;
                    int tmp4 = tmp10 - tmp5;

                    // Final output stage: scale down by a factor of 8 and range-limit
                    blockData[row] = DoLimit(Descale(tmp0 + tmp7, Pass1Bits + 3) & RangeMask);
                    blockData[row + 7] = DoLimit(Descale(tmp0 - tmp7, Pass1Bits + 3) & RangeMask);
                    blockData[row + 1] = DoLimit(Descale(tmp1 + tmp6, Pass1Bits + 3) & RangeMask);
                    blockData[row + 6] = DoLimit(Descale(tmp1 - tmp6, Pass1Bits + 3) & RangeMask);
                    blockData[row + 2] = DoLimit(Descale(tmp2 + tmp5, Pass1Bits + 3) & RangeMask);
                    blockData[row + 5] = DoLimit(Descale(tmp2 - tmp5, Pass1Bits + 3) & RangeMask);
                    blockData[row + 3] = DoLimit(Descale(tmp3 + tmp4, Pass1Bits + 3) & RangeMask);
                    blockData[row + 4] = DoLimit(Descale(tmp3 - tmp4, Pass1Bits + 3) & RangeMask);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Multiply(int val, int c)
        {
            return Descale(val * c, ConstBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RightShift(int x, int shft)
        {
            return x >> shft;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Descale(int x, int n)
        {
            return RightShift(x + (1 << (n - 1)), n);
        }

        /// <summary>
        /// Offsets the value by 128 and limits it to the 0..255 range
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>The <see cref="byte"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte DoLimit(int value)
        {
            return Limit[value + LimitOffset];
        }
    }
}