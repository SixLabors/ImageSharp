// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.ParallelUtils;
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
        public AffineTransformProcessor(AffineTransformProcessor definition)
        {
            this.Definition = definition;
        }

        protected AffineTransformProcessor Definition { get; }

        private Size TargetDimensions => this.Definition.TargetDimensions;

        private Matrix3x2 TransformMatrix => this.Definition.TransformMatrix;

        /// <inheritdoc/>
        protected override Image<TPixel> CreateDestination(Image<TPixel> source, Rectangle sourceRectangle)
        {
            // We will always be creating the clone even for mutate because we may need to resize the canvas
            IEnumerable<ImageFrame<TPixel>> frames = source.Frames.Select<ImageFrame<TPixel>, ImageFrame<TPixel>>(
                x => new ImageFrame<TPixel>(source.GetConfiguration(), this.TargetDimensions, x.Metadata.DeepClone()));

            // Use the overload to prevent an extra frame being added
            return new Image<TPixel>(source.GetConfiguration(), source.Metadata.DeepClone(), frames);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(
            ImageFrame<TPixel> source,
            ImageFrame<TPixel> destination,
            Rectangle sourceRectangle,
            Configuration configuration)
        {
            // Handle transforms that result in output identical to the original.
            if (this.TransformMatrix.Equals(default) || this.TransformMatrix.Equals(Matrix3x2.Identity))
            {
                // The clone will be blank here copy all the pixel data over
                source.GetPixelSpan().CopyTo(destination.GetPixelSpan());
                return;
            }

            int width = this.TargetDimensions.Width;
            var targetBounds = new Rectangle(Point.Empty, this.TargetDimensions);

            // Convert from screen to world space.
            Matrix3x2.Invert(this.TransformMatrix, out Matrix3x2 matrix);

            var sampler = this.Definition.Sampler;

            if (sampler is NearestNeighborResampler)
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
                                    if (sourceRectangle.Contains(point.X, point.Y))
                                    {
                                        destRow[x] = source[point.X, point.Y];
                                    }
                                }
                            }
                        });

                return;
            }

            var kernel = new TransformKernelMap(configuration, source.Size(), destination.Size(), sampler);
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
