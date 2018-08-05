// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Applies a global histogram equalization to the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class HistogramEqualizationProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HistogramEqualizationProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.</param>
        public HistogramEqualizationProcessor(int luminanceLevels)
        {
            Guard.MustBeGreaterThan(luminanceLevels, 0, nameof(luminanceLevels));

            this.LuminanceLevels = luminanceLevels;
        }

        /// <summary>
        /// Gets the number of luminance levels.
        /// </summary>
        public int LuminanceLevels { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            MemoryAllocator memoryAllocator = configuration.MemoryAllocator;
            int numberOfPixels = source.Width * source.Height;
            Span<TPixel> pixels = source.GetPixelSpan();

            // Build the histogram of the grayscale levels.
            using (IMemoryOwner<int> histogramBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
            using (IMemoryOwner<int> cdfBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
            {
                Span<int> histogram = histogramBuffer.GetSpan();
                for (int i = 0; i < pixels.Length; i++)
                {
                    TPixel sourcePixel = pixels[i];
                    int luminance = this.GetLuminance(sourcePixel, this.LuminanceLevels);
                    histogram[luminance]++;
                }

                // Calculate the cumulative distribution function, which will map each input pixel to a new value.
                Span<int> cdf = cdfBuffer.GetSpan();
                int cdfMin = this.CalculateCdf(cdf, histogram);

                // Apply the cdf to each pixel of the image
                float numberOfPixelsMinusCdfMin = numberOfPixels - cdfMin;
                for (int i = 0; i < pixels.Length; i++)
                {
                    TPixel sourcePixel = pixels[i];

                    int luminance = this.GetLuminance(sourcePixel, this.LuminanceLevels);
                    float luminanceEqualized = cdf[luminance] / numberOfPixelsMinusCdfMin;

                    pixels[i].PackFromVector4(new Vector4(luminanceEqualized));
                }
            }
        }

        /// <summary>
        /// Calculates the cumulative distribution function.
        /// </summary>
        /// <param name="cdf">The array holding the cdf.</param>
        /// <param name="histogram">The histogram of the input image.</param>
        /// <returns>The first none zero value of the cdf.</returns>
        private int CalculateCdf(Span<int> cdf, Span<int> histogram)
        {
            // Calculate the cumulative histogram
            int histSum = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                histSum += histogram[i];
                cdf[i] = histSum;
            }

            // Get the first none zero value of the cumulative histogram
            int cdfMin = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                if (cdf[i] != 0)
                {
                    cdfMin = cdf[i];
                    break;
                }
            }

            // Creating the lookup table: subtracting cdf min, so we do not need to do that inside the for loop
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
        [MethodImpl(InliningOptions.ShortMethod)]
        private int GetLuminance(TPixel sourcePixel, int luminanceLevels)
        {
            // Convert to grayscale using ITU-R Recommendation BT.709
            var vector = sourcePixel.ToVector4();
            int luminance = Convert.ToInt32(((.2126F * vector.X) + (.7152F * vector.Y) + (.0722F * vector.Y)) * (luminanceLevels - 1));

            return luminance;
        }
    }
}
