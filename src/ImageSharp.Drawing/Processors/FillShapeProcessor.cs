// <copyright file="FillShapeProcessor.cs" company="James Jackson-South">
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
    using Shapes;
    using Rectangle = ImageSharp.Rectangle;

    /// <summary>
    /// Usinf a brsuh and a shape fills shape with contents of brush the
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    /// <seealso cref="ImageSharp.Processing.ImageProcessor{TColor}" />
    public class FillShapeProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        private const float AntialiasFactor = 1f;
        private const int DrawPadding = 1;
        private readonly IBrush<TColor> fillColor;
        private readonly IShape poly;
        private readonly GraphicsOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="FillShapeProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        public FillShapeProcessor(IBrush<TColor> brush, IShape shape, GraphicsOptions options)
        {
            this.poly = shape;
            this.fillColor = brush;
            this.options = options;
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            Rectangle rect = RectangleF.Ceiling(this.poly.Bounds); // rounds the points out away from the center

            int polyStartY = rect.Y - DrawPadding;
            int polyEndY = rect.Bottom + DrawPadding;
            int startX = rect.X - DrawPadding;
            int endX = rect.Right + DrawPadding;

            int minX = Math.Max(sourceRectangle.Left, startX);
            int maxX = Math.Min(sourceRectangle.Right - 1, endX);
            int minY = Math.Max(sourceRectangle.Top, polyStartY);
            int maxY = Math.Min(sourceRectangle.Bottom - 1, polyEndY);

            // Align start/end positions.
            minX = Math.Max(0, minX);
            maxX = Math.Min(source.Width, maxX);
            minY = Math.Max(0, minY);
            maxY = Math.Min(source.Height, maxY);

            ArrayPool<Vector2> arrayPool = ArrayPool<Vector2>.Shared;

            int maxIntersections = this.poly.MaxIntersections;

            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            using (BrushApplicator<TColor> applicator = this.fillColor.CreateApplicator(sourcePixels, rect))
            {
                Parallel.For(
                minY,
                maxY,
                this.ParallelOptions,
                y =>
                {
                    Vector2[] buffer = arrayPool.Rent(maxIntersections);

                    try
                    {
                        Vector2 left = new Vector2(startX, y);
                        Vector2 right = new Vector2(endX, y);

                        // foreach line we get all the points where this line crosses the polygon
                        int pointsFound = this.poly.FindIntersections(left, right, buffer, maxIntersections, 0);
                        if (pointsFound == 0)
                        {
                            // nothign on this line skip
                            return;
                        }

                        QuickSortX(buffer, pointsFound);

                        int currentIntersection = 0;
                        float nextPoint = buffer[0].X;
                        float lastPoint = float.MinValue;
                        bool isInside = false;

                        // every odd point is the start of a line
                        Vector2 currentPoint = default(Vector2);

                        for (int x = minX; x < maxX; x++)
                        {
                            currentPoint.X = x;
                            currentPoint.Y = y;
                            if (!isInside)
                            {
                                if (x < (nextPoint - DrawPadding) && x > (lastPoint + DrawPadding))
                                {
                                    if (nextPoint == right.X)
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
                                    nextPoint = right.X;
                                }
                                else
                                {
                                    nextPoint = buffer[currentIntersection].X;

                                    // double point from a corner flip the bit back and move on again
                                    if (nextPoint == lastPoint)
                                    {
                                        onCorner = true;
                                        isInside ^= true;
                                        currentIntersection++;
                                        if (currentIntersection == pointsFound)
                                        {
                                            nextPoint = right.X;
                                        }
                                        else
                                        {
                                            nextPoint = buffer[currentIntersection].X;
                                        }
                                    }
                                }

                                isInside ^= true;
                            }

                            float opacity = 1;
                            if (!isInside && !onCorner)
                            {
                                if (this.options.Antialias)
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
                                Vector4 sourceVector = applicator.GetColor(currentPoint).ToVector4();

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

                if (this.options.Antialias)
                {
                    // we only need to do the X can for antialiasing purposes
                    Parallel.For(
                    minX,
                    maxX,
                    this.ParallelOptions,
                    x =>
                    {
                        Vector2[] buffer = arrayPool.Rent(maxIntersections);

                        try
                        {
                            Vector2 left = new Vector2(x, polyStartY);
                            Vector2 right = new Vector2(x, polyEndY);

                            // foreach line we get all the points where this line crosses the polygon
                            int pointsFound = this.poly.FindIntersections(left, right, buffer, maxIntersections, 0);
                            if (pointsFound == 0)
                            {
                                // nothign on this line skip
                                return;
                            }

                            QuickSortY(buffer, pointsFound);

                            int currentIntersection = 0;
                            float nextPoint = buffer[0].Y;
                            float lastPoint = left.Y;
                            bool isInside = false;

                            // every odd point is the start of a line
                            Vector2 currentPoint = default(Vector2);

                            for (int y = minY; y < maxY; y++)
                            {
                                currentPoint.X = x;
                                currentPoint.Y = y;
                                if (!isInside)
                                {
                                    if (y < (nextPoint - DrawPadding) && y > (lastPoint + DrawPadding))
                                    {
                                        if (nextPoint == right.Y)
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
                                        if (nextPoint == right.Y)
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
                                        nextPoint = right.Y;
                                    }
                                    else
                                    {
                                        nextPoint = buffer[currentIntersection].Y;

                                        // double point from a corner flip the bit back and move on again
                                        if (nextPoint == lastPoint)
                                        {
                                            onCorner = true;
                                            isInside ^= true;
                                            currentIntersection++;
                                            if (currentIntersection == pointsFound)
                                            {
                                                nextPoint = right.Y;
                                            }
                                            else
                                            {
                                                nextPoint = buffer[currentIntersection].Y;
                                            }
                                        }
                                    }

                                    isInside ^= true;
                                }

                                float opacity = 1;
                                if (!isInside && !onCorner)
                                {
                                    if (this.options.Antialias)
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
                                    Vector4 sourceVector = applicator.GetColor(currentPoint).ToVector4();

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

        private static void Swap(Vector2[] data, int left, int right)
        {
            Vector2 tmp = data[left];
            data[left] = data[right];
            data[right] = tmp;
        }

        private static void QuickSortY(Vector2[] data, int size)
        {
            int hi = Math.Min(data.Length - 1, size - 1);
            QuickSortY(data, 0, hi);
        }

        private static void QuickSortY(Vector2[] data, int lo, int hi)
        {
            if (lo < hi)
            {
                int p = PartitionY(data, lo, hi);
                QuickSortY(data, lo, p);
                QuickSortY(data, p + 1, hi);
            }
        }

        private static void QuickSortX(Vector2[] data, int size)
        {
            int hi = Math.Min(data.Length - 1, size - 1);
            QuickSortX(data, 0, hi);
        }

        private static void QuickSortX(Vector2[] data, int lo, int hi)
        {
            if (lo < hi)
            {
                int p = PartitionX(data, lo, hi);
                QuickSortX(data, lo, p);
                QuickSortX(data, p + 1, hi);
            }
        }

        private static int PartitionX(Vector2[] data, int lo, int hi)
        {
            float pivot = data[lo].X;
            int i = lo - 1;
            int j = hi + 1;
            while (true)
            {
                do
                {
                    i = i + 1;
                }
                while (data[i].X < pivot && i < hi);

                do
                {
                    j = j - 1;
                }
                while (data[j].X > pivot && j > lo);

                if (i >= j)
                {
                    return j;
                }

                Swap(data, i, j);
            }
        }

        private static int PartitionY(Vector2[] data, int lo, int hi)
        {
            float pivot = data[lo].Y;
            int i = lo - 1;
            int j = hi + 1;
            while (true)
            {
                do
                {
                    i = i + 1;
                }
                while (data[i].Y < pivot && i < hi);

                do
                {
                    j = j - 1;
                }
                while (data[j].Y > pivot && j > lo);

                if (i >= j)
                {
                    return j;
                }

                Swap(data, i, j);
            }
        }
    }
}