// <copyright file="DefaultNormalPixelBlender{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats.PixelBlenders
{
    using System;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Abstract base class for calling pixel composition functions
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel</typeparam>
    internal class DefaultNormalPixelBlender<TPixel> : PixelBlender<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc />
        public override TPixel Compose(TPixel background, TPixel source, float amount)
        {
            return source;
        }

        /// <inheritdoc />
        public override void Compose(BufferSpan<TPixel> destination, BufferSpan<TPixel> background, BufferSpan<TPixel> source, BufferSpan<float> amount)
        {
            Guard.MustBeGreaterThanOrEqualTo(destination.Length, background.Length, nameof(destination));
            Guard.MustBeGreaterThanOrEqualTo(source.Length, background.Length, nameof(destination));
            Guard.MustBeGreaterThanOrEqualTo(amount.Length, background.Length, nameof(destination));

            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = source[i];
            }
        }
    }
}
