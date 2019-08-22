// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
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
        /// <param name="luminanceLevels">
        /// The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.
        /// </param>
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
            var workingRect = new Rectangle(0, 0, source.Width, source.Height);

            using (IMemoryOwner<int> histogramBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
            using (IMemoryOwner<int> cdfBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
            {
                // Build the histogram of the grayscale levels.
                ParallelHelper.IterateRows(
                    workingRect,
                    configuration,
                    rows =>
                        {
                            ref int histogramBase = ref MemoryMarshal.GetReference(histogramBuffer.GetSpan());
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                ref TPixel pixelBase = ref MemoryMarshal.GetReference(source.GetPixelRowSpan(y));

                                for (int x = 0; x < workingRect.Width; x++)
                                {
                                    int luminance = GetLuminance(Unsafe.Add(ref pixelBase, x), this.LuminanceLevels);
                                    Unsafe.Add(ref histogramBase, luminance)++;
                                }
                            }
                        });

                Span<int> histogram = histogramBuffer.GetSpan();
                if (this.ClipHistogramEnabled)
                {
                    this.ClipHistogram(histogram, this.ClipLimitPercentage, numberOfPixels);
                }

                // Calculate the cumulative distribution function, which will map each input pixel to a new value.
                int cdfMin = this.CalculateCdf(
                    ref MemoryMarshal.GetReference(cdfBuffer.GetSpan()),
                    ref MemoryMarshal.GetReference(histogram),
                    histogram.Length - 1);

                float numberOfPixelsMinusCdfMin = numberOfPixels - cdfMin;

                // Apply the cdf to each pixel of the image
                ParallelHelper.IterateRows(
                    workingRect,
                    configuration,
                    rows =>
                        {
                            ref int cdfBase = ref MemoryMarshal.GetReference(cdfBuffer.GetSpan());
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                ref TPixel pixelBase = ref MemoryMarshal.GetReference(source.GetPixelRowSpan(y));

                                for (int x = 0; x < workingRect.Width; x++)
                                {
                                    ref TPixel pixel = ref Unsafe.Add(ref pixelBase, x);
                                    int luminance = GetLuminance(pixel, this.LuminanceLevels);
                                    float luminanceEqualized = Unsafe.Add(ref cdfBase, luminance) / numberOfPixelsMinusCdfMin;
                                    pixel.FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, pixel.ToVector4().W));
                                }
                            }
                        });
            }
        }
    }
}
