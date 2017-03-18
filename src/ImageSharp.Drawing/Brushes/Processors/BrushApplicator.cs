// <copyright file="BrushApplicator.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// primitive that converts a point in to a color for discovering the fill color based on an implementation
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <seealso cref="System.IDisposable" />
    public abstract class BrushApplicator<TColor> : IDisposable // disposable will be required if/when there is an ImageBrush
        where TColor : struct, IPixel<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrushApplicator{TColor}"/> class.
        /// </summary>
        /// <param name="target">The target.</param>
        internal BrushApplicator(PixelAccessor<TColor> target)
        {
            this.Target = target;
        }

        /// <summary>
        /// Gets the destinaion
        /// </summary>
        protected PixelAccessor<TColor> Target { get; }

        /// <summary>
        /// Gets the color for a single pixel.
        /// </summary>
        /// <param name="x">The x cordinate.</param>
        /// <param name="y">The y cordinate.</param>
        /// <returns>The a <typeparamref name="TColor"/> that should be applied to the pixel.</returns>
        internal abstract TColor this[int x, int y] { get; }

        /// <inheritdoc/>
        public abstract void Dispose();

        /// <summary>
        /// Applies the opactiy weighting for each pixel in a scanline to the target based on the pattern contained in the brush.
        /// </summary>
        /// <param name="scanlineBuffer">The a collection of opacity values between 0 and 1 to be merged with the brushed color value before being applied to the target.</param>
        /// <param name="scanlineWidth">The number of pixels effected by this scanline.</param>
        /// <param name="offset">The offset fromthe begining of <paramref name="scanlineBuffer" /> the opacity data starts.</param>
        /// <param name="x">The x position in the target pixel space that the start of the scanline data corresponds to.</param>
        /// <param name="y">The y position in  the target pixel space that whole scanline corresponds to.</param>
        /// <remarks>scanlineBuffer will be > scanlineWidth but provide and offset in case we want to share a larger buffer across runs.</remarks>
        internal virtual void Apply(float[] scanlineBuffer, int scanlineWidth, int offset, int x, int y)
        {
            DebugGuard.MustBeGreaterThanOrEqualTo(scanlineBuffer.Length, offset + scanlineWidth, nameof(scanlineWidth));

            using (PinnedBuffer<float> buffer = new PinnedBuffer<float>(scanlineBuffer))
            {
                BufferPointer<float> slice = buffer.Slice(offset);

                for (int xPos = 0; xPos < scanlineWidth; xPos++)
                {
                    int targetX = xPos + x;
                    int targetY = y;

                    float opacity = slice[xPos];
                    if (opacity > Constants.Epsilon)
                    {
                        Vector4 backgroundVector = this.Target[targetX, targetY].ToVector4();

                        Vector4 sourceVector = this[targetX, targetY].ToVector4();

                        Vector4 finalColor = Vector4BlendTransforms.PremultipliedLerp(backgroundVector, sourceVector, opacity);

                        TColor packed = default(TColor);
                        packed.PackFromVector4(finalColor);
                        this.Target[targetX, targetY] = packed;
                    }
                }
            }
        }
    }
}
