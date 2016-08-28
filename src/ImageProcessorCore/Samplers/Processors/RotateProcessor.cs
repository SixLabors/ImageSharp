// <copyright file="RotateProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System;

namespace ImageProcessorCore.Processors
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the rotating of images.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class RotateProcessor<TColor, TPacked> : Matrix3x2Processor<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// The tranform matrix to apply.
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
        protected override void OnApply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            const float Epsilon = .0001F;

            if (Math.Abs(Angle) < Epsilon || Math.Abs(Angle - 90) < Epsilon || Math.Abs(Angle - 180) < Epsilon || Math.Abs(Angle - 270) < Epsilon)
            {
                return;
            }

            this.processMatrix = Point.CreateRotation(new Point(0, 0), -this.Angle);
            if (this.Expand)
            {
                CreateNewTarget(target, sourceRectangle, this.processMatrix);
            }
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            if (OptimizedApply(target, source))
            {
                return;
            }

            int height = target.Height;
            int width = target.Width;
            Matrix3x2 matrix = GetCenteredMatrix(target, source, this.processMatrix);

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock())
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

                        this.OnRowProcessed();
                    });
            }
        }

        /// <summary>
        /// Rotates the images with an optimized method when the angle is 90, 180 or 270 degrees.
        /// </summary>
        /// <param name="target">The target image.</param>
        /// <param name="source">The source image.</param>
        /// <returns></returns>
        private bool OptimizedApply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source)
        {
            const float Epsilon = .0001F;
            if (Math.Abs(Angle) < Epsilon)
            {
                target.ClonePixels(target.Width, target.Height, source.Pixels);
                return true;
            }

            if (Math.Abs(Angle - 90) < Epsilon)
            {
                this.Rotate90(target, source);
                return true;
            }

            if (Math.Abs(Angle - 180) < Epsilon)
            {
                this.Rotate180(target, source);
                return true;
            }

            if (Math.Abs(Angle - 270) < Epsilon)
            {
                this.Rotate270(target, source);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Rotates the image 270 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="target">The target image.</param>
        /// <param name="source">The source image.</param>
        private void Rotate270(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source)
        {
            int width = source.Width;
            int height = source.Height;
            Image<TColor, TPacked> temp = new Image<TColor, TPacked>(height, width);

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (PixelAccessor<TColor, TPacked> tempPixels = temp.Lock())
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
                            tempPixels[newX, newY] = sourcePixels[x, y];
                        }

                        this.OnRowProcessed();
                    });
            }

            target.SetPixels(height, width, temp.Pixels);
        }

        /// <summary>
        /// Rotates the image 180 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="target">The target image.</param>
        /// <param name="source">The source image.</param>
        private void Rotate180(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source)
        {
            int width = source.Width;
            int height = source.Height;

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock())
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

                        this.OnRowProcessed();
                    });
            }
        }

        /// <summary>
        /// Rotates the image 90 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="target">The target image.</param>
        /// <param name="source">The source image.</param>
        private void Rotate90(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source)
        {
            int width = source.Width;
            int height = source.Height;
            Image<TColor, TPacked> temp = new Image<TColor, TPacked>(height, width);

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (PixelAccessor<TColor, TPacked> tempPixels = temp.Lock())
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
                            tempPixels[newX, x] = sourcePixels[x, y];
                        }

                        this.OnRowProcessed();
                    });
            }

            target.SetPixels(height, width, temp.Pixels);
        }
    }
}