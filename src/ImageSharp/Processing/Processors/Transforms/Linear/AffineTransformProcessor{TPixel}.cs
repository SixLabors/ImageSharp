// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
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
    internal class AffineTransformProcessor<TPixel> : TransformProcessor<TPixel>, IResamplingTransformImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly Size destinationSize;
        private readonly Matrix3x2 transformMatrix;
        private readonly IResampler resampler;
        private ImageFrame<TPixel> source;
        private ImageFrame<TPixel> destination;

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
            Matrix3x2 matrix = this.transformMatrix;

            // Handle transforms that result in output identical to the original.
            if (matrix.Equals(default) || matrix.Equals(Matrix3x2.Identity))
            {
                // The clone will be blank here copy all the pixel data over
                source.GetPixelMemoryGroup().CopyTo(destination.GetPixelMemoryGroup());
                return;
            }

            // Convert from screen to world space.
            Matrix3x2.Invert(matrix, out matrix);

            if (sampler is NearestNeighborResampler)
            {
                var nnOperation = new NNAffineOperation(source, destination, matrix);
                ParallelRowIterator.IterateRows(
                    configuration,
                    destination.Bounds(),
                    in nnOperation);

                return;
            }

            var operation = new AffineOperation<TResampler>(
                configuration,
                source,
                destination,
                in sampler,
                matrix);

            ParallelRowIterator.IterateRowIntervals<AffineOperation<TResampler>, Vector4>(
                configuration,
                destination.Bounds(),
                in operation);
        }

        private readonly struct NNAffineOperation : IRowOperation
        {
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;
            private readonly Rectangle bounds;
            private readonly Matrix3x2 matrix;

            [MethodImpl(InliningOptions.ShortMethod)]
            public NNAffineOperation(
                ImageFrame<TPixel> source,
                ImageFrame<TPixel> destination,
                Matrix3x2 matrix)
            {
                this.source = source;
                this.destination = destination;
                this.bounds = source.Bounds();
                this.matrix = matrix;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y)
            {
                Buffer2D<TPixel> sourceBuffer = this.source.PixelBuffer;
                Span<TPixel> destRow = this.destination.GetPixelRowSpan(y);

                for (int x = 0; x < destRow.Length; x++)
                {
                    var point = Vector2.Transform(new Vector2(x, y), this.matrix);
                    int px = (int)MathF.Round(point.X);
                    int py = (int)MathF.Round(point.Y);

                    if (this.bounds.Contains(px, py))
                    {
                        destRow[x] = sourceBuffer.GetElementUnsafe(px, py);
                    }
                }
            }
        }

        private readonly struct AffineOperation<TResampler> : IRowIntervalOperation<Vector4>
            where TResampler : struct, IResampler
        {
            private readonly Configuration configuration;
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;
            private readonly TResampler sampler;
            private readonly Matrix3x2 matrix;
            private readonly float yRadius;
            private readonly float xRadius;

            [MethodImpl(InliningOptions.ShortMethod)]
            public AffineOperation(
                Configuration configuration,
                ImageFrame<TPixel> source,
                ImageFrame<TPixel> destination,
                in TResampler sampler,
                Matrix3x2 matrix)
            {
                this.configuration = configuration;
                this.source = source;
                this.destination = destination;
                this.sampler = sampler;
                this.matrix = matrix;

                this.yRadius = LinearTransformUtility.GetSamplingRadius(in sampler, source.Height, destination.Height);
                this.xRadius = LinearTransformUtility.GetSamplingRadius(in sampler, source.Width, destination.Width);
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows, Span<Vector4> span)
            {
                Matrix3x2 matrix = this.matrix;
                TResampler sampler = this.sampler;
                float yRadius = this.yRadius;
                float xRadius = this.xRadius;
                int maxY = this.source.Height - 1;
                int maxX = this.source.Width - 1;

                Buffer2D<TPixel> sourceBuffer = this.source.PixelBuffer;

                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> rowSpan = this.destination.GetPixelRowSpan(y);
                    PixelOperations<TPixel>.Instance.ToVector4(
                        this.configuration,
                        rowSpan,
                        span,
                        PixelConversionModifiers.Scale);

                    for (int x = 0; x < span.Length; x++)
                    {
                        var point = Vector2.Transform(new Vector2(x, y), matrix);
                        float pY = point.Y;
                        float pX = point.X;

                        int top = LinearTransformUtility.GetRangeStart(yRadius, pY, maxY);
                        int bottom = LinearTransformUtility.GetRangeEnd(yRadius, pY, maxY);
                        int left = LinearTransformUtility.GetRangeStart(xRadius, pX, maxX);
                        int right = LinearTransformUtility.GetRangeEnd(xRadius, pX, maxX);

                        if (bottom == top || right == left)
                        {
                            continue;
                        }

                        Vector4 sum = Vector4.Zero;
                        for (int yK = top; yK <= bottom; yK++)
                        {
                            float yWeight = sampler.GetValue(yK - pY);

                            for (int xK = left; xK <= right; xK++)
                            {
                                float xWeight = sampler.GetValue(xK - pX);

                                Vector4 current = sourceBuffer.GetElementUnsafe(xK, yK).ToScaledVector4();
                                Numerics.Premultiply(ref current);
                                sum += current * xWeight * yWeight;
                            }
                        }

                        Numerics.UnPremultiply(ref sum);
                        span[x] = sum;
                    }

                    // Numerics.UnPremultiply(span);
                    PixelOperations<TPixel>.Instance.FromVector4Destructive(
                        this.configuration,
                        span,
                        rowSpan,
                        PixelConversionModifiers.Scale);
                }
            }
        }
    }
}
