// <copyright file="BrushApplicator.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// primitive that converts a point in to a color for discovering the fill color based on an implementation
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <seealso cref="System.IDisposable" />
    public abstract class BrushApplicator<TPixel> : IDisposable // disposable will be required if/when there is an ImageBrush
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrushApplicator{TPixel}"/> class.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="options">The options.</param>
        internal BrushApplicator(ImageBase<TPixel> target, GraphicsOptions options)
        {
            this.Target = target;

            this.Options = options;

            this.Blender = PixelOperations<TPixel>.Instance.GetPixelBlender(options.BlenderMode);
        }

        /// <summary>
        /// Gets the blendder
        /// </summary>
        internal PixelBlender<TPixel> Blender { get; }

        /// <summary>
        /// Gets the destinaion
        /// </summary>
        protected ImageBase<TPixel> Target { get; }

        /// <summary>
        /// Gets the blend percentage
        /// </summary>
        protected GraphicsOptions Options { get; private set; }

        /// <summary>
        /// Gets the color for a single pixel.
        /// </summary>
        /// <param name="x">The x cordinate.</param>
        /// <param name="y">The y cordinate.</param>
        /// <returns>The a <typeparamref name="TPixel"/> that should be applied to the pixel.</returns>
        internal abstract TPixel this[int x, int y] { get; }

        /// <inheritdoc/>
        public abstract void Dispose();

        /// <summary>
        /// Applies the opactiy weighting for each pixel in a scanline to the target based on the pattern contained in the brush.
        /// </summary>
        /// <param name="scanline">The a collection of opacity values between 0 and 1 to be merged with the brushed color value before being applied to the target.</param>
        /// <param name="x">The x position in the target pixel space that the start of the scanline data corresponds to.</param>
        /// <param name="y">The y position in  the target pixel space that whole scanline corresponds to.</param>
        /// <remarks>scanlineBuffer will be > scanlineWidth but provide and offset in case we want to share a larger buffer across runs.</remarks>
        internal virtual void Apply(Span<float> scanline, int x, int y)
        {
            using (var amountBuffer = new Buffer<float>(scanline.Length))
            using (var overlay = new Buffer<TPixel>(scanline.Length))
            {
                for (int i = 0; i < scanline.Length; i++)
                {
                    if (this.Options.BlendPercentage < 1)
                    {
                        amountBuffer[i] = scanline[i] * this.Options.BlendPercentage;
                    }

                    overlay[i] = this[x + i, y];
                }

                Span<TPixel> destinationRow = this.Target.GetRowSpan(x, y).Slice(0, scanline.Length);
                this.Blender.Blend(destinationRow, destinationRow, overlay, amountBuffer);
            }
        }
    }
}
