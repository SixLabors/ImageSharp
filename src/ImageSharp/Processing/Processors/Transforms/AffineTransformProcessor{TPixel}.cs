// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Provides the base methods to perform affine transforms on an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class AffineTransformProcessor<TPixel> : TransformProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Size targetSize;
        private readonly Matrix3x2 transformMatrix;
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
                source.GetPixelMemoryGroup().CopyTo(destination.GetPixelMemoryGroup());
                return;
            }

            int width = this.targetSize.Width;
            var targetBounds = new Rectangle(Point.Empty, this.targetSize);
            Configuration configuration = this.Configuration;

            // Convert from screen to world space.
            Matrix3x2.Invert(this.transformMatrix, out Matrix3x2 matrix);

            if (this.resampler is NearestNeighborResampler)
            {
                var nnOperation = new NearestNeighborRowIntervalOperation(this.SourceRectangle, ref matrix, width, source, destination);
                ParallelRowIterator.IterateRows(
                    configuration,
                    targetBounds,
                    in nnOperation);

                return;
            }

            using var kernelMap = new TransformKernelMap(configuration, source.Size(), destination.Size(), this.resampler);

            var operation = new RowIntervalOperation(configuration, kernelMap, ref matrix, width, source, destination);
            ParallelRowIterator.IterateRows<RowIntervalOperation, Vector4>(
                configuration,
                targetBounds,
                in operation);
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the nearest neighbor resampler logic for <see cref="AffineTransformProcessor{T}"/>.
        /// </summary>
        private readonly struct NearestNeighborRowIntervalOperation : IRowIntervalOperation
        {
            private readonly Rectangle bounds;
            private readonly Matrix3x2 matrix;
            private readonly int maxX;
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;

            [MethodImpl(InliningOptions.ShortMethod)]
            public NearestNeighborRowIntervalOperation(
                Rectangle bounds,
                ref Matrix3x2 matrix,
                int maxX,
                ImageFrame<TPixel> source,
                ImageFrame<TPixel> destination)
            {
                this.bounds = bounds;
                this.matrix = matrix;
                this.maxX = maxX;
                this.source = source;
                this.destination = destination;
            }

            /// <inheritdoc/>
            /// <param name="rows"></param>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> destRow = this.destination.GetPixelRowSpan(y);

                    for (int x = 0; x < this.maxX; x++)
                    {
                        var point = Point.Transform(new Point(x, y), this.matrix);
                        if (this.bounds.Contains(point.X, point.Y))
                        {
                            destRow[x] = this.source[point.X, point.Y];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the transformation logic for <see cref="AffineTransformProcessor{T}"/>.
        /// </summary>
        private readonly struct RowIntervalOperation : IRowIntervalOperation<Vector4>
        {
            private readonly Configuration configuration;
            private readonly TransformKernelMap kernelMap;
            private readonly Matrix3x2 matrix;
            private readonly int maxX;
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowIntervalOperation(
                Configuration configuration,
                TransformKernelMap kernelMap,
                ref Matrix3x2 matrix,
                int maxX,
                ImageFrame<TPixel> source,
                ImageFrame<TPixel> destination)
            {
                this.configuration = configuration;
                this.kernelMap = kernelMap;
                this.matrix = matrix;
                this.maxX = maxX;
                this.source = source;
                this.destination = destination;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows, Span<Vector4> span)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> targetRowSpan = this.destination.GetPixelRowSpan(y);
                    PixelOperations<TPixel>.Instance.ToVector4(this.configuration, targetRowSpan, span);
                    ref float ySpanRef = ref this.kernelMap.GetYStartReference(y);
                    ref float xSpanRef = ref this.kernelMap.GetXStartReference(y);

                    for (int x = 0; x < this.maxX; x++)
                    {
                        // Use the single precision position to calculate correct bounding pixels
                        // otherwise we get rogue pixels outside of the bounds.
                        var point = Vector2.Transform(new Vector2(x, y), this.matrix);
                        this.kernelMap.Convolve(
                            point,
                            x,
                            ref ySpanRef,
                            ref xSpanRef,
                            this.source.PixelBuffer,
                            span);
                    }

                    PixelOperations<TPixel>.Instance.FromVector4Destructive(
                        this.configuration,
                        span,
                        targetRowSpan);
                }
            }
        }
    }
}
