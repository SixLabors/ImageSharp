// <copyright file="FillShapeProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;
    using Common;
    using Drawing;
    using ImageSharp.Processors;
    using Shapes;
    using Rectangle = ImageSharp.Rectangle;

    /// <summary>
    /// Usinf a brsuh and a shape fills shape with contents of brush the
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    /// <seealso cref="ImageSharp.Processors.ImageFilteringProcessor{TColor}" />
    public class FillShapeProcessor<TColor> : ImageFilteringProcessor<TColor>
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
        /// <param name="options">The graphics options.</param>
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

            // Reset offset if necessary.
            if (minX > 0)
            {
                startX = 0;
            }

            if (minY > 0)
            {
                polyStartY = 0;
            }

            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            using (IBrushApplicator<TColor> applicator = this.fillColor.CreateApplicator(sourcePixels, rect))
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
                        float dist = this.poly.Distance(currentPoint);
                        float opacity = this.Opacity(dist);

                        if (opacity > Constants.Epsilon)
                        {
                            Vector4 backgroundVector = sourcePixels[offsetX, offsetY].ToVector4();
                            Vector4 sourceVector = applicator.GetColor(currentPoint).ToVector4();

                            Vector4 finalColor = Vector4BlendTransforms.PremultipliedLerp(backgroundVector, sourceVector, opacity);
                            finalColor.W = backgroundVector.W;

                            TColor packed = default(TColor);
                            packed.PackFromVector4(finalColor);
                            sourcePixels[offsetX, offsetY] = packed;
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Returns the correct alpha value for the given distance.
        /// </summary>
        /// <param name="distance">
        /// The distance.
        /// </param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        private float Opacity(float distance)
        {
            if (distance <= 0)
            {
                return 1;
            }

            if (this.options.Antialias && distance < AntialiasFactor)
            {
                return 1 - (distance / AntialiasFactor);
            }

            return 0;
        }
    }
}