// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// The function implements the nearest neighbor algorithm. This uses an unscaled filter
    /// which will select the closest pixel to the new pixels position.
    /// </summary>
    public readonly struct NearestNeighborResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 1;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public float GetValue(float x) => x;

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
