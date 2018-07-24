// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Applies an adaptive histogram equalization to the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class AdaptiveHistEqualizationProcessor<TPixel> : HistogramEqualizationProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveHistEqualizationProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.</param>
        public AdaptiveHistEqualizationProcessor(int luminanceLevels)
            : base(luminanceLevels)
        {
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            MemoryAllocator memoryAllocator = configuration.MemoryAllocator;
            int numberOfPixels = source.Width * source.Height;
            Span<TPixel> pixels = source.GetPixelSpan();

            int gridSize = 64;
            int pixelsInGrid = gridSize * gridSize;
            int contrastLimit = 5;
            int halfGridSize = gridSize / 2;
            using (IBuffer<int> histogramBuffer = memoryAllocator.AllocateClean<int>(this.LuminanceLevels))
            using (IBuffer<int> histogramBufferCopy = memoryAllocator.AllocateClean<int>(this.LuminanceLevels))
            using (IBuffer<int> cdfBuffer = memoryAllocator.AllocateClean<int>(this.LuminanceLevels))
            using (Buffer2D<TPixel> targetPixels = configuration.MemoryAllocator.Allocate2D<TPixel>(source.Width, source.Height))
            {
                Span<int> histogram = histogramBuffer.GetSpan();
                Span<int> histogramCopy = histogramBufferCopy.GetSpan();
                Span<int> cdf = cdfBuffer.GetSpan();

                for (int x = halfGridSize; x < source.Width - halfGridSize; x++)
                {
                    histogram.Clear();

                    // Build the histogram of grayscale values for the current grid.
                    for (int dy = 0; dy < gridSize; dy++)
                    {
                        Span<TPixel> rowSpan = source.GetPixelRowSpan(dy).Slice(start: x - halfGridSize, length: gridSize);
                        this.AddPixelsTooHistogram(rowSpan, histogram, this.LuminanceLevels);
                    }

                    for (int y = halfGridSize + 1; y < source.Height - halfGridSize; y++)
                    {
                        // Remove top most row from the histogram
                        Span<TPixel> rowSpan = source.GetPixelRowSpan(y - halfGridSize - 1).Slice(start: x - halfGridSize, length: gridSize);
                        this.RemovePixelsFromHistogram(rowSpan, histogram, this.LuminanceLevels);

                        // Add new bottom row to the histogram
                        rowSpan = source.GetPixelRowSpan(y + halfGridSize - 1).Slice(start: x - halfGridSize, length: gridSize);
                        this.AddPixelsTooHistogram(rowSpan, histogram, this.LuminanceLevels);

                        // Clipping the histogram, but doing it on a copy to keep the original un-clipped values
                        histogram.CopyTo(histogramCopy);
                        this.ClipHistogram(histogramCopy, gridSize, contrastLimit);
                        cdf.Clear();
                        int cdfMin = this.CalculateCdf(cdf, histogramCopy);
                        double numberOfPixelsMinusCdfMin = (double)(pixelsInGrid - cdfMin);

                        // Map the current pixel to the new equalized value
                        int luminance = this.GetLuminance(pixels[(y * source.Width) + x], this.LuminanceLevels);
                        double luminanceEqualized = cdf[luminance] / numberOfPixelsMinusCdfMin;
                        targetPixels[x, y].PackFromVector4(new Vector4((float)luminanceEqualized));
                    }
                }

                Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
            }
        }

        /// <summary>
        /// AHE tends to over amplify the contrast in near-constant regions of the image, since the histogram in such regions is highly concentrated.
        /// Clipping the histogram is meant to reduce this effect, by cutting of histogram bin's which exceed a certain amount and redistribute
        /// the values over the clip limit to all other bins equally.
        /// </summary>
        /// <param name="histogram">The histogram to apply the clipping</param>
        /// <param name="gridSize">The grid size of the AHE</param>
        /// <param name="contrastLimit">The contrast limit. Defaults to 5.</param>
        /// <returns>The number of pixels redistributed to all other bins</returns>
        private int ClipHistogram(Span<int> histogram, int gridSize, int contrastLimit = 5)
        {
            int clipLimit = (contrastLimit * (gridSize * gridSize)) / this.LuminanceLevels;

            int sumOverClip = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                if (histogram[i] > clipLimit)
                {
                    sumOverClip += histogram[i] - clipLimit;
                    histogram[i] = clipLimit;
                }
            }

            int addToEachBin = (int)Math.Floor(sumOverClip / (double)this.LuminanceLevels);
            for (int i = 0; i < histogram.Length; i++)
            {
                histogram[i] += addToEachBin;
            }

            // The redistribution will push some bins over the clip limit again.
            // Re-Applying the clipping until this effect no longer occurs.
            while (addToEachBin > 1)
            {
                addToEachBin = this.ClipHistogram(histogram, gridSize, contrastLimit);
            }

            return addToEachBin;
        }

        /// <summary>
        /// Adds a row of grey values to the histogram.
        /// </summary>
        /// <param name="greyValues">The grey values to add</param>
        /// <param name="histogram">The histogram</param>
        /// <param name="luminanceLevels">The number of different luminance levels.</param>
        private void AddPixelsTooHistogram(Span<TPixel> greyValues, Span<int> histogram, int luminanceLevels)
        {
            for (int i = 0; i < greyValues.Length; i++)
            {
                int luminance = this.GetLuminance(greyValues[i], luminanceLevels);
                histogram[luminance]++;
            }
        }

        /// <summary>
        /// Removes a row of grey values from the histogram.
        /// </summary>
        /// <param name="greyValues">The grey values to remove</param>
        /// <param name="histogram">The histogram</param>
        /// <param name="luminanceLevels">The number of different luminance levels.</param>
        private void RemovePixelsFromHistogram(Span<TPixel> greyValues, Span<int> histogram, int luminanceLevels)
        {
            for (int i = 0; i < greyValues.Length; i++)
            {
                int luminance = this.GetLuminance(greyValues[i], luminanceLevels);
                histogram[luminance]--;
            }
        }
    }
}
