// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// The function implements the triangle (bilinear) algorithm.
    /// Bilinear interpolation can be used where perfect image transformation with pixel matching is impossible,
    /// so that one can calculate and assign appropriate intensity values to pixels.
    /// </summary>
    public readonly struct TriangleResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 1;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public float GetValue(float x)
        {
            if (x < 0F)
            {
                x = -x;
            }

            if (x < 1F)
            {
                return 1F - x;
            }

            return 0F;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ApplyAffineTransform<TPixel>(
            Configuration configuration,
            ImageFrame<TPixel> source,
            ImageFrame<TPixel> destination,
            Matrix3x2 matrix)
            where TPixel : struct, IPixel<TPixel> => ResamplerExtensions.ApplyAffineTransform(
                configuration,
                in this,
                source,
                destination,
                matrix);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ApplyProjectiveTransform<TPixel>(
            Configuration configuration,
            ImageFrame<TPixel> source,
            ImageFrame<TPixel> destination,
            Matrix4x4 matrix)
            where TPixel : struct, IPixel<TPixel> => ResamplerExtensions.ApplyProjectiveTransform(
                configuration,
                in this,
                source,
                destination,
                matrix);
    }
}
