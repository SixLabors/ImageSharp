// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    /// <summary>
    /// Applies an adaptive histogram equalization to the image. The image is split up in tiles. For each tile a cumulative distribution function (cdf) is calculated.
    /// To calculate the final equalized pixel value, the cdf value of four adjacent tiles will be interpolated.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class AdaptiveHistogramEqualizationProcessor<TPixel> : HistogramEqualizationProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveHistogramEqualizationProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.</param>
        /// <param name="clipHistogram">Indicating whether to clip the histogram bins at a specific value.</param>
        /// <param name="clipLimit">The histogram clip limit. Histogram bins which exceed this limit, will be capped at this value.</param>
        /// <param name="tiles">The number of tiles the image is split into (horizontal and vertically). Minimum value is 2. Maximum value is 100.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public AdaptiveHistogramEqualizationProcessor(
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
            int sourceWidth = source.Width;
            int sourceHeight = source.Height;
            int tileWidth = (int)MathF.Ceiling(sourceWidth / (float)this.Tiles);
            int tileHeight = (int)MathF.Ceiling(sourceHeight / (float)this.Tiles);
            int tileCount = this.Tiles;
            int halfTileWidth = tileWidth / 2;
            int halfTileHeight = tileHeight / 2;
            int luminanceLevels = this.LuminanceLevels;

            // The image is split up into tiles. For each tile the cumulative distribution function will be calculated.
            using (var cdfData = new CdfTileData(this.Configuration, sourceWidth, sourceHeight, this.Tiles, this.Tiles, tileWidth, tileHeight, luminanceLevels))
            {
                cdfData.CalculateLookupTables(source, this);

                var tileYStartPositions = new List<(int y, int cdfY)>();
                int cdfY = 0;
                int yStart = halfTileHeight;
                for (int tile = 0; tile < tileCount - 1; tile++)
                {
                    tileYStartPositions.Add((yStart, cdfY));
                    cdfY++;
                    yStart += tileHeight;
                }

                var operation = new RowIntervalOperation(cdfData, tileYStartPositions, tileWidth, tileHeight, tileCount, halfTileWidth, luminanceLevels, source);
                ParallelRowIterator.IterateRowIntervals(
                    this.Configuration,
                    new Rectangle(0, 0, sourceWidth, tileYStartPositions.Count),
                    in operation);

                // Fix left column
                ProcessBorderColumn(source, cdfData, 0, sourceHeight, this.Tiles, tileHeight, xStart: 0, xEnd: halfTileWidth, luminanceLevels);

                // Fix right column
                int rightBorderStartX = ((this.Tiles - 1) * tileWidth) + halfTileWidth;
                ProcessBorderColumn(source, cdfData, this.Tiles - 1, sourceHeight, this.Tiles, tileHeight, xStart: rightBorderStartX, xEnd: sourceWidth, luminanceLevels);

                // Fix top row
                ProcessBorderRow(source, cdfData, 0, sourceWidth, this.Tiles, tileWidth, yStart: 0, yEnd: halfTileHeight, luminanceLevels);

                // Fix bottom row
                int bottomBorderStartY = ((this.Tiles - 1) * tileHeight) + halfTileHeight;
                ProcessBorderRow(source, cdfData, this.Tiles - 1, sourceWidth, this.Tiles, tileWidth, yStart: bottomBorderStartY, yEnd: sourceHeight, luminanceLevels);

                // Left top corner
                ProcessCornerTile(source, cdfData, 0, 0, xStart: 0, xEnd: halfTileWidth, yStart: 0, yEnd: halfTileHeight, luminanceLevels);

                // Left bottom corner
                ProcessCornerTile(source, cdfData, 0, this.Tiles - 1, xStart: 0, xEnd: halfTileWidth, yStart: bottomBorderStartY, yEnd: sourceHeight, luminanceLevels);

                // Right top corner
                ProcessCornerTile(source, cdfData, this.Tiles - 1, 0, xStart: rightBorderStartX, xEnd: sourceWidth, yStart: 0, yEnd: halfTileHeight, luminanceLevels);

                // Right bottom corner
                ProcessCornerTile(source, cdfData, this.Tiles - 1, this.Tiles - 1, xStart: rightBorderStartX, xEnd: sourceWidth, yStart: bottomBorderStartY, yEnd: sourceHeight, luminanceLevels);
            }
        }

        /// <summary>
        /// Processes the part of a corner tile which was previously left out. It consists of 1 / 4 of a tile and does not need interpolation.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="cdfData">The lookup table to remap the grey values.</param>
        /// <param name="cdfX">The x-position in the CDF lookup map.</param>
        /// <param name="cdfY">The y-position in the CDF lookup map.</param>
        /// <param name="xStart">X start position.</param>
        /// <param name="xEnd">X end position.</param>
        /// <param name="yStart">Y start position.</param>
        /// <param name="yEnd">Y end position.</param>
        /// <param name="luminanceLevels">
        /// The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.
        /// </param>
        private static void ProcessCornerTile(
            ImageFrame<TPixel> source,
            CdfTileData cdfData,
            int cdfX,
            int cdfY,
            int xStart,
            int xEnd,
            int yStart,
            int yEnd,
            int luminanceLevels)
        {
            for (int dy = yStart; dy < yEnd; dy++)
            {
                Span<TPixel> rowSpan = source.GetPixelRowSpan(dy);
                for (int dx = xStart; dx < xEnd; dx++)
                {
                    ref TPixel pixel = ref rowSpan[dx];
                    float luminanceEqualized = cdfData.RemapGreyValue(cdfX, cdfY, GetLuminance(pixel, luminanceLevels));
                    pixel.FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, pixel.ToVector4().W));
                }
            }
        }

        /// <summary>
        /// Processes a border column of the image which is half the size of the tile width.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="cdfData">The pre-computed lookup tables to remap the grey values for each tiles.</param>
        /// <param name="cdfX">The X index of the lookup table to use.</param>
        /// <param name="sourceHeight">The source image height.</param>
        /// <param name="tileCount">The number of vertical tiles.</param>
        /// <param name="tileHeight">The height of a tile.</param>
        /// <param name="xStart">X start position in the image.</param>
        /// <param name="xEnd">X end position of the image.</param>
        /// <param name="luminanceLevels">
        /// The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.
        /// </param>
        private static void ProcessBorderColumn(
            ImageFrame<TPixel> source,
            CdfTileData cdfData,
            int cdfX,
            int sourceHeight,
            int tileCount,
            int tileHeight,
            int xStart,
            int xEnd,
            int luminanceLevels)
        {
            int halfTileHeight = tileHeight / 2;

            int cdfY = 0;
            int y = halfTileHeight;
            for (int tile = 0; tile < tileCount - 1; tile++)
            {
                int yLimit = Math.Min(y + tileHeight, sourceHeight - 1);
                int tileY = 0;
                for (int dy = y; dy < yLimit; dy++)
                {
                    Span<TPixel> rowSpan = source.GetPixelRowSpan(dy);
                    for (int dx = xStart; dx < xEnd; dx++)
                    {
                        ref TPixel pixel = ref rowSpan[dx];
                        float luminanceEqualized = InterpolateBetweenTwoTiles(pixel, cdfData, cdfX, cdfY, cdfX, cdfY + 1, tileY, tileHeight, luminanceLevels);
                        pixel.FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, pixel.ToVector4().W));
                    }

                    tileY++;
                }

                cdfY++;
                y += tileHeight;
            }
        }

        /// <summary>
        /// Processes a border row of the image which is half of the size of the tile height.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="cdfData">The pre-computed lookup tables to remap the grey values for each tiles.</param>
        /// <param name="cdfY">The Y index of the lookup table to use.</param>
        /// <param name="sourceWidth">The source image width.</param>
        /// <param name="tileCount">The number of horizontal tiles.</param>
        /// <param name="tileWidth">The width of a tile.</param>
        /// <param name="yStart">Y start position in the image.</param>
        /// <param name="yEnd">Y end position of the image.</param>
        /// <param name="luminanceLevels">
        /// The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.
        /// </param>
        private static void ProcessBorderRow(
            ImageFrame<TPixel> source,
            CdfTileData cdfData,
            int cdfY,
            int sourceWidth,
            int tileCount,
            int tileWidth,
            int yStart,
            int yEnd,
            int luminanceLevels)
        {
            int halfTileWidth = tileWidth / 2;

            int cdfX = 0;
            int x = halfTileWidth;
            for (int tile = 0; tile < tileCount - 1; tile++)
            {
                for (int dy = yStart; dy < yEnd; dy++)
                {
                    Span<TPixel> rowSpan = source.GetPixelRowSpan(dy);
                    int tileX = 0;
                    int xLimit = Math.Min(x + tileWidth, sourceWidth - 1);
                    for (int dx = x; dx < xLimit; dx++)
                    {
                        ref TPixel pixel = ref rowSpan[dx];
                        float luminanceEqualized = InterpolateBetweenTwoTiles(pixel, cdfData, cdfX, cdfY, cdfX + 1, cdfY, tileX, tileWidth, luminanceLevels);
                        pixel.FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, pixel.ToVector4().W));
                        tileX++;
                    }
                }

                cdfX++;
                x += tileWidth;
            }
        }

        /// <summary>
        /// Bilinear interpolation between four adjacent tiles.
        /// </summary>
        /// <param name="sourcePixel">The pixel to remap the grey value from.</param>
        /// <param name="cdfData">The pre-computed lookup tables to remap the grey values for each tiles.</param>
        /// <param name="tileCountX">The number of tiles in the x-direction.</param>
        /// <param name="tileCountY">The number of tiles in the y-direction.</param>
        /// <param name="tileX">X position inside the tile.</param>
        /// <param name="tileY">Y position inside the tile.</param>
        /// <param name="cdfX">X index of the top left lookup table to use.</param>
        /// <param name="cdfY">Y index of the top left lookup table to use.</param>
        /// <param name="tileWidth">Width of one tile in pixels.</param>
        /// <param name="tileHeight">Height of one tile in pixels.</param>
        /// <param name="luminanceLevels">
        /// The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.
        /// </param>
        /// <returns>A re-mapped grey value.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static float InterpolateBetweenFourTiles(
            TPixel sourcePixel,
            CdfTileData cdfData,
            int tileCountX,
            int tileCountY,
            int tileX,
            int tileY,
            int cdfX,
            int cdfY,
            int tileWidth,
            int tileHeight,
            int luminanceLevels)
        {
            int luminance = GetLuminance(sourcePixel, luminanceLevels);
            float tx = tileX / (float)(tileWidth - 1);
            float ty = tileY / (float)(tileHeight - 1);

            int yTop = cdfY;
            int yBottom = Math.Min(tileCountY - 1, yTop + 1);
            int xLeft = cdfX;
            int xRight = Math.Min(tileCountX - 1, xLeft + 1);

            float cdfLeftTopLuminance = cdfData.RemapGreyValue(xLeft, yTop, luminance);
            float cdfRightTopLuminance = cdfData.RemapGreyValue(xRight, yTop, luminance);
            float cdfLeftBottomLuminance = cdfData.RemapGreyValue(xLeft, yBottom, luminance);
            float cdfRightBottomLuminance = cdfData.RemapGreyValue(xRight, yBottom, luminance);
            return BilinearInterpolation(tx, ty, cdfLeftTopLuminance, cdfRightTopLuminance, cdfLeftBottomLuminance, cdfRightBottomLuminance);
        }

        /// <summary>
        /// Linear interpolation between two tiles.
        /// </summary>
        /// <param name="sourcePixel">The pixel to remap the grey value from.</param>
        /// <param name="cdfData">The CDF lookup map.</param>
        /// <param name="tileX1">X position inside the first tile.</param>
        /// <param name="tileY1">Y position inside the first tile.</param>
        /// <param name="tileX2">X position inside the second tile.</param>
        /// <param name="tileY2">Y position inside the second tile.</param>
        /// <param name="tilePos">Position inside the tile.</param>
        /// <param name="tileWidth">Width of the tile.</param>
        /// <param name="luminanceLevels">
        /// The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.
        /// </param>
        /// <returns>A re-mapped grey value.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static float InterpolateBetweenTwoTiles(
            TPixel sourcePixel,
            CdfTileData cdfData,
            int tileX1,
            int tileY1,
            int tileX2,
            int tileY2,
            int tilePos,
            int tileWidth,
            int luminanceLevels)
        {
            int luminance = GetLuminance(sourcePixel, luminanceLevels);
            float tx = tilePos / (float)(tileWidth - 1);

            float cdfLuminance1 = cdfData.RemapGreyValue(tileX1, tileY1, luminance);
            float cdfLuminance2 = cdfData.RemapGreyValue(tileX2, tileY2, luminance);
            return LinearInterpolation(cdfLuminance1, cdfLuminance2, tx);
        }

        /// <summary>
        /// Bilinear interpolation between four tiles.
        /// </summary>
        /// <param name="tx">The interpolation value in x direction in the range of [0, 1].</param>
        /// <param name="ty">The interpolation value in y direction in the range of [0, 1].</param>
        /// <param name="lt">Luminance from top left tile.</param>
        /// <param name="rt">Luminance from right top tile.</param>
        /// <param name="lb">Luminance from left bottom tile.</param>
        /// <param name="rb">Luminance from right bottom tile.</param>
        /// <returns>Interpolated Luminance.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static float BilinearInterpolation(float tx, float ty, float lt, float rt, float lb, float rb)
            => LinearInterpolation(LinearInterpolation(lt, rt, tx), LinearInterpolation(lb, rb, tx), ty);

        /// <summary>
        /// Linear interpolation between two grey values.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <param name="t">The interpolation value between the two values in the range of [0, 1].</param>
        /// <returns>The interpolated value.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static float LinearInterpolation(float left, float right, float t)
            => left + ((right - left) * t);

        private readonly struct RowIntervalOperation : IRowIntervalOperation
        {
            private readonly CdfTileData cdfData;
            private readonly List<(int y, int cdfY)> tileYStartPositions;
            private readonly int tileWidth;
            private readonly int tileHeight;
            private readonly int tileCount;
            private readonly int halfTileWidth;
            private readonly int luminanceLevels;
            private readonly ImageFrame<TPixel> source;
            private readonly int sourceWidth;
            private readonly int sourceHeight;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowIntervalOperation(
                CdfTileData cdfData,
                List<(int y, int cdfY)> tileYStartPositions,
                int tileWidth,
                int tileHeight,
                int tileCount,
                int halfTileWidth,
                int luminanceLevels,
                ImageFrame<TPixel> source)
            {
                this.cdfData = cdfData;
                this.tileYStartPositions = tileYStartPositions;
                this.tileWidth = tileWidth;
                this.tileHeight = tileHeight;
                this.tileCount = tileCount;
                this.halfTileWidth = halfTileWidth;
                this.luminanceLevels = luminanceLevels;
                this.source = source;
                this.sourceWidth = source.Width;
                this.sourceHeight = source.Height;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                for (int index = rows.Min; index < rows.Max; index++)
                {
                    (int y, int cdfY) tileYStartPosition = this.tileYStartPositions[index];
                    int y = tileYStartPosition.y;
                    int cdfYY = tileYStartPosition.cdfY;

                    int cdfX = 0;
                    int x = this.halfTileWidth;
                    for (int tile = 0; tile < this.tileCount - 1; tile++)
                    {
                        int tileY = 0;
                        int yEnd = Math.Min(y + this.tileHeight, this.sourceHeight);
                        int xEnd = Math.Min(x + this.tileWidth, this.sourceWidth);
                        for (int dy = y; dy < yEnd; dy++)
                        {
                            Span<TPixel> rowSpan = this.source.GetPixelRowSpan(dy);
                            int tileX = 0;
                            for (int dx = x; dx < xEnd; dx++)
                            {
                                ref TPixel pixel = ref rowSpan[dx];
                                float luminanceEqualized = InterpolateBetweenFourTiles(
                                    pixel,
                                    this.cdfData,
                                    this.tileCount,
                                    this.tileCount,
                                    tileX,
                                    tileY,
                                    cdfX,
                                    cdfYY,
                                    this.tileWidth,
                                    this.tileHeight,
                                    this.luminanceLevels);

                                pixel.FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, pixel.ToVector4().W));
                                tileX++;
                            }

                            tileY++;
                        }

                        cdfX++;
                        x += this.tileWidth;
                    }
                }
            }
        }

        /// <summary>
        /// Contains the results of the cumulative distribution function for all tiles.
        /// </summary>
        private sealed class CdfTileData : IDisposable
        {
            private readonly Configuration configuration;
            private readonly MemoryAllocator memoryAllocator;

            // Used for storing the minimum value for each CDF entry.
            private readonly Buffer2D<int> cdfMinBuffer2D;

            // Used for storing the LUT for each CDF entry.
            private readonly Buffer2D<int> cdfLutBuffer2D;
            private readonly int pixelsInTile;
            private readonly int sourceWidth;
            private readonly int tileWidth;
            private readonly int tileHeight;
            private readonly int luminanceLevels;
            private readonly List<(int y, int cdfY)> tileYStartPositions;

            public CdfTileData(
                Configuration configuration,
                int sourceWidth,
                int sourceHeight,
                int tileCountX,
                int tileCountY,
                int tileWidth,
                int tileHeight,
                int luminanceLevels)
            {
                this.configuration = configuration;
                this.memoryAllocator = configuration.MemoryAllocator;
                this.luminanceLevels = luminanceLevels;
                this.cdfMinBuffer2D = this.memoryAllocator.Allocate2D<int>(tileCountX, tileCountY);
                this.cdfLutBuffer2D = this.memoryAllocator.Allocate2D<int>(tileCountX * luminanceLevels, tileCountY);
                this.sourceWidth = sourceWidth;
                this.tileWidth = tileWidth;
                this.tileHeight = tileHeight;
                this.pixelsInTile = tileWidth * tileHeight;

                // Calculate the start positions and rent buffers.
                this.tileYStartPositions = new List<(int y, int cdfY)>();
                int cdfY = 0;
                for (int y = 0; y < sourceHeight; y += tileHeight)
                {
                    this.tileYStartPositions.Add((y, cdfY));
                    cdfY++;
                }
            }

            public void CalculateLookupTables(ImageFrame<TPixel> source, HistogramEqualizationProcessor<TPixel> processor)
            {
                var operation = new RowIntervalOperation(
                    processor,
                    this.memoryAllocator,
                    this.cdfMinBuffer2D,
                    this.cdfLutBuffer2D,
                    this.tileYStartPositions,
                    this.tileWidth,
                    this.tileHeight,
                    this.luminanceLevels,
                    source);

                ParallelRowIterator.IterateRowIntervals(
                    this.configuration,
                    new Rectangle(0, 0, this.sourceWidth, this.tileYStartPositions.Count),
                    in operation);
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public Span<int> GetCdfLutSpan(int tileX, int tileY) => this.cdfLutBuffer2D.GetRowSpan(tileY).Slice(tileX * this.luminanceLevels, this.luminanceLevels);

            /// <summary>
            /// Remaps the grey value with the cdf.
            /// </summary>
            /// <param name="tilesX">The tiles x-position.</param>
            /// <param name="tilesY">The tiles y-position.</param>
            /// <param name="luminance">The original luminance.</param>
            /// <returns>The remapped luminance.</returns>
            [MethodImpl(InliningOptions.ShortMethod)]
            public float RemapGreyValue(int tilesX, int tilesY, int luminance)
            {
                int cdfMin = this.cdfMinBuffer2D[tilesX, tilesY];
                Span<int> cdfSpan = this.GetCdfLutSpan(tilesX, tilesY);
                return (this.pixelsInTile - cdfMin) == 0
                           ? cdfSpan[luminance] / this.pixelsInTile
                           : cdfSpan[luminance] / (float)(this.pixelsInTile - cdfMin);
            }

            public void Dispose()
            {
                this.cdfMinBuffer2D.Dispose();
                this.cdfLutBuffer2D.Dispose();
            }

            private readonly struct RowIntervalOperation : IRowIntervalOperation
            {
                private readonly HistogramEqualizationProcessor<TPixel> processor;
                private readonly MemoryAllocator allocator;
                private readonly Buffer2D<int> cdfMinBuffer2D;
                private readonly Buffer2D<int> cdfLutBuffer2D;
                private readonly List<(int y, int cdfY)> tileYStartPositions;
                private readonly int tileWidth;
                private readonly int tileHeight;
                private readonly int luminanceLevels;
                private readonly ImageFrame<TPixel> source;
                private readonly int sourceWidth;
                private readonly int sourceHeight;

                [MethodImpl(InliningOptions.ShortMethod)]
                public RowIntervalOperation(
                    HistogramEqualizationProcessor<TPixel> processor,
                    MemoryAllocator allocator,
                    Buffer2D<int> cdfMinBuffer2D,
                    Buffer2D<int> cdfLutBuffer2D,
                    List<(int y, int cdfY)> tileYStartPositions,
                    int tileWidth,
                    int tileHeight,
                    int luminanceLevels,
                    ImageFrame<TPixel> source)
                {
                    this.processor = processor;
                    this.allocator = allocator;
                    this.cdfMinBuffer2D = cdfMinBuffer2D;
                    this.cdfLutBuffer2D = cdfLutBuffer2D;
                    this.tileYStartPositions = tileYStartPositions;
                    this.tileWidth = tileWidth;
                    this.tileHeight = tileHeight;
                    this.luminanceLevels = luminanceLevels;
                    this.source = source;
                    this.sourceWidth = source.Width;
                    this.sourceHeight = source.Height;
                }

                /// <inheritdoc/>
                [MethodImpl(InliningOptions.ShortMethod)]
                public void Invoke(in RowInterval rows)
                {
                    for (int index = rows.Min; index < rows.Max; index++)
                    {
                        int cdfX = 0;
                        int cdfY = this.tileYStartPositions[index].cdfY;
                        int y = this.tileYStartPositions[index].y;
                        int endY = Math.Min(y + this.tileHeight, this.sourceHeight);
                        Span<int> cdfMinSpan = this.cdfMinBuffer2D.GetRowSpan(cdfY);

                        using IMemoryOwner<int> histogramBuffer = this.allocator.Allocate<int>(this.luminanceLevels);
                        Span<int> histogram = histogramBuffer.GetSpan();
                        ref int histogramBase = ref MemoryMarshal.GetReference(histogram);

                        for (int x = 0; x < this.sourceWidth; x += this.tileWidth)
                        {
                            histogram.Clear();
                            Span<int> cdfLutSpan = this.cdfLutBuffer2D.GetRowSpan(index).Slice(cdfX * this.luminanceLevels, this.luminanceLevels);
                            ref int cdfBase = ref MemoryMarshal.GetReference(cdfLutSpan);

                            int xlimit = Math.Min(x + this.tileWidth, this.sourceWidth);
                            for (int dy = y; dy < endY; dy++)
                            {
                                Span<TPixel> rowSpan = this.source.GetPixelRowSpan(dy);
                                for (int dx = x; dx < xlimit; dx++)
                                {
                                    int luminance = GetLuminance(rowSpan[dx], this.luminanceLevels);
                                    histogram[luminance]++;
                                }
                            }

                            if (this.processor.ClipHistogramEnabled)
                            {
                                this.processor.ClipHistogram(histogram, this.processor.ClipLimit);
                            }

                            cdfMinSpan[cdfX] += this.processor.CalculateCdf(ref cdfBase, ref histogramBase, histogram.Length - 1);

                            cdfX++;
                        }
                    }
                }
            }
        }
    }
}
