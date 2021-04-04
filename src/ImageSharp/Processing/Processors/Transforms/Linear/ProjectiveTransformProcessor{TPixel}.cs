// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Provides the base methods to perform non-affine transforms on an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class ProjectiveTransformProcessor<TPixel> : TransformProcessor<TPixel>, IResamplingTransformImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly Size destinationSize;
        private readonly IResampler resampler;
        private readonly Matrix4x4 transformMatrix;
        private ImageFrame<TPixel> source;
        private ImageFrame<TPixel> destination;

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
            this.destinationSize = definition.DestinationSize;
            this.transformMatrix = definition.TransformMatrix;
            this.resampler = definition.Sampler;
        }

        protected override Size GetDestinationSize() => this.destinationSize;

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        {
            this.source = source;
            this.destination = destination;
            this.resampler.ApplyTransform(this);
        }

        /// <inheritdoc/>
        public void ApplyTransform<TResampler>(in TResampler sampler)
            where TResampler : struct, IResampler
        {
            Configuration configuration = this.Configuration;
            ImageFrame<TPixel> source = this.source;
            ImageFrame<TPixel> destination = this.destination;
            Matrix4x4 matrix = this.transformMatrix;

            // Handle transforms that result in output identical to the original.
            if (matrix.Equals(default) || matrix.Equals(Matrix4x4.Identity))
            {
                // The clone will be blank here copy all the pixel data over
                source.GetPixelMemoryGroup().CopyTo(destination.GetPixelMemoryGroup());
                return;
            }

            // Convert from screen to world space.
            Matrix4x4.Invert(matrix, out matrix);

            if (sampler is NearestNeighborResampler)
            {
                var nnOperation = new NNProjectiveOperation(source, destination, matrix);
                ParallelRowIterator.IterateRows(
                    configuration,
                    destination.Bounds(),
                    in nnOperation);

                return;
            }

            var operation = new ProjectiveOperation<TResampler>(
                configuration,
                source,
                destination,
                in sampler,
                matrix);

            ParallelRowIterator.IterateRowIntervals<ProjectiveOperation<TResampler>, Vector4>(
                configuration,
                destination.Bounds(),
                in operation);
        }

        private readonly struct NNProjectiveOperation : IRowOperation
        {
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;
            private readonly Rectangle bounds;
            private readonly Matrix4x4 matrix;
            private readonly int maxX;

            [MethodImpl(InliningOptions.ShortMethod)]
            public NNProjectiveOperation(
                ImageFrame<TPixel> source,
                ImageFrame<TPixel> destination,
                Matrix4x4 matrix)
            {
                this.source = source;
                this.destination = destination;
                this.bounds = source.Bounds();
                this.matrix = matrix;
                this.maxX = destination.Width;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y)
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

        private readonly struct ProjectiveOperation<TResampler> : IRowIntervalOperation<Vector4>
            where TResampler : struct, IResampler
        {
            private readonly Configuration configuration;
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;
            private readonly TResampler sampler;
            private readonly Matrix4x4 matrix;

            [MethodImpl(InliningOptions.ShortMethod)]
            public ProjectiveOperation(
                Configuration configuration,
                ImageFrame<TPixel> source,
                ImageFrame<TPixel> destination,
                in TResampler sampler,
                Matrix4x4 matrix)
            {
                this.configuration = configuration;
                this.source = source;
                this.destination = destination;
                this.sampler = sampler;
                this.matrix = matrix;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows, Span<Vector4> span)
            {
                MemoryAllocator allocator = this.configuration.MemoryAllocator;
                Matrix4x4 matrix = this.matrix;

                using var horizontalKernelMap = new LinearTransformKernelFactory<TResampler>(
                        this.sampler,
                        this.source.Width,
                        this.destination.Width,
                        allocator);

                using var verticalKernelMap = new LinearTransformKernelFactory<TResampler>(
                      this.sampler,
                      this.source.Height,
                      this.destination.Height,
                      allocator);

                Buffer2D<TPixel> sourceBuffer = this.source.PixelBuffer;

                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> rowSpan = this.destination.GetPixelRowSpan(y);
                    PixelOperations<TPixel>.Instance.ToVector4(
                        this.configuration,
                        rowSpan,
                        span);

                    for (int x = 0; x < span.Length; x++)
                    {
                        Vector2 point = TransformUtils.ProjectiveTransform2D(x, y, matrix);
                        LinearTransformKernel yKernel = verticalKernelMap.BuildKernel(point.Y);
                        LinearTransformKernel xKernel = horizontalKernelMap.BuildKernel(point.X);
                        ref float yWeightsRef = ref MemoryMarshal.GetReference(yKernel.Values);
                        ref float xWeightsRef = ref MemoryMarshal.GetReference(xKernel.Values);

                        int top = yKernel.Start;
                        int bottom = yKernel.End;
                        int left = xKernel.Start;
                        int right = xKernel.End;

                        Vector4 sum = Vector4.Zero;
                        for (int yK = top; yK <= bottom; yK++)
                        {
                            float yWeight = Unsafe.Add(ref yWeightsRef, yK - top);

                            for (int xK = left; xK <= right; xK++)
                            {
                                float xWeight = Unsafe.Add(ref xWeightsRef, xK - left);

                                var current = sourceBuffer.GetElementUnsafe(xK, yK).ToVector4();
                                Numerics.Premultiply(ref current);
                                sum += current * xWeight * yWeight;
                            }
                        }

                        span[x] = sum;
                    }

                    Numerics.UnPremultiply(span);
                    PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, span, rowSpan);
                }
            }
        }
    }
}
