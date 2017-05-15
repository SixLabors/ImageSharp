// <copyright file="DrawPathProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using Pens;

    /// <summary>
    /// Draws a path using the processor pipeline
    /// </summary>
    /// <typeparam name="TPixel">The type of the color.</typeparam>
    /// <seealso cref="ImageSharp.Processing.ImageProcessor{TPixel}" />
    internal class DrawPathProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private const float AntialiasFactor = 1f;
        private const int PaddingFactor = 1; // needs to been the same or greater than AntialiasFactor

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawPathProcessor{TPixel}" /> class.
        /// </summary>
        /// <param name="pen">The details how to draw the outline/path.</param>
        /// <param name="drawable">The details of the paths and outlines to draw.</param>
        /// <param name="options">The drawing configuration options.</param>
        public DrawPathProcessor(IPen<TPixel> pen, Drawable drawable, GraphicsOptions options)
        {
            this.Path = drawable;
            this.Pen = pen;
            this.Options = options;
        }

        /// <summary>
        /// Gets the graphics options.
        /// </summary>
        public GraphicsOptions Options { get; }

        /// <summary>
        /// Gets the pen.
        /// </summary>
        public IPen<TPixel> Pen { get; }

        /// <summary>
        /// Gets the path.
        /// </summary>
        public Drawable Path { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            using (PixelAccessor<TPixel> sourcePixels = source.Lock())
            using (PenApplicator<TPixel> applicator = this.Pen.CreateApplicator(sourcePixels, this.Path.Bounds, this.Options))
            {
                Rectangle rect = RectangleF.Ceiling(applicator.RequiredRegion);

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

                int width = maxX - minX;
                PixelBlender<TPixel> blender = PixelOperations<TPixel>.Instance.GetPixelBlender(this.Options.BlenderMode);

                Parallel.For(
                minY,
                maxY,
                this.ParallelOptions,
                y =>
                {
                    int offsetY = y - polyStartY;

                    using (Buffer<float> amount = new Buffer<float>(width))
                    using (Buffer<TPixel> colors = new Buffer<TPixel>(width))
                    {
                        for (int i = 0; i < width; i++)
                        {
                            int x = i + minX;
                            int offsetX = x - startX;
                            PointInfo info = this.Path.GetPointInfo(offsetX, offsetY);
                            ColoredPointInfo<TPixel> color = applicator.GetColor(offsetX, offsetY, info);
                            amount[i] = (this.Opacity(color.DistanceFromElement) * this.Options.BlendPercentage).Clamp(0, 1);
                            colors[i] = color.Color;
                        }

                        Span<TPixel> destination = sourcePixels.GetRowSpan(offsetY).Slice(minX - startX, width);
                        blender.Blend(destination, destination, colors, amount);
                    }
                });
            }
        }

        /// <summary>
        /// Returns the correct opacity for the given distance.
        /// </summary>
        /// <param name="distance">Thw distance from the central point.</param>
        /// <returns>The <see cref="float"/></returns>
        private float Opacity(float distance)
        {
            if (distance <= 0)
            {
                return 1;
            }

            if (this.Options.Antialias && distance < AntialiasFactor)
            {
                return 1 - (distance / AntialiasFactor);
            }

            return 0;
        }
    }
}