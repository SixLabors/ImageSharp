// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
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
        protected override void OnApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            if (this.OptimizedApply(source, configuration))
            {
                return;
            }

            int height = this.CanvasRectangle.Height;
            int width = this.CanvasRectangle.Width;
            Matrix3x2 matrix = this.GetCenteredMatrix(source, this.processMatrix);
            Rectangle sourceBounds = source.Bounds();

            using (var targetPixels = new PixelAccessor<TPixel>(width, height))
            {
                Parallel.For(
                    0,
                    height,
                    configuration.ParallelOptions,
                    y =>
                    {
                        Span<TPixel> targetRow = targetPixels.GetRowSpan(y);

                        for (int x = 0; x < width; x++)
                        {
                            var transformedPoint = Point.Rotate(new Point(x, y), matrix);

                            if (sourceBounds.Contains(transformedPoint.X, transformedPoint.Y))
                            {
                                targetRow[x] = source[transformedPoint.X, transformedPoint.Y];
                            }
                        }
                    });

                source.SwapPixelsBuffers(targetPixels);
            }
        }

        /// <inheritdoc/>
        protected override void BeforeApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
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

        /// <inheritdoc/>
        protected override void AfterImageApply(Image<TPixel> source, Rectangle sourceRectangle)
        {
            ExifProfile profile = source.MetaData.ExifProfile;
            if (profile == null)
            {
                return;
            }

            if (MathF.Abs(this.Angle) < Constants.Epsilon)
            {
                // No need to do anything so return.
                return;
            }

            profile.RemoveValue(ExifTag.Orientation);

            if (this.Expand && profile.GetValue(ExifTag.PixelXDimension) != null)
            {
                profile.SetValue(ExifTag.PixelXDimension, source.Width);
                profile.SetValue(ExifTag.PixelYDimension, source.Height);
            }
        }

        /// <summary>
        /// Rotates the images with an optimized method when the angle is 90, 180 or 270 degrees.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>
        /// The <see cref="bool" />
        /// </returns>
        private bool OptimizedApply(ImageFrame<TPixel> source, Configuration configuration)
        {
            if (MathF.Abs(this.Angle) < Constants.Epsilon)
            {
                // No need to do anything so return.
                return true;
            }

            if (MathF.Abs(this.Angle - 90) < Constants.Epsilon)
            {
                this.Rotate90(source, configuration);
                return true;
            }

            if (MathF.Abs(this.Angle - 180) < Constants.Epsilon)
            {
                this.Rotate180(source, configuration);
                return true;
            }

            if (MathF.Abs(this.Angle - 270) < Constants.Epsilon)
            {
                this.Rotate270(source, configuration);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Rotates the image 270 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="configuration">The configuration.</param>
        private void Rotate270(ImageFrame<TPixel> source, Configuration configuration)
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
                        configuration.ParallelOptions,
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
        /// <param name="configuration">The configuration.</param>
        private void Rotate180(ImageFrame<TPixel> source, Configuration configuration)
        {
            int width = source.Width;
            int height = source.Height;

            using (var targetPixels = new PixelAccessor<TPixel>(width, height))
            {
                Parallel.For(
                    0,
                    height,
                    configuration.ParallelOptions,
                    y =>
                    {
                        Span<TPixel> sourceRow = source.GetPixelRowSpan(y);
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
        /// <param name="configuration">The configuration.</param>
        private void Rotate90(ImageFrame<TPixel> source, Configuration configuration)
        {
            int width = source.Width;
            int height = source.Height;

            using (var targetPixels = new PixelAccessor<TPixel>(height, width))
            {
                Parallel.For(
                    0,
                    height,
                    configuration.ParallelOptions,
                    y =>
                    {
                        Span<TPixel> sourceRow = source.GetPixelRowSpan(y);
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