// <copyright file="DefaultSoftLightPixelBlender{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats.PixelBlenders
{
    using System;
    using System.Numerics;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Abstract base class for calling pixel composition functions
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel</typeparam>
    internal class DefaultSoftLightPixelBlender<TPixel> : PixelBlender<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc />
        public override TPixel Compose(TPixel background, TPixel source, float amount)
        {
            Vector4 result = Vector4BlendTransforms.SoftLight(background.ToVector4(), source.ToVector4());
            TPixel resultPixel = default(TPixel);
            resultPixel.PackFromVector4(result);
            return resultPixel;
        }

        /// <inheritdoc />
        public override void Compose(BufferSpan<TPixel> destination, BufferSpan<TPixel> background, BufferSpan<TPixel> source, BufferSpan<float> amount)
        {
            Guard.MustBeGreaterThanOrEqualTo(destination.Length, background.Length, nameof(destination));
            Guard.MustBeGreaterThanOrEqualTo(source.Length, background.Length, nameof(destination));
            Guard.MustBeGreaterThanOrEqualTo(amount.Length, background.Length, nameof(destination));

            for (int i = 0; i < destination.Length; i++)
            {
                Vector4 result = Vector4BlendTransforms.SoftLight(background[i].ToVector4(), source[i].ToVector4());
                destination[i].PackFromVector4(result);
            }
        }
    }
}
