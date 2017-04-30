// <copyright file="DefaultScreenPixelBlender{TPixel}.cs" company="James Jackson-South">
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
    internal class DefaultScreenPixelBlender<TPixel> : PixelBlender<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the static instance of this blender.
        /// </summary>
        public static DefaultScreenPixelBlender<TPixel> Instance { get; } = new DefaultScreenPixelBlender<TPixel>();

        /// <inheritdoc />
        public override TPixel Compose(TPixel background, TPixel source, float amount)
        {
            return PorterDuffFunctions<TPixel>.ScreenFunction(background, source, amount);
        }

        /// <inheritdoc />
        public override void Compose(BufferSpan<TPixel> destination, BufferSpan<TPixel> background, BufferSpan<TPixel> source, BufferSpan<float> amount)
        {
            Guard.MustBeGreaterThanOrEqualTo(background.Length, destination.Length, nameof(background.Length));
            Guard.MustBeGreaterThanOrEqualTo(source.Length, destination.Length, nameof(source.Length));
            Guard.MustBeGreaterThanOrEqualTo(amount.Length, destination.Length, nameof(amount.Length));

            using (Buffer<Vector4> buffer = new Buffer<Vector4>(destination.Length * 3))
            {
                BufferSpan<Vector4> destinationSpan = buffer.Slice(0, destination.Length);
                BufferSpan<Vector4> backgroundSpan = buffer.Slice(destination.Length, destination.Length);
                BufferSpan<Vector4> sourceSpan = buffer.Slice(destination.Length * 2, destination.Length);

                PixelOperations<TPixel>.Instance.ToVector4(background, backgroundSpan, destination.Length);
                PixelOperations<TPixel>.Instance.ToVector4(source, sourceSpan, destination.Length);

                for (int i = 0; i < destination.Length; i++)
                {
                    destinationSpan[i] = PorterDuffFunctions.ScreenFunction(backgroundSpan[i], sourceSpan[i], amount[i]);
                }

                PixelOperations<TPixel>.Instance.PackFromVector4(destinationSpan, destination, destination.Length);
            }
        }
    }
}
