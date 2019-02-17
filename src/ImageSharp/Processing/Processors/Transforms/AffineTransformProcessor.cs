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
        /// <summary>
        /// Initializes a new instance of the <see cref="AffineTransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The transform matrix.</param>
        /// <param name="sampler">The sampler to perform the transform operation.</param>
        /// <param name="targetDimensions">The target dimensions.</param>
        public AffineTransformProcessor(Matrix3x2 matrix, IResampler sampler, Size targetDimensions)
        {
            Guard.NotNull(sampler, nameof(sampler));
            this.Sampler = sampler;
            this.TransformMatrix = matrix;
            this.TargetDimensions = targetDimensions;
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
            // Handle tranforms that result in output identical to the original.
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
                                if (sourceRectangle.Contains(point.X, point.Y))
                                {
                                    destRow[x] = source[point.X, point.Y];
                                }
                            }
                        }
                    });

                return;
            }

            var kernel = new TransformKernelMap(configuration, source.Size(), destination.Size(), this.Sampler);
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
                                kernel.Convolve(point, x, ref ySpanRef, ref xSpanRef, source.PixelBuffer, vectorSpan);
                            }

                            PixelOperations<TPixel>.Instance.FromVector4(configuration, vectorSpan, targetRowSpan);
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