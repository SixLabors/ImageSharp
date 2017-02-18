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
    public class FillRegionProcessor<TColor> : ImageProcessor<TColor>
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
            Rectangle rect = this.Region.Bounds;

            int polyStartY = sourceRectangle.Y - DrawPadding;
            int polyEndY = sourceRectangle.Bottom + DrawPadding;
            int startX = sourceRectangle.X - DrawPadding;
            int endX = sourceRectangle.Right + DrawPadding;

            int minX = Math.Max(sourceRectangle.Left, startX);
            int maxX = Math.Min(sourceRectangle.Right - 1, endX);
            int minY = Math.Max(sourceRectangle.Top, polyStartY);
            int maxY = Math.Min(sourceRectangle.Bottom - 1, polyEndY);

            // Align start/end positions.
            minX = Math.Max(0, minX);
            maxX = Math.Min(source.Width, maxX);
            minY = Math.Max(0, minY);
            maxY = Math.Min(source.Height, maxY);

            ArrayPool<float> arrayPool = ArrayPool<float>.Shared;

            int maxIntersections = this.Region.MaxIntersections;

            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            using (BrushApplicator<TColor> applicator = this.Brush.CreateApplicator(sourcePixels, rect))
            {
                Parallel.For(
                minY,
                maxY,
                this.ParallelOptions,
                (int y) =>
                {
                    float[] buffer = arrayPool.Rent(maxIntersections);

                    try
                    {
                        float right = endX;

                        // foreach line we get all the points where this line crosses the polygon
                        int pointsFound = this.Region.ScanY(y, buffer, maxIntersections, 0);
                        if (pointsFound == 0)
                        {
                            // nothing on this line skip
                            return;
                        }

                        QuickSort(buffer, pointsFound);

                        int currentIntersection = 0;
                        float nextPoint = buffer[0];
                        float lastPoint = float.MinValue;
                        bool isInside = false;

                        for (int x = minX; x < maxX; x++)
                        {
                            if (!isInside)
                            {
                                if (x < (nextPoint - DrawPadding) && x > (lastPoint + DrawPadding))
                                {
                                    if (nextPoint == right)
                                    {
                                        // we are in the ends run skip it
                                        x = maxX;
                                        continue;
                                    }

                                    // lets just jump forward
                                    x = (int)Math.Floor(nextPoint) - DrawPadding;
                                }
                            }

                            bool onCorner = false;

                            // there seems to be some issue with this switch.
                            if (x >= nextPoint)
                            {
                                currentIntersection++;
                                lastPoint = nextPoint;
                                if (currentIntersection == pointsFound)
                                {
                                    nextPoint = right;
                                }
                                else
                                {
                                    nextPoint = buffer[currentIntersection];

                                    // double point from a corner flip the bit back and move on again
                                    if (nextPoint == lastPoint)
                                    {
                                        onCorner = true;
                                        isInside ^= true;
                                        currentIntersection++;
                                        if (currentIntersection == pointsFound)
                                        {
                                            nextPoint = right;
                                        }
                                        else
                                        {
                                            nextPoint = buffer[currentIntersection];
                                        }
                                    }
                                }

                                isInside ^= true;
                            }

                            float opacity = 1;
                            if (!isInside && !onCorner)
                            {
                                if (this.Options.Antialias)
                                {
                                    float distance = float.MaxValue;
                                    if (x == lastPoint || x == nextPoint)
                                    {
                                        // we are to far away from the line
                                        distance = 0;
                                    }
                                    else if (nextPoint - AntialiasFactor < x)
                                    {
                                        // we are near the left of the line
                                        distance = nextPoint - x;
                                    }
                                    else if (lastPoint + AntialiasFactor > x)
                                    {
                                        // we are near the right of the line
                                        distance = x - lastPoint;
                                    }
                                    else
                                    {
                                        // we are to far away from the line
                                        continue;
                                    }
                                    opacity = 1 - (distance / AntialiasFactor);
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            if (opacity > Constants.Epsilon)
                            {
                                Vector4 backgroundVector = sourcePixels[x, y].ToVector4();
                                Vector4 sourceVector = applicator[x, y].ToVector4();

                                Vector4 finalColor = Vector4BlendTransforms.PremultipliedLerp(backgroundVector, sourceVector, opacity);
                                finalColor.W = backgroundVector.W;

                                TColor packed = default(TColor);
                                packed.PackFromVector4(finalColor);
                                sourcePixels[x, y] = packed;
                            }
                        }
                    }
                    finally
                    {
                        arrayPool.Return(buffer);
                    }
                });

                if (this.Options.Antialias)
                {
                    // we only need to do the X can for antialiasing purposes
                    Parallel.For(
                    minX,
                    maxX,
                    this.ParallelOptions,
                    (int x) =>
                    {
                        float[] buffer = arrayPool.Rent(maxIntersections);

                        try
                        {
                            float left = polyStartY;
                            float right = polyEndY;

                            // foreach line we get all the points where this line crosses the polygon
                            int pointsFound = this.Region.ScanX(x, buffer, maxIntersections, 0);
                            if (pointsFound == 0)
                            {
                                // nothign on this line skip
                                return;
                            }

                            QuickSort(buffer, pointsFound);

                            int currentIntersection = 0;
                            float nextPoint = buffer[0];
                            float lastPoint = left;
                            bool isInside = false;

                            for (int y = minY; y < maxY; y++)
                            {
                                if (!isInside)
                                {
                                    if (y < (nextPoint - DrawPadding) && y > (lastPoint + DrawPadding))
                                    {
                                        if (nextPoint == right)
                                        {
                                            // we are in the ends run skip it
                                            y = maxY;
                                            continue;
                                        }

                                        // lets just jump forward
                                        y = (int)Math.Floor(nextPoint) - DrawPadding;
                                    }
                                }
                                else
                                {
                                    if (y < nextPoint - DrawPadding)
                                    {
                                        if (nextPoint == right)
                                        {
                                            // we are in the ends run skip it
                                            y = maxY;
                                            continue;
                                        }

                                        // lets just jump forward
                                        y = (int)Math.Floor(nextPoint);
                                    }
                                }

                                bool onCorner = false;

                                if (y >= nextPoint)
                                {
                                    currentIntersection++;
                                    lastPoint = nextPoint;
                                    if (currentIntersection == pointsFound)
                                    {
                                        nextPoint = right;
                                    }
                                    else
                                    {
                                        nextPoint = buffer[currentIntersection];

                                        // double point from a corner flip the bit back and move on again
                                        if (nextPoint == lastPoint)
                                        {
                                            onCorner = true;
                                            isInside ^= true;
                                            currentIntersection++;
                                            if (currentIntersection == pointsFound)
                                            {
                                                nextPoint = right;
                                            }
                                            else
                                            {
                                                nextPoint = buffer[currentIntersection];
                                            }
                                        }
                                    }

                                    isInside ^= true;
                                }

                                float opacity = 1;
                                if (!isInside && !onCorner)
                                {
                                    if (this.Options.Antialias)
                                    {
                                        float distance = float.MaxValue;
                                        if (y == lastPoint || y == nextPoint)
                                        {
                                            // we are to far away from the line
                                            distance = 0;
                                        }
                                        else if (nextPoint - AntialiasFactor < y)
                                        {
                                            // we are near the left of the line
                                            distance = nextPoint - y;
                                        }
                                        else if (lastPoint + AntialiasFactor > y)
                                        {
                                            // we are near the right of the line
                                            distance = y - lastPoint;
                                        }
                                        else
                                        {
                                            // we are to far away from the line
                                            continue;
                                        }
                                        opacity = 1 - (distance / AntialiasFactor);
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }

                                // don't set full opactiy color as it will have been gotten by the first scan
                                if (opacity > Constants.Epsilon && opacity < 1)
                                {
                                    Vector4 backgroundVector = sourcePixels[x, y].ToVector4();
                                    Vector4 sourceVector = applicator[x, y].ToVector4();
                                    Vector4 finalColor = Vector4BlendTransforms.PremultipliedLerp(backgroundVector, sourceVector, opacity);
                                    finalColor.W = backgroundVector.W;

                                    TColor packed = default(TColor);
                                    packed.PackFromVector4(finalColor);
                                    sourcePixels[x, y] = packed;
                                }
                            }
                        }
                        finally
                        {
                            arrayPool.Return(buffer);
                        }
                    });
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