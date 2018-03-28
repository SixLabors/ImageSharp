// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed class ImageFrame<TPixel> : IPixelSource<TPixel>, IDisposable
        where TPixel : struct, IPixel<TPixel>
    {
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
        /// </summary>
        /// <param name="memoryManager">The <see cref="MemoryManager"/> to use for buffer allocations.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        internal ImageFrame(MemoryManager memoryManager, int width, int height)
            : this(memoryManager, width, height, new ImageFrameMetaData())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
        /// </summary>
        /// <param name="memoryManager">The <see cref="MemoryManager"/> to use for buffer allocations.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="metaData">The meta data.</param>
        internal ImageFrame(MemoryManager memoryManager, int width, int height, ImageFrameMetaData metaData)
        {
            Guard.NotNull(memoryManager, nameof(memoryManager));
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));
            Guard.NotNull(metaData, nameof(metaData));

            this.MemoryManager = memoryManager;
            this.PixelBuffer = memoryManager.AllocateClean2D<TPixel>(width, height);
            this.MetaData = metaData;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
        /// </summary>
        /// <param name="memoryManager">The <see cref="MemoryManager"/> to use for buffer allocations.</param>
        /// <param name="size">The <see cref="Size"/> of the frame.</param>
        /// <param name="metaData">The meta data.</param>
        internal ImageFrame(MemoryManager memoryManager, Size size, ImageFrameMetaData metaData)
            : this(memoryManager, size.Width, size.Height, metaData)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
        /// </summary>
        /// <param name="memoryManager">The <see cref="MemoryManager"/> to use for buffer allocations.</param>
        /// <param name="source">The source.</param>
        internal ImageFrame(MemoryManager memoryManager, ImageFrame<TPixel> source)
        {
            this.MemoryManager = memoryManager;
            this.PixelBuffer = memoryManager.Allocate2D<TPixel>(source.PixelBuffer.Width, source.PixelBuffer.Height);
            source.PixelBuffer.Span.CopyTo(this.PixelBuffer.Span);
            this.MetaData = source.MetaData.Clone();
        }

        /// <summary>
        /// Gets the <see cref="MemoryManager" /> to use for buffer allocations.
        /// </summary>
        public MemoryManager MemoryManager { get; }

        /// <summary>
        /// Gets the image pixels. Not private as Buffer2D requires an array in its constructor.
        /// </summary>
        internal Buffer2D<TPixel> PixelBuffer { get; private set; }

        /// <inheritdoc/>
        Buffer2D<TPixel> IPixelSource<TPixel>.PixelBuffer => this.PixelBuffer;

        /// <summary>
        /// Gets the width.
        /// </summary>
        public int Width => this.PixelBuffer.Width;

        /// <summary>
        /// Gets the height.
        /// </summary>
        public int Height => this.PixelBuffer.Height;

        /// <summary>
        /// Gets the meta data of the frame.
        /// </summary>
        public ImageFrameMetaData MetaData { get; }

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
                return this.PixelBuffer[x, y];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.PixelBuffer[x, y] = value;
            }
        }

        /// <summary>
        /// Gets the size of the frame.
        /// </summary>
        /// <returns>The <see cref="Size"/></returns>
        public Size Size() => new Size(this.Width, this.Height);

        /// <summary>
        /// Gets the bounds of the frame.
        /// </summary>
        /// <returns>The <see cref="Rectangle"/></returns>
        public Rectangle Bounds() => new Rectangle(0, 0, this.Width, this.Height);

        /// <summary>
        /// Gets a reference to the pixel at the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than or equal to zero and less than the width of the image.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than or equal to zero and less than the height of the image.</param>
        /// <returns>The <see typeparam="TPixel"/> at the specified position.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref TPixel GetPixelReference(int x, int y)
        {
            return ref this.PixelBuffer[x, y];
        }

        /// <summary>
        /// Locks the image providing access to the pixels.
        /// <remarks>
        /// It is imperative that the accessor is correctly disposed off after use.
        /// </remarks>
        /// </summary>
        /// <returns>The <see cref="PixelAccessor{TPixel}"/></returns>
        internal PixelAccessor<TPixel> Lock()
        {
            return new PixelAccessor<TPixel>(this);
        }

        /// <summary>
        /// Copies the pixels to a <see cref="PixelAccessor{TPixel}"/> of the same size.
        /// </summary>
        /// <param name="target">The target pixel buffer accessor.</param>
        internal void CopyTo(PixelAccessor<TPixel> target)
        {
            this.CopyTo(target.PixelBuffer);
        }

        /// <summary>
        /// Copies the pixels to a <see cref="Buffer2D{TPixel}"/> of the same size.
        /// </summary>
        /// <param name="target">The target pixel buffer accessor.</param>
        internal void CopyTo(Buffer2D<TPixel> target)
        {
            if (this.Size() != target.Size())
            {
                throw new ArgumentException("ImageFrame<TPixel>.CopyTo(): target must be of the same size!", nameof(target));
            }

            this.GetPixelSpan().CopyTo(target.Span);
        }

        /// <summary>
        /// Switches the buffers used by the image and the PixelAccessor meaning that the Image will "own" the buffer from the PixelAccessor and the PixelAccessor will now own the Images buffer.
        /// </summary>
        /// <param name="pixelSource">The pixel source.</param>
        internal void SwapPixelsBuffers(PixelAccessor<TPixel> pixelSource)
        {
            Guard.NotNull(pixelSource, nameof(pixelSource));

            // Push my memory into the accessor (which in turn unpins the old buffer ready for the images use)
            Buffer2D<TPixel> newPixels = pixelSource.SwapBufferOwnership(this.PixelBuffer);
            this.PixelBuffer = newPixels;
        }

        /// <summary>
        /// Switches the buffers used by the image and the pixelSource meaning that the Image will "own" the buffer from the pixelSource and the pixelSource will now own the Images buffer.
        /// </summary>
        /// <param name="pixelSource">The pixel source.</param>
        internal void SwapPixelsBuffers(ImageFrame<TPixel> pixelSource)
        {
            Guard.NotNull(pixelSource, nameof(pixelSource));

            Buffer2D<TPixel> temp = this.PixelBuffer;
            this.PixelBuffer = pixelSource.PixelBuffer;
            pixelSource.PixelBuffer = temp;
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        internal void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.PixelBuffer?.Dispose();
            this.PixelBuffer = null;

            // Note disposing is done.
            this.isDisposed = true;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"ImageFrame<{typeof(TPixel).Name}>: {this.Width}x{this.Height}";
        }

        /// <summary>
        /// Returns a copy of the image frame in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <returns>The <see cref="ImageFrame{TPixel2}"/></returns>
        internal ImageFrame<TPixel2> CloneAs<TPixel2>()
            where TPixel2 : struct, IPixel<TPixel2>
        {
            if (typeof(TPixel2) == typeof(TPixel))
            {
                return this.Clone() as ImageFrame<TPixel2>;
            }

            var target = new ImageFrame<TPixel2>(this.MemoryManager, this.Width, this.Height, this.MetaData.Clone());

            Parallel.For(
                0,
                target.Height,
                Configuration.Default.ParallelOptions,
                y =>
                {
                    Span<TPixel> sourceRow = this.GetPixelRowSpan(y);
                    Span<TPixel2> targetRow = target.GetPixelRowSpan(y);

                    for (int x = 0; x < target.Width; x++)
                    {
                        ref var pixel = ref targetRow[x];
                        pixel.PackFromScaledVector4(sourceRow[x].ToScaledVector4());
                    }
                });

            return target;
        }

        /// <summary>
        /// Clones the current instance.
        /// </summary>
        /// <returns>The <see cref="ImageFrame{TPixel}"/></returns>
        internal ImageFrame<TPixel> Clone()
        {
            return new ImageFrame<TPixel>(this.MemoryManager, this);
        }

        /// <inheritdoc/>
        void IDisposable.Dispose()
        {
            this.Dispose();
        }
    }
}