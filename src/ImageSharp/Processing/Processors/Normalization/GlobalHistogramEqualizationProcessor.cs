// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
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
    internal class GlobalHistogramEqualizationProcessor<TPixel> : HistogramEqualizationProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalHistogramEqualizationProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.</param>
        /// <param name="clipHistogram">Indicating whether to clip the histogram bins at a specific value.</param>
        /// <param name="clipLimitPercentage">Histogram clip limit in percent of the total pixels. Histogram bins which exceed this limit, will be capped at this value.</param>
        public GlobalHistogramEqualizationProcessor(int luminanceLevels, bool clipHistogram, float clipLimitPercentage)
            : base(luminanceLevels, clipHistogram, clipLimitPercentage)
        {
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            MemoryAllocator memoryAllocator = configuration.MemoryAllocator;
            int numberOfPixels = source.Width * source.Height;
            Span<TPixel> pixels = source.GetPixelSpan();

            using (System.Buffers.IMemoryOwner<int> histogramBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
            using (System.Buffers.IMemoryOwner<int> cdfBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
            {
                // Build the histogram of the grayscale levels.
                Span<int> histogram = histogramBuffer.GetSpan();
                for (int i = 0; i < pixels.Length; i++)
                {
                    TPixel sourcePixel = pixels[i];
                    int luminance = this.GetLuminance(sourcePixel, this.LuminanceLevels);
                    histogram[luminance]++;
                }

                if (this.ClipHistogramEnabled)
                {
                    this.ClipHistogram(histogram, this.ClipLimitPercentage, numberOfPixels);
                }

                // Calculate the cumulative distribution function, which will map each input pixel to a new value.
                Span<int> cdf = cdfBuffer.GetSpan();
                int cdfMin = this.CalculateCdf(cdf, histogram, histogram.Length - 1);

                // Apply the cdf to each pixel of the image
                float numberOfPixelsMinusCdfMin = numberOfPixels - cdfMin;
                for (int i = 0; i < pixels.Length; i++)
                {
                    TPixel sourcePixel = pixels[i];

                    int luminance = this.GetLuminance(sourcePixel, this.LuminanceLevels);
                    float luminanceEqualized = cdf[luminance] / numberOfPixelsMinusCdfMin;

                    pixels[i].FromVector4(new Vector4(luminanceEqualized));
                }
            }
        }
    }
}
