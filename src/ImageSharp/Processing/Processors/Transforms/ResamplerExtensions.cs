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
    /// Extensions for <see cref="IResampler"/>.
    /// </summary>
    public static partial class ResamplerExtensions
    {
        /// <summary>
        /// Applies an affine transformation upon an image.
        /// </summary>
        /// <typeparam name="TResampler">The type of sampler.</typeparam>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="sampler">The pixel sampler.</param>
        /// <param name="source">The source image frame.</param>
        /// <param name="destination">The destination image frame.</param>
        /// <param name="matrix">The transform matrix.</param>
        public static void ApplyAffineTransform<TResampler, TPixel>(
            Configuration configuration,
            in TResampler sampler,
            ImageFrame<TPixel> source,
            ImageFrame<TPixel> destination,
            Matrix3x2 matrix)
            where TResampler : unmanaged, IResampler
            where TPixel : struct, IPixel<TPixel>
        {
            // Handle transforms that result in output identical to the original.
            if (matrix.Equals(default) || matrix.Equals(Matrix3x2.Identity))
            {
                // The clone will be blank here copy all the pixel data over
                source.GetPixelSpan().CopyTo(destination.GetPixelSpan());
                return;
            }

            // Convert from screen to world space.
            Matrix3x2.Invert(matrix, out matrix);

            if (sampler is NearestNeighborResampler)
            {
                var nnOperation = new NNAffineOperation<TPixel>(source, destination, matrix);
                ParallelRowIterator.IterateRows(
                    configuration,
                    destination.Bounds(),
                    in nnOperation);

                return;
            }

            int yRadius = GetSamplingRadius(in sampler, source.Height, destination.Height);
            int xRadius = GetSamplingRadius(in sampler, source.Width, destination.Width);
            var radialExtents = new Vector2(xRadius, yRadius);
            int yLength = (yRadius * 2) + 1;
            int xLength = (xRadius * 2) + 1;

            // We use 2D buffers so that we can access the weight spans per row in parallel.
            using Buffer2D<float> yKernelBuffer = configuration.MemoryAllocator.Allocate2D<float>(yLength, destination.Height);
            using Buffer2D<float> xKernelBuffer = configuration.MemoryAllocator.Allocate2D<float>(xLength, destination.Height);

            int maxX = source.Width - 1;
            int maxY = source.Height - 1;
            var maxSourceExtents = new Vector4(maxX, maxY, maxX, maxY);

            var operation = new AffineOperation<TResampler, TPixel>(
                configuration,
                source,
                destination,
                yKernelBuffer,
                xKernelBuffer,
                in sampler,
                matrix,
                radialExtents,
                maxSourceExtents);

            ParallelRowIterator.IterateRows<AffineOperation<TResampler, TPixel>, Vector4>(
                configuration,
                destination.Bounds(),
                in operation);
        }

        /// <summary>
        /// Applies a projective transformation upon an image.
        /// </summary>
        /// <typeparam name="TResampler">The type of sampler.</typeparam>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="sampler">The pixel sampler.</param>
        /// <param name="source">The source image frame.</param>
        /// <param name="destination">The destination image frame.</param>
        /// <param name="matrix">The transform matrix.</param>
        public static void ApplyProjectiveTransform<TResampler, TPixel>(
            Configuration configuration,
            in TResampler sampler,
            ImageFrame<TPixel> source,
            ImageFrame<TPixel> destination,
            Matrix4x4 matrix)
            where TResampler : unmanaged, IResampler
            where TPixel : struct, IPixel<TPixel>
        {
            // Handle transforms that result in output identical to the original.
            if (matrix.Equals(default) || matrix.Equals(Matrix4x4.Identity))
            {
                // The clone will be blank here copy all the pixel data over
                source.GetPixelSpan().CopyTo(destination.GetPixelSpan());
                return;
            }

            // Convert from screen to world space.
            Matrix4x4.Invert(matrix, out matrix);

            if (sampler is NearestNeighborResampler)
            {
                var nnOperation = new NNProjectiveOperation<TPixel>(source, destination, matrix);
                ParallelRowIterator.IterateRows(
                    configuration,
                    destination.Bounds(),
                    in nnOperation);

                return;
            }

            int yRadius = GetSamplingRadius(in sampler, source.Height, destination.Height);
            int xRadius = GetSamplingRadius(in sampler, source.Width, destination.Width);
            var radialExtents = new Vector2(xRadius, yRadius);
            int yLength = (yRadius * 2) + 1;
            int xLength = (xRadius * 2) + 1;

            // We use 2D buffers so that we can access the weight spans per row in parallel.
            using Buffer2D<float> yKernelBuffer = configuration.MemoryAllocator.Allocate2D<float>(yLength, destination.Height);
            using Buffer2D<float> xKernelBuffer = configuration.MemoryAllocator.Allocate2D<float>(xLength, destination.Height);

            int maxX = source.Width - 1;
            int maxY = source.Height - 1;
            var maxSourceExtents = new Vector4(maxX, maxY, maxX, maxY);

            var operation = new ProjectiveOperation<TResampler, TPixel>(
                configuration,
                source,
                destination,
                yKernelBuffer,
                xKernelBuffer,
                in sampler,
                matrix,
                radialExtents,
                maxSourceExtents);

            ParallelRowIterator.IterateRows<ProjectiveOperation<TResampler, TPixel>, Vector4>(
                configuration,
                destination.Bounds(),
                in operation);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void Convolve<TResampler, TPixel>(
            in TResampler sampler,
            Vector2 transformedPoint,
            Buffer2D<TPixel> sourcePixels,
            Span<Vector4> targetRow,
            int column,
            ref float yKernelSpanRef,
            ref float xKernelSpanRef,
            Vector2 radialExtents,
            Vector4 maxSourceExtents)
            where TResampler : unmanaged, IResampler
            where TPixel : struct, IPixel<TPixel>
        {
            // Clamp sampling pixel radial extents to the source image edges
            Vector2 minXY = transformedPoint - radialExtents;
            Vector2 maxXY = transformedPoint + radialExtents;

            // left, top, right, bottom
            var sourceExtents = new Vector4(
                MathF.Ceiling(minXY.X),
                MathF.Ceiling(minXY.Y),
                MathF.Floor(maxXY.X),
                MathF.Floor(maxXY.Y));

            sourceExtents = Vector4.Clamp(sourceExtents, Vector4.Zero, maxSourceExtents);

            int left = (int)sourceExtents.X;
            int top = (int)sourceExtents.Y;
            int right = (int)sourceExtents.Z;
            int bottom = (int)sourceExtents.W;

            if (left == right || top == bottom)
            {
                return;
            }

            CalculateWeights(in sampler, top, bottom, transformedPoint.Y, ref yKernelSpanRef);
            CalculateWeights(in sampler, left, right, transformedPoint.X, ref xKernelSpanRef);

            Vector4 sum = Vector4.Zero;
            for (int kernelY = 0, y = top; y <= bottom; y++, kernelY++)
            {
                float yWeight = Unsafe.Add(ref yKernelSpanRef, kernelY);

                for (int kernelX = 0, x = left; x <= right; x++, kernelX++)
                {
                    float xWeight = Unsafe.Add(ref xKernelSpanRef, kernelX);

                    // Values are first premultiplied to prevent darkening of edge pixels.
                    var current = sourcePixels[x, y].ToVector4();
                    Vector4Utils.Premultiply(ref current);
                    sum += current * xWeight * yWeight;
                }
            }

            // Reverse the premultiplication
            Vector4Utils.UnPremultiply(ref sum);
            targetRow[column] = sum;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void CalculateWeights<TResampler>(in TResampler sampler, int min, int max, float point, ref float weightsRef)
            where TResampler : unmanaged, IResampler
        {
            float sum = 0;
            for (int x = 0, i = min; i <= max; i++, x++)
            {
                float weight = sampler.GetValue(i - point);
                sum += weight;
                Unsafe.Add(ref weightsRef, x) = weight;
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetSamplingRadius<TResampler>(in TResampler sampler, int sourceSize, int destinationSize)
            where TResampler : unmanaged, IResampler
        {
            double scale = sourceSize / destinationSize;
            if (scale < 1)
            {
                scale = 1;
            }

            return (int)Math.Ceiling(scale * sampler.Radius);
        }
    }
}
