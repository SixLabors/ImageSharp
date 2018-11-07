// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

// TODO: It would be great if we could somehow optimize this to calculate the weights once.
// currently we cannot do that  as we are calulating the weight of the transformed point dimension
// not the point in the original image.
namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Contains the methods required to calculate kernel sampling  weights on-the-fly.
    /// </summary>
    internal class TransformKernelMap : IDisposable
    {
        private readonly Buffer2D<float> yBuffer;
        private readonly Buffer2D<float> xBuffer;
        private readonly int yLength;
        private readonly int xLength;
        private readonly Vector2 extents;
        private Vector4 maxSourceExtents;
        private readonly IResampler sampler;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransformKernelMap"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="source">The source size.</param>
        /// <param name="destination">The destination size.</param>
        /// <param name="sampler">The sampler.</param>
        public TransformKernelMap(Configuration configuration, Size source, Size destination, IResampler sampler)
        {
            this.sampler = sampler;
            float yRadius = this.GetSamplingRadius(source.Height, destination.Height);
            float xRadius = this.GetSamplingRadius(source.Width, destination.Width);

            this.extents = new Vector2(xRadius, yRadius);
            this.xLength = (int)MathF.Ceiling((this.extents.X * 2) + 2);
            this.yLength = (int)MathF.Ceiling((this.extents.Y * 2) + 2);

            // We use 2D buffers so that we can access the weight spans in parallel.
            this.yBuffer = configuration.MemoryAllocator.Allocate2D<float>(this.yLength, destination.Height);
            this.xBuffer = configuration.MemoryAllocator.Allocate2D<float>(this.xLength, destination.Height);

            int maxX = source.Width - 1;
            int maxY = source.Height - 1;
            this.maxSourceExtents = new Vector4(maxX, maxY, maxX, maxY);
        }

        /// <summary>
        /// Gets a reference to the first item of the y window.
        /// </summary>
        /// <returns>The reference to the first item of the window.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public ref float GetYStartReference(int y)
            => ref MemoryMarshal.GetReference(this.yBuffer.GetRowSpan(y));

        /// <summary>
        /// Gets a reference to the first item of the x window.
        /// </summary>
        /// <returns>The reference to the first item of the window.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public ref float GetXStartReference(int y)
            => ref MemoryMarshal.GetReference(this.xBuffer.GetRowSpan(y));

        public void Convolve<TPixel>(
            Vector2 transformedPoint,
            int column,
            ref float ySpanRef,
            ref float xSpanRef,
            Buffer2D<TPixel> sourcePixels,
            Span<Vector4> targetRow)
            where TPixel : struct, IPixel<TPixel>
        {
            // Clamp sampling pixel radial extents to the source image edges
            Vector2 minXY = transformedPoint - this.extents;
            Vector2 maxXY = transformedPoint + this.extents;

            // minX, minY, maxX, maxY
            var extents = new Vector4(
                MathF.Ceiling(minXY.X - .5F),
                MathF.Ceiling(minXY.Y - .5F),
                MathF.Floor(maxXY.X + .5F),
                MathF.Floor(maxXY.Y + .5F));

            int left = (int)extents.X;
            int top = (int)extents.Y;
            int right = (int)extents.Z;
            int bottom = (int)extents.W;

            extents = Vector4.Clamp(extents, Vector4.Zero, this.maxSourceExtents);

            int minX = (int)extents.X;
            int minY = (int)extents.Y;
            int maxX = (int)extents.Z;
            int maxY = (int)extents.W;

            if (minX == maxX || minY == maxY)
            {
                return;
            }

            // TODO: Get Anton to use his superior brain on this one.
            // It looks to me like we're calculating the same weights over and over again
            // since min(X+Y) and max(X+Y) are the same distance apart.
            this.CalculateWeights(minY, maxY, maxY - minY, transformedPoint.Y, ref ySpanRef);
            this.CalculateWeights(minX, maxX, maxX - minX, transformedPoint.X, ref xSpanRef);

            Vector4 sum = Vector4.Zero;
            for (int kernelY = 0, y = minY; y <= maxY; y++, kernelY++)
            {
                float yWeight = Unsafe.Add(ref ySpanRef, kernelY);

                for (int kernelX = 0, x = minX; x <= maxX; x++, kernelX++)
                {
                    float xWeight = Unsafe.Add(ref xSpanRef, kernelX);

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

        /// <summary>
        /// Calculated the normalized weights for the given point.
        /// </summary>
        /// <param name="min">The minimum sampling offset</param>
        /// <param name="max">The maximum sampling offset</param>
        /// <param name="length">The length of the weights collection</param>
        /// <param name="point">The transformed point dimension</param>
        /// <param name="weightsRef">The reference to the collection of weights</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void CalculateWeights(int min, int max, int length, float point, ref float weightsRef)
        {
            float sum = 0;
            for (int x = 0, i = min; i <= max; i++, x++)
            {
                float weight = this.sampler.GetValue(i - point);
                sum += weight;
                Unsafe.Add(ref weightsRef, x) = this.sampler.GetValue(i - point);
            }

            // TODO: Do we need this? Check what happens when we scale an image down.
            // if (sum > 0)
            // {
            //    for (int i = 0; i < length; i++)
            //    {
            //        ref float wRef = ref Unsafe.Add(ref weightsRef, i);
            //        wRef /= sum;
            //    }
            // }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private float GetSamplingRadius(int sourceSize, int destinationSize)
        {
            float scale = (float)sourceSize / destinationSize;

            if (scale < 1F)
            {
                scale = 1F;
            }

            return MathF.Ceiling(scale * this.sampler.Radius);
        }

        public void Dispose()
        {
            this.yBuffer?.Dispose();
            this.xBuffer?.Dispose();
        }
    }
}