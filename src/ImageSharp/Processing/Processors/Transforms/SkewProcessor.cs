// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Helpers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Provides methods that allow the skewing of images.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class SkewProcessor<TPixel> : AffineProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private Matrix3x2 transformMatrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="SkewProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="sampler">The sampler to perform the skew operation.</param>
        public SkewProcessor(IResampler sampler)
            : base(sampler)
        {
        }

        /// <summary>
        /// Gets or sets the angle of rotation along the x-axis in degrees.
        /// </summary>
        public float AngleX { get; set; }

        /// <summary>
        /// Gets or sets the angle of rotation along the y-axis in degrees.
        /// </summary>
        public float AngleY { get; set; }

        /// <inheritdoc/>
        protected override Matrix3x2 CreateProcessingMatrix()
        {
            if (this.transformMatrix == default(Matrix3x2))
            {
                this.transformMatrix = Matrix3x2Extensions.CreateSkewDegrees(-this.AngleX, -this.AngleY, PointF.Empty);
            }

            return this.transformMatrix;
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration)
        {
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
                                var transformedPoint = Point.Skew(new Point(x, y), matrix);
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

            Parallel.For(
                0,
                height,
                configuration.ParallelOptions,
                y =>
                    {
                        Span<TPixel> destRow = destination.GetPixelRowSpan(y);
                        for (int x = 0; x < width; x++)
                        {
                            var transformedPoint = Point.Skew(new Point(x, y), matrix);
                            if (sourceBounds.Contains(transformedPoint.X, transformedPoint.Y))
                            {
                                WeightsWindow windowX = this.HorizontalWeights.Weights[transformedPoint.X];
                                WeightsWindow windowY = this.VerticalWeights.Weights[transformedPoint.Y];

                                Vector4 dXY = this.ComputeWeightedSumAtPosition(source, maxX, maxY, ref windowX, ref windowY, ref transformedPoint);
                                ref TPixel dest = ref destRow[x];
                                dest.PackFromVector4(dXY);
                            }
                        }
                    });
        }
    }
}