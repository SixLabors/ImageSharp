// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Represents a pixel-specific image frame containing all pixel data and <see cref="ImageFrameMetadata"/>.
    /// In case of animated formats like gif, it contains the single frame in a animation.
    /// In all other cases it is the only frame of the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed class ImageFrame<TPixel> : ImageFrame, IPixelSource<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
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

            this.PixelBuffer = this.GetConfiguration().MemoryAllocator.Allocate2D<TPixel>(width, height, AllocationOptions.Clean);
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

            this.PixelBuffer = this.GetConfiguration().MemoryAllocator.Allocate2D<TPixel>(width, height);
            this.Clear(backgroundColor);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}" /> class wrapping an existing buffer.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="memorySource">The memory source.</param>
        internal ImageFrame(Configuration configuration, int width, int height, MemoryGroup<TPixel> memorySource)
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
        internal ImageFrame(Configuration configuration, int width, int height, MemoryGroup<TPixel> memorySource, ImageFrameMetadata metadata)
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

            this.PixelBuffer = this.GetConfiguration().MemoryAllocator.Allocate2D<TPixel>(source.PixelBuffer.Width, source.PixelBuffer.Height);
            source.PixelBuffer.FastMemoryGroup.CopyTo(this.PixelBuffer.FastMemoryGroup);
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
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the provided (x,y) coordinates are outside the image boundary.</exception>
        public TPixel this[int x, int y]
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get
            {
                this.VerifyCoords(x, y);
                return this.PixelBuffer.GetElementUnsafe(x, y);
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            set
            {
                this.VerifyCoords(x, y);
                this.PixelBuffer.GetElementUnsafe(x, y) = value;
            }
        }

        /// <summary>
        /// Gets the representation of the pixels as a <see cref="Span{T}"/> of contiguous memory
        /// at row <paramref name="rowIndex"/> beginning from the first pixel on that row.
        /// </summary>
        /// <param name="rowIndex">The row.</param>
        /// <returns>The <see cref="Span{TPixel}"/></returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when row index is out of range.</exception>
        public Span<TPixel> GetPixelRowSpan(int rowIndex)
        {
            Guard.MustBeGreaterThanOrEqualTo(rowIndex, 0, nameof(rowIndex));
            Guard.MustBeLessThan(rowIndex, this.Height, nameof(rowIndex));

            return this.PixelBuffer.GetRowSpan(rowIndex);
        }

        /// <summary>
        /// Gets the representation of the pixels as a <see cref="Span{T}"/> in the source image's pixel format
        /// stored in row major order, if the backing buffer is contiguous.
        /// </summary>
        /// <param name="span">The <see cref="Span{T}"/>.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public bool TryGetSinglePixelSpan(out Span<TPixel> span)
        {
            IMemoryGroup<TPixel> mg = this.GetPixelMemoryGroup();
            if (mg.Count > 1)
            {
                span = default;
                return false;
            }

            span = mg.Single().Span;
            return true;
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

            this.PixelBuffer.FastMemoryGroup.CopyTo(target.FastMemoryGroup);
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

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                this.PixelBuffer?.Dispose();
                this.PixelBuffer = null;
            }

            this.isDisposed = true;
        }

        internal override void CopyPixelsTo<TDestinationPixel>(MemoryGroup<TDestinationPixel> destination)
        {
            if (typeof(TPixel) == typeof(TDestinationPixel))
            {
                this.PixelBuffer.FastMemoryGroup.TransformTo(destination, (s, d) =>
                {
                    Span<TPixel> d1 = MemoryMarshal.Cast<TDestinationPixel, TPixel>(d);
                    s.CopyTo(d1);
                });
                return;
            }

            this.PixelBuffer.FastMemoryGroup.TransformTo(destination, (s, d) =>
            {
                PixelOperations<TPixel>.Instance.To(this.GetConfiguration(), s, d);
            });
        }

        /// <inheritdoc/>
        public override string ToString() => $"ImageFrame<{typeof(TPixel).Name}>({this.Width}x{this.Height})";

        /// <summary>
        /// Clones the current instance.
        /// </summary>
        /// <returns>The <see cref="ImageFrame{TPixel}"/></returns>
        internal ImageFrame<TPixel> Clone() => this.Clone(this.GetConfiguration());

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
            where TPixel2 : unmanaged, IPixel<TPixel2> => this.CloneAs<TPixel2>(this.GetConfiguration());

        /// <summary>
        /// Returns a copy of the image frame in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <returns>The <see cref="ImageFrame{TPixel2}"/></returns>
        internal ImageFrame<TPixel2> CloneAs<TPixel2>(Configuration configuration)
            where TPixel2 : unmanaged, IPixel<TPixel2>
        {
            if (typeof(TPixel2) == typeof(TPixel))
            {
                return this.Clone(configuration) as ImageFrame<TPixel2>;
            }

            var target = new ImageFrame<TPixel2>(configuration, this.Width, this.Height, this.Metadata.DeepClone());
            var operation = new RowIntervalOperation<TPixel2>(this, target, configuration);

            ParallelRowIterator.IterateRowIntervals(
                configuration,
                this.Bounds(),
                in operation);

            return target;
        }

        /// <summary>
        /// Clears the bitmap.
        /// </summary>
        /// <param name="value">The value to initialize the bitmap with.</param>
        internal void Clear(TPixel value)
        {
            MemoryGroup<TPixel> group = this.PixelBuffer.FastMemoryGroup;

            if (value.Equals(default))
            {
                group.Clear();
            }
            else
            {
                group.Fill(value);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private void VerifyCoords(int x, int y)
        {
            if (x < 0 || x >= this.Width)
            {
                ThrowArgumentOutOfRangeException(nameof(x));
            }

            if (y < 0 || y >= this.Height)
            {
                ThrowArgumentOutOfRangeException(nameof(y));
            }
        }

        [MethodImpl(InliningOptions.ColdPath)]
        private static void ThrowArgumentOutOfRangeException(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the clone logic for <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        private readonly struct RowIntervalOperation<TPixel2> : IRowIntervalOperation
            where TPixel2 : unmanaged, IPixel<TPixel2>
        {
            private readonly ImageFrame<TPixel> source;
            private readonly ImageFrame<TPixel2> target;
            private readonly Configuration configuration;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowIntervalOperation(
                ImageFrame<TPixel> source,
                ImageFrame<TPixel2> target,
                Configuration configuration)
            {
                this.source = source;
                this.target = target;
                this.configuration = configuration;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> sourceRow = this.source.GetPixelRowSpan(y);
                    Span<TPixel2> targetRow = this.target.GetPixelRowSpan(y);
                    PixelOperations<TPixel>.Instance.To(this.configuration, sourceRow, targetRow);
                }
            }
        }
    }
}
