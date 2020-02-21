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
    /// Provides the base methods to perform non-affine transforms on an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class ProjectiveTransformProcessor<TPixel> : TransformProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Size targetSize;
        private readonly IResampler resampler;
        private readonly Matrix4x4 transformMatrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectiveTransformProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="ProjectiveTransformProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public ProjectiveTransformProcessor(Configuration configuration, ProjectiveTransformProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
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
            if (this.transformMatrix.Equals(default) || this.transformMatrix.Equals(Matrix4x4.Identity))
            {
                // The clone will be blank here copy all the pixel data over
                source.GetPixelMemoryGroup().CopyTo(destination.GetPixelMemoryGroup());
                return;
            }

            int width = this.targetSize.Width;
            var targetBounds = new Rectangle(Point.Empty, this.targetSize);
            Configuration configuration = this.Configuration;

            // Convert from screen to world space.
            Matrix4x4.Invert(this.transformMatrix, out Matrix4x4 matrix);

            if (this.resampler is NearestNeighborResampler)
            {
                Rectangle sourceBounds = this.SourceRectangle;

                var nnOperation = new NearestNeighborRowIntervalOperation(sourceBounds, ref matrix, width, source, destination);
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

        private readonly struct NearestNeighborRowIntervalOperation : IRowIntervalOperation
        {
            private readonly Rectangle bounds;
            private readonly Matrix4x4 matrix;
            private readonly int maxX;
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;

            [MethodImpl(InliningOptions.ShortMethod)]
            public NearestNeighborRowIntervalOperation(
                Rectangle bounds,
                ref Matrix4x4 matrix,
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

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> destRow = this.destination.GetPixelRowSpan(y);

                    for (int x = 0; x < this.maxX; x++)
                    {
                        Vector2 point = TransformUtils.ProjectiveTransform2D(x, y, this.matrix);
                        int px = (int)MathF.Round(point.X);
                        int py = (int)MathF.Round(point.Y);

                        if (this.bounds.Contains(px, py))
                        {
                            destRow[x] = this.source[px, py];
                        }
                    }
                }
            }
        }

        private readonly struct RowIntervalOperation : IRowIntervalOperation<Vector4>
        {
            private readonly Configuration configuration;
            private readonly TransformKernelMap kernelMap;
            private readonly Matrix4x4 matrix;
            private readonly int maxX;
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowIntervalOperation(
                Configuration configuration,
                TransformKernelMap kernelMap,
                ref Matrix4x4 matrix,
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
                        Vector2 point = TransformUtils.ProjectiveTransform2D(x, y, this.matrix);
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
