// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Provides per-pixel access to generic <see cref="Image{TPixel}"/> pixels.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal sealed class PixelAccessor<TPixel> : IDisposable, IBuffer2D<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PixelAccessor{TPixel}"/> class.
        /// </summary>
        /// <param name="image">The image to provide pixel access for.</param>
        public PixelAccessor(IPixelSource<TPixel> image)
        {
            Guard.NotNull(image, nameof(image));
            Guard.MustBeGreaterThan(image.PixelBuffer.Width, 0, "image width");
            Guard.MustBeGreaterThan(image.PixelBuffer.Height, 0, "image height");

            this.SetPixelBufferUnsafe(image.PixelBuffer);
        }

        /// <summary>
        /// Gets the <see cref="Buffer2D{T}"/> containing the pixel data.
        /// </summary>
        internal Buffer2D<TPixel> PixelBuffer { get; private set; }

        /// <summary>
        /// Gets the size of a single pixel in the number of bytes.
        /// </summary>
        public int PixelSize { get; private set; }

        /// <summary>
        /// Gets the width of one row in the number of bytes.
        /// </summary>
        public int RowStride { get; private set; }

        /// <inheritdoc />
        public int Width { get; private set; }

        /// <inheritdoc />
        public int Height { get; private set; }

        /// <inheritdoc />
        public Span<TPixel> Span => this.PixelBuffer.Span;

        /// <summary>
        /// Gets or sets the pixel at the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than or equal to zero and less than the width of the image.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than or equal to zero and less than the height of the image.</param>
        /// <returns>The <see typeparam="TPixel"/> at the specified position.</returns>
        public TPixel this[int x, int y]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                this.CheckCoordinates(x, y);
                return this.Span[(y * this.Width) + x];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.CheckCoordinates(x, y);
                Span<TPixel> span = this.Span;
                span[(y * this.Width) + x] = value;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <summary>
        /// Resets all the pixels to it's initial value.
        /// </summary>
        public void Reset()
        {
            this.PixelBuffer.Buffer.Clear();
        }

        /// <summary>
        /// Sets the pixel buffer in an unsafe manner. This should not be used unless you know what its doing!!!
        /// </summary>
        /// <param name="pixels">The pixels.</param>
        /// <returns>Returns the old pixel data thats has gust been replaced.</returns>
        /// <remarks>If <see cref="M:PixelAccessor.PooledMemory"/> is true then caller is responsible for ensuring <see cref="M:PixelDataPool.Return()"/> is called.</remarks>
        internal Buffer2D<TPixel> SwapBufferOwnership(Buffer2D<TPixel> pixels)
        {
            Buffer2D<TPixel> oldPixels = this.PixelBuffer;
            this.SetPixelBufferUnsafe(pixels);
            return oldPixels;
        }

        /// <summary>
        /// Copies the pixels to another <see cref="PixelAccessor{TPixel}"/> of the same size.
        /// </summary>
        /// <param name="target">The target pixel buffer accessor.</param>
        internal void CopyTo(PixelAccessor<TPixel> target)
        {
            this.PixelBuffer.Span.CopyTo(target.PixelBuffer.Span);
        }

        /// <summary>
        /// Sets the pixel buffer in an unsafe manor this should not be used unless you know what its doing!!!
        /// </summary>
        /// <param name="pixels">The pixel buffer</param>
        private void SetPixelBufferUnsafe(Buffer2D<TPixel> pixels)
        {
            this.PixelBuffer = pixels;

            this.Width = pixels.Width;
            this.Height = pixels.Height;
            this.PixelSize = Unsafe.SizeOf<TPixel>();
            this.RowStride = this.Width * this.PixelSize;
        }

        /// <summary>
        /// Checks the coordinates to ensure they are within bounds.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than zero and less than the width of the image.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than zero and less than the height of the image.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the coordinates are not within the bounds of the image.
        /// </exception>
        [Conditional("DEBUG")]
        private void CheckCoordinates(int x, int y)
        {
            if (x < 0 || x >= this.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, $"{x} is outwith the image bounds.");
            }

            if (y < 0 || y >= this.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, $"{y} is outwith the image bounds.");
            }
        }
    }
}