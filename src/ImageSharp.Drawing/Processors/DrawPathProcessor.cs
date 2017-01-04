// <copyright file="DrawPathProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using ImageSharp.Processing;
    using Paths;
    using Pens;
    using Shapes;
    using Rectangle = ImageSharp.Rectangle;

    /// <summary>
    /// Draws a path using the processor pipeline
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    /// <seealso cref="ImageSharp.Processing.ImageProcessor{TColor}" />
    public class DrawPathProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        private const float AntialiasFactor = 1f;
        private const int PaddingFactor = 1; // needs to been the same or greater than AntialiasFactor

        private readonly IPen<TColor> pen;
        private readonly IPath[] paths;
        private readonly RectangleF region;
        private readonly GraphicsOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawPathProcessor{TColor}" /> class.
        /// </summary>
        /// <param name="pen">The pen.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        public DrawPathProcessor(IPen<TColor> pen, IShape shape, GraphicsOptions options)
            : this(pen, shape.ToArray(), options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawPathProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="pen">The pen.</param>
        /// <param name="path">The path.</param>
        /// <param name="options">The options.</param>
        public DrawPathProcessor(IPen<TColor> pen, IPath path, GraphicsOptions options)
            : this(pen, new[] { path }, options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawPathProcessor{TColor}" /> class.
        /// </summary>
        /// <param name="pen">The pen.</param>
        /// <param name="paths">The paths.</param>
        /// <param name="options">The options.</param>
        public DrawPathProcessor(IPen<TColor> pen, IPath[] paths, GraphicsOptions options)
        {
            this.paths = paths;
            this.pen = pen;
            this.options = options;

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
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            using (PenApplicator<TColor> applicator = this.pen.CreateApplicator(sourcePixels, this.region))
            {
                var rect = RectangleF.Ceiling(applicator.RequiredRegion);

                int polyStartY = rect.Y - PaddingFactor;
                int polyEndY = rect.Bottom + PaddingFactor;
                int startX = rect.X - PaddingFactor;
                int endX = rect.Right + PaddingFactor;

                int minX = Math.Max(sourceRectangle.Left, startX);
                int maxX = Math.Min(sourceRectangle.Right, endX);
                int minY = Math.Max(sourceRectangle.Top, polyStartY);
                int maxY = Math.Min(sourceRectangle.Bottom, polyEndY);

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
                    polyStartY = 0;
                }

                Parallel.For(
                minY,
                maxY,
                this.ParallelOptions,
                y =>
                {
                    int offsetY = y - polyStartY;
                    var currentPoint = default(Vector2);
                    for (int x = minX; x < maxX; x++)
                    {
                        int offsetX = x - startX;
                        currentPoint.X = offsetX;
                        currentPoint.Y = offsetY;

                        var dist = this.Closest(currentPoint);

                        var color = applicator.GetColor(dist);

                        var opacity = this.Opacity(color.DistanceFromElement);

                        if (opacity > Constants.Epsilon)
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