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
    using ImageSharp.Processing;
    using Pens;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawPathProcessor{TColor}" /> class.
        /// </summary>
        /// <param name="pen">The details how to draw the outline/path.</param>
        /// <param name="drawable">The details of the paths and outlines to draw.</param>
        /// <param name="options">The drawing configuration options.</param>
        public DrawPathProcessor(IPen<TColor> pen, Drawable drawable, GraphicsOptions options)
        {
            this.Path = drawable;
            this.Pen = pen;
            this.Options = options;
        }

        /// <summary>
        /// Gets the options.
        /// </summary>
        /// <value>
        /// The options.
        /// </value>
        public GraphicsOptions Options { get; }

        /// <summary>
        /// Gets the pen.
        /// </summary>
        /// <value>
        /// The pen.
        /// </value>
        public IPen<TColor> Pen { get; }

        /// <summary>
        /// Gets the path.
        /// </summary>
        /// <value>
        /// The path.
        /// </value>
        public Drawable Path { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            using (PenApplicator<TColor> applicator = this.Pen.CreateApplicator(sourcePixels, this.Path.Bounds))
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
                (int y) =>
                {
                    int offsetY = y - polyStartY;

                    for (int x = minX; x < maxX; x++)
                    {
                        // TODO add find intersections code to skip and scan large regions of this.
                        int offsetX = x - startX;
                        var info = this.Path.GetPointInfo(offsetX, offsetY);

                        var color = applicator.GetColor(offsetX, offsetY, info);

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

        private float Opacity(float distance)
        {
            if (distance <= 0)
            {
                return 1;
            }
            else if (this.Options.Antialias && distance < AntialiasFactor)
            {
                return 1 - (distance / AntialiasFactor);
            }

            return 0;
        }
    }
}