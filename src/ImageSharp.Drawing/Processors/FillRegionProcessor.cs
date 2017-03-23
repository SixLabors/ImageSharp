// <copyright file="FillRegionProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Buffers;
    using System.Numerics;
    using System.Threading.Tasks;
    using Drawing;
    using ImageSharp.Processing;

    /// <summary>
    /// Usinf a brsuh and a shape fills shape with contents of brush the
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    /// <seealso cref="ImageSharp.Processing.ImageProcessor{TColor}" />
    internal class FillRegionProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPixel<TColor>
    {
        private const float AntialiasFactor = 1f;
        private const int DrawPadding = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="FillRegionProcessor{TColor}" /> class.
        /// </summary>
        /// <param name="brush">The details how to fill the region of interest.</param>
        /// <param name="region">The region of interest to be filled.</param>
        /// <param name="options">The configuration options.</param>
        public FillRegionProcessor(IBrush<TColor> brush, Region region, GraphicsOptions options)
        {
            this.Region = region;
            this.Brush = brush;
            this.Options = options;
        }

        /// <summary>
        /// Gets the brush.
        /// </summary>
        public IBrush<TColor> Brush { get; }

        /// <summary>
        /// Gets the region that this processor applies to.
        /// </summary>
        public Region Region { get; }

        /// <summary>
        /// Gets the options.
        /// </summary>
        /// <value>
        /// The options.
        /// </value>
        public GraphicsOptions Options { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            Region region = this.Region;
            Rectangle rect = region.Bounds;

            // Align start/end positions.
            int minX = Math.Max(0, rect.Left);
            int maxX = Math.Min(source.Width, rect.Right);
            int minY = Math.Max(0, rect.Top);
            int maxY = Math.Min(source.Height, rect.Bottom);
            if (minX >= maxX)
            {
                return; // no effect inside image;
            }

            if (minY >= maxY)
            {
                return; // no effect inside image;
            }

            ArrayPool<float> arrayPool = ArrayPool<float>.Shared;

            int maxIntersections = region.MaxIntersections;
            float subpixelCount = 4;
            if (this.Options.Antialias)
            {
                subpixelCount = this.Options.AntialiasSubpixelDepth;
                if (subpixelCount < 4)
                {
                    subpixelCount = 4;
                }
            }

            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            using (BrushApplicator<TColor> applicator = this.Brush.CreateApplicator(sourcePixels, rect))
            {
                float[] buffer = arrayPool.Rent(maxIntersections);
                int scanlineWidth = maxX - minX;
                float[] scanline = ArrayPool<float>.Shared.Rent(scanlineWidth);
                try
                {
                    bool scanlineDirty = true;
                    for (int y = minY; y < maxY; y++)
                    {
                        if (scanlineDirty)
                        {
                            // clear the buffer
                            for (int x = 0; x < scanlineWidth; x++)
                            {
                                scanline[x] = 0;
                            }

                            scanlineDirty = false;
                        }

                        float subpixelFraction = 1f / subpixelCount;
                        float subpixelFractionPoint = subpixelFraction / subpixelCount;
                        for (float subPixel = (float)y; subPixel < y + 1; subPixel += subpixelFraction)
                        {
                            int pointsFound = region.Scan(subPixel, buffer, maxIntersections, 0);
                            if (pointsFound == 0)
                            {
                                // nothing on this line skip
                                continue;
                            }

                            QuickSort(buffer, pointsFound);

                            for (int point = 0; point < pointsFound; point += 2)
                            {
                                // points will be paired up
                                float scanStart = buffer[point] - minX;
                                float scanEnd = buffer[point + 1] - minX;
                                int startX = (int)Math.Floor(scanStart);
                                int endX = (int)Math.Floor(scanEnd);

                                if (startX >= 0 && startX < scanline.Length)
                                {
                                    for (float x = scanStart; x < startX + 1; x += subpixelFraction)
                                    {
                                        scanline[startX] += subpixelFractionPoint;
                                        scanlineDirty = true;
                                    }
                                }

                                if (endX >= 0 && endX < scanline.Length)
                                {
                                    for (float x = endX; x < scanEnd; x += subpixelFraction)
                                    {
                                        scanline[endX] += subpixelFractionPoint;
                                        scanlineDirty = true;
                                    }
                                }

                                int nextX = startX + 1;
                                if (nextX >= 0 && endX < scanline.Length)
                                {
                                    for (int x = nextX; x < endX; x++)
                                    {
                                        scanline[x] += subpixelFraction;
                                        scanlineDirty = true;
                                    }
                                }
                            }
                        }

                        if (scanlineDirty)
                        {
                            if (!this.Options.Antialias)
                            {
                                for (int x = 0; x < scanlineWidth; x++)
                                {
                                    if (scanline[x] > 0.5)
                                    {
                                        scanline[x] = 1;
                                    }
                                    else
                                    {
                                        scanline[x] = 0;
                                    }
                                }
                            }

                            applicator.Apply(scanline, scanlineWidth, 0, minX, y);
                        }
                    }
                }
                finally
                {
                    arrayPool.Return(buffer);
                    ArrayPool<float>.Shared.Return(scanline);
                }
            }
        }

        private static void Swap(float[] data, int left, int right)
        {
            float tmp = data[left];
            data[left] = data[right];
            data[right] = tmp;
        }

        private static void QuickSort(float[] data, int size)
        {
            int hi = Math.Min(data.Length - 1, size - 1);
            QuickSort(data, 0, hi);
        }

        private static void QuickSort(float[] data, int lo, int hi)
        {
            if (lo < hi)
            {
                int p = Partition(data, lo, hi);
                QuickSort(data, lo, p);
                QuickSort(data, p + 1, hi);
            }
        }

        private static int Partition(float[] data, int lo, int hi)
        {
            float pivot = data[lo];
            int i = lo - 1;
            int j = hi + 1;
            while (true)
            {
                do
                {
                    i = i + 1;
                }
                while (data[i] < pivot && i < hi);

                do
                {
                    j = j - 1;
                }
                while (data[j] > pivot && j > lo);

                if (i >= j)
                {
                    return j;
                }

                Swap(data, i, j);
            }
        }
    }
}