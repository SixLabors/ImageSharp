// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Utility methods for affine and projective transforms.
    /// </summary>
    internal static class LinearTransformUtils
    {
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static int GetSamplingRadius<TResampler>(in TResampler sampler, int sourceSize, int destinationSize)
            where TResampler : struct, IResampler
        {
            double scale = sourceSize / destinationSize;
            if (scale < 1)
            {
                scale = 1;
            }

            return (int)Math.Ceiling(scale * sampler.Radius);
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
            where TResampler : struct, IResampler
            where TPixel : unmanaged, IPixel<TPixel>
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

            sourceExtents = Numerics.Clamp(sourceExtents, Vector4.Zero, maxSourceExtents);

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
                    Numerics.Premultiply(ref current);
                    sum += current * xWeight * yWeight;
                }
            }

            // Reverse the premultiplication
            Numerics.UnPremultiply(ref sum);
            targetRow[column] = sum;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void CalculateWeights<TResampler>(in TResampler sampler, int min, int max, float point, ref float weightsRef)
            where TResampler : struct, IResampler
        {
            float sum = 0;
            for (int x = 0, i = min; i <= max; i++, x++)
            {
                float weight = sampler.GetValue(i - point);
                sum += weight;
                Unsafe.Add(ref weightsRef, x) = weight;
            }
        }
    }
}
