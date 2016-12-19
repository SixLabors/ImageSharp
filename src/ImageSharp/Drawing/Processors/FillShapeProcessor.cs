// <copyright file="FillShapeProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;
    using Drawing;
    using ImageSharp.Processors;
    using Shapes;
    using Rectangle = ImageSharp.Rectangle;

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
        private const int DrawPadding = 1;
        private readonly IBrush<TColor, TPacked> fillColor;
        private readonly IShape poly;
        private readonly GraphicsOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="FillShapeProcessor{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The graphics options.</param>
        public FillShapeProcessor(IBrush<TColor, TPacked> brush, IShape shape, GraphicsOptions options)
        {
            this.poly = shape;
            this.fillColor = brush;
            this.options = options;
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor, TPacked> source, Rectangle sourceRectangle)
        {
            var rect = RectangleF.Ceiling(this.poly.Bounds); // rounds the points out away from the center

            int polyStartY = rect.Y - DrawPadding;
            int polyEndY = rect.Bottom + DrawPadding;
            int startX = rect.X - DrawPadding;
            int endX = rect.Right + DrawPadding;

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

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (IBrushApplicator<TColor, TPacked> applicator = this.fillColor.CreateApplicator(rect))
            {
                Parallel.For(
                minY,
                maxY,
                this.ParallelOptions,
                y =>
                {
                    int offsetY = y - polyStartY;

                    Vector2 currentPoint = default(Vector2);

                    for (int x = minX; x < maxX; x++)
                    {
                        int offsetX = x - startX;
                        currentPoint.X = offsetX;
                        currentPoint.Y = offsetY;
                        var dist = this.poly.Distance(currentPoint);
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