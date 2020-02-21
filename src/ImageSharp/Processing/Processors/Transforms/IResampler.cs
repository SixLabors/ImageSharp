// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Encapsulates an interpolation algorithm for resampling images.
    /// </summary>
    public interface IResampler
    {
        /// <summary>
        /// Gets the radius in which to sample pixels.
        /// </summary>
        float Radius { get; }

        /// <summary>
        /// Gets the result of the interpolation algorithm.
        /// </summary>
        /// <param name="x">The value to process.</param>
        /// <returns>
        /// The <see cref="float"/>
        /// </returns>
        float GetValue(float x);

        /// <summary>
        /// Applies an affine transformation upon an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="source">The source image frame.</param>
        /// <param name="destination">The destination image frame.</param>
        /// <param name="matrix">The transform matrix.</param>
        void ApplyAffineTransform<TPixel>(
            Configuration configuration,
            ImageFrame<TPixel> source,
            ImageFrame<TPixel> destination,
            Matrix3x2 matrix)
            where TPixel : struct, IPixel<TPixel>;

        /// <summary>
        /// Applies a projective transformation upon an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="source">The source image frame.</param>
        /// <param name="destination">The destination image frame.</param>
        /// <param name="matrix">The transform matrix.</param>
        void ApplyProjectiveTransform<TPixel>(
            Configuration configuration,
            ImageFrame<TPixel> source,
            ImageFrame<TPixel> destination,
            Matrix4x4 matrix)
            where TPixel : struct, IPixel<TPixel>;
    }
}
