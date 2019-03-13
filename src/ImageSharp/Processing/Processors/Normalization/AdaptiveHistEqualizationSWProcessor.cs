// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Applies an adaptive histogram equalization to the image using an sliding window approach.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class AdaptiveHistEqualizationSWProcessor<TPixel> : HistogramEqualizationProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveHistEqualizationSWProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.</param>
        /// <param name="clipHistogram">Indicating whether to clip the histogram bins at a specific value.</param>
        /// <param name="clipLimitPercentage">Histogram clip limit in percent of the total pixels in the tile. Histogram bins which exceed this limit, will be capped at this value.</param>
        /// <param name="tiles">The number of tiles the image is split into (horizontal and vertically). Minimum value is 2.</param>
        public AdaptiveHistEqualizationSWProcessor(int luminanceLevels, bool clipHistogram, float clipLimitPercentage, int tiles)
            : base(luminanceLevels, clipHistogram, clipLimitPercentage)
        {
            Guard.MustBeGreaterThanOrEqualTo(tiles, 2, nameof(tiles));

            this.Tiles = tiles;
        }

        /// <summary>
        /// Gets the number of tiles the image is split into (horizontal and vertically) for the adaptive histogram equalization.
        /// </summary>
        private int Tiles { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            MemoryAllocator memoryAllocator = configuration.MemoryAllocator;
            int numberOfPixels = source.Width * source.Height;
            Span<TPixel> pixels = source.GetPixelSpan();

            var parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = configuration.MaxDegreeOfParallelism };
            int tileWidth = source.Width / this.Tiles;
            int tileHeight = tileWidth;
            int pixeInTile = tileWidth * tileHeight;
            int halfTileHeight = tileHeight / 2;
            int halfTileWidth = halfTileHeight;
            using (Buffer2D<TPixel> targetPixels = configuration.MemoryAllocator.Allocate2D<TPixel>(source.Width, source.Height))
            {
                Parallel.For(
                    0,
                    source.Width,
                    parallelOptions,
                    x =>
                    {
                        using (IMemoryOwner<int> histogramBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
                        using (IMemoryOwner<int> histogramBufferCopy = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
                        using (IMemoryOwner<int> cdfBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
                        using (IMemoryOwner<TPixel> pixelRowBuffer = memoryAllocator.Allocate<TPixel>(tileWidth, AllocationOptions.Clean))
                        {
                            Span<int> histogram = histogramBuffer.GetSpan();
                            ref int histogramBase = ref MemoryMarshal.GetReference(histogram);
                            Span<int> histogramCopy = histogramBufferCopy.GetSpan();
                            ref int histogramCopyBase = ref MemoryMarshal.GetReference(histogramCopy);
                            ref int cdfBase = ref MemoryMarshal.GetReference(cdfBuffer.GetSpan());
                            Span<TPixel> pixelRow = pixelRowBuffer.GetSpan();

                            // Build the histogram of grayscale values for the current tile.
                            for (int dy = -halfTileHeight; dy < halfTileHeight; dy++)
                            {
                                Span<TPixel> rowSpan = this.GetPixelRow(source, pixelRow, x - halfTileWidth, dy, tileWidth);
                                this.AddPixelsToHistogram(rowSpan, histogram, this.LuminanceLevels);
                            }

                            for (int y = 0; y < source.Height; y++)
                            {
                                if (this.ClipHistogramEnabled)
                                {
                                    // Clipping the histogram, but doing it on a copy to keep the original un-clipped values for the next iteration.
                                    histogram.CopyTo(histogramCopy);
                                    this.ClipHistogram(histogramCopy, this.ClipLimitPercentage, pixeInTile);
                                }

                                // Calculate the cumulative distribution function, which will map each input pixel in the current tile to a new value.
                                int cdfMin = this.ClipHistogramEnabled
                                ? this.CalculateCdf(ref cdfBase, ref histogramCopyBase, histogram.Length - 1)
                                : this.CalculateCdf(ref cdfBase, ref histogramBase, histogram.Length - 1);

                                float numberOfPixelsMinusCdfMin = pixeInTile - cdfMin;

                                // Map the current pixel to the new equalized value
                                int luminance = GetLuminance(source[x, y], this.LuminanceLevels);
                                float luminanceEqualized = Unsafe.Add(ref cdfBase, luminance) / numberOfPixelsMinusCdfMin;
                                targetPixels[x, y].FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, source[x, y].ToVector4().W));

                                // Remove top most row from the histogram, mirroring rows which exceeds the borders.
                                Span<TPixel> rowSpan = this.GetPixelRow(source, pixelRow, x - halfTileWidth, y - halfTileWidth, tileWidth);
                                this.RemovePixelsFromHistogram(rowSpan, histogram, this.LuminanceLevels);

                                // Add new bottom row to the histogram, mirroring rows which exceeds the borders.
                                rowSpan = this.GetPixelRow(source, pixelRow, x - halfTileWidth, y + halfTileWidth, tileWidth);
                                this.AddPixelsToHistogram(rowSpan, histogram, this.LuminanceLevels);
                            }
                        }
                    });

                Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
            }
        }

        /// <summary>
        /// Get the a pixel row at a given position with a length of the tile width. Mirrors pixels which exceeds the edges.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="rowPixels">Pre-allocated pixel row span of the size of a the tile width.</param>
        /// <param name="x">The x position.</param>
        /// <param name="y">The y position.</param>
        /// <param name="tileWidth">The width in pixels of a tile.</param>
        /// <returns>A pixel row of the length of the tile width.</returns>
        private Span<TPixel> GetPixelRow(ImageFrame<TPixel> source, Span<TPixel> rowPixels, int x, int y, int tileWidth)
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

            // Special cases for the left and the right border where GetPixelRowSpan can not be used
            if (x < 0)
            {
                rowPixels.Clear();
                int idx = 0;
                for (int dx = x; dx < x + tileWidth; dx++)
                {
                    rowPixels[idx] = source[Math.Abs(dx), y];
                    idx++;
                }

                return rowPixels;
            }
            else if (x + tileWidth > source.Width)
            {
                rowPixels.Clear();
                int idx = 0;
                for (int dx = x; dx < x + tileWidth; dx++)
                {
                    if (dx >= source.Width)
                    {
                        int diff = dx - source.Width;
                        rowPixels[idx] = source[dx - diff - 1, y];
                    }
                    else
                    {
                        rowPixels[idx] = source[dx, y];
                    }

                    idx++;
                }

                return rowPixels;
            }

            return source.GetPixelRowSpan(y).Slice(start: x, length: tileWidth);
        }

        /// <summary>
        /// Adds a column of grey values to the histogram.
        /// </summary>
        /// <param name="greyValues">The grey values to add.</param>
        /// <param name="histogram">The histogram.</param>
        /// <param name="luminanceLevels">The number of different luminance levels.</param>
        private void AddPixelsToHistogram(Span<TPixel> greyValues, Span<int> histogram, int luminanceLevels)
        {
            for (int idx = 0; idx < greyValues.Length; idx++)
            {
                int luminance = GetLuminance(greyValues[idx], luminanceLevels);
                histogram[luminance]++;
            }
        }

        /// <summary>
        /// Removes a column of grey values from the histogram.
        /// </summary>
        /// <param name="greyValues">The grey values to remove.</param>
        /// <param name="histogram">The histogram.</param>
        /// <param name="luminanceLevels">The number of different luminance levels.</param>
        private void RemovePixelsFromHistogram(Span<TPixel> greyValues, Span<int> histogram, int luminanceLevels)
        {
            for (int idx = 0; idx < greyValues.Length; idx++)
            {
                int luminance = GetLuminance(greyValues[idx], luminanceLevels);
                histogram[luminance]--;
            }
        }
    }
}
