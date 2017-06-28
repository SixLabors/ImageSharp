namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System;
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
    }
}
