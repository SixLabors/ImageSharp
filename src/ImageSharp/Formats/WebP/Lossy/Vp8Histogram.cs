// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Webp;

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    internal class Vp8Histogram
    {
        /// <summary>
        /// Size of histogram used by CollectHistogram.
        /// </summary>
        private const int MaxCoeffThresh = 31;

        private int maxValue;

        private int lastNonZero;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8Histogram" /> class.
        /// </summary>
        public Vp8Histogram()
        {
            this.maxValue = 0;
            this.lastNonZero = 1;
        }

        public int GetAlpha()
        {
            // 'alpha' will later be clipped to [0..MAX_ALPHA] range, clamping outer
            // values which happen to be mostly noise. This leaves the maximum precision
            // for handling the useful small values which contribute most.
            int maxValue = this.maxValue;
            int lastNonZero = this.lastNonZero;
            int alpha = (maxValue > 1) ? WebpConstants.AlphaScale * lastNonZero / maxValue : 0;
            return alpha;
        }

        public void CollectHistogram(Span<byte> reference, Span<byte> pred, int startBlock, int endBlock)
        {
            int j;
            int[] distribution = new int[MaxCoeffThresh + 1];
            for (j = startBlock; j < endBlock; ++j)
            {
                short[] output = new short[16];

                this.Vp8FTransform(reference.Slice(WebpLookupTables.Vp8DspScan[j]), pred.Slice(WebpLookupTables.Vp8DspScan[j]), output);

                // Convert coefficients to bin.
                for (int k = 0; k < 16; ++k)
                {
                    int v = Math.Abs(output[k]) >> 3;
                    int clippedValue = ClipMax(v, MaxCoeffThresh);
                    ++distribution[clippedValue];
                }
            }

            this.SetHistogramData(distribution);
        }

        public void Merge(Vp8Histogram other)
        {
            if (this.maxValue > other.maxValue)
            {
                other.maxValue = this.maxValue;
            }

            if (this.lastNonZero > other.lastNonZero)
            {
                other.lastNonZero = this.lastNonZero;
            }
        }

        private void SetHistogramData(int[] distribution)
        {
            int maxValue = 0;
            int lastNonZero = 1;
            for (int k = 0; k <= MaxCoeffThresh; ++k)
            {
                int value = distribution[k];
                if (value > 0)
                {
                    if (value > maxValue)
                    {
                        maxValue = value;
                    }

                    lastNonZero = k;
                }
            }

            this.maxValue = maxValue;
            this.lastNonZero = lastNonZero;
        }

        private void Vp8FTransform(Span<byte> src, Span<byte> reference, Span<short> output)
        {
            int i;
            int[] tmp = new int[16];
            for (i = 0; i < 4; ++i)
            {
                int d0 = src[0] - reference[0];   // 9bit dynamic range ([-255,255])
                int d1 = src[1] - reference[1];
                int d2 = src[2] - reference[2];
                int d3 = src[3] - reference[3];
                int a0 = d0 + d3; // 10b [-510,510]
                int a1 = d1 + d2;
                int a2 = d1 - d2;
                int a3 = d0 - d3;
                tmp[0 + (i * 4)] = (a0 + a1) * 8; // 14b [-8160,8160]
                tmp[1 + (i * 4)] = ((a2 * 2217) + (a3 * 5352) + 1812) >> 9; // [-7536,7542]
                tmp[2 + (i * 4)] = (a0 - a1) * 8;
                tmp[3 + (i * 4)] = ((a3 * 2217) - (a2 * 5352) + 937) >> 9;

                // Do not change the span in the last iteration.
                if (i < 3)
                {
                    src = src.Slice(WebpConstants.Bps);
                    reference = reference.Slice(WebpConstants.Bps);
                }
            }

            for (i = 0; i < 4; ++i)
            {
                int a0 = tmp[0 + i] + tmp[12 + i];  // 15b
                int a1 = tmp[4 + i] + tmp[8 + i];
                int a2 = tmp[4 + i] - tmp[8 + i];
                int a3 = tmp[0 + i] - tmp[12 + i];
                output[0 + i] = (short)((a0 + a1 + 7) >> 4); // 12b
                output[4 + i] = (short)((((a2 * 2217) + (a3 * 5352) + 12000) >> 16) + ((a3 != 0) ? 1 : 0));
                output[8 + i] = (short)((a0 - a1 + 7) >> 4);
                output[12 + i] = (short)(((a3 * 2217) - (a2 * 5352) + 51000) >> 16);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int ClipMax(int v, int max) => (v > max) ? max : v;
    }
}
