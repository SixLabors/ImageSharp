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
    internal class AffineTransformProcessor<TPixel> : TransformProcessorBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Rectangle transformedRectangle;

        /// <summary>
        /// Initializes a new instance of the <see cref="AffineTransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The transform matrix</param>
        /// <param name="sampler">The sampler to perform the transform operation.</param>
        /// <param name="sourceSize">The source image size</param>
        public AffineTransformProcessor(Matrix3x2 matrix, IResampler sampler, Size sourceSize)
        {
            Guard.NotNull(sampler, nameof(sampler));
            this.Sampler = sampler;
            this.TransformMatrix = matrix;
            this.transformedRectangle = TransformUtils.GetTransformedRectangle(
                    new Rectangle(Point.Empty, sourceSize),
                    matrix);

            // We want to resize the canvas here taking into account any translations.
            this.TargetDimensions = new Size(this.transformedRectangle.Right, this.transformedRectangle.Bottom);

            // Handle a negative translation that exceeds the original with of the image.
            if (this.TargetDimensions.Width <= 0 || this.TargetDimensions.Height <= 0)
            {
                this.TargetDimensions = sourceSize;
            }
        }

        /// <summary>
        /// Gets the sampler to perform interpolation of the transform operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <summary>
        /// Gets the matrix used to supply the affine transform.
        /// </summary>
        public Matrix3x2 TransformMatrix { get; }

        /// <summary>
        /// Gets the target dimensions to constrain the transformed image to.
        /// </summary>
        public Size TargetDimensions { get; }

        /// <inheritdoc/>
        protected override Image<TPixel> CreateDestination(Image<TPixel> source, Rectangle sourceRectangle)
        {
            // We will always be creating the clone even for mutate because we may need to resize the canvas
            IEnumerable<ImageFrame<TPixel>> frames =
                source.Frames.Select(x => new ImageFrame<TPixel>(source.GetConfiguration(), this.TargetDimensions, x.MetaData.DeepClone()));

            // Use the overload to prevent an extra frame being added
            return new Image<TPixel>(source.GetConfiguration(), source.MetaData.DeepClone(), frames);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(
            ImageFrame<TPixel> source,
            ImageFrame<TPixel> destination,
            Rectangle sourceRectangle,
            Configuration configuration)
        {
            int height = this.TargetDimensions.Height;
            int width = this.TargetDimensions.Width;

            Rectangle sourceBounds = source.Bounds();
            var targetBounds = new Rectangle(Point.Empty, this.TargetDimensions);

            // Convert from screen to world space.
            Matrix3x2.Invert(this.TransformMatrix, out Matrix3x2 matrix);

            if (this.Sampler is NearestNeighborResampler)
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

            using (var kernel = new TransformKernelMap(configuration, source.Size(), destination.Size(), this.Sampler))
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
                                kernel.Convolve(point, x, ref ySpanRef, ref xSpanRef, source.PixelBuffer, vectorSpan);
                            }

                            PixelOperations<TPixel>.Instance.FromVector4(configuration, vectorSpan, targetRowSpan);
                        }
                    });
            }
        }
    }
}