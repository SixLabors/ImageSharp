// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Advanced.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Provides the base methods to perform affine transforms on an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class AffineTransformProcessor<TPixel> : TransformProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private Size targetSize;
        private Matrix3x2 transformMatrix;
        private readonly IResampler resampler;

        /// <summary>
        /// Initializes a new instance of the <see cref="AffineTransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="AffineTransformProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public AffineTransformProcessor(Configuration configuration, AffineTransformProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.targetSize = definition.TargetDimensions;
            this.transformMatrix = definition.TransformMatrix;
            this.resampler = definition.Sampler;
        }

        protected override Size GetTargetSize() => this.targetSize;

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        {
            // Handle transforms that result in output identical to the original.
            if (this.transformMatrix.Equals(default) || this.transformMatrix.Equals(Matrix3x2.Identity))
            {
                // The clone will be blank here copy all the pixel data over
                source.GetPixelSpan().CopyTo(destination.GetPixelSpan());
                return;
            }

            int width = this.targetSize.Width;
            Rectangle sourceBounds = this.SourceRectangle;
            var targetBounds = new Rectangle(Point.Empty, this.targetSize);
            Configuration configuration = this.Configuration;

            // Convert from screen to world space.
            Matrix3x2.Invert(this.transformMatrix, out Matrix3x2 matrix);

            if (this.resampler is NearestNeighborResampler)
            {
                ParallelHelper.IterateRows(
                    targetBounds,
                    configuration,
                    rows =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                Span<TPixel> destRow = destination.GetPixelRowSpan(y);

                                for (int x = 0; x < width; x++)
                                {
                                    var point = Point.Transform(new Point(x, y), matrix);
                                    if (sourceBounds.Contains(point.X, point.Y))
                                    {
                                        destRow[x] = source[point.X, point.Y];
                                    }
                                }
                            }
                        });

                return;
            }

            var kernel = new TransformKernelMap(configuration, source.Size(), destination.Size(), this.resampler);

            try
            {
                ParallelHelper.IterateRowsWithTempBuffer<Vector4>(
                    targetBounds,
                    configuration,
                    (rows, vectorBuffer) =>
                        {
                            Span<Vector4> vectorSpan = vectorBuffer.Span;
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                Span<TPixel> targetRowSpan = destination.GetPixelRowSpan(y);
                                PixelOperations<TPixel>.Instance.ToVector4(configuration, targetRowSpan, vectorSpan);
                                ref float ySpanRef = ref kernel.GetYStartReference(y);
                                ref float xSpanRef = ref kernel.GetXStartReference(y);

                                for (int x = 0; x < width; x++)
                                {
                                    // Use the single precision position to calculate correct bounding pixels
                                    // otherwise we get rogue pixels outside of the bounds.
                                    var point = Vector2.Transform(new Vector2(x, y), matrix);
                                    kernel.Convolve(
                                        point,
                                        x,
                                        ref ySpanRef,
                                        ref xSpanRef,
                                        source.PixelBuffer,
                                        vectorSpan);
                                }

                                PixelOperations<TPixel>.Instance.FromVector4Destructive(
                                    configuration,
                                    vectorSpan,
                                    targetRowSpan);
                            }
                        });
            }
            finally
            {
                kernel.Dispose();
            }
        }
    }
}
