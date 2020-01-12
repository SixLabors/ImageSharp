// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// A primitive that converts a point into a color for discovering the fill color based on an implementation.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <seealso cref="IDisposable" />
    public abstract class BrushApplicator<TPixel> : IDisposable
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrushApplicator{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration instance to use when performing operations.</param>
        /// <param name="options">The graphics options.</param>
        /// <param name="target">The target.</param>
        internal BrushApplicator(Configuration configuration, GraphicsOptions options, ImageFrame<TPixel> target)
        {
            this.Configuration = configuration;
            this.Target = target;
            this.Options = options;
            this.Blender = PixelOperations<TPixel>.Instance.GetPixelBlender(options);
        }

        /// <summary>
        /// Gets the configuration instance to use when performing operations.
        /// </summary>
        protected Configuration Configuration { get; }

        /// <summary>
        /// Gets the pixel blender.
        /// </summary>
        internal PixelBlender<TPixel> Blender { get; }

        /// <summary>
        /// Gets the target image.
        /// </summary>
        protected ImageFrame<TPixel> Target { get; }

        /// <summary>
        /// Gets thegraphics options
        /// </summary>
        protected GraphicsOptions Options { get; }

        /// <summary>
        /// Gets the overlay pixel at the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate.</param>
        /// <param name="y">The y-coordinate.</param>
        /// <returns>The <see typeparam="TPixel"/> at the specified position.</returns>
        internal abstract TPixel this[int x, int y] { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">Whether to dispose managed and unmanaged objects.</param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Applies the opacity weighting for each pixel in a scanline to the target based on the pattern contained in the brush.
        /// </summary>
        /// <param name="scanline">A collection of opacity values between 0 and 1 to be merged with the brushed color value before being applied to the target.</param>
        /// <param name="x">The x-position in the target pixel space that the start of the scanline data corresponds to.</param>
        /// <param name="y">The y-position in  the target pixel space that whole scanline corresponds to.</param>
        /// <remarks>scanlineBuffer will be > scanlineWidth but provide and offset in case we want to share a larger buffer across runs.</remarks>
        internal virtual void Apply(Span<float> scanline, int x, int y)
        {
            MemoryAllocator memoryAllocator = this.Target.MemoryAllocator;

            using (IMemoryOwner<float> amountBuffer = memoryAllocator.Allocate<float>(scanline.Length))
            using (IMemoryOwner<TPixel> overlay = memoryAllocator.Allocate<TPixel>(scanline.Length))
            {
                Span<float> amountSpan = amountBuffer.Memory.Span;
                Span<TPixel> overlaySpan = overlay.Memory.Span;
                float blendPercentage = this.Options.BlendPercentage;

                if (blendPercentage < 1)
                {
                    for (int i = 0; i < scanline.Length; i++)
                    {
                        amountSpan[i] = scanline[i] * blendPercentage;
                        overlaySpan[i] = this[x + i, y];
                    }
                }
                else
                {
                    for (int i = 0; i < scanline.Length; i++)
                    {
                        amountSpan[i] = scanline[i];
                        overlaySpan[i] = this[x + i, y];
                    }
                }

                Span<TPixel> destinationRow = this.Target.GetPixelRowSpan(y).Slice(x, scanline.Length);
                this.Blender.Blend(this.Configuration, destinationRow, destinationRow, overlaySpan, amountSpan);
            }
        }
    }
}
