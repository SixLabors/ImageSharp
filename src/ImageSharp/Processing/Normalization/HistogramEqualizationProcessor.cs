// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Normalization
{
    /// <summary>
    /// Applies a global histogram equalization to the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class HistogramEqualizationProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            MemoryAllocator memoryAllocator = configuration.MemoryAllocator;
            int numberOfPixels = source.Width * source.Height;
            bool is16bitPerChannel = typeof(TPixel) == typeof(Rgb48) || typeof(TPixel) == typeof(Rgba64);

            Span<TPixel> pixels = source.GetPixelSpan();

            // build the histogram of the grayscale levels
            int luminanceLevels = is16bitPerChannel ? 65536 : 256;
            using (IBuffer<int> histogramBuffer = memoryAllocator.AllocateClean<int>(luminanceLevels))
            using (IBuffer<int> cdfBuffer = memoryAllocator.AllocateClean<int>(luminanceLevels))
            {
                Span<int> histogram = histogramBuffer.GetSpan();
                for (int i = 0; i < pixels.Length; i++)
                {
                    TPixel sourcePixel = pixels[i];
                    int luminance = this.GetLuminance(sourcePixel, luminanceLevels);
                    histogram[luminance]++;
                }

                // calculate the cumulative distribution function, which will map each input pixel to a new value
                Span<int> cdf = cdfBuffer.GetSpan();
                int cdfMin = this.CaluclateCdf(cdf, histogram);

                // apply the cdf to each pixel of the image
                double numberOfPixelsMinusCdfMin = (double)(numberOfPixels - cdfMin);
                for (int i = 0; i < pixels.Length; i++)
                {
                    TPixel sourcePixel = pixels[i];

                    int luminance = this.GetLuminance(sourcePixel, luminanceLevels);
                    double luminanceEqualized = cdf[luminance] / numberOfPixelsMinusCdfMin;

                    pixels[i].PackFromVector4(new Vector4((float)luminanceEqualized));
                }
            }
        }

        /// <summary>
        /// Calculate the cumulative distribution function
        /// </summary>
        /// <param name="cdf">The array holding the cdf</param>
        /// <param name="histogram">The histogram of the input image</param>
        /// <returns>The first none zero value of the cdf</returns>
        private int CaluclateCdf(Span<int> cdf, Span<int> histogram)
        {
            // calculate the cumulative histogram
            int histSum = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                histSum += histogram[i];
                cdf[i] = histSum;
            }

            // get the first none zero value of the cumulative histogram
            int cdfMin = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                if (cdf[i] != 0)
                {
                    cdfMin = cdf[i];
                    break;
                }
            }

            // creating the lookup table: subtracting cdf min, so we do not need to do that inside the for loop
            for (int i = 0; i < histogram.Length; i++)
            {
                cdf[i] = Math.Max(0, cdf[i] - cdfMin);
            }

            return cdfMin;
        }

        /// <summary>
        /// Convert the pixel values to grayscale using ITU-R Recommendation BT.709.
        /// </summary>
        /// <param name="sourcePixel">The pixel to get the luminance from</param>
        /// <param name="luminanceLevels">The number of luminance levels (256 for 8 bit, 65536 for 16 bit grayscale images)</param>
        private int GetLuminance(TPixel sourcePixel, int luminanceLevels)
        {
            // Convert to grayscale using ITU-R Recommendation BT.709
            var vector = sourcePixel.ToVector4();
            int luminance = Convert.ToInt32(((.2126F * vector.X) + (.7152F * vector.Y) + (.0722F * vector.Y)) * luminanceLevels);

            return luminance;
        }
    }
}
