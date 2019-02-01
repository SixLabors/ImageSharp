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
                    source.Height,
                    parallelOptions,
                    y =>
                    {
                        using (IMemoryOwner<int> histogramBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
                        using (IMemoryOwner<int> histogramBufferCopy = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
                        using (IMemoryOwner<int> cdfBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
                        using (IMemoryOwner<TPixel> pixelColumnBuffer = memoryAllocator.Allocate<TPixel>(tileHeight, AllocationOptions.Clean))
                        {
                            Span<int> histogram = histogramBuffer.GetSpan();
                            ref int histogramBase = ref MemoryMarshal.GetReference(histogram);
                            Span<int> histogramCopy = histogramBufferCopy.GetSpan();
                            ref int histogramCopyBase = ref MemoryMarshal.GetReference(histogramCopy);
                            ref int cdfBase = ref MemoryMarshal.GetReference(cdfBuffer.GetSpan());

                            Span<TPixel> pixelColumn = pixelColumnBuffer.GetSpan();

                            // Build the histogram of grayscale values for the current tile.
                            for (int dx = -halfTileWidth; dx < halfTileWidth; dx++)
                            {
                                Span<TPixel> columnSpan = this.GetPixelColumn(source, pixelColumn, dx, y - halfTileHeight, tileHeight);
                                this.AddPixelsToHistogram(columnSpan, histogram, this.LuminanceLevels);
                            }

                            for (int x = 0; x < source.Width; x++)
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

                                // Map the current pixel to the new equalized value.
                                int luminance = GetLuminance(source[x, y], this.LuminanceLevels);
                                float luminanceEqualized = Unsafe.Add(ref cdfBase, luminance) / numberOfPixelsMinusCdfMin;
                                targetPixels[x, y].FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, source[x, y].ToVector4().W));

                                // Remove left most column from the histogram, mirroring columns which exceeds the borders of the image.
                                Span<TPixel> columnSpan = this.GetPixelColumn(source, pixelColumn, x - halfTileWidth, y - halfTileHeight, tileHeight);
                                this.RemovePixelsFromHistogram(columnSpan, histogram, this.LuminanceLevels);

                                // Add new right column to the histogram, mirroring columns which exceeds the borders of the image.
                                columnSpan = this.GetPixelColumn(source, pixelColumn, x + halfTileWidth, y - halfTileHeight, tileHeight);
                                this.AddPixelsToHistogram(columnSpan, histogram, this.LuminanceLevels);
                            }
                        }
                    });

                Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
            }
        }

        /// <summary>
        /// Get the a pixel column at a given position with the size of the tile height. Mirrors pixels which exceeds the edges of the image.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="columnPixels">Pre-allocated pixel span of the size of a tile height.</param>
        /// <param name="x">The x position.</param>
        /// <param name="y">The y position.</param>
        /// <param name="tileHeight">The height in pixels of a tile.</param>
        /// <returns>A pixel row of the length of the tile width.</returns>
        private Span<TPixel> GetPixelColumn(ImageFrame<TPixel> source, Span<TPixel> columnPixels, int x, int y, int tileHeight)
        {
            if (x < 0)
            {
                x = Math.Abs(x);
            }
            else if (x >= source.Width)
            {
                int diff = x - source.Width;
                x = source.Width - diff - 1;
            }

            int idx = 0;
            if (y < 0)
            {
                for (int dy = y; dy < y + tileHeight; dy++)
                {
                    columnPixels[idx] = source[x, Math.Abs(dy)];
                    idx++;
                }
            }
            else if (y + tileHeight > source.Height)
            {
                for (int dy = y; dy < y + tileHeight; dy++)
                {
                    if (dy >= source.Height)
                    {
                        int diff = dy - source.Height;
                        columnPixels[idx] = source[x, dy - diff - 1];
                    }
                    else
                    {
                        columnPixels[idx] = source[x, dy];
                    }

                    idx++;
                }
            }
            else
            {
                for (int dy = y; dy < y + tileHeight; dy++)
                {
                    columnPixels[idx] = source[x, dy];
                    idx++;
                }
            }

            return columnPixels;
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
