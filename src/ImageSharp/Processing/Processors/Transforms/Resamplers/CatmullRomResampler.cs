// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// The Catmull-Rom filter is a well known standard Cubic Filter often used as a interpolation function.
    /// This filter produces a reasonably sharp edge, but without a the pronounced gradient change on large
    /// scale image enlargements that a 'Lagrange' filter can produce.
    /// <see href="http://www.imagemagick.org/Usage/filter/#cubic_bc"/>
    /// </summary>
    public readonly struct CatmullRomResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 2;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public float GetValue(float x)
        {
            const float B = 0;
            const float C = 0.5F;

            return ImageMaths.GetBcValue(x, B, C);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ApplyResizeTransform<TPixel>(
            Configuration configuration,
            Image<TPixel> source,
            Image<TPixel> destination,
            Rectangle sourceRectangle,
            Rectangle destinationRectangle,
            bool compand)
            where TPixel : struct, IPixel<TPixel> => ResamplerExtensions.ApplyResizeTransform(
                configuration,
                in this,
                source,
                destination,
                sourceRectangle,
                destinationRectangle,
                compand);

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
