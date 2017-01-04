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
                    var buffer = arrayPool.Rent(maxIntersections);
                    var left = new Vector2(startX, y);
                    var right = new Vector2(endX, y);

                    // foreach line we get all the points where this line crosses the polygon
                    var pointsFound = this.poly.FindIntersections(left, right, buffer, maxIntersections, 0);
                    if (pointsFound == 0)
                    {
                        arrayPool.Return(buffer);

                        // nothign on this line skip
                        return;
                    }

                    QuickSortX(buffer, 0, pointsFound);

                    int currentIntersection = 0;
                    float nextPoint = buffer[0].X;
                    float lastPoint = float.MinValue;
                    float targetPoint = nextPoint;
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

                    arrayPool.Return(buffer);
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
                        var buffer = arrayPool.Rent(maxIntersections);
                        var left = new Vector2(x, polyStartY);
                        var right = new Vector2(x, polyEndY);

                        // foreach line we get all the points where this line crosses the polygon
                        var pointsFound = this.poly.FindIntersections(left, right, buffer, maxIntersections, 0);
                        if (pointsFound == 0)
                        {
                            arrayPool.Return(buffer);

                            // nothign on this line skip
                            return;
                        }

                        QuickSortY(buffer, 0, pointsFound);

                        int currentIntersection = 0;
                        float nextPoint = buffer[0].Y;
                        float lastPoint = left.Y;
                        float targetPoint = nextPoint;
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

                        arrayPool.Return(buffer);
                    });
                }
            }
        }

        private static void QuickSortX(Vector2[] data, int left, int right)
        {
            int i = left - 1;
            int j = right;

            while (true)
            {
                float x = data[left].X;
                do
                {
                    i++;
                }
                while (data[i].X < x);

                do
                {
                    j--;
                }
                while (data[j].X > x);

                if (i < j)
                {
                    Vector2 tmp = data[i];
                    data[i] = data[j];
                    data[j] = tmp;
                }
                else
                {
                    if (left < j)
                    {
                        QuickSortX(data, left, j);
                    }

                    if (++j < right)
                    {
                        QuickSortX(data, j, right);
                    }

                    return;
                }
            }
        }

        private static void QuickSortY(Vector2[] data, int left, int right)
        {
            int i = left - 1;
            int j = right;

            while (true)
            {
                float d = data[left].Y;
                do
                {
                    i++;
                }
                while (data[i].Y < d);

                do
                {
                    j--;
                }
                while (data[j].Y > d);

                if (i < j)
                {
                    Vector2 tmp = data[i];
                    data[i] = data[j];
                    data[j] = tmp;
                }
                else
                {
                    if (left < j)
                    {
                        QuickSortY(data, left, j);
                    }

                    if (++j < right)
                    {
                        QuickSortY(data, j, right);
                    }

                    return;
                }
            }
        }
    }
}