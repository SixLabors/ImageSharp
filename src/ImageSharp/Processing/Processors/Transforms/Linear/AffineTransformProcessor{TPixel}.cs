// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

            // Since all image frame dimensions have to be the same we can calculate
            // the kernel maps and reuse for all frames.
            MemoryAllocator allocator = configuration.MemoryAllocator;
            //using var horizontalKernelMap = ResizeKernelMap.Calculate(
            //    in sampler,
            //    destination.Width,
            //    source.Width,
            //    allocator);
            using var horizontalKernelMap = new LinearTransformKernelFactory<TResampler>(
                allocator,
                sampler,
                source.Width,
                destination.Width);

            //using var verticalKernelMap = ResizeKernelMap.Calculate(
            //    in sampler,
            //    destination.Height,
            //    source.Height,
            //    allocator);
            using var verticalKernelMap = new LinearTransformKernelFactory<TResampler>(
                allocator,
                sampler,
                source.Height,
                destination.Height);

            using IMemoryOwner<Vector4> destVectors = configuration.MemoryAllocator.Allocate<Vector4>(destination.Width);
            Span<Vector4> span = destVectors.GetSpan();

            int sourceHeight = source.Height - 1;
            int sourceWidth = source.Width - 1;
            Rectangle bounds = source.Bounds();
            for (int y = 0; y < destination.Height; y++)
            {
                Span<TPixel> rowSpan = destination.GetPixelRowSpan(y);
                PixelOperations<TPixel>.Instance.ToVector4(
                    configuration,
                    rowSpan,
                    span);

                for (int x = 0; x < destination.Width; x++)
                {
                    var point = Vector2.Transform(new Vector2(x, y), matrix);
                    int pY = (int)point.Y;
                    int pX = (int)point.X;

                    if (bounds.Contains(pX, pY))
                    {
                        Vector4 sum = Vector4.Zero;
                        var yKernel = verticalKernelMap.GetKernel(y, point.Y);
                        var xKernel = horizontalKernelMap.GetKernel(x, point.X);
                        int yRadius = yKernel.Length / 2;
                        int xRadius = xKernel.Length / 2;
                        Span<float> yWeights = yKernel.Values;
                        Span<float> xWeights = xKernel.Values;

                        int top = Math.Max(pY - yRadius, 0);
                        int bottom = Math.Min(pY + yRadius, sourceHeight);
                        int right = Math.Min(pX + xRadius, sourceWidth);

                        for (int yy = 0; yy < yWeights.Length; yy++)
                        {
                            top = Math.Min(top, bottom);
                            int left = Math.Max(pX - xRadius, 0);
                            float yW = yWeights[yy];

                            for (int xx = 0; xx < xWeights.Length; xx++)
                            {
                                float xW = xWeights[xx];

                                left = Math.Min(left, right);
                                var current = source[left, top].ToVector4();
                                Numerics.Premultiply(ref current);

                                sum += current * yW * yW;
                                left++;
                            }

                            top++;
                        }

                        //for (int yy = top, kY = 0; yy <= bottom; yy++, kY++)
                        //{
                        //    var yW = yKernel.Values[kY];
                        //    for (int xx = left, kX = 0; xx <= right; xx++, kX++)
                        //    {
                        //        try
                        //        {
                        //            float xW = xKernel.Values[kX];
                        //            var current = source[xx, yy].ToVector4();
                        //            Numerics.Premultiply(ref current);
                        //            sum += current * yW * yW;
                        //        }
                        //        catch
                        //        {
                        //            throw;
                        //        }
                        //    }
                        //}

                        Numerics.UnPremultiply(ref sum);
                        // sum.W = 1; // Super hack so I can see the output.
                        span[x] = sum;

                        //ResizeKernel.Enumerator yE = yKernel.GetEnumerator();
                        //ResizeKernel.Enumerator xE = xKernel.GetEnumerator();

                        //// Theoretically we want to sample in both directions for the full kernel.
                        //// The below enumerators are attempting to do this but it doesn't work.
                        //while (yE.MoveNext())
                        //{
                        //    if (sourceY > maxY)
                        //    {
                        //        continue;
                        //    }

                        //    while (xE.MoveNext())
                        //    {
                        //        if (sourceX > maxX)
                        //        {
                        //            continue;
                        //        }

                        //        var current = source[sourceX, sourceY].ToVector4();
                        //        Numerics.Premultiply(ref current);
                        //        sum += current * xE.Current * yE.Current;
                        //        sourceX++;
                        //    }

                        //    sourceY++;
                        //}

                        //Numerics.UnPremultiply(ref sum);
                        //sum.W = 1; // Super hack so I can see the output.
                        //span[x] = sum;
                    }
                }

                PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, span, rowSpan);
            }

            return;

            //int yRadius = LinearTransformUtils.GetSamplingRadius(in sampler, source.Height, destination.Height);
            //int xRadius = LinearTransformUtils.GetSamplingRadius(in sampler, source.Width, destination.Width);
            //var radialExtents = new Vector2(xRadius, yRadius);
            //int yLength = (yRadius * 2) + 1;
            //int xLength = (xRadius * 2) + 1;

            //// We use 2D buffers so that we can access the weight spans per row in parallel.
            //using Buffer2D<float> yKernelBuffer = configuration.MemoryAllocator.Allocate2D<float>(yLength, destination.Height);
            //using Buffer2D<float> xKernelBuffer = configuration.MemoryAllocator.Allocate2D<float>(xLength, destination.Height);

            //// int maxX = source.Width - 1;
            //// int maxY = source.Height - 1;
            //var maxSourceExtents = new Vector4(maxX, maxY, maxX, maxY);

            //var operation = new AffineOperation<TResampler>(
            //    configuration,
            //    source,
            //    destination,
            //    yKernelBuffer,
            //    xKernelBuffer,
            //    in sampler,
            //    matrix,
            //    radialExtents,
            //    maxSourceExtents);

            //ParallelRowIterator.IterateRows<AffineOperation<TResampler>, Vector4>(
            //    configuration,
            //    destination.Bounds(),
            //    in operation);
        }

        private readonly struct NNAffineOperation : IRowOperation
        {
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;
            private readonly Rectangle bounds;
            private readonly Matrix3x2 matrix;
            private readonly int maxX;

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
                this.maxX = destination.Width;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y)
            {
                Span<TPixel> destRow = this.destination.GetPixelRowSpan(y);

                for (int x = 0; x < this.maxX; x++)
                {
                    var point = Vector2.Transform(new Vector2(x, y), this.matrix);
                    int px = (int)MathF.Round(point.X);
                    int py = (int)MathF.Round(point.Y);

                    if (this.bounds.Contains(px, py))
                    {
                        destRow[x] = this.source[px, py];
                    }
                }
            }
        }

        private readonly struct AffineOperation<TResampler> : IRowOperation<Vector4>
            where TResampler : struct, IResampler
        {
            private readonly Configuration configuration;
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel> destination;
            private readonly Buffer2D<float> yKernelBuffer;
            private readonly Buffer2D<float> xKernelBuffer;
            private readonly TResampler sampler;
            private readonly Matrix3x2 matrix;
            private readonly Vector2 radialExtents;
            private readonly Vector4 maxSourceExtents;
            private readonly int maxX;

            [MethodImpl(InliningOptions.ShortMethod)]
            public AffineOperation(
                Configuration configuration,
                ImageFrame<TPixel> source,
                ImageFrame<TPixel> destination,
                Buffer2D<float> yKernelBuffer,
                Buffer2D<float> xKernelBuffer,
                in TResampler sampler,
                Matrix3x2 matrix,
                Vector2 radialExtents,
                Vector4 maxSourceExtents)
            {
                this.configuration = configuration;
                this.source = source;
                this.destination = destination;
                this.yKernelBuffer = yKernelBuffer;
                this.xKernelBuffer = xKernelBuffer;
                this.sampler = sampler;
                this.matrix = matrix;
                this.radialExtents = radialExtents;
                this.maxSourceExtents = maxSourceExtents;
                this.maxX = destination.Width;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y, Span<Vector4> span)
            {
                Buffer2D<TPixel> sourceBuffer = this.source.PixelBuffer;

                PixelOperations<TPixel>.Instance.ToVector4(
                    this.configuration,
                    this.destination.GetPixelRowSpan(y),
                    span);

                ref float yKernelSpanRef = ref MemoryMarshal.GetReference(this.yKernelBuffer.GetRowSpan(y));
                ref float xKernelSpanRef = ref MemoryMarshal.GetReference(this.xKernelBuffer.GetRowSpan(y));

                for (int x = 0; x < this.maxX; x++)
                {
                    // Use the single precision position to calculate correct bounding pixels
                    // otherwise we get rogue pixels outside of the bounds.
                    var point = Vector2.Transform(new Vector2(x, y), this.matrix);
                    LinearTransformUtils.Convolve(
                        in this.sampler,
                        point,
                        sourceBuffer,
                        span,
                        x,
                        ref yKernelSpanRef,
                        ref xKernelSpanRef,
                        this.radialExtents,
                        this.maxSourceExtents);
                }

                PixelOperations<TPixel>.Instance.FromVector4Destructive(
                    this.configuration,
                    span,
                    this.destination.GetPixelRowSpan(y));
            }
        }
    }
}
