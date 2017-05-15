// <copyright file="DefaultSubstractPixelBlender{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats.PixelBlenders
{
    using System;
    using System.Numerics;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Applies an "Subtract" blending to pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel</typeparam>
    internal class DefaultSubstractPixelBlender<TPixel> : PixelBlender<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the static instance of this blender.
        /// </summary>
        public static DefaultSubstractPixelBlender<TPixel> Instance { get; } = new DefaultSubstractPixelBlender<TPixel>();

        /// <inheritdoc />
        public override TPixel Blend(TPixel background, TPixel source, float amount)
        {
            return PorterDuffFunctions<TPixel>.SubstractFunction(background, source, amount);
        }

        /// <inheritdoc />
        public override void Blend(Span<TPixel> destination, Span<TPixel> background, Span<TPixel> source, Span<float> amount)
        {
            Guard.MustBeGreaterThanOrEqualTo(background.Length, destination.Length, nameof(background.Length));
            Guard.MustBeGreaterThanOrEqualTo(source.Length, destination.Length, nameof(source.Length));
            Guard.MustBeGreaterThanOrEqualTo(amount.Length, destination.Length, nameof(amount.Length));

            using (Buffer<Vector4> buffer = new Buffer<Vector4>(destination.Length * 3))
            {
                Span<Vector4> destinationSpan = buffer.Slice(0, destination.Length);
                Span<Vector4> backgroundSpan = buffer.Slice(destination.Length, destination.Length);
                Span<Vector4> sourceSpan = buffer.Slice(destination.Length * 2, destination.Length);

                PixelOperations<TPixel>.Instance.ToVector4(background, backgroundSpan, destination.Length);
                PixelOperations<TPixel>.Instance.ToVector4(source, sourceSpan, destination.Length);

                for (int i = 0; i < destination.Length; i++)
                {
                    destinationSpan[i] = PorterDuffFunctions.SubstractFunction(backgroundSpan[i], sourceSpan[i], amount[i]);
                }

                PixelOperations<TPixel>.Instance.PackFromVector4(destinationSpan, destination, destination.Length);
            }
        }
    }
}
