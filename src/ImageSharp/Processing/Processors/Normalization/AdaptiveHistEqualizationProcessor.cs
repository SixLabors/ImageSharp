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
            int xtiles = Convert.ToInt32(Math.Ceiling(source.Width / (double)this.GridSize));
            int ytiles = Convert.ToInt32(Math.Ceiling(source.Height / (double)this.GridSize));

            var cdfData = new CdfData[xtiles, ytiles];
            using (System.Buffers.IMemoryOwner<int> histogramBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
            using (System.Buffers.IMemoryOwner<int> cdfBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
            {
                Span<int> histogram = histogramBuffer.GetSpan();
                Span<int> cdf = cdfBuffer.GetSpan();

                // The image is split up in square tiles of the size of the parameter GridSize.
                // For each tile the cumulative distribution function will be calculated.
                int cdfPosX = 0;
                int cdfPosY = 0;
                for (int y = 0; y < source.Height; y += this.GridSize)
                {
                    cdfPosX = 0;
                    for (int x = 0; x < source.Width; x += this.GridSize)
                    {
                        histogram.Clear();
                        cdf.Clear();
                        int ylimit = Math.Min(y + this.GridSize, source.Height);
                        int xlimit = Math.Min(x + this.GridSize, source.Width);
                        for (int dy = y; dy < ylimit; dy++)
                        {
                            for (int dx = x; dx < xlimit; dx++)
                            {
                                int luminace = this.GetLuminance(source[dx, dy], this.LuminanceLevels);
                                histogram[luminace]++;
                            }
                        }

                        if (this.ClipHistogramEnabled)
                        {
                            this.ClipHistogram(histogram, this.ClipLimitPercentage, pixelsInGrid);
                        }

                        int cdfMin = this.CalculateCdf(cdf, histogram, histogram.Length - 1);
                        var currentCdf = new CdfData(cdf.ToArray(), cdfMin);
                        cdfData[cdfPosX, cdfPosY] = currentCdf;

                        cdfPosX++;
                    }

                    cdfPosY++;
                }

                int tilePosX = 0;
                int tilePosY = 0;
                for (int y = halfGridSize; y < source.Height - halfGridSize; y += this.GridSize)
                {
                    tilePosX = 0;
                    for (int x = halfGridSize; x < source.Width - halfGridSize; x += this.GridSize)
                    {
                        int gridPosX = 0;
                        int gridPosY = 0;
                        int ylimit = Math.Min(y + this.GridSize, source.Height);
                        int xlimit = Math.Min(x + this.GridSize, source.Width);
                        for (int dy = y; dy < ylimit; dy++)
                        {
                            gridPosX = 0;
                            for (int dx = x; dx < xlimit; dx++)
                            {
                                TPixel sourcePixel = source[dx, dy];
                                int luminace = this.GetLuminance(sourcePixel, this.LuminanceLevels);

                                float cdfLeftTopLuminance = cdfData[tilePosX, tilePosY].RemapGreyValue(luminace, pixelsInGrid);
                                float cdfRightTopLuminance = cdfData[tilePosX + 1, tilePosY].RemapGreyValue(luminace, pixelsInGrid);
                                float cdfLeftBottomLuminance = cdfData[tilePosX, tilePosY + 1].RemapGreyValue(luminace, pixelsInGrid);
                                float cdfRightBottomLuminance = cdfData[tilePosX + 1, tilePosY + 1].RemapGreyValue(luminace, pixelsInGrid);

                                float luminanceEqualized = this.BilinearInterpolation(gridPosX, gridPosY, this.GridSize, cdfLeftTopLuminance, cdfRightTopLuminance, cdfLeftBottomLuminance, cdfRightBottomLuminance);
                                pixels[(dy * source.Width) + dx].PackFromVector4(new Vector4(luminanceEqualized));
                                gridPosX++;
                            }

                            gridPosY++;
                        }

                        tilePosX++;
                    }

                    tilePosY++;
                }
            }
        }

        /// <summary>
        /// Bilinear interpolation between four tiles.
        /// </summary>
        /// <param name="x">X position.</param>
        /// <param name="y">Y position.</param>
        /// <param name="gridSize">The size of the grid.</param>
        /// <param name="lt">Luminance from tile top left.</param>
        /// <param name="rt">Luminance from tile right top.</param>
        /// <param name="lb">Luminance from tile left bottom.</param>
        /// <param name="rb">Luminance from tile right bottom.</param>
        /// <returns>Interpolated Luminance.</returns>
        private float BilinearInterpolation(int x, int y, int gridSize, float lt, float rt, float lb, float rb)
        {
            float r1 = ((gridSize - x) / (float)gridSize * lb) + ((x / (float)gridSize) * rb);
            float r2 = ((gridSize - x) / (float)gridSize * lt) + ((x / (float)gridSize) * rt);

            float res = ((y / ((float)gridSize)) * r1) + (((y - gridSize) / (float)(-gridSize)) * r2);

            return res;
        }

        private class CdfData
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CdfData"/> class.
            /// </summary>
            /// <param name="cdf">The cumulative distribution function, which remaps the grey values.</param>
            /// <param name="cdfMin">The minimum value of the cdf.</param>
            public CdfData(int[] cdf, int cdfMin)
            {
                this.Cdf = cdf;
                this.CdfMin = cdfMin;
            }

            /// <summary>
            /// Gets the CDF.
            /// </summary>
            public int[] Cdf { get; }

            /// <summary>
            /// Gets minimum value of the cdf.
            /// </summary>
            public int CdfMin { get; }

            /// <summary>
            /// Remaps the grey value with the cdf.
            /// </summary>
            /// <param name="luminance">The original luminance.</param>
            /// <param name="pixelsInGrid">The pixels in grid.</param>
            /// <returns>The remapped luminance.</returns>
            public float RemapGreyValue(int luminance, int pixelsInGrid)
            {
                return (pixelsInGrid - this.CdfMin) == 0 ? this.Cdf[luminance] / (float)pixelsInGrid : this.Cdf[luminance] / (float)(pixelsInGrid - this.CdfMin);
            }
        }
    }
}
