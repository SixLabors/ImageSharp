// <copyright file="IDCT.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    internal class IDCT
    {
        private const int w1 = 2841; // 2048*sqrt(2)*cos(1*pi/16)
        private const int w2 = 2676; // 2048*sqrt(2)*cos(2*pi/16)
        private const int w3 = 2408; // 2048*sqrt(2)*cos(3*pi/16)
        private const int w5 = 1609; // 2048*sqrt(2)*cos(5*pi/16)
        private const int w6 = 1108; // 2048*sqrt(2)*cos(6*pi/16)
        private const int w7 = 565;  // 2048*sqrt(2)*cos(7*pi/16)

        private const int w1pw7 = w1 + w7;
        private const int w1mw7 = w1 - w7;
        private const int w2pw6 = w2 + w6;
        private const int w2mw6 = w2 - w6;
        private const int w3pw5 = w3 + w5;
        private const int w3mw5 = w3 - w5;

        private const int r2 = 181; // 256/sqrt(2)

        // idct performs a 2-D Inverse Discrete Cosine Transformation.
        //
        // The input coefficients should already have been multiplied by the
        // appropriate quantization table. We use fixed-point computation, with the
        // number of bits for the fractional component varying over the intermediate
        // stages.
        //
        // For more on the actual algorithm, see Z. Wang, "Fast algorithms for the
        // discrete W transform and for the discrete Fourier transform", IEEE Trans. on
        // ASSP, Vol. ASSP- 32, pp. 803-816, Aug. 1984.
        public static void Transform(Block src)
        {
            // Horizontal 1-D IDCT.
            for (int y = 0; y < 8; y++)
            {
                int y8 = y * 8;

                // If all the AC components are zero, then the IDCT is trivial.
                if (src[y8 + 1] == 0 && src[y8 + 2] == 0 && src[y8 + 3] == 0 &&
                    src[y8 + 4] == 0 && src[y8 + 5] == 0 && src[y8 + 6] == 0 && src[y8 + 7] == 0)
                {
                    int dc = src[y8 + 0] << 3;
                    src[y8 + 0] = dc;
                    src[y8 + 1] = dc;
                    src[y8 + 2] = dc;
                    src[y8 + 3] = dc;
                    src[y8 + 4] = dc;
                    src[y8 + 5] = dc;
                    src[y8 + 6] = dc;
                    src[y8 + 7] = dc;
                    continue;
                }

                // Prescale.
                int x0 = (src[y8 + 0] << 11) + 128;
                int x1 = src[y8 + 4] << 11;
                int x2 = src[y8 + 6];
                int x3 = src[y8 + 2];
                int x4 = src[y8 + 1];
                int x5 = src[y8 + 7];
                int x6 = src[y8 + 5];
                int x7 = src[y8 + 3];

                // Stage 1.
                int x8 = w7 * (x4 + x5);
                x4 = x8 + w1mw7 * x4;
                x5 = x8 - w1pw7 * x5;
                x8 = w3 * (x6 + x7);
                x6 = x8 - w3mw5 * x6;
                x7 = x8 - w3pw5 * x7;

                // Stage 2.
                x8 = x0 + x1;
                x0 -= x1;
                x1 = w6 * (x3 + x2);
                x2 = x1 - w2pw6 * x2;
                x3 = x1 + w2mw6 * x3;
                x1 = x4 + x6;
                x4 -= x6;
                x6 = x5 + x7;
                x5 -= x7;

                // Stage 3.
                x7 = x8 + x3;
                x8 -= x3;
                x3 = x0 + x2;
                x0 -= x2;
                x2 = (r2 * (x4 + x5) + 128) >> 8;
                x4 = (r2 * (x4 - x5) + 128) >> 8;

                // Stage 4.
                src[y8 + 0] = (x7 + x1) >> 8;
                src[y8 + 1] = (x3 + x2) >> 8;
                src[y8 + 2] = (x0 + x4) >> 8;
                src[y8 + 3] = (x8 + x6) >> 8;
                src[y8 + 4] = (x8 - x6) >> 8;
                src[y8 + 5] = (x0 - x4) >> 8;
                src[y8 + 6] = (x3 - x2) >> 8;
                src[y8 + 7] = (x7 - x1) >> 8;
            }

            // Vertical 1-D IDCT.
            for (int x = 0; x < 8; x++)
            {
                // Similar to the horizontal 1-D IDCT case, if all the AC components are zero, then the IDCT is trivial.
                // However, after performing the horizontal 1-D IDCT, there are typically non-zero AC components, so
                // we do not bother to check for the all-zero case.

                // Prescale.
                int y0 = (src[x] << 8) + 8192;
                int y1 = src[32 + x] << 8;
                int y2 = src[48 + x];
                int y3 = src[16 + x];
                int y4 = src[8 + x];
                int y5 = src[56 + x];
                int y6 = src[40 + x];
                int y7 = src[24 + x];

                // Stage 1.
                int y8 = w7 * (y4 + y5) + 4;
                y4 = (y8 + w1mw7 * y4) >> 3;
                y5 = (y8 - w1pw7 * y5) >> 3;
                y8 = w3 * (y6 + y7) + 4;
                y6 = (y8 - w3mw5 * y6) >> 3;
                y7 = (y8 - w3pw5 * y7) >> 3;

                // Stage 2.
                y8 = y0 + y1;
                y0 -= y1;
                y1 = w6 * (y3 + y2) + 4;
                y2 = (y1 - w2pw6 * y2) >> 3;
                y3 = (y1 + w2mw6 * y3) >> 3;
                y1 = y4 + y6;
                y4 -= y6;
                y6 = y5 + y7;
                y5 -= y7;

                // Stage 3.
                y7 = y8 + y3;
                y8 -= y3;
                y3 = y0 + y2;
                y0 -= y2;
                y2 = (r2 * (y4 + y5) + 128) >> 8;
                y4 = (r2 * (y4 - y5) + 128) >> 8;

                // Stage 4.
                src[x] = (y7 + y1) >> 14;
                src[8 + x] = (y3 + y2) >> 14;
                src[16 + x] = (y0 + y4) >> 14;
                src[24 + x] = (y8 + y6) >> 14;
                src[32 + x] = (y8 - y6) >> 14;
                src[40 + x] = (y0 - y4) >> 14;
                src[48 + x] = (y3 - y2) >> 14;
                src[56 + x] = (y7 - y1) >> 14;
            }
        }
    }
}
