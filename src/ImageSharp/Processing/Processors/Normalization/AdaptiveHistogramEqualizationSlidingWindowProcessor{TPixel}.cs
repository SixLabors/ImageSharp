// Copyright (c) Six Labors.
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

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Applies an adaptive histogram equalization to the image using an sliding window approach.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class AdaptiveHistogramEqualizationSlidingWindowProcessor<TPixel> : HistogramEqualizationProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveHistogramEqualizationSlidingWindowProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.</param>
        /// <param name="clipHistogram">Indicating whether to clip the histogram bins at a specific value.</param>
        /// <param name="clipLimit">The histogram clip limit. Histogram bins which exceed this limit, will be capped at this value.</param>
        /// <param name="tiles">The number of tiles the image is split into (horizontal and vertically). Minimum value is 2. Maximum value is 100.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public AdaptiveHistogramEqualizationSlidingWindowProcessor(
            Configuration configuration,
            int luminanceLevels,
            bool clipHistogram,
            int clipLimit,
            int tiles,
            Image<TPixel> source,
            Rectangle sourceRectangle)
            : base(configuration, luminanceLevels, clipHistogram, clipLimit, source, sourceRectangle)
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
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            MemoryAllocator memoryAllocator = this.Configuration.MemoryAllocator;

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = this.Configuration.MaxDegreeOfParallelism };
            int tileWidth = source.Width / this.Tiles;
            int tileHeight = tileWidth;
            int pixelInTile = tileWidth * tileHeight;
            int halfTileHeight = tileHeight / 2;
            int halfTileWidth = halfTileHeight;
            var slidingWindowInfos = new SlidingWindowInfos(tileWidth, tileHeight, halfTileWidth, halfTileHeight, pixelInTile);

            // TODO: If the process was able to be switched to operate in parallel rows instead of columns
            // then we could take advantage of batching and allocate per-row buffers only once per batch.
            using Buffer2D<TPixel> targetPixels = this.Configuration.MemoryAllocator.Allocate2D<TPixel>(source.Width, source.Height);

            // Process the inner tiles, which do not require to check the borders.
            var innerOperation = new SlidingWindowOperation(
                    this.Configuration,
                    this,
                    source,
                    memoryAllocator,
                    targetPixels,
                    slidingWindowInfos,
                    yStart: halfTileHeight,
                    yEnd: source.Height - halfTileHeight,
                    useFastPath: true);

            Parallel.For(
                halfTileWidth,
                source.Width - halfTileWidth,
                parallelOptions,
                innerOperation.Invoke);

            // Process the left border of the image.
            var leftBorderOperation = new SlidingWindowOperation(
                    this.Configuration,
                    this,
                    source,
                    memoryAllocator,
                    targetPixels,
                    slidingWindowInfos,
                    yStart: 0,
                    yEnd: source.Height,
                    useFastPath: false);

            Parallel.For(
                0,
                halfTileWidth,
                parallelOptions,
                leftBorderOperation.Invoke);

            // Process the right border of the image.
            var rightBorderOperation = new SlidingWindowOperation(
                    this.Configuration,
                    this,
                    source,
                    memoryAllocator,
                    targetPixels,
                    slidingWindowInfos,
                    yStart: 0,
                    yEnd: source.Height,
                    useFastPath: false);

            Parallel.For(
                source.Width - halfTileWidth,
                source.Width,
                parallelOptions,
                rightBorderOperation.Invoke);

            // Process the top border of the image.
            var topBorderOperation = new SlidingWindowOperation(
                    this.Configuration,
                    this,
                    source,
                    memoryAllocator,
                    targetPixels,
                    slidingWindowInfos,
                    yStart: 0,
                    yEnd: halfTileHeight,
                    useFastPath: false);

            Parallel.For(
                halfTileWidth,
                source.Width - halfTileWidth,
                parallelOptions,
                topBorderOperation.Invoke);

            // Process the bottom border of the image.
            var bottomBorderOperation = new SlidingWindowOperation(
                    this.Configuration,
                    this,
                    source,
                    memoryAllocator,
                    targetPixels,
                    slidingWindowInfos,
                    yStart: source.Height - halfTileHeight,
                    yEnd: source.Height,
                    useFastPath: false);

            Parallel.For(
                halfTileWidth,
                source.Width - halfTileWidth,
                parallelOptions,
                bottomBorderOperation.Invoke);

            Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
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
                y = Numerics.Abs(y);
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
                    rowPixels[idx] = source[Numerics.Abs(dx), y].ToVector4();
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
                int luminance = ColorNumerics.GetBT709Luminance(ref Unsafe.Add(ref greyValuesBase, idx), luminanceLevels);
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
                int luminance = ColorNumerics.GetBT709Luminance(ref Unsafe.Add(ref greyValuesBase, idx), luminanceLevels);
                Unsafe.Add(ref histogramBase, luminance)--;
            }
        }

        /// <summary>
        /// Applies the sliding window equalization to one column of the image. The window is moved from top to bottom.
        /// Moving the window one pixel down requires to remove one row from the top of the window from the histogram and
        /// adding a new row at the bottom.
        /// </summary>
        private readonly struct SlidingWindowOperation
        {
            private readonly Configuration configuration;
            private readonly AdaptiveHistogramEqualizationSlidingWindowProcessor<TPixel> processor;
            private readonly ImageFrame<TPixel> source;
            private readonly MemoryAllocator memoryAllocator;
            private readonly Buffer2D<TPixel> targetPixels;
            private readonly SlidingWindowInfos swInfos;
            private readonly int yStart;
            private readonly int yEnd;
            private readonly bool useFastPath;

            /// <summary>
            /// Initializes a new instance of the <see cref="SlidingWindowOperation"/> struct.
            /// </summary>
            /// <param name="configuration">The configuration.</param>
            /// <param name="processor">The histogram processor.</param>
            /// <param name="source">The source image.</param>
            /// <param name="memoryAllocator">The memory allocator.</param>
            /// <param name="targetPixels">The target pixels.</param>
            /// <param name="swInfos"><see cref="SlidingWindowInfos"/> about the sliding window dimensions.</param>
            /// <param name="yStart">The y start position.</param>
            /// <param name="yEnd">The y end position.</param>
            /// <param name="useFastPath">if set to true the borders of the image will not be checked.</param>
            [MethodImpl(InliningOptions.ShortMethod)]
            public SlidingWindowOperation(
                Configuration configuration,
                AdaptiveHistogramEqualizationSlidingWindowProcessor<TPixel> processor,
                ImageFrame<TPixel> source,
                MemoryAllocator memoryAllocator,
                Buffer2D<TPixel> targetPixels,
                SlidingWindowInfos swInfos,
                int yStart,
                int yEnd,
                bool useFastPath)
            {
                this.configuration = configuration;
                this.processor = processor;
                this.source = source;
                this.memoryAllocator = memoryAllocator;
                this.targetPixels = targetPixels;
                this.swInfos = swInfos;
                this.yStart = yStart;
                this.yEnd = yEnd;
                this.useFastPath = useFastPath;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int x)
            {
                using (IMemoryOwner<int> histogramBuffer = this.memoryAllocator.Allocate<int>(this.processor.LuminanceLevels, AllocationOptions.Clean))
                using (IMemoryOwner<int> histogramBufferCopy = this.memoryAllocator.Allocate<int>(this.processor.LuminanceLevels, AllocationOptions.Clean))
                using (IMemoryOwner<int> cdfBuffer = this.memoryAllocator.Allocate<int>(this.processor.LuminanceLevels, AllocationOptions.Clean))
                using (IMemoryOwner<Vector4> pixelRowBuffer = this.memoryAllocator.Allocate<Vector4>(this.swInfos.TileWidth, AllocationOptions.Clean))
                {
                    Span<int> histogram = histogramBuffer.GetSpan();
                    ref int histogramBase = ref MemoryMarshal.GetReference(histogram);

                    Span<int> histogramCopy = histogramBufferCopy.GetSpan();
                    ref int histogramCopyBase = ref MemoryMarshal.GetReference(histogramCopy);

                    ref int cdfBase = ref MemoryMarshal.GetReference(cdfBuffer.GetSpan());

                    Span<Vector4> pixelRow = pixelRowBuffer.GetSpan();
                    ref Vector4 pixelRowBase = ref MemoryMarshal.GetReference(pixelRow);

                    // Build the initial histogram of grayscale values.
                    for (int dy = this.yStart - this.swInfos.HalfTileHeight; dy < this.yStart + this.swInfos.HalfTileHeight; dy++)
                    {
                        if (this.useFastPath)
                        {
                            this.processor.CopyPixelRowFast(this.source, pixelRow, x - this.swInfos.HalfTileWidth, dy, this.swInfos.TileWidth, this.configuration);
                        }
                        else
                        {
                            this.processor.CopyPixelRow(this.source, pixelRow, x - this.swInfos.HalfTileWidth, dy, this.swInfos.TileWidth, this.configuration);
                        }

                        this.processor.AddPixelsToHistogram(ref pixelRowBase, ref histogramBase, this.processor.LuminanceLevels, pixelRow.Length);
                    }

                    for (int y = this.yStart; y < this.yEnd; y++)
                    {
                        if (this.processor.ClipHistogramEnabled)
                        {
                            // Clipping the histogram, but doing it on a copy to keep the original un-clipped values for the next iteration.
                            histogram.CopyTo(histogramCopy);
                            this.processor.ClipHistogram(histogramCopy, this.processor.ClipLimit);
                        }

                        // Calculate the cumulative distribution function, which will map each input pixel in the current tile to a new value.
                        int cdfMin = this.processor.ClipHistogramEnabled
                                         ? this.processor.CalculateCdf(ref cdfBase, ref histogramCopyBase, histogram.Length - 1)
                                         : this.processor.CalculateCdf(ref cdfBase, ref histogramBase, histogram.Length - 1);

                        float numberOfPixelsMinusCdfMin = this.swInfos.PixelInTile - cdfMin;

                        // Map the current pixel to the new equalized value.
                        int luminance = GetLuminance(this.source[x, y], this.processor.LuminanceLevels);
                        float luminanceEqualized = Unsafe.Add(ref cdfBase, luminance) / numberOfPixelsMinusCdfMin;
                        this.targetPixels[x, y].FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, this.source[x, y].ToVector4().W));

                        // Remove top most row from the histogram, mirroring rows which exceeds the borders.
                        if (this.useFastPath)
                        {
                            this.processor.CopyPixelRowFast(this.source, pixelRow, x - this.swInfos.HalfTileWidth, y - this.swInfos.HalfTileWidth, this.swInfos.TileWidth, this.configuration);
                        }
                        else
                        {
                            this.processor.CopyPixelRow(this.source, pixelRow, x - this.swInfos.HalfTileWidth, y - this.swInfos.HalfTileWidth, this.swInfos.TileWidth, this.configuration);
                        }

                        this.processor.RemovePixelsFromHistogram(ref pixelRowBase, ref histogramBase, this.processor.LuminanceLevels, pixelRow.Length);

                        // Add new bottom row to the histogram, mirroring rows which exceeds the borders.
                        if (this.useFastPath)
                        {
                            this.processor.CopyPixelRowFast(this.source, pixelRow, x - this.swInfos.HalfTileWidth, y + this.swInfos.HalfTileWidth, this.swInfos.TileWidth, this.configuration);
                        }
                        else
                        {
                            this.processor.CopyPixelRow(this.source, pixelRow, x - this.swInfos.HalfTileWidth, y + this.swInfos.HalfTileWidth, this.swInfos.TileWidth, this.configuration);
                        }

                        this.processor.AddPixelsToHistogram(ref pixelRowBase, ref histogramBase, this.processor.LuminanceLevels, pixelRow.Length);
                    }
                }
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

            public int TileWidth { get; }

            public int TileHeight { get; }

            public int PixelInTile { get; }

            public int HalfTileWidth { get; }

            public int HalfTileHeight { get; }
        }
    }
}
