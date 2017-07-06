namespace ImageSharp.Formats.Jpeg.Port.Components
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

        private const int ScaleBits = 2; /* fractional bits in scale factors */

        /*
        * Each IDCT routine is responsible for range-limiting its results and
        * converting them to unsigned form (0..255).  The raw outputs could
        * be quite far out of range if the input data is corrupt, so a bulletproof
        * range-limiting step is required.  We use a mask-and-table-lookup method
        * to do the combined operations quickly, assuming that 255+1
        * is a power of 2.  See the comments with prepare_range_limit_table for more info.
        */
        private const int RangeMask = (255 * 4) + 3; /* 2 bits wider than legal samples */

        private static readonly byte[] Limit = new byte[5 * (255 + 1)];

        static IDCT()
        {
            // First segment of range limit table: limit[x] = 0 for x < 0
            // allow negative subscripts of simple table */
            int tableOffset = 2 * (255 + 1);

            // Main part of range limit table: limit[x] = x
            int i;
            for (i = 0; i <= 255; i++)
            {
                Limit[tableOffset + i] = (byte)i;
            }

            /* End of range limit table: limit[x] = MAXJSAMPLE for x > MAXJSAMPLE */
            for (; i < 3 * (255 + 1); i++)
            {
                Limit[tableOffset + i] = 255;
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
                    t = (t < -2040) ? 0 : (t >= 2024) ? 255 : (t + 2056) >> 4;
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
                p0 = (p0 < 16) ? 0 : (p0 >= 4080) ? 255 : p0 >> 4;
                p1 = (p1 < 16) ? 0 : (p1 >= 4080) ? 255 : p1 >> 4;
                p2 = (p2 < 16) ? 0 : (p2 >= 4080) ? 255 : p2 >> 4;
                p3 = (p3 < 16) ? 0 : (p3 >= 4080) ? 255 : p3 >> 4;
                p4 = (p4 < 16) ? 0 : (p4 >= 4080) ? 255 : p4 >> 4;
                p5 = (p5 < 16) ? 0 : (p5 >= 4080) ? 255 : p5 >> 4;
                p6 = (p6 < 16) ? 0 : (p6 >= 4080) ? 255 : p6 >> 4;
                p7 = (p7 < 16) ? 0 : (p7 >= 4080) ? 255 : p7 >> 4;

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
        /// TODO: This does not work!!
        /// A 2-D IDCT can be done by 1-D IDCT on each column followed by 1-D IDCT
        /// on each row(or vice versa, but it's more convenient to emit a row at
        /// a time).  Direct algorithms are also available, but they are much more
        /// complex and seem not to be any faster when reduced to code.
        ///
        /// This implementation is based on Arai, Agui, and Nakajima's algorithm for
        /// scaled DCT.Their original paper (Trans.IEICE E-71(11):1095) is in
        /// Japanese, but the algorithm is described in the Pennebaker & Mitchell
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
        public static void QuantizeAndInverseAlt(QuantizationTables quantizationTables, ref FrameComponent component, int blockBufferOffset, Buffer<short> computationBuffer)
        {
            Span<short> qt = quantizationTables.Tables.GetRowSpan(component.QuantizationIdentifier);
            Span<short> blockData = component.BlockData.Slice(blockBufferOffset);
            Span<short> computationBufferSpan = computationBuffer;

            int p0, p1, p2, p3, p4, p5, p6, p7;

            for (int col = 0; col < 8; col++)
            {
                // Gather block data
                p0 = blockData[col];
                p1 = blockData[col + 8];
                p2 = blockData[col + 16];
                p3 = blockData[col + 24];
                p4 = blockData[col + 32];
                p5 = blockData[col + 40];
                p6 = blockData[col + 48];
                p7 = blockData[col + 56];

                int tmp0 = p0 * qt[col];

                // Check for all-zero AC coefficients
                if ((p1 | p2 | p3 | p4 | p5 | p6 | p7) == 0)
                {
                    short dcval = (short)tmp0;

                    computationBufferSpan[col] = dcval;
                    computationBufferSpan[col + 8] = dcval;
                    computationBufferSpan[col + 16] = dcval;
                    computationBufferSpan[col + 24] = dcval;
                    computationBufferSpan[col + 32] = dcval;
                    computationBufferSpan[col + 40] = dcval;
                    computationBufferSpan[col + 48] = dcval;
                    computationBufferSpan[col + 56] = dcval;
                    continue;
                }

                // Even part
                int tmp1 = p2 * qt[col + 16];
                int tmp2 = p4 * qt[col + 32];
                int tmp3 = p6 * qt[col + 48];

                int tmp10 = tmp0 + tmp2;    // Phase 3
                int tmp11 = tmp0 - tmp2;

                int tmp13 = tmp1 + tmp3;    // Phases 5-3
                int tmp12 = Multiply(tmp1 - tmp3, FIX_1_414213562) - tmp13; // 2*c4

                tmp0 = tmp10 + tmp13;   // Phase 2
                tmp3 = tmp10 - tmp13;
                tmp1 = tmp11 + tmp12;
                tmp2 = tmp11 - tmp12;

                // Odd Part
                int tmp4 = p1 * qt[col + 8];
                int tmp5 = p3 * qt[col + 24];
                int tmp6 = p5 * qt[col + 40];
                int tmp7 = p7 * qt[col + 56];

                int z13 = tmp6 + tmp5;      // Phase 6
                int z10 = tmp6 - tmp5;
                int z11 = tmp4 + tmp7;
                int z12 = tmp4 - tmp7;

                tmp7 = z11 + z13;       // Phase 5
                tmp11 = Multiply(z11 - z13, FIX_1_414213562); // 2*c4

                int z5 = Multiply(z10 + z12, FIX_1_847759065); // 2*c2
                tmp10 = Multiply(z12, FIX_1_082392200) - z5; // 2*(c2-c6)
                tmp12 = Multiply(z10, FIX_2_613125930) + z5; // 2*(c2+c6)

                tmp6 = tmp12 - tmp7;    // Phase 2
                tmp5 = tmp11 - tmp6;
                tmp4 = tmp10 - tmp5;

                computationBufferSpan[col] = (short)(tmp0 + tmp7);
                computationBufferSpan[col + 56] = (short)(tmp0 - tmp7);
                computationBufferSpan[col + 8] = (short)(tmp1 + tmp6);
                computationBufferSpan[col + 48] = (short)(tmp1 - tmp6);
                computationBufferSpan[col + 16] = (short)(tmp2 + tmp5);
                computationBufferSpan[col + 40] = (short)(tmp2 - tmp5);
                computationBufferSpan[col + 32] = (short)(tmp3 + tmp4);
                computationBufferSpan[col + 24] = (short)(tmp3 - tmp4);
            }

            // Pass 2: process rows from work array, store into output array.
            // Note that we must descale the results by a factor of 8 == 2**3,
            // and also undo the pass 1 bits scaling.
            for (int row = 0; row < 64; row += 8)
            {
                p0 = computationBufferSpan[row];
                p1 = computationBufferSpan[row + 1];
                p2 = computationBufferSpan[row + 2];
                p3 = computationBufferSpan[row + 3];
                p4 = computationBufferSpan[row + 4];
                p5 = computationBufferSpan[row + 5];
                p6 = computationBufferSpan[row + 6];
                p7 = computationBufferSpan[row + 7];

                // Check for all-zero AC coefficients
                if ((p1 | p2 | p3 | p4 | p5 | p6 | p7) == 0)
                {
                    byte dcval = Limit[Descale(p0, ScaleBits + 3) & RangeMask];

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
                int tmp10 = p0 + p4;
                int tmp11 = p0 - p4;

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

                int tmp7 = z11 + z13;       // Phase 5
                tmp11 = Multiply(z11 - z13, FIX_1_414213562); // 2*c4

                int z5 = Multiply(z10 + z12, FIX_1_847759065); // 2*c2
                tmp10 = Multiply(z12, FIX_1_082392200) - z5; // 2*(c2-c6)
                tmp12 = Multiply(z10, FIX_2_613125930) + z5; // -2*(c2+c6)

                int tmp6 = tmp12 - tmp7;    // Phase 2
                int tmp5 = tmp11 - tmp6;
                int tmp4 = tmp10 - tmp5;

                // Final output stage: scale down by a factor of 8 and range-limit
                blockData[row] = Limit[Descale(tmp0 + tmp7, ScaleBits + 3) & RangeMask];
                blockData[row + 7] = Limit[Descale(tmp0 - tmp7, ScaleBits + 3) & RangeMask];
                blockData[row + 1] = Limit[Descale(tmp1 + tmp6, ScaleBits + 3) & RangeMask];
                blockData[row + 6] = Limit[Descale(tmp1 - tmp6, ScaleBits + 3) & RangeMask];
                blockData[row + 2] = Limit[Descale(tmp2 + tmp5, ScaleBits + 3) & RangeMask];
                blockData[row + 5] = Limit[Descale(tmp2 - tmp5, ScaleBits + 3) & RangeMask];
                blockData[row + 3] = Limit[Descale(tmp3 + tmp4, ScaleBits + 3) & RangeMask];
                blockData[row + 4] = Limit[Descale(tmp3 - tmp4, ScaleBits + 3) & RangeMask];
            }
        }

        private static int Multiply(int val, int c)
        {
            return Descale(val * c, 8);
        }

        private static int RightShift(int x, int shft)
        {
            return x >> shft;
        }

        private static int Descale(int x, int n)
        {
            return RightShift(x + (1 << (n - 1)), n);
        }
    }
}