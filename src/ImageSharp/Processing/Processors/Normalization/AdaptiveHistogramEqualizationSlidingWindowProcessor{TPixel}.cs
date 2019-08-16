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
    internal class AdaptiveHistogramEqualizationSlidingWindowProcessor<TPixel> : HistogramEqualizationProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveHistogramEqualizationSlidingWindowProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.</param>
        /// <param name="clipHistogram">Indicating whether to clip the histogram bins at a specific value.</param>
        /// <param name="clipLimitPercentage">Histogram clip limit in percent of the total pixels in the tile. Histogram bins which exceed this limit, will be capped at this value.</param>
        /// <param name="tiles">The number of tiles the image is split into (horizontal and vertically). Minimum value is 2. Maximum value is 100.</param>
        public AdaptiveHistogramEqualizationSlidingWindowProcessor(int luminanceLevels, bool clipHistogram, float clipLimitPercentage, int tiles)
            : base(luminanceLevels, clipHistogram, clipLimitPercentage)
        {
            Guard.MustBeGreaterThanOrEqualTo(tiles, 2, nameof(tiles));
            Guard.MustBeLessThanOrEqualTo(tiles, 100, nameof(tiles));

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

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = configuration.MaxDegreeOfParallelism };
            int tileWidth = source.Width / this.Tiles;
            int tileHeight = tileWidth;
            int pixelInTile = tileWidth * tileHeight;
            int halfTileHeight = tileHeight / 2;
            int halfTileWidth = halfTileHeight;
            var slidingWindowInfos = new SlidingWindowInfos(tileWidth, tileHeight, halfTileWidth, halfTileHeight, pixelInTile);

            using (Buffer2D<TPixel> targetPixels = configuration.MemoryAllocator.Allocate2D<TPixel>(source.Width, source.Height))
            {
                // Process the inner tiles, which do not require to check the borders.
                Parallel.For(
                    halfTileWidth,
                    source.Width - halfTileWidth,
                    parallelOptions,
                    this.ProcessSlidingWindow(
                        source,
                        memoryAllocator,
                        targetPixels,
                        slidingWindowInfos,
                        yStart: halfTileHeight,
                        yEnd: source.Height - halfTileHeight,
                        useFastPath: true,
                        configuration));

                // Process the left border of the image.
                Parallel.For(
                    0,
                    halfTileWidth,
                    parallelOptions,
                    this.ProcessSlidingWindow(
                        source,
                        memoryAllocator,
                        targetPixels,
                        slidingWindowInfos,
                        yStart: 0,
                        yEnd: source.Height,
                        useFastPath: false,
                        configuration));

                // Process the right border of the image.
                Parallel.For(
                    source.Width - halfTileWidth,
                    source.Width,
                    parallelOptions,
                    this.ProcessSlidingWindow(
                        source,
                        memoryAllocator,
                        targetPixels,
                        slidingWindowInfos,
                        yStart: 0,
                        yEnd: source.Height,
                        useFastPath: false,
                        configuration));

                // Process the top border of the image.
                Parallel.For(
                    halfTileWidth,
                    source.Width - halfTileWidth,
                    parallelOptions,
                    this.ProcessSlidingWindow(
                        source,
                        memoryAllocator,
                        targetPixels,
                        slidingWindowInfos,
                        yStart: 0,
                        yEnd: halfTileHeight,
                        useFastPath: false,
                        configuration));

                // Process the bottom border of the image.
                Parallel.For(
                    halfTileWidth,
                    source.Width - halfTileWidth,
                    parallelOptions,
                    this.ProcessSlidingWindow(
                        source,
                        memoryAllocator,
                        targetPixels,
                        slidingWindowInfos,
                        yStart: source.Height - halfTileHeight,
                        yEnd: source.Height,
                        useFastPath: false,
                        configuration));

                Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
            }
        }

        /// <summary>
        /// Applies the sliding window equalization to one column of the image. The window is moved from top to bottom.
        /// Moving the window one pixel down requires to remove one row from the top of the window from the histogram and
        /// adding a new row at the bottom.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="targetPixels">The target pixels.</param>
        /// <param name="swInfos"><see cref="SlidingWindowInfos"/> about the sliding window dimensions.</param>
        /// <param name="yStart">The y start position.</param>
        /// <param name="yEnd">The y end position.</param>
        /// <param name="useFastPath">if set to true the borders of the image will not be checked.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>Action Delegate.</returns>
        private Action<int> ProcessSlidingWindow(
            ImageFrame<TPixel> source,
            MemoryAllocator memoryAllocator,
            Buffer2D<TPixel> targetPixels,
            SlidingWindowInfos swInfos,
            int yStart,
            int yEnd,
            bool useFastPath,
            Configuration configuration)
        {
            return x =>
                {
                    using (IMemoryOwner<int> histogramBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
                    using (IMemoryOwner<int> histogramBufferCopy = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
                    using (IMemoryOwner<int> cdfBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
                    using (IMemoryOwner<Vector4> pixelRowBuffer = memoryAllocator.Allocate<Vector4>(swInfos.TileWidth, AllocationOptions.Clean))
                    {
                        Span<int> histogram = histogramBuffer.GetSpan();
                        ref int histogramBase = ref MemoryMarshal.GetReference(histogram);

                        Span<int> histogramCopy = histogramBufferCopy.GetSpan();
                        ref int histogramCopyBase = ref MemoryMarshal.GetReference(histogramCopy);

                        ref int cdfBase = ref MemoryMarshal.GetReference(cdfBuffer.GetSpan());

                        Span<Vector4> pixelRow = pixelRowBuffer.GetSpan();
                        ref Vector4 pixelRowBase = ref MemoryMarshal.GetReference(pixelRow);

                        // Build the initial histogram of grayscale values.
                        for (int dy = yStart - swInfos.HalfTileHeight; dy < yStart + swInfos.HalfTileHeight; dy++)
                        {
                            if (useFastPath)
                            {
                                this.CopyPixelRowFast(source, pixelRow, x - swInfos.HalfTileWidth, dy, swInfos.TileWidth, configuration);
                            }
                            else
                            {
                                this.CopyPixelRow(source, pixelRow, x - swInfos.HalfTileWidth, dy, swInfos.TileWidth, configuration);
                            }

                            this.AddPixelsToHistogram(ref pixelRowBase, ref histogramBase, this.LuminanceLevels, pixelRow.Length);
                        }

                        for (int y = yStart; y < yEnd; y++)
                        {
                            if (this.ClipHistogramEnabled)
                            {
                                // Clipping the histogram, but doing it on a copy to keep the original un-clipped values for the next iteration.
                                histogram.CopyTo(histogramCopy);
                                this.ClipHistogram(histogramCopy, this.ClipLimitPercentage, swInfos.PixelInTile);
                            }

                            // Calculate the cumulative distribution function, which will map each input pixel in the current tile to a new value.
                            int cdfMin = this.ClipHistogramEnabled
                                             ? this.CalculateCdf(ref cdfBase, ref histogramCopyBase, histogram.Length - 1)
                                             : this.CalculateCdf(ref cdfBase, ref histogramBase, histogram.Length - 1);

                            float numberOfPixelsMinusCdfMin = swInfos.PixelInTile - cdfMin;

                            // Map the current pixel to the new equalized value.
                            int luminance = GetLuminance(source[x, y], this.LuminanceLevels);
                            float luminanceEqualized = Unsafe.Add(ref cdfBase, luminance) / numberOfPixelsMinusCdfMin;
                            targetPixels[x, y].FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, source[x, y].ToVector4().W));

                            // Remove top most row from the histogram, mirroring rows which exceeds the borders.
                            if (useFastPath)
                            {
                                this.CopyPixelRowFast(source, pixelRow, x - swInfos.HalfTileWidth, y - swInfos.HalfTileWidth, swInfos.TileWidth, configuration);
                            }
                            else
                            {
                                this.CopyPixelRow(source, pixelRow, x - swInfos.HalfTileWidth, y - swInfos.HalfTileWidth, swInfos.TileWidth, configuration);
                            }

                            this.RemovePixelsFromHistogram(ref pixelRowBase, ref histogramBase, this.LuminanceLevels, pixelRow.Length);

                            // Add new bottom row to the histogram, mirroring rows which exceeds the borders.
                            if (useFastPath)
                            {
                                this.CopyPixelRowFast(source, pixelRow, x - swInfos.HalfTileWidth, y + swInfos.HalfTileWidth, swInfos.TileWidth, configuration);
                            }
                            else
                            {
                                this.CopyPixelRow(source, pixelRow, x - swInfos.HalfTileWidth, y + swInfos.HalfTileWidth, swInfos.TileWidth, configuration);
                            }

                            this.AddPixelsToHistogram(ref pixelRowBase, ref histogramBase, this.LuminanceLevels, pixelRow.Length);
                        }
                    }
                };
        }

        /// <summary>
        /// Get the a pixel row at a given position with a length of the tile width. Mirrors pixels which exceeds the edges.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="rowPixels">Pre-allocated pixel row span of the size of a the tile width.</param>
        /// <param name="x">The x position.</param>
        /// <param name="y">The y position.</param>
        /// <param name="tileWidth">The width in pixels of a tile.</param>
        /// <param name="configuration">The configuration.</param>
        private void CopyPixelRow(
            ImageFrame<TPixel> source,
            Span<Vector4> rowPixels,
            int x,
            int y,
            int tileWidth,
            Configuration configuration)
        {
            if (y < 0)
            {
                y = ImageMaths.FastAbs(y);
            }
            else if (y >= source.Height)
            {
                int diff = y - source.Height;
                y = source.Height - diff - 1;
            }

            // Special cases for the left and the right border where GetPixelRowSpan can not be used.
            if (x < 0)
            {
                rowPixels.Clear();
                int idx = 0;
                for (int dx = x; dx < x + tileWidth; dx++)
                {
                    rowPixels[idx] = source[ImageMaths.FastAbs(dx), y].ToVector4();
                    idx++;
                }

                return;
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
                        rowPixels[idx] = source[dx - diff - 1, y].ToVector4();
                    }
                    else
                    {
                        rowPixels[idx] = source[dx, y].ToVector4();
                    }

                    idx++;
                }

                return;
            }

            this.CopyPixelRowFast(source, rowPixels, x, y, tileWidth, configuration);
        }

        /// <summary>
        /// Get the a pixel row at a given position with a length of the tile width.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="rowPixels">Pre-allocated pixel row span of the size of a the tile width.</param>
        /// <param name="x">The x position.</param>
        /// <param name="y">The y position.</param>
        /// <param name="tileWidth">The width in pixels of a tile.</param>
        /// <param name="configuration">The configuration.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void CopyPixelRowFast(
            ImageFrame<TPixel> source,
            Span<Vector4> rowPixels,
            int x,
            int y,
            int tileWidth,
            Configuration configuration)
            => PixelOperations<TPixel>.Instance.ToVector4(configuration, source.GetPixelRowSpan(y).Slice(start: x, length: tileWidth), rowPixels);

        /// <summary>
        /// Adds a column of grey values to the histogram.
        /// </summary>
        /// <param name="greyValuesBase">The reference to the span of grey values to add.</param>
        /// <param name="histogramBase">The reference to the histogram span.</param>
        /// <param name="luminanceLevels">The number of different luminance levels.</param>
        /// <param name="length">The grey values span length.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void AddPixelsToHistogram(ref Vector4 greyValuesBase, ref int histogramBase, int luminanceLevels, int length)
        {
            for (int idx = 0; idx < length; idx++)
            {
                int luminance = GetLuminance(ref Unsafe.Add(ref greyValuesBase, idx), luminanceLevels);
                Unsafe.Add(ref histogramBase, luminance)++;
            }
        }

        /// <summary>
        /// Removes a column of grey values from the histogram.
        /// </summary>
        /// <param name="greyValuesBase">The reference to the span of grey values to remove.</param>
        /// <param name="histogramBase">The reference to the histogram span.</param>
        /// <param name="luminanceLevels">The number of different luminance levels.</param>
        /// <param name="length">The grey values span length.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void RemovePixelsFromHistogram(ref Vector4 greyValuesBase, ref int histogramBase, int luminanceLevels, int length)
        {
            for (int idx = 0; idx < length; idx++)
            {
                int luminance = GetLuminance(ref Unsafe.Add(ref greyValuesBase, idx), luminanceLevels);
                Unsafe.Add(ref histogramBase, luminance)--;
            }
        }

        private class SlidingWindowInfos
        {
            public SlidingWindowInfos(int tileWidth, int tileHeight, int halfTileWidth, int halfTileHeight, int pixelInTile)
            {
                this.TileWidth = tileWidth;
                this.TileHeight = tileHeight;
                this.HalfTileWidth = halfTileWidth;
                this.HalfTileHeight = halfTileHeight;
                this.PixelInTile = pixelInTile;
            }

            public int TileWidth { get; private set; }

            public int TileHeight { get; private set; }

            public int PixelInTile { get; private set; }

            public int HalfTileWidth { get; private set; }

            public int HalfTileHeight { get; private set; }
        }
    }
}
