// <copyright file="RotateProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the rotating of images.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class RotateProcessor<TColor> : Matrix3x2Processor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The transform matrix to apply.
        /// </summary>
        private Matrix3x2 processMatrix;

        /// <summary>
        /// Gets or sets the angle of processMatrix in degrees.
        /// </summary>
        public float Angle { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to expand the canvas to fit the rotated image.
        /// </summary>
        public bool Expand { get; set; } = true;

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            if (this.OptimizedApply(source))
            {
                return;
            }

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
                            Point transformedPoint = Point.Rotate(new Point(x, y), matrix);
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
            if (Math.Abs(this.Angle) < Constants.Epsilon || Math.Abs(this.Angle - 90) < Constants.Epsilon || Math.Abs(this.Angle - 180) < Constants.Epsilon || Math.Abs(this.Angle - 270) < Constants.Epsilon)
            {
                return;
            }

            this.processMatrix = Point.CreateRotation(new Point(0, 0), -this.Angle);
            if (this.Expand)
            {
                this.CreateNewCanvas(sourceRectangle, this.processMatrix);
            }
        }

        /// <summary>
        /// Rotates the images with an optimized method when the angle is 90, 180 or 270 degrees.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <returns>The <see cref="bool"/></returns>
        private bool OptimizedApply(ImageBase<TColor> source)
        {
            if (Math.Abs(this.Angle) < Constants.Epsilon)
            {
                // No need to do anything so return.
                return true;
            }

            if (Math.Abs(this.Angle - 90) < Constants.Epsilon)
            {
                this.Rotate90(source);
                return true;
            }

            if (Math.Abs(this.Angle - 180) < Constants.Epsilon)
            {
                this.Rotate180(source);
                return true;
            }

            if (Math.Abs(this.Angle - 270) < Constants.Epsilon)
            {
                this.Rotate270(source);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Rotates the image 270 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="source">The source image.</param>
        private void Rotate270(ImageBase<TColor> source)
        {
            int width = source.Width;
            int height = source.Height;
            TColor[] target = new TColor[width * height];

            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            using (PixelAccessor<TColor> targetPixels = target.Lock<TColor>(height, width))
            {
                Parallel.For(
                    0,
                    height,
                    this.ParallelOptions,
                    y =>
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int newX = height - y - 1;
                            newX = height - newX - 1;
                            int newY = width - x - 1;
                            targetPixels[newX, newY] = sourcePixels[x, y];
                        }
                    });
            }

            source.SetPixels(height, width, target);
        }

        /// <summary>
        /// Rotates the image 180 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="source">The source image.</param>
        private void Rotate180(ImageBase<TColor> source)
        {
            int width = source.Width;
            int height = source.Height;
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
                            int newX = width - x - 1;
                            int newY = height - y - 1;
                            targetPixels[newX, newY] = sourcePixels[x, y];
                        }
                    });
            }

            source.SetPixels(width, height, target);
        }

        /// <summary>
        /// Rotates the image 90 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="source">The source image.</param>
        private void Rotate90(ImageBase<TColor> source)
        {
            int width = source.Width;
            int height = source.Height;
            TColor[] target = new TColor[width * height];

            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            using (PixelAccessor<TColor> targetPixels = target.Lock<TColor>(height, width))
            {
                Parallel.For(
                    0,
                    height,
                    this.ParallelOptions,
                    y =>
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int newX = height - y - 1;
                            targetPixels[newX, x] = sourcePixels[x, y];
                        }
                    });
            }

            source.SetPixels(height, width, target);
        }
    }
}