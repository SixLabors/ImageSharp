// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal sealed class Vp8Histogram
    {
        private readonly int[] scratch = new int[16];

        private readonly short[] output = new short[16];

        private readonly int[] distribution = new int[MaxCoeffThresh + 1];

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
            int alpha = maxValue > 1 ? WebpConstants.AlphaScale * lastNonZero / maxValue : 0;
            return alpha;
        }

        public void CollectHistogram(Span<byte> reference, Span<byte> pred, int startBlock, int endBlock)
        {
            int j;
            this.distribution.AsSpan().Clear();
            for (j = startBlock; j < endBlock; j++)
            {
                Vp8Encoding.FTransform(reference.Slice(WebpLookupTables.Vp8DspScan[j]), pred.Slice(WebpLookupTables.Vp8DspScan[j]), this.output, this.scratch);

                // Convert coefficients to bin.
                for (int k = 0; k < 16; ++k)
                {
                    int v = Math.Abs(this.output[k]) >> 3;
                    int clippedValue = ClipMax(v, MaxCoeffThresh);
                    ++this.distribution[clippedValue];
                }
            }

            this.SetHistogramData(this.distribution);
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

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int ClipMax(int v, int max) => v > max ? max : v;
    }
}
