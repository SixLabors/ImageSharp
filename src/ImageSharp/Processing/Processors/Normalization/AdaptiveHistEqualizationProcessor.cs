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
    /// Applies an adaptive histogram equalization to the image. The image is split up in tiles. For each tile a cumulative distribution function (cdf) is calculated.
    /// To calculate the final equalized pixel value, the cdf value of four adjacent tiles will be interpolated.
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
        /// <param name="tiles">The number of tiles the image is split into (horizontal and vertically).</param>
        public AdaptiveHistEqualizationProcessor(int luminanceLevels, bool clipHistogram, float clipLimitPercentage, int tiles)
            : base(luminanceLevels, clipHistogram, clipLimitPercentage)
        {
            Guard.MustBeGreaterThanOrEqualTo(tiles, 0, nameof(tiles));

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

            int tileWidth = Convert.ToInt32(Math.Ceiling(source.Width / (double)this.Tiles));
            int tileHeight = Convert.ToInt32(Math.Ceiling(source.Height / (double)this.Tiles));
            int pixelsInTile = tileWidth * tileHeight;
            int halfTileWidth = tileWidth / 2;
            int halfTileHeight = tileHeight / 2;

            using (System.Buffers.IMemoryOwner<int> histogramBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
            using (System.Buffers.IMemoryOwner<int> cdfBuffer = memoryAllocator.Allocate<int>(this.LuminanceLevels, AllocationOptions.Clean))
            {
                Span<int> histogram = histogramBuffer.GetSpan();
                Span<int> cdf = cdfBuffer.GetSpan();

                // The image is split up into tiles. For each tile the cumulative distribution function will be calculated.
                CdfData[,] cdfData = this.CalculateLookupTables(source, histogram, cdf, this.Tiles, this.Tiles, tileWidth, tileHeight);

                int cdfX = 0;
                int cdfY = 0;
                int tileX = 0;
                int tileY = 0;
                for (int y = halfTileHeight; y < source.Height - halfTileHeight; y += tileHeight)
                {
                    cdfX = 0;
                    for (int x = halfTileWidth; x < source.Width - halfTileWidth; x += tileWidth)
                    {
                        tileY = 0;
                        int yEnd = Math.Min(y + tileHeight, source.Height);
                        int xEnd = Math.Min(x + tileWidth, source.Width);
                        for (int dy = y; dy < yEnd; dy++)
                        {
                            tileX = 0;
                            for (int dx = x; dx < xEnd; dx++)
                            {
                                float luminanceEqualized = this.InterpolateBetweenFourTiles(source[dx, dy], cdfData, tileX, tileY, cdfX, cdfY, tileWidth, tileHeight, pixelsInTile);
                                pixels[(dy * source.Width) + dx].PackFromVector4(new Vector4(luminanceEqualized));
                                tileX++;
                            }

                            tileY++;
                        }

                        cdfX++;
                    }

                    cdfY++;
                }

                // fix left column
                this.ProcessBorderColumn(source, pixels, cdfData, 0, tileWidth, tileHeight, xStart: 0, xEnd: halfTileWidth);

                // fix right column
                this.ProcessBorderColumn(source, pixels, cdfData, this.Tiles - 1, tileWidth, tileHeight, xStart: source.Width - halfTileWidth, xEnd: source.Width);

                // fix top row
                this.ProcessBorderRow(source, pixels, cdfData, 0, tileWidth, tileHeight, yStart: 0, yEnd: halfTileHeight);

                // fix bottom row
                this.ProcessBorderRow(source, pixels, cdfData, this.Tiles - 1, tileWidth, tileHeight, yStart: source.Height - halfTileHeight, yEnd: source.Height);
            }
        }

        /// <summary>
        /// Processes a border column of the image which is half the size of the tile width.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="pixels">The output pixels.</param>
        /// <param name="cdfData">The pre-computed lookup tables to remap the grey values for each tiles.</param>
        /// <param name="cdfX">The X index of the lookup table to use.</param>
        /// <param name="tileWidth">The width of a tile.</param>
        /// <param name="tileHeight">The height of a tile.</param>
        /// <param name="xStart">X start position in the image.</param>
        /// <param name="xEnd">X end position of the image.</param>
        private void ProcessBorderColumn(ImageFrame<TPixel> source, Span<TPixel> pixels, CdfData[,] cdfData, int cdfX, int tileWidth, int tileHeight, int xStart, int xEnd)
        {
            int halfTileWidth = tileWidth / 2;
            int halfTileHeight = tileHeight / 2;
            int pixelsInTile = tileWidth * tileHeight;

            int cdfY = 0;
            for (int y = halfTileWidth; y < source.Height - halfTileWidth; y += tileHeight)
            {
                int yLimit = Math.Min(y + tileHeight, source.Height - 1);
                int tileY = 0;
                for (int dy = y; dy < yLimit; dy++)
                {
                    int tileX = halfTileWidth;
                    for (int dx = xStart; dx < xEnd; dx++)
                    {
                        float luminanceEqualized = this.InterpolateBetweenTwoTiles(source[dx, dy], cdfData[cdfX, cdfY], cdfData[cdfX, cdfY + 1], tileY, tileHeight, pixelsInTile);
                        pixels[(dy * source.Width) + dx].PackFromVector4(new Vector4(luminanceEqualized));
                        tileX++;
                    }

                    tileY++;
                }

                cdfY++;
            }
        }

        /// <summary>
        /// Processes a border row of the image which is half of the size of the tile height.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="pixels">The output pixels.</param>
        /// <param name="cdfData">The pre-computed lookup tables to remap the grey values for each tiles.</param>
        /// <param name="cdfY">The Y index of the lookup table to use.</param>
        /// <param name="tileWidth">The width of a tile.</param>
        /// <param name="tileHeight">The height of a tile.</param>
        /// <param name="yStart">Y start position in the image.</param>
        /// <param name="yEnd">Y end position of the image.</param>
        private void ProcessBorderRow(ImageFrame<TPixel> source, Span<TPixel> pixels, CdfData[,] cdfData, int cdfY, int tileWidth, int tileHeight, int yStart, int yEnd)
        {
            int halfTileWidth = tileWidth / 2;
            int halfTileHeight = tileHeight / 2;
            int pixelsInTile = tileWidth * tileHeight;

            int cdfX = 0;
            for (int x = halfTileWidth; x < source.Width - halfTileWidth; x += tileWidth)
            {
                int tileY = 0;
                for (int dy = yStart; dy < yEnd; dy++)
                {
                    int tileX = 0;
                    int xLimit = Math.Min(x + tileWidth, source.Width - 1);
                    for (int dx = x; dx < xLimit; dx++)
                    {
                        float luminanceEqualized = this.InterpolateBetweenTwoTiles(source[dx, dy], cdfData[cdfX, cdfY], cdfData[cdfX + 1, cdfY], tileX, tileWidth, pixelsInTile);
                        pixels[(dy * source.Width) + dx].PackFromVector4(new Vector4(luminanceEqualized));
                        tileX++;
                    }

                    tileY++;
                }

                cdfX++;
            }
        }

        /// <summary>
        /// Bilinear interpolation between four adjacent tiles.
        /// </summary>
        /// <param name="sourcePixel">The pixel to remap the grey value from.</param>
        /// <param name="cdfData">The pre-computed lookup tables to remap the grey values for each tiles.</param>
        /// <param name="tileX">X position inside the tile.</param>
        /// <param name="tileY">Y position inside the tile.</param>
        /// <param name="cdfX">X index of the top left lookup table to use.</param>
        /// <param name="cdfY">Y index of the top left lookup table to use.</param>
        /// <param name="tileWidth">Width of one tile in pixels.</param>
        /// <param name="tileHeight">Height of one tile in pixels.</param>
        /// <param name="pixelsInTile">Amount of pixels in one tile.</param>
        /// <returns>A re-mapped grey value.</returns>
        private float InterpolateBetweenFourTiles(TPixel sourcePixel, CdfData[,] cdfData, int tileX, int tileY, int cdfX, int cdfY, int tileWidth, int tileHeight, int pixelsInTile)
        {
            int luminace = this.GetLuminance(sourcePixel, this.LuminanceLevels);
            float tx = tileX / (float)(tileWidth - 1);
            float ty = tileY / (float)(tileHeight - 1);

            int yTop = cdfY;
            int yBottom = Math.Min(this.Tiles - 1, yTop + 1);
            int xLeft = cdfX;
            int xRight = Math.Min(this.Tiles - 1, xLeft + 1);

            float cdfLeftTopLuminance = cdfData[xLeft, yTop].RemapGreyValue(luminace, pixelsInTile);
            float cdfRightTopLuminance = cdfData[xRight, yTop].RemapGreyValue(luminace, pixelsInTile);
            float cdfLeftBottomLuminance = cdfData[xLeft, yBottom].RemapGreyValue(luminace, pixelsInTile);
            float cdfRightBottomLuminance = cdfData[xRight, yBottom].RemapGreyValue(luminace, pixelsInTile);
            float luminanceEqualized = this.BilinearInterpolation(tx, ty, cdfLeftTopLuminance, cdfRightTopLuminance, cdfLeftBottomLuminance, cdfRightBottomLuminance);

            return luminanceEqualized;
        }

        /// <summary>
        /// Linear interpolation between two tiles.
        /// </summary>
        /// <param name="sourcePixel">The pixel to remap the grey value from.</param>
        /// <param name="cdfData1">First lookup table.</param>
        /// <param name="cdfData2">Second lookup table.</param>
        /// <param name="tilePos">Position inside the tile.</param>
        /// <param name="tileWidth">Width of the tile.</param>
        /// <param name="pixelsInTile">Pixels in one tile.</param>
        /// <returns>A re-mapped grey value.</returns>
        private float InterpolateBetweenTwoTiles(TPixel sourcePixel, CdfData cdfData1, CdfData cdfData2, int tilePos, int tileWidth, int pixelsInTile)
        {
            int luminace = this.GetLuminance(sourcePixel, this.LuminanceLevels);
            float tx = tilePos / (float)(tileWidth - 1);

            float cdfLuminance1 = cdfData1.RemapGreyValue(luminace, pixelsInTile);
            float cdfLuminance2 = cdfData2.RemapGreyValue(luminace, pixelsInTile);
            float luminanceEqualized = this.LinearInterpolation(cdfLuminance1, cdfLuminance2, tx);

            return luminanceEqualized;
        }

        /// <summary>
        /// Calculates the lookup tables for each tile of the image.
        /// </summary>
        /// <param name="source">The input image for which the tiles will be calculated.</param>
        /// <param name="histogram">Histogram buffer.</param>
        /// <param name="cdf">Buffer for calculating the cumulative distribution function.</param>
        /// <param name="numTilesX">Number of tiles in the X Direction.</param>
        /// <param name="numTilesY">Number of tiles in Y Direction</param>
        /// <param name="tileWidth">Width in pixels of one tile.</param>
        /// <param name="tileHeight">Height in pixels of one tile.</param>
        /// <returns>All lookup tables for each tile in the image.</returns>
        private CdfData[,] CalculateLookupTables(ImageFrame<TPixel> source, Span<int> histogram, Span<int> cdf, int numTilesX, int numTilesY, int tileWidth, int tileHeight)
        {
            var cdfData = new CdfData[numTilesX, numTilesY];
            int pixelsInTile = tileWidth * tileHeight;
            int tileX = 0;
            int tileY = 0;
            for (int y = 0; y < source.Height; y += tileHeight)
            {
                tileX = 0;
                for (int x = 0; x < source.Width; x += tileWidth)
                {
                    histogram.Clear();
                    cdf.Clear();
                    int ylimit = Math.Min(y + tileHeight, source.Height);
                    int xlimit = Math.Min(x + tileWidth, source.Width);
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
                        this.ClipHistogram(histogram, this.ClipLimitPercentage, pixelsInTile);
                    }

                    int cdfMin = this.CalculateCdf(cdf, histogram, histogram.Length - 1);
                    var currentCdf = new CdfData(cdf.ToArray(), cdfMin);
                    cdfData[tileX, tileY] = currentCdf;

                    tileX++;
                }

                tileY++;
            }

            return cdfData;
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
        private float BilinearInterpolation(float tx, float ty, float lt, float rt, float lb, float rb)
        {
            return this.LinearInterpolation(this.LinearInterpolation(lt, rt, tx), this.LinearInterpolation(lb, rb, tx), ty);
        }

        /// <summary>
        /// Linear interpolation between two grey values.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <param name="t">The interpolation value between the two values in the range of [0, 1].</param>
        /// <returns>The interpolated value.</returns>
        private float LinearInterpolation(float left, float right, float t)
        {
            return left + ((right - left) * t);
        }

        /// <summary>
        /// Lookup table for remapping the grey values of one tile.
        /// </summary>
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
            /// <param name="pixelsInTile">The number of pixels in the tile.</param>
            /// <returns>The remapped luminance.</returns>
            public float RemapGreyValue(int luminance, int pixelsInTile)
            {
                return (pixelsInTile - this.CdfMin) == 0 ? this.Cdf[luminance] / (float)pixelsInTile : this.Cdf[luminance] / (float)(pixelsInTile - this.CdfMin);
            }
        }
    }
}
