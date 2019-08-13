// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Represents a pixel-specific image frame containing all pixel data and <see cref="ImageFrameMetadata"/>.
    /// In case of animated formats like gif, it contains the single frame in a animation.
    /// In all other cases it is the only frame of the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed class ImageFrame<TPixel> : ImageFrame, IPixelSource<TPixel>, IDisposable
        where TPixel : struct, IPixel<TPixel>
    {
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        internal ImageFrame(Configuration configuration, int width, int height)
            : this(configuration, width, height, new ImageFrameMetadata())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="size">The <see cref="Size"/> of the frame.</param>
        /// <param name="metadata">The metadata.</param>
        internal ImageFrame(Configuration configuration, Size size, ImageFrameMetadata metadata)
            : this(configuration, size.Width, size.Height, metadata)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="metadata">The metadata.</param>
        internal ImageFrame(Configuration configuration, int width, int height, ImageFrameMetadata metadata)
            : base(configuration, width, height, metadata)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.PixelBuffer = this.MemoryAllocator.Allocate2D<TPixel>(width, height, AllocationOptions.Clean);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="backgroundColor">The color to clear the image with.</param>
        internal ImageFrame(Configuration configuration, int width, int height, TPixel backgroundColor)
            : this(configuration, width, height, backgroundColor, new ImageFrameMetadata())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="backgroundColor">The color to clear the image with.</param>
        /// <param name="metadata">The metadata.</param>
        internal ImageFrame(Configuration configuration, int width, int height, TPixel backgroundColor, ImageFrameMetadata metadata)
            : base(configuration, width, height, metadata)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.PixelBuffer = this.MemoryAllocator.Allocate2D<TPixel>(width, height);
            this.Clear(backgroundColor);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class wrapping an existing buffer.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="memorySource">The memory source.</param>
        internal ImageFrame(Configuration configuration, int width, int height, MemorySource<TPixel> memorySource)
            : this(configuration, width, height, memorySource, new ImageFrameMetadata())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class wrapping an existing buffer.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="memorySource">The memory source.</param>
        /// <param name="metadata">The metadata.</param>
        internal ImageFrame(Configuration configuration, int width, int height, MemorySource<TPixel> memorySource, ImageFrameMetadata metadata)
            : base(configuration, width, height, metadata)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.PixelBuffer = new Buffer2D<TPixel>(memorySource, width, height);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="source">The source.</param>
        internal ImageFrame(Configuration configuration, ImageFrame<TPixel> source)
            : base(configuration, source.Width, source.Height, source.Metadata.DeepClone())
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(source, nameof(source));

            this.PixelBuffer = this.MemoryAllocator.Allocate2D<TPixel>(source.PixelBuffer.Width, source.PixelBuffer.Height);
            source.PixelBuffer.GetSpan().CopyTo(this.PixelBuffer.GetSpan());
        }

        /// <summary>
        /// Gets the image pixels. Not private as Buffer2D requires an array in its constructor.
        /// </summary>
        internal Buffer2D<TPixel> PixelBuffer { get; private set; }

        /// <inheritdoc/>
        Buffer2D<TPixel> IPixelSource<TPixel>.PixelBuffer => this.PixelBuffer;

        /// <summary>
        /// Gets or sets the pixel at the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than or equal to zero and less than the width of the image.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than or equal to zero and less than the height of the image.</param>
        /// <returns>The <see typeparam="TPixel"/> at the specified position.</returns>
        public TPixel this[int x, int y]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.PixelBuffer[x, y];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.PixelBuffer[x, y] = value;
        }

        /// <summary>
        /// Gets a reference to the pixel at the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than or equal to zero and less than the width of the image.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than or equal to zero and less than the height of the image.</param>
        /// <returns>The <see typeparam="TPixel"/> at the specified position.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref TPixel GetPixelReference(int x, int y) => ref this.PixelBuffer[x, y];

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

            this.GetPixelSpan().CopyTo(target.GetSpan());
        }

        /// <summary>
        /// Switches the buffers used by the image and the pixelSource meaning that the Image will "own" the buffer from the pixelSource and the pixelSource will now own the Images buffer.
        /// </summary>
        /// <param name="pixelSource">The pixel source.</param>
        internal void SwapOrCopyPixelsBufferFrom(ImageFrame<TPixel> pixelSource)
        {
            Guard.NotNull(pixelSource, nameof(pixelSource));

            Buffer2D<TPixel>.SwapOrCopyContent(this.PixelBuffer, pixelSource.PixelBuffer);
            this.UpdateSize(this.PixelBuffer.Size());
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        public override void Dispose()
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

        internal override void CopyPixelsTo<TDestinationPixel>(Span<TDestinationPixel> destination)
        {
            if (typeof(TPixel) == typeof(TDestinationPixel))
            {
                Span<TPixel> dest1 = MemoryMarshal.Cast<TDestinationPixel, TPixel>(destination);
                this.PixelBuffer.Span.CopyTo(dest1);
            }

            PixelOperations<TPixel>.Instance.To(this.Configuration, this.PixelBuffer.Span, destination);
        }

        /// <inheritdoc/>
        public override string ToString() => $"ImageFrame<{typeof(TPixel).Name}>({this.Width}x{this.Height})";

        /// <summary>
        /// Clones the current instance.
        /// </summary>
        /// <returns>The <see cref="ImageFrame{TPixel}"/></returns>
        internal ImageFrame<TPixel> Clone() => this.Clone(this.Configuration);

        /// <summary>
        /// Clones the current instance.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <returns>The <see cref="ImageFrame{TPixel}"/></returns>
        internal ImageFrame<TPixel> Clone(Configuration configuration) => new ImageFrame<TPixel>(configuration, this);

        /// <summary>
        /// Returns a copy of the image frame in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <returns>The <see cref="ImageFrame{TPixel2}"/></returns>
        internal ImageFrame<TPixel2> CloneAs<TPixel2>()
            where TPixel2 : struct, IPixel<TPixel2> => this.CloneAs<TPixel2>(this.Configuration);

        /// <summary>
        /// Returns a copy of the image frame in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <returns>The <see cref="ImageFrame{TPixel2}"/></returns>
        internal ImageFrame<TPixel2> CloneAs<TPixel2>(Configuration configuration)
            where TPixel2 : struct, IPixel<TPixel2>
        {
            if (typeof(TPixel2) == typeof(TPixel))
            {
                return this.Clone(configuration) as ImageFrame<TPixel2>;
            }

            var target = new ImageFrame<TPixel2>(configuration, this.Width, this.Height, this.Metadata.DeepClone());

            ParallelHelper.IterateRows(
                this.Bounds(),
                configuration,
                rows =>
                    {
                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            Span<TPixel> sourceRow = this.GetPixelRowSpan(y);
                            Span<TPixel2> targetRow = target.GetPixelRowSpan(y);
                            PixelOperations<TPixel>.Instance.To(configuration, sourceRow, targetRow);
                        }
                    });

            return target;
        }

        /// <summary>
        /// Clears the bitmap.
        /// </summary>
        /// <param name="value">The value to initialize the bitmap with.</param>
        internal void Clear(TPixel value)
        {
            Span<TPixel> span = this.GetPixelSpan();

            if (value.Equals(default))
            {
                span.Clear();
            }
            else
            {
                span.Fill(value);
            }
        }
    }
}
