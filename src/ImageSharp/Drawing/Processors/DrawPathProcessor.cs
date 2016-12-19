// <copyright file="DrawPathProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;

    using ImageSharp.Processors;
    using Paths;
    using Pens;
    using Pens.Processors;
    using Shapes;
    using Rectangle = ImageSharp.Rectangle;

    /// <summary>
    /// Draws a path using the processor pipeline
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    /// <typeparam name="TPacked">The type of the packed.</typeparam>
    /// <seealso cref="ImageSharp.Processors.ImageFilteringProcessor{TColor, TPacked}" />
    public class DrawPathProcessor<TColor, TPacked> : ImageFilteringProcessor<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct, IEquatable<TPacked>
    {
        private const float AntialiasFactor = 1f;
        private const int PaddingFactor = 2; // needs to be greater than AntialiasFactor
        private const float Epsilon = 0.001f;

        private readonly IPen<TColor, TPacked> pen;
        private readonly IPath[] paths;
        private readonly RectangleF region;
        private readonly GraphicsOptions options;
        private readonly Vector2 offset;

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawPathProcessor{TColor, TPacked}" /> class.
        /// </summary>
        /// <param name="pen">The pen.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="position">The position.</param>
        /// <param name="options">The options.</param>
        public DrawPathProcessor(IPen<TColor, TPacked> pen, IShape shape, Vector2 position, GraphicsOptions options)
            : this(pen, Convert(shape), position, options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawPathProcessor{TColor, TPacked}" /> class.
        /// </summary>
        /// <param name="pen">The pen.</param>
        /// <param name="shapes">The shapes.</param>
        /// <param name="position">The position.</param>
        /// <param name="options">The options.</param>
        public DrawPathProcessor(IPen<TColor, TPacked> pen, IShape[] shapes, Vector2 position, GraphicsOptions options)
            : this(pen, Convert(shapes), position, options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawPathProcessor{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="pen">The pen.</param>
        /// <param name="paths">The paths.</param>
        /// <param name="options">The options.</param>
        public DrawPathProcessor(IPen<TColor, TPacked> pen, IPath[] paths, GraphicsOptions options)
            : this(pen, paths, Vector2.Zero, options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawPathProcessor{TColor, TPacked}" /> class.
        /// </summary>
        /// <param name="pen">The pen.</param>
        /// <param name="paths">The paths.</param>
        /// <param name="position">The position.</param>
        /// <param name="options">The options.</param>
        public DrawPathProcessor(IPen<TColor, TPacked> pen, IPath[] paths, Vector2 position, GraphicsOptions options)
        {
            this.paths = paths;
            this.pen = pen;
            this.options = options;
            this.offset = position;

            if (paths.Length != 1)
            {
                var maxX = paths.Max(x => x.Bounds.Right);
                var minX = paths.Min(x => x.Bounds.Left);
                var maxY = paths.Max(x => x.Bounds.Bottom);
                var minY = paths.Min(x => x.Bounds.Top);

                this.region = new RectangleF(minX, minY, maxX - minX, maxY - minY);
            }
            else
            {
                this.region = paths[0].Bounds;
            }
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor, TPacked> source, Rectangle sourceRectangle)
        {
            using (IPenApplicator<TColor, TPacked> applicator = this.pen.CreateApplicator(this.region))
            {
                foreach (IPath p in this.paths)
                {
                    RectangleF b = new RectangleF(p.Bounds.X + this.offset.X, p.Bounds.Y + this.offset.Y, p.Bounds.Width, p.Bounds.Height);
                    Rectangle rect = RectangleF.Ceiling(b);

                    rect = Rectangle.Outset(rect, PaddingFactor + applicator.DrawingPadding);

                    int startY = rect.Y;
                    int endY = rect.Bottom;
                    int startX = rect.X;
                    int endX = rect.Right;

                    int minX = Math.Max(sourceRectangle.Left, startX);
                    int maxX = Math.Min(sourceRectangle.Right, endX);
                    int minY = Math.Max(sourceRectangle.Top, startY);
                    int maxY = Math.Min(sourceRectangle.Bottom, endY);

                    // Align start/end positions.
                    minX = Math.Max(0, minX);
                    maxX = Math.Min(source.Width, maxX);
                    minY = Math.Max(0, minY);
                    maxY = Math.Min(source.Height, maxY);

                    // Reset offset if necessary.
                    if (minX > 0)
                    {
                        startX = 0;
                    }

                    if (minY > 0)
                    {
                        startY = 0;
                    }

                    using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
                    {
                        Parallel.For(
                        minY,
                        maxY,
                        this.ParallelOptions,
                        y =>
                        {
                            int offsetY = y - startY;
                            var currentPoint = default(Vector2);
                            for (int x = minX; x < maxX; x++)
                            {
                                int offsetX = x - startX;
                                currentPoint.X = offsetX;
                                currentPoint.Y = offsetY;

                                var dist = p.Distance(currentPoint - this.offset);

                                var color = applicator.GetColor(dist);

                                var opacity = this.Opacity(color.DistanceFromElement);

                                if (opacity > Epsilon)
                                {
                                    int offsetColorX = x - minX;

                                    Vector4 backgroundVector = sourcePixels[offsetX, offsetY].ToVector4();
                                    Vector4 sourceVector = color.Color.ToVector4();

                                    var finalColor = Vector4BlendTransforms.PremultipliedLerp(backgroundVector, sourceVector, opacity);
                                    finalColor.W = backgroundVector.W;

                                    TColor packed = default(TColor);
                                    packed.PackFromVector4(finalColor);
                                    sourcePixels[offsetX, offsetY] = packed;
                                }
                            }
                        });
                    }
                }
            }
        }

        private static IPath[] Convert(IShape shape)
        {
            return shape.ToArray();
        }

        private static IPath[] Convert(IShape[] shape)
        {
            List<IPath> paths = new List<Paths.IPath>();
            foreach (var s in shape)
            {
                paths.AddRange(s);
            }

            return paths.ToArray();
        }

        private PointInfo Closest(Vector2 point)
        {
            PointInfo result = default(PointInfo);
            float distance = float.MaxValue;

            for (int i = 0; i < this.paths.Length; i++)
            {
                var p = this.paths[i].Distance(point);
                if (p.DistanceFromPath < distance)
                {
                    distance = p.DistanceFromPath;
                    result = p;
                }
            }

            return result;
        }

        private float Opacity(float distance)
        {
            if (distance <= 0)
            {
                return 1;
            }
            else if (this.options.Antialias && distance < AntialiasFactor)
            {
                return 1 - (distance / AntialiasFactor);
            }

            return 0;
        }
    }
}