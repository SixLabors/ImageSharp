// <copyright file="FillShapeProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using Drawing;
    using ImageSharp.Processors;
    using Shapes;

    /// <summary>
    /// Usinf a brsuh and a shape fills shape with contents of brush the
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    /// <typeparam name="TPacked">The type of the packed.</typeparam>
    /// <seealso cref="ImageSharp.Processors.ImageFilteringProcessor{TColor, TPacked}" />
    public class FillShapeProcessor<TColor, TPacked> : ImageFilteringProcessor<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct, IEquatable<TPacked>
    {
        private const float Epsilon = 0.001f;

        private const float AntialiasFactor = 1f;
        private const int DrawPadding = 2;
        private readonly IBrush<TColor, TPacked> fillColor;
        private readonly IShape[] polys;
        private readonly GraphicsOptions options;
        private readonly Vector2 origon;

        /// <summary>
        /// Initializes a new instance of the <see cref="FillShapeProcessor{TColor, TPacked}" /> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="shapes">The shapes.</param>
        /// <param name="origon">The origon.</param>
        /// <param name="options">The options.</param>
        public FillShapeProcessor(IBrush<TColor, TPacked> brush, IShape[] shapes, Vector2 origon, GraphicsOptions options)
        {
            this.polys = shapes;
            this.fillColor = brush;
            this.options = options;
            this.origon = origon;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FillShapeProcessor{TColor, TPacked}" /> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="origon">The origon.</param>
        /// <param name="options">The options.</param>
        public FillShapeProcessor(IBrush<TColor, TPacked> brush, IShape shape, Vector2 origon, GraphicsOptions options)
            : this(brush, new[] { shape }, origon, options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FillShapeProcessor{TColor, TPacked}" /> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        public FillShapeProcessor(IBrush<TColor, TPacked> brush, IShape shape, GraphicsOptions options)
            : this(brush, shape, Vector2.Zero, options)
        {
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor, TPacked> source, Rectangle sourceRectangle)
        {
            RectangleF fillBounds;
            if (this.polys.Length == 1)
            {
                fillBounds = this.polys[0].Bounds;
            }
            else
            {
                var polysmaxX = this.polys.Max(x => x.Bounds.Right);
                var polysminX = this.polys.Min(x => x.Bounds.Left);
                var polysmaxY = this.polys.Max(x => x.Bounds.Bottom);
                var polysminY = this.polys.Min(x => x.Bounds.Top);

                fillBounds = new RectangleF(polysminX, polysminY, polysmaxX - polysminX, polysmaxY - polysminY);
            }

            fillBounds = new RectangleF(fillBounds.X + this.origon.X, fillBounds.Y + this.origon.Y, fillBounds.Width, fillBounds.Height);

            var fillRect = RectangleF.Ceiling(fillBounds); // rounds the points out away from the center

            fillRect = Rectangle.Outset(fillRect, DrawPadding);

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (IBrushApplicator<TColor, TPacked> applicator = this.fillColor.CreateApplicator(fillRect))
            {
                foreach (var poly in this.polys)
                {
                    var bounds = poly.Bounds;
                    bounds = new RectangleF(bounds.X + this.origon.X, bounds.Y + this.origon.Y, bounds.Width, bounds.Height);
                    var rect = RectangleF.Ceiling(bounds); // rounds the points out away from the center

                    rect = Rectangle.Outset(rect, DrawPadding);

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

                    Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                    {
                        int offsetY = y - startY;

                        Vector2 currentPoint = default(Vector2);
                        for (int x = minX; x < maxX; x++)
                        {
                            int offsetX = x - startX;
                            currentPoint.X = offsetX;
                            currentPoint.Y = offsetY;

                            var dist = poly.Distance(currentPoint - this.origon);

                            var opacity = this.Opacity(dist);

                            if (opacity > Epsilon)
                            {
                                int offsetColorX = x - minX;

                                Vector4 backgroundVector = sourcePixels[offsetX, offsetY].ToVector4();
                                Vector4 sourceVector = applicator.GetColor(currentPoint).ToVector4();

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