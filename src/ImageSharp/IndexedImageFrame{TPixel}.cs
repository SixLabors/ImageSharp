// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// A pixel-specific image frame where each pixel buffer value represents an index in a color palette.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed class IndexedImageFrame<TPixel> : IPixelSource, IDisposable
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private Buffer2D<byte> pixelBuffer;
        private IMemoryOwner<TPixel> paletteOwner;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexedImageFrame{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">
        /// The configuration which allows altering default behaviour or extending the library.
        /// </param>
        /// <param name="width">The frame width.</param>
        /// <param name="height">The frame height.</param>
        /// <param name="palette">The color palette.</param>
        internal IndexedImageFrame(Configuration configuration, int width, int height, ReadOnlyMemory<TPixel> palette)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.MustBeLessThanOrEqualTo(palette.Length, QuantizerConstants.MaxColors, nameof(palette));
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.Configuration = configuration;
            this.Width = width;
            this.Height = height;
            this.pixelBuffer = configuration.MemoryAllocator.Allocate2D<byte>(width, height);

            // Copy the palette over. We want the lifetime of this frame to be independant of any palette source.
            this.paletteOwner = configuration.MemoryAllocator.Allocate<TPixel>(palette.Length);
            palette.Span.CopyTo(this.paletteOwner.GetSpan());
            this.Palette = this.paletteOwner.Memory.Slice(0, palette.Length);
        }

        /// <summary>
        /// Gets the configuration which allows altering default behaviour or extending the library.
        /// </summary>
        public Configuration Configuration { get; }

        /// <summary>
        /// Gets the width of this <see cref="IndexedImageFrame{TPixel}"/>.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height of this <see cref="IndexedImageFrame{TPixel}"/>.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the color palette of this <see cref="IndexedImageFrame{TPixel}"/>.
        /// </summary>
        public ReadOnlyMemory<TPixel> Palette { get; }

        /// <inheritdoc/>
        Buffer2D<byte> IPixelSource.PixelBuffer => this.pixelBuffer;

        /// <summary>
        /// Gets the representation of the pixels as a <see cref="ReadOnlySpan{T}"/> of contiguous memory
        /// at row <paramref name="rowIndex"/> beginning from the first pixel on that row.
        /// </summary>
        /// <param name="rowIndex">The row index in the pixel buffer.</param>
        /// <returns>The pixel row as a <see cref="ReadOnlySpan{T}"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public ReadOnlySpan<byte> GetPixelRowSpan(int rowIndex)
            => this.GetWritablePixelRowSpanUnsafe(rowIndex);

        /// <summary>
        /// <para>
        /// Gets the representation of the pixels as a <see cref="Span{T}"/> of contiguous memory
        /// at row <paramref name="rowIndex"/> beginning from the first pixel on that row.
        /// </para>
        /// <para>
        /// Note: Values written to this span are not sanitized against the palette length.
        /// Care should be taken during assignment to prevent out-of-bounds errors.
        /// </para>
        /// </summary>
        /// <param name="rowIndex">The row index in the pixel buffer.</param>
        /// <returns>The pixel row as a <see cref="Span{T}"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Span<byte> GetWritablePixelRowSpanUnsafe(int rowIndex)
            => this.pixelBuffer.GetRowSpan(rowIndex);

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                this.pixelBuffer.Dispose();
                this.paletteOwner.Dispose();
                this.pixelBuffer = null;
                this.paletteOwner = null;
            }
        }
    }
}
