// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Defines a processor that normalizes the histogram of an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class HistogramEqualizationProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly float luminanceLevelsFloat;

        /// <summary>
        /// Initializes a new instance of the <see cref="HistogramEqualizationProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.</param>
        /// <param name="clipHistogram">Indicates, if histogram bins should be clipped.</param>
        /// <param name="clipLimitPercentage">Histogram clip limit in percent of the total pixels in the tile. Histogram bins which exceed this limit, will be capped at this value.</param>
        protected HistogramEqualizationProcessor(int luminanceLevels, bool clipHistogram, float clipLimitPercentage)
        {
            Guard.MustBeGreaterThan(luminanceLevels, 0, nameof(luminanceLevels));
            Guard.MustBeGreaterThan(clipLimitPercentage, 0F, nameof(clipLimitPercentage));

            this.LuminanceLevels = luminanceLevels;
            this.luminanceLevelsFloat = luminanceLevels;
            this.ClipHistogramEnabled = clipHistogram;
            this.ClipLimitPercentage = clipLimitPercentage;
        }

        /// <summary>
        /// Gets the number of luminance levels.
        /// </summary>
        public int LuminanceLevels { get; }

        /// <summary>
        /// Gets a value indicating whether to clip the histogram bins at a specific value.
        /// </summary>
        public bool ClipHistogramEnabled { get; }

        /// <summary>
        /// Gets the histogram clip limit in percent of the total pixels in the tile. Histogram bins which exceed this limit, will be capped at this value.
        /// </summary>
        public float ClipLimitPercentage { get; }

        /// <summary>
        /// Calculates the cumulative distribution function.
        /// </summary>
        /// <param name="cdfBase">The reference to the array holding the cdf.</param>
        /// <param name="histogramBase">The reference to the histogram of the input image.</param>
        /// <param name="maxIdx">Index of the maximum of the histogram.</param>
        /// <returns>The first none zero value of the cdf.</returns>
        public int CalculateCdf(ref int cdfBase, ref int histogramBase, int maxIdx)
        {
            int histSum = 0;
            int cdfMin = 0;
            bool cdfMinFound = false;

            for (int i = 0; i <= maxIdx; i++)
            {
                histSum += Unsafe.Add(ref histogramBase, i);
                if (!cdfMinFound && histSum != 0)
                {
                    cdfMin = histSum;
                    cdfMinFound = true;
                }

                // Creating the lookup table: subtracting cdf min, so we do not need to do that inside the for loop.
                Unsafe.Add(ref cdfBase, i) = Math.Max(0, histSum - cdfMin);
            }

            return cdfMin;
        }

        /// <summary>
        /// AHE tends to over amplify the contrast in near-constant regions of the image, since the histogram in such regions is highly concentrated.
        /// Clipping the histogram is meant to reduce this effect, by cutting of histogram bin's which exceed a certain amount and redistribute
        /// the values over the clip limit to all other bins equally.
        /// </summary>
        /// <param name="histogram">The histogram to apply the clipping.</param>
        /// <param name="clipLimitPercentage">Histogram clip limit in percent of the total pixels in the tile. Histogram bins which exceed this limit, will be capped at this value.</param>
        /// <param name="pixelCount">The numbers of pixels inside the tile.</param>
        public void ClipHistogram(Span<int> histogram, float clipLimitPercentage, int pixelCount)
        {
            int clipLimit = (int)MathF.Round(pixelCount * clipLimitPercentage);
            int sumOverClip = 0;
            ref int histogramBase = ref MemoryMarshal.GetReference(histogram);

            for (int i = 0; i < histogram.Length; i++)
            {
                ref int histogramLevel = ref Unsafe.Add(ref histogramBase, i);
                if (histogramLevel > clipLimit)
                {
                    sumOverClip += histogramLevel - clipLimit;
                    histogramLevel = clipLimit;
                }
            }

            int addToEachBin = sumOverClip > 0 ? (int)MathF.Floor(sumOverClip / this.luminanceLevelsFloat) : 0;
            if (addToEachBin > 0)
            {
                for (int i = 0; i < histogram.Length; i++)
                {
                    Unsafe.Add(ref histogramBase, i) += addToEachBin;
                }
            }
        }

        /// <summary>
        /// Convert the pixel values to grayscale using ITU-R Recommendation BT.709.
        /// </summary>
        /// <param name="sourcePixel">The pixel to get the luminance from</param>
        /// <param name="luminanceLevels">The number of luminance levels (256 for 8 bit, 65536 for 16 bit grayscale images)</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static int GetLuminance(TPixel sourcePixel, int luminanceLevels)
        {
            var vector = sourcePixel.ToVector4();
            return GetLuminance(ref vector, luminanceLevels);
        }

        /// <summary>
        /// Convert the pixel values to grayscale using ITU-R Recommendation BT.709.
        /// </summary>
        /// <param name="vector">The vector to get the luminance from</param>
        /// <param name="luminanceLevels">The number of luminance levels (256 for 8 bit, 65536 for 16 bit grayscale images)</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static int GetLuminance(ref Vector4 vector, int luminanceLevels)
            => (int)MathF.Round(((.2126F * vector.X) + (.7152F * vector.Y) + (.0722F * vector.Y)) * (luminanceLevels - 1));
    }
}