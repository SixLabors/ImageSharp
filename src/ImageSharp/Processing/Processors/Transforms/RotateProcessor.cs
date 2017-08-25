// <copyright file="RotateProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;
    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    /// <summary>
    /// Provides methods that allow the rotating of images.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class RotateProcessor<TPixel> : Matrix3x2Processor<TPixel>
        where TPixel : struct, IPixel<TPixel>
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
        protected override void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            if (this.OptimizedApply(source))
            {
                return;
            }

            int height = this.CanvasRectangle.Height;
            int width = this.CanvasRectangle.Width;
            Matrix3x2 matrix = this.GetCenteredMatrix(source, this.processMatrix);

            using (var targetPixels = new PixelAccessor<TPixel>(width, height))
            {
                Parallel.For(
                    0,
                    height,
                    this.ParallelOptions,
                    y =>
                    {
                        Span<TPixel> targetRow = targetPixels.GetRowSpan(y);

                        for (int x = 0; x < width; x++)
                        {
                            var transformedPoint = Point.Rotate(new Point(x, y), matrix);

                            if (source.Bounds.Contains(transformedPoint.X, transformedPoint.Y))
                            {
                                targetRow[x] = source[transformedPoint.X, transformedPoint.Y];
                            }
                        }
                    });

                source.SwapPixelsBuffers(targetPixels);
            }
        }

        /// <inheritdoc/>
        protected override void BeforeApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            if (MathF.Abs(this.Angle) < Constants.Epsilon || MathF.Abs(this.Angle - 90) < Constants.Epsilon || MathF.Abs(this.Angle - 180) < Constants.Epsilon || MathF.Abs(this.Angle - 270) < Constants.Epsilon)
            {
                return;
            }

            this.processMatrix = Matrix3x2Extensions.CreateRotationDegrees(-this.Angle, new Point(0, 0));
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
        private bool OptimizedApply(ImageBase<TPixel> source)
        {
            if (MathF.Abs(this.Angle) < Constants.Epsilon)
            {
                // No need to do anything so return.
                return true;
            }

            if (MathF.Abs(this.Angle - 90) < Constants.Epsilon)
            {
                this.Rotate90(source);
                return true;
            }

            if (MathF.Abs(this.Angle - 180) < Constants.Epsilon)
            {
                this.Rotate180(source);
                return true;
            }

            if (MathF.Abs(this.Angle - 270) < Constants.Epsilon)
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
        private void Rotate270(ImageBase<TPixel> source)
        {
            int width = source.Width;
            int height = source.Height;

            using (var targetPixels = new PixelAccessor<TPixel>(height, width))
            {
                using (PixelAccessor<TPixel> sourcePixels = source.Lock())
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

                source.SwapPixelsBuffers(targetPixels);
            }
        }

        /// <summary>
        /// Rotates the image 180 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="source">The source image.</param>
        private void Rotate180(ImageBase<TPixel> source)
        {
            int width = source.Width;
            int height = source.Height;

            using (var targetPixels = new PixelAccessor<TPixel>(width, height))
            {
                Parallel.For(
                    0,
                    height,
                    this.ParallelOptions,
                    y =>
                    {
                        Span<TPixel> sourceRow = source.GetRowSpan(y);
                        Span<TPixel> targetRow = targetPixels.GetRowSpan(height - y - 1);

                        for (int x = 0; x < width; x++)
                        {
                            targetRow[width - x - 1] = sourceRow[x];
                        }
                    });

                source.SwapPixelsBuffers(targetPixels);
            }
        }

        /// <summary>
        /// Rotates the image 90 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="source">The source image.</param>
        private void Rotate90(ImageBase<TPixel> source)
        {
            int width = source.Width;
            int height = source.Height;

            using (var targetPixels = new PixelAccessor<TPixel>(height, width))
            {
                Parallel.For(
                    0,
                    height,
                    this.ParallelOptions,
                    y =>
                    {
                        Span<TPixel> sourceRow = source.GetRowSpan(y);
                        int newX = height - y - 1;
                        for (int x = 0; x < width; x++)
                        {
                            targetPixels[newX, x] = sourceRow[x];
                        }
                    });

                source.SwapPixelsBuffers(targetPixels);
            }
        }
    }
}