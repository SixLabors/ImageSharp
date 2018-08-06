// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
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
        /// <param name="clipHistogram">Indicating whether to clip the histogram bins at a specific value.</param>
        /// <param name="clipLimitPercentage">Histogram clip limit in percent of the total pixels in the grid. Histogram bins which exceed this limit, will be capped at this value.</param>
        /// <param name="gridSize">The grid size of the adaptive histogram equalization. Minimum value is 4.</param>
        public AdaptiveHistEqualizationProcessor(int luminanceLevels, bool clipHistogram, float clipLimitPercentage, int gridSize)
            : base(luminanceLevels, clipHistogram, clipLimitPercentage)
        {
            Guard.MustBeGreaterThanOrEqualTo(gridSize, 4, nameof(gridSize));

            this.GridSize = gridSize;
        }

        /// <summary>
        /// Gets the size of the grid for the adaptive histogram equalization.
        /// </summary>
        public int GridSize { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            MemoryAllocator memoryAllocator = configuration.MemoryAllocator;
            int numberOfPixels = source.Width * source.Height;
            Span<TPixel> pixels = source.GetPixelSpan();

            int pixelsInGrid = this.GridSize * this.GridSize;
            int halfGridSize = this.GridSize / 2;
            using (Buffer2D<TPixel> targetPixels = configuration.MemoryAllocator.Allocate2D<TPixel>(source.Width, source.Height))
            {
                Parallel.For(
                    0,
                    source.Width,
                    configuration.ParallelOptions,
                    x =>
                    {
                        using (IBuffer<int> histogramBuffer = memoryAllocator.AllocateClean<int>(this.LuminanceLevels))
                        using (IBuffer<int> histogramBufferCopy = memoryAllocator.AllocateClean<int>(this.LuminanceLevels))
                        using (IBuffer<int> cdfBuffer = memoryAllocator.AllocateClean<int>(this.LuminanceLevels))
                        {
                            Span<int> histogram = histogramBuffer.GetSpan();
                            Span<int> histogramCopy = histogramBufferCopy.GetSpan();
                            Span<int> cdf = cdfBuffer.GetSpan();

                            histogram.Clear();

                            // Build the histogram of grayscale values for the current grid.
                            for (int dy = -halfGridSize; dy < halfGridSize; dy++)
                            {
                                Span<TPixel> rowSpan = this.GetPixelRow(source, x - halfGridSize, dy, this.GridSize);
                                this.AddPixelsTooHistogram(rowSpan, histogram, this.LuminanceLevels);
                            }

                            for (int y = 0; y < source.Height; y++)
                            {
                                if (this.ClipHistogramEnabled)
                                {
                                    // Clipping the histogram, but doing it on a copy to keep the original un-clipped values for the next iteration.
                                    histogram.CopyTo(histogramCopy);
                                    this.ClipHistogram(histogramCopy, this.ClipLimitPercentage, pixelsInGrid);
                                }

                                // Calculate the cumulative distribution function, which will map each input pixel in the current grid to a new value.
                                int cdfMin = this.ClipHistogramEnabled ? this.CalculateCdf(cdf, histogramCopy) : this.CalculateCdf(cdf, histogram);
                                float numberOfPixelsMinusCdfMin = pixelsInGrid - cdfMin;

                                // Map the current pixel to the new equalized value
                                int luminance = this.GetLuminance(source[x, y], this.LuminanceLevels);
                                float luminanceEqualized = cdf[luminance] / numberOfPixelsMinusCdfMin;
                                targetPixels[x, y].PackFromVector4(new Vector4(luminanceEqualized));

                                // Remove top most row from the histogram, mirroring rows which exceeds the borders.
                                Span<TPixel> rowSpan = this.GetPixelRow(source, x - halfGridSize, y - halfGridSize, this.GridSize);
                                this.RemovePixelsFromHistogram(rowSpan, histogram, this.LuminanceLevels);

                                // Add new bottom row to the histogram, mirroring rows which exceeds the borders.
                                rowSpan = this.GetPixelRow(source, x - halfGridSize, y + halfGridSize, this.GridSize);
                                this.AddPixelsTooHistogram(rowSpan, histogram, this.LuminanceLevels);
                            }
                        }
                    });

                Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
            }
        }

        /// <summary>
        /// Get the a pixel row at a given position with a length of the grid size. Mirrors pixels which exceeds the edges.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="x">The x position.</param>
        /// <param name="y">The y position.</param>
        /// <param name="gridSize">The grid size.</param>
        /// <returns>A pixel row of the length of the grid size.</returns>
        private Span<TPixel> GetPixelRow(ImageFrame<TPixel> source, int x, int y, int gridSize)
        {
            if (y < 0)
            {
                y = Math.Abs(y);
            }
            else if (y >= source.Height)
            {
                int diff = y - source.Height;
                y = source.Height - diff - 1;
            }

            // special cases for the left and the right border where GetPixelRowSpan can not be used
            if (x < 0)
            {
                var rowPixels = new List<TPixel>();
                for (int dx = x; dx < x + gridSize; dx++)
                {
                    rowPixels.Add(source[Math.Abs(dx), y]);
                }

                return rowPixels.ToArray();
            }
            else if (x + gridSize > source.Width)
            {
                var rowPixels = new List<TPixel>();
                for (int dx = x; dx < x + gridSize; dx++)
                {
                    if (dx >= source.Width)
                    {
                        int diff = dx - source.Width;
                        rowPixels.Add(source[dx - diff - 1, y]);
                    }
                    else
                    {
                        rowPixels.Add(source[dx, y]);
                    }
                }

                return rowPixels.ToArray();
            }

            return source.GetPixelRowSpan(y).Slice(start: x, length: gridSize);
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
