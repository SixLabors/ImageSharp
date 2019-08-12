// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
    internal class AdaptiveHistogramEqualizationProcessor<TPixel> : HistogramEqualizationProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveHistogramEqualizationProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.</param>
        /// <param name="clipHistogram">Indicating whether to clip the histogram bins at a specific value.</param>
        /// <param name="clipLimitPercentage">Histogram clip limit in percent of the total pixels in the tile. Histogram bins which exceed this limit, will be capped at this value.</param>
        /// <param name="tiles">The number of tiles the image is split into (horizontal and vertically). Minimum value is 2. Maximum value is 100.</param>
        public AdaptiveHistogramEqualizationProcessor(int luminanceLevels, bool clipHistogram, float clipLimitPercentage, int tiles)
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
            int sourceWidth = source.Width;
            int sourceHeight = source.Height;
            int tileWidth = (int)MathF.Ceiling(sourceWidth / (float)this.Tiles);
            int tileHeight = (int)MathF.Ceiling(sourceHeight / (float)this.Tiles);
            int tileCount = this.Tiles;
            int halfTileWidth = tileWidth / 2;
            int halfTileHeight = tileHeight / 2;
            int luminanceLevels = this.LuminanceLevels;

            // The image is split up into tiles. For each tile the cumulative distribution function will be calculated.
            using (var cdfData = new CdfTileData(configuration, sourceWidth, sourceHeight, this.Tiles, this.Tiles, tileWidth, tileHeight, luminanceLevels))
            {
                cdfData.CalculateLookupTables(source, this);

                var tileYStartPositions = new List<(int y, int cdfY)>();
                int cdfY = 0;
                for (int y = halfTileHeight; y < sourceHeight - halfTileHeight; y += tileHeight)
                {
                    tileYStartPositions.Add((y, cdfY));
                    cdfY++;
                }

                Parallel.For(
                    0,
                    tileYStartPositions.Count,
                    new ParallelOptions { MaxDegreeOfParallelism = configuration.MaxDegreeOfParallelism },
                    index =>
                        {
                            int y = tileYStartPositions[index].y;
                            int cdfYY = tileYStartPositions[index].cdfY;

                            // It's unfortunate that we have to do this per iteration.
                            ref TPixel sourceBase = ref source.GetPixelReference(0, 0);

                            int cdfX = 0;
                            for (int x = halfTileWidth; x < sourceWidth - halfTileWidth; x += tileWidth)
                            {
                                int tileY = 0;
                                int yEnd = Math.Min(y + tileHeight, sourceHeight);
                                int xEnd = Math.Min(x + tileWidth, sourceWidth);
                                for (int dy = y; dy < yEnd; dy++)
                                {
                                    int dyOffSet = dy * sourceWidth;
                                    int tileX = 0;
                                    for (int dx = x; dx < xEnd; dx++)
                                    {
                                        ref TPixel pixel = ref Unsafe.Add(ref sourceBase, dyOffSet + dx);
                                        float luminanceEqualized = InterpolateBetweenFourTiles(
                                            pixel,
                                            cdfData,
                                            tileCount,
                                            tileCount,
                                            tileX,
                                            tileY,
                                            cdfX,
                                            cdfYY,
                                            tileWidth,
                                            tileHeight,
                                            luminanceLevels);

                                        pixel.FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, pixel.ToVector4().W));
                                        tileX++;
                                    }

                                    tileY++;
                                }

                                cdfX++;
                            }
                        });

                ref TPixel pixelsBase = ref source.GetPixelReference(0, 0);

                // Fix left column
                ProcessBorderColumn(ref pixelsBase, cdfData, 0, sourceWidth, sourceHeight, tileWidth, tileHeight, xStart: 0, xEnd: halfTileWidth, luminanceLevels);

                // Fix right column
                int rightBorderStartX = ((this.Tiles - 1) * tileWidth) + halfTileWidth;
                ProcessBorderColumn(ref pixelsBase, cdfData, this.Tiles - 1, sourceWidth, sourceHeight, tileWidth, tileHeight, xStart: rightBorderStartX, xEnd: sourceWidth, luminanceLevels);

                // Fix top row
                ProcessBorderRow(ref pixelsBase, cdfData, 0, sourceWidth, tileWidth, yStart: 0, yEnd: halfTileHeight, luminanceLevels);

                // Fix bottom row
                int bottomBorderStartY = ((this.Tiles - 1) * tileHeight) + halfTileHeight;
                ProcessBorderRow(ref pixelsBase, cdfData, this.Tiles - 1, sourceWidth, tileWidth, yStart: bottomBorderStartY, yEnd: sourceHeight, luminanceLevels);

                // Left top corner
                ProcessCornerTile(ref pixelsBase, cdfData, sourceWidth, 0, 0, xStart: 0, xEnd: halfTileWidth, yStart: 0, yEnd: halfTileHeight, luminanceLevels);

                // Left bottom corner
                ProcessCornerTile(ref pixelsBase, cdfData, sourceWidth, 0, this.Tiles - 1, xStart: 0, xEnd: halfTileWidth, yStart: bottomBorderStartY, yEnd: sourceHeight, luminanceLevels);

                // Right top corner
                ProcessCornerTile(ref pixelsBase, cdfData, sourceWidth, this.Tiles - 1, 0, xStart: rightBorderStartX, xEnd: sourceWidth, yStart: 0, yEnd: halfTileHeight, luminanceLevels);

                // Right bottom corner
                ProcessCornerTile(ref pixelsBase, cdfData, sourceWidth, this.Tiles - 1, this.Tiles - 1, xStart: rightBorderStartX, xEnd: sourceWidth, yStart: bottomBorderStartY, yEnd: sourceHeight, luminanceLevels);
            }
        }

        /// <summary>
        /// Processes the part of a corner tile which was previously left out. It consists of 1 / 4 of a tile and does not need interpolation.
        /// </summary>
        /// <param name="pixelsBase">The output pixels base reference.</param>
        /// <param name="cdfData">The lookup table to remap the grey values.</param>
        /// <param name="sourceWidth">The source image width.</param>
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
            ref TPixel pixelsBase,
            CdfTileData cdfData,
            int sourceWidth,
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
                int dyOffSet = dy * sourceWidth;
                for (int dx = xStart; dx < xEnd; dx++)
                {
                    ref TPixel pixel = ref Unsafe.Add(ref pixelsBase, dyOffSet + dx);
                    float luminanceEqualized = cdfData.RemapGreyValue(cdfX, cdfY, GetLuminance(pixel, luminanceLevels));
                    pixel.FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, pixel.ToVector4().W));
                }
            }
        }

        /// <summary>
        /// Processes a border column of the image which is half the size of the tile width.
        /// </summary>
        /// <param name="pixelBase">The output pixels reference.</param>
        /// <param name="cdfData">The pre-computed lookup tables to remap the grey values for each tiles.</param>
        /// <param name="cdfX">The X index of the lookup table to use.</param>
        /// <param name="sourceWidth">The source image width.</param>
        /// <param name="sourceHeight">The source image height.</param>
        /// <param name="tileWidth">The width of a tile.</param>
        /// <param name="tileHeight">The height of a tile.</param>
        /// <param name="xStart">X start position in the image.</param>
        /// <param name="xEnd">X end position of the image.</param>
        /// <param name="luminanceLevels">
        /// The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.
        /// </param>
        private static void ProcessBorderColumn(
            ref TPixel pixelBase,
            CdfTileData cdfData,
            int cdfX,
            int sourceWidth,
            int sourceHeight,
            int tileWidth,
            int tileHeight,
            int xStart,
            int xEnd,
            int luminanceLevels)
        {
            int halfTileHeight = tileHeight / 2;

            int cdfY = 0;
            for (int y = halfTileHeight; y < sourceHeight - halfTileHeight; y += tileHeight)
            {
                int yLimit = Math.Min(y + tileHeight, sourceHeight - 1);
                int tileY = 0;
                for (int dy = y; dy < yLimit; dy++)
                {
                    int dyOffSet = dy * sourceWidth;
                    for (int dx = xStart; dx < xEnd; dx++)
                    {
                        ref TPixel pixel = ref Unsafe.Add(ref pixelBase, dyOffSet + dx);
                        float luminanceEqualized = InterpolateBetweenTwoTiles(pixel, cdfData, cdfX, cdfY, cdfX, cdfY + 1, tileY, tileHeight, luminanceLevels);
                        pixel.FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, pixel.ToVector4().W));
                    }

                    tileY++;
                }

                cdfY++;
            }
        }

        /// <summary>
        /// Processes a border row of the image which is half of the size of the tile height.
        /// </summary>
        /// <param name="pixelBase">The output pixels base reference.</param>
        /// <param name="cdfData">The pre-computed lookup tables to remap the grey values for each tiles.</param>
        /// <param name="cdfY">The Y index of the lookup table to use.</param>
        /// <param name="sourceWidth">The source image width.</param>
        /// <param name="tileWidth">The width of a tile.</param>
        /// <param name="yStart">Y start position in the image.</param>
        /// <param name="yEnd">Y end position of the image.</param>
        /// <param name="luminanceLevels">
        /// The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.
        /// </param>
        private static void ProcessBorderRow(
            ref TPixel pixelBase,
            CdfTileData cdfData,
            int cdfY,
            int sourceWidth,
            int tileWidth,
            int yStart,
            int yEnd,
            int luminanceLevels)
        {
            int halfTileWidth = tileWidth / 2;

            int cdfX = 0;
            for (int x = halfTileWidth; x < sourceWidth - halfTileWidth; x += tileWidth)
            {
                for (int dy = yStart; dy < yEnd; dy++)
                {
                    int dyOffSet = dy * sourceWidth;
                    int tileX = 0;
                    int xLimit = Math.Min(x + tileWidth, sourceWidth - 1);
                    for (int dx = x; dx < xLimit; dx++)
                    {
                        ref TPixel pixel = ref Unsafe.Add(ref pixelBase, dyOffSet + dx);
                        float luminanceEqualized = InterpolateBetweenTwoTiles(pixel, cdfData, cdfX, cdfY, cdfX + 1, cdfY, tileX, tileWidth, luminanceLevels);
                        pixel.FromVector4(new Vector4(luminanceEqualized, luminanceEqualized, luminanceEqualized, pixel.ToVector4().W));
                        tileX++;
                    }
                }

                cdfX++;
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
            private readonly int sourceHeight;
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
                this.sourceHeight = sourceHeight;
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
                int sourceWidth = this.sourceWidth;
                int sourceHeight = this.sourceHeight;
                int tileWidth = this.tileWidth;
                int tileHeight = this.tileHeight;
                int luminanceLevels = this.luminanceLevels;

                Parallel.For(
                    0,
                    this.tileYStartPositions.Count,
                    new ParallelOptions { MaxDegreeOfParallelism = this.configuration.MaxDegreeOfParallelism },
                    index =>
                        {
                            int cdfX = 0;
                            int cdfY = this.tileYStartPositions[index].cdfY;
                            int y = this.tileYStartPositions[index].y;
                            int endY = Math.Min(y + tileHeight, sourceHeight);
                            ref TPixel sourceBase = ref source.GetPixelReference(0, 0);
                            ref int cdfMinBase = ref MemoryMarshal.GetReference(this.cdfMinBuffer2D.GetRowSpan(cdfY));

                            using (IMemoryOwner<int> histogramBuffer = this.memoryAllocator.Allocate<int>(luminanceLevels))
                            {
                                Span<int> histogram = histogramBuffer.GetSpan();
                                ref int histogramBase = ref MemoryMarshal.GetReference(histogram);

                                for (int x = 0; x < sourceWidth; x += tileWidth)
                                {
                                    histogram.Clear();
                                    ref int cdfBase = ref MemoryMarshal.GetReference(this.GetCdfLutSpan(cdfX, index));

                                    int xlimit = Math.Min(x + tileWidth, sourceWidth);
                                    for (int dy = y; dy < endY; dy++)
                                    {
                                        int dyOffset = dy * sourceWidth;
                                        for (int dx = x; dx < xlimit; dx++)
                                        {
                                            int luminance = GetLuminance(Unsafe.Add(ref sourceBase, dyOffset + dx), luminanceLevels);
                                            histogram[luminance]++;
                                        }
                                    }

                                    if (processor.ClipHistogramEnabled)
                                    {
                                        processor.ClipHistogram(histogram, processor.ClipLimitPercentage, this.pixelsInTile);
                                    }

                                    Unsafe.Add(ref cdfMinBase, cdfX) = processor.CalculateCdf(ref cdfBase, ref histogramBase, histogram.Length - 1);

                                    cdfX++;
                                }
                            }
                        });
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
        }
    }
}
