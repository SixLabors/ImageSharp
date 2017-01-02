// <copyright file="SkewProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the skewing of images.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class SkewProcessor<TColor> : Matrix3x2Processor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The transform matrix to apply.
        /// </summary>
        private Matrix3x2 processMatrix;

        /// <summary>
        /// Gets or sets the angle of rotation along the x-axis in degrees.
        /// </summary>
        public float AngleX { get; set; }

        /// <summary>
        /// Gets or sets the angle of rotation along the y-axis in degrees.
        /// </summary>
        public float AngleY { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to expand the canvas to fit the skewed image.
        /// </summary>
        public bool Expand { get; set; } = true;

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            int height = this.CanvasRectangle.Height;
            int width = this.CanvasRectangle.Width;
            Matrix3x2 matrix = this.GetCenteredMatrix(source, this.processMatrix);
            TColor[] target = new TColor[width * height];

            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            using (PixelAccessor<TColor> targetPixels = target.Lock<TColor>(width, height))
            {
                Parallel.For(
                    0,
                    height,
                    this.ParallelOptions,
                    y =>
                        {
                            for (int x = 0; x < width; x++)
                            {
                                Point transformedPoint = Point.Skew(new Point(x, y), matrix);
                                if (source.Bounds.Contains(transformedPoint.X, transformedPoint.Y))
                                {
                                    targetPixels[x, y] = sourcePixels[transformedPoint.X, transformedPoint.Y];
                                }
                            }
                        });
            }

            source.SetPixels(width, height, target);
        }

        /// <inheritdoc/>
        protected override void BeforeApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            this.processMatrix = Point.CreateSkew(new Point(0, 0), -this.AngleX, -this.AngleY);
            if (this.Expand)
            {
                this.CreateNewCanvas(sourceRectangle, this.processMatrix);
            }
        }
    }
}