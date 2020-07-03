// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
        public void ApplyTransform<TPixel>(IResamplingTransformImageProcessor<TPixel> processor)
            where TPixel : unmanaged, IPixel<TPixel>
            => processor.ApplyTransform(in this);
    }
}
