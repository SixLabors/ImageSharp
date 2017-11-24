// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Helpers;
using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Provides methods that allow the rotating of images.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class RotateProcessor<TPixel> : AffineProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private Matrix3x2 transformMatrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="RotateProcessor{TPixel}"/> class.
        /// </summary>
        public RotateProcessor()
            : base(KnownResamplers.NearestNeighbor)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RotateProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="sampler">The sampler to perform the rotating operation.</param>
        public RotateProcessor(IResampler sampler)
            : base(sampler)
        {
        }

        /// <summary>
        /// Gets or sets the angle of processMatrix in degrees.
        /// </summary>
        public float Angle { get; set; }

        /// <inheritdoc/>
        protected override Matrix3x2 CreateProcessingMatrix()
        {
            if (this.transformMatrix == default(Matrix3x2))
            {
                this.transformMatrix = Matrix3x2Extensions.CreateRotationDegrees(-this.Angle, PointF.Empty);
            }

            return this.transformMatrix;
        }

        /// <inheritdoc/>
        protected override void CreateNewCanvas(Rectangle sourceRectangle)
        {
            if (MathF.Abs(this.Angle) < Constants.Epsilon ||
                MathF.Abs(this.Angle - 180) < Constants.Epsilon)
            {
                this.ResizeRectangle = sourceRectangle;
            }

            if (MathF.Abs(this.Angle - 90) < Constants.Epsilon ||
                MathF.Abs(this.Angle - 270) < Constants.Epsilon)
            {
                // We always expand enumerated rectangle values
                this.ResizeRectangle = new Rectangle(0, 0, sourceRectangle.Height, sourceRectangle.Width);
            }

            base.CreateNewCanvas(sourceRectangle);
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration)
        {
            if (this.OptimizedApply(source, destination, configuration))
            {
                return;
            }

            int height = this.ResizeRectangle.Height;
            int width = this.ResizeRectangle.Width;
            Matrix3x2 matrix = this.GetCenteredMatrix(source);
            Rectangle sourceBounds = source.Bounds();

            if (this.Sampler is NearestNeighborResampler)
            {
                Parallel.For(
                    0,
                    height,
                    configuration.ParallelOptions,
                    y =>
                        {
                            Span<TPixel> destRow = destination.GetPixelRowSpan(y);

                            for (int x = 0; x < width; x++)
                            {
                                var transformedPoint = Point.Rotate(new Point(x, y), matrix);
                                if (sourceBounds.Contains(transformedPoint.X, transformedPoint.Y))
                                {
                                    destRow[x] = source[transformedPoint.X, transformedPoint.Y];
                                }
                            }
                        });

                return;
            }

            int maxX = source.Width - 1;
            int maxY = source.Height - 1;
            int radius = Math.Max((int)this.Sampler.Radius, 1);

            Parallel.For(
                0,
                height,
                configuration.ParallelOptions,
                y =>
                    {
                        Span<TPixel> destRow = destination.GetPixelRowSpan(y);
                        for (int x = 0; x < width; x++)
                        {
                            var transformedPoint = Point.Rotate(new Point(x, y), matrix);
                            if (sourceBounds.Contains(transformedPoint.X, transformedPoint.Y))
                            {
                                WeightsWindow windowX = this.HorizontalWeights.Weights[transformedPoint.X];
                                WeightsWindow windowY = this.VerticalWeights.Weights[transformedPoint.Y];

                                Vector4 dXY = this.ComputeWeightedSumAtPosition(source, maxX, maxY, radius, ref windowX, ref windowY, ref transformedPoint);
                                ref TPixel dest = ref destRow[x];
                                dest.PackFromVector4(dXY);
                            }
                        }
                    });
        }

        /// <inheritdoc/>
        protected override void AfterImageApply(Image<TPixel> source, Image<TPixel> destination, Rectangle sourceRectangle)
        {
            ExifProfile profile = destination.MetaData.ExifProfile;
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
        /// <param name="destination">The destination image.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>
        /// The <see cref="bool" />
        /// </returns>
        private bool OptimizedApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Configuration configuration)
        {
            if (MathF.Abs(this.Angle) < Constants.Epsilon)
            {
                // The destination will be blank here so copy all the pixel data over
                source.GetPixelSpan().CopyTo(destination.GetPixelSpan());
                return true;
            }

            if (MathF.Abs(this.Angle - 90) < Constants.Epsilon)
            {
                this.Rotate90(source, destination, configuration);
                return true;
            }

            if (MathF.Abs(this.Angle - 180) < Constants.Epsilon)
            {
                this.Rotate180(source, destination, configuration);
                return true;
            }

            if (MathF.Abs(this.Angle - 270) < Constants.Epsilon)
            {
                this.Rotate270(source, destination, configuration);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Rotates the image 270 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="destination">The destination image.</param>
        /// <param name="configuration">The configuration.</param>
        private void Rotate270(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Configuration configuration)
        {
            int width = source.Width;
            int height = source.Height;

            Parallel.For(
                0,
                height,
                configuration.ParallelOptions,
                y =>
                {
                    Span<TPixel> sourceRow = source.GetPixelRowSpan(y);
                    for (int x = 0; x < width; x++)
                    {
                        int newX = height - y - 1;
                        newX = height - newX - 1;
                        int newY = width - x - 1;

                        destination[newX, newY] = sourceRow[x];
                    }
                });
        }

        /// <summary>
        /// Rotates the image 180 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="destination">The destination image.</param>
        /// <param name="configuration">The configuration.</param>
        private void Rotate180(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Configuration configuration)
        {
            int width = source.Width;
            int height = source.Height;

            Parallel.For(
                0,
                height,
                configuration.ParallelOptions,
                y =>
                {
                    Span<TPixel> sourceRow = source.GetPixelRowSpan(y);
                    Span<TPixel> targetRow = destination.GetPixelRowSpan(height - y - 1);

                    for (int x = 0; x < width; x++)
                    {
                        targetRow[width - x - 1] = sourceRow[x];
                    }
                });
        }

        /// <summary>
        /// Rotates the image 90 degrees clockwise at the centre point.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="destination">The destination image.</param>
        /// <param name="configuration">The configuration.</param>
        private void Rotate90(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Configuration configuration)
        {
            int width = source.Width;
            int height = source.Height;

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
                        destination[newX, x] = sourceRow[x];
                    }
                });
        }
    }
}