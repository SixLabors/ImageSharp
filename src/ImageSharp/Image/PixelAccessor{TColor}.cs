// <copyright file="PixelAccessor{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides per-pixel access to generic <see cref="Image{TColor}"/> pixels.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public sealed unsafe class PixelAccessor<TColor> : IDisposable, IPinnedImageBuffer<TColor>
        where TColor : struct, IPixel<TColor>
    {
        /// <summary>
        /// The position of the first pixel in the image.
        /// </summary>
        private byte* pixelsBase;

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose() method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;

        /// <summary>
        /// The <see cref="PinnedBuffer{T}"/> containing the pixel data.
        /// </summary>
        private PinnedImageBuffer<TColor> pixelBuffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelAccessor{TColor}"/> class.
        /// </summary>
        /// <param name="image">The image to provide pixel access for.</param>
        public PixelAccessor(ImageBase<TColor> image)
        {
            Guard.NotNull(image, nameof(image));
            Guard.MustBeGreaterThan(image.Width, 0, "image width");
            Guard.MustBeGreaterThan(image.Height, 0, "image height");

            this.SetPixelBufferUnsafe(image.Width, image.Height, image.Pixels);
            this.ParallelOptions = image.Configuration.ParallelOptions;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelAccessor{TColor}"/> class.
        /// </summary>
        /// <param name="width">The width of the image represented by the pixel buffer.</param>
        /// <param name="height">The height of the image represented by the pixel buffer.</param>
        public PixelAccessor(int width, int height)
            : this(width, height, PinnedImageBuffer<TColor>.CreateClean(width, height))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelAccessor{TColor}" /> class.
        /// </summary>
        /// <param name="width">The width of the image represented by the pixel buffer.</param>
        /// <param name="height">The height of the image represented by the pixel buffer.</param>
        /// <param name="pixels">The pixel buffer.</param>
        private PixelAccessor(int width, int height, PinnedImageBuffer<TColor> pixels)
        {
            Guard.NotNull(pixels, nameof(pixels));
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.SetPixelBufferUnsafe(width, height, pixels);

            this.ParallelOptions = Configuration.Default.ParallelOptions;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="PixelAccessor{TColor}"/> class.
        /// </summary>
        ~PixelAccessor()
        {
            this.Dispose();
        }

        /// <summary>
        /// Gets the pixel buffer array.
        /// </summary>
        public TColor[] PixelArray => this.pixelBuffer.Array;

        /// <summary>
        /// Gets the pointer to the pixel buffer.
        /// </summary>
        public IntPtr DataPointer => this.pixelBuffer.Pointer;

        /// <summary>
        /// Gets the size of a single pixel in the number of bytes.
        /// </summary>
        public int PixelSize { get; private set; }

        /// <summary>
        /// Gets the width of one row in the number of bytes.
        /// </summary>
        public int RowStride { get; private set; }

        /// <summary>
        /// Gets the width of the image.
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Gets the height of the image.
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// Gets the global parallel options for processing tasks in parallel.
        /// </summary>
        public ParallelOptions ParallelOptions { get; }

        /// <inheritdoc />
        BufferSpan<TColor> IPinnedImageBuffer<TColor>.Span => this.pixelBuffer;

        private static BulkPixelOperations<TColor> Operations => BulkPixelOperations<TColor>.Instance;

        /// <summary>
        /// Gets or sets the pixel at the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than or equal to zero and less than the width of the image.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than or equal to zero and less than the height of the image.</param>
        /// <returns>The <see typeparam="TColor"/> at the specified position.</returns>
        public TColor this[int x, int y]
        {
            get
            {
                this.CheckCoordinates(x, y);

                return Unsafe.Read<TColor>(this.pixelsBase + (((y * this.Width) + x) * Unsafe.SizeOf<TColor>()));
            }

            set
            {
                this.CheckCoordinates(x, y);

                Unsafe.Write(this.pixelsBase + (((y * this.Width) + x) * Unsafe.SizeOf<TColor>()), value);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            // Note disposing is done.
            this.isDisposed = true;

            this.pixelBuffer.Dispose();

            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SuppressFinalize to
            // take this object off the finalization queue
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Resets all the pixels to it's initial value.
        /// </summary>
        public void Reset()
        {
            Unsafe.InitBlock(this.pixelsBase, 0, (uint)(this.RowStride * this.Height));
        }

        /// <summary>
        /// Copy an area of pixels to the image.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="targetY">The target row index.</param>
        /// <param name="targetX">The target column index.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown when an unsupported component order value is passed.
        /// </exception>
        internal void CopyFrom(PixelArea<TColor> area, int targetY, int targetX = 0)
        {
            this.CheckCoordinates(area, targetX, targetY);

            this.CopyFrom(area, targetX, targetY, area.Width, area.Height);
        }

        /// <summary>
        /// Copy pixels from the image to an area of pixels.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="sourceY">The source row index.</param>
        /// <param name="sourceX">The source column index.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown when an unsupported component order value is passed.
        /// </exception>
        internal void CopyTo(PixelArea<TColor> area, int sourceY, int sourceX = 0)
        {
            this.CheckCoordinates(area, sourceX, sourceY);

            this.CopyTo(area, sourceX, sourceY, area.Width, area.Height);
        }

        /// <summary>
        /// Copy pixels from the image to an area of pixels. This method will make sure that the pixels
        /// that are copied are within the bounds of the image.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="sourceY">The source row index.</param>
        /// <param name="sourceX">The source column index.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown when an unsupported component order value is passed.
        /// </exception>
        internal void SafeCopyTo(PixelArea<TColor> area, int sourceY, int sourceX = 0)
        {
            int width = Math.Min(area.Width, this.Width - sourceX);
            if (width < 1)
            {
                return;
            }

            int height = Math.Min(area.Height, this.Height - sourceY);
            if (height < 1)
            {
                return;
            }

            this.CopyTo(area, sourceX, sourceY, width, height);
        }

        /// <summary>
        /// Sets the pixel buffer in an unsafe manner. This should not be used unless you know what its doing!!!
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="pixels">The pixels.</param>
        /// <returns>Returns the old pixel data thats has gust been replaced.</returns>
        /// <remarks>If <see cref="M:PixelAccessor.PooledMemory"/> is true then caller is responsible for ensuring <see cref="M:PixelDataPool.Return()"/> is called.</remarks>
        internal TColor[] ReturnCurrentPixelsAndReplaceThemInternally(int width, int height, TColor[] pixels)
        {
            TColor[] oldPixels = this.pixelBuffer.UnPinAndTakeArrayOwnership();
            this.SetPixelBufferUnsafe(width, height, pixels);
            return oldPixels;
        }

        /// <summary>
        /// Copies the pixels to another <see cref="PixelAccessor{TColor}"/> of the same size.
        /// </summary>
        /// <param name="target">The target pixel buffer accessor.</param>
        internal void CopyTo(PixelAccessor<TColor> target)
        {
            uint byteCount = (uint)(this.Width * this.Height * Unsafe.SizeOf<TColor>());

            Unsafe.CopyBlock(target.pixelsBase, this.pixelsBase, byteCount);
        }

        /// <summary>
        /// Copies from an area in <see cref="ComponentOrder.Zyx"/> format.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="targetX">The target column index.</param>
        /// <param name="targetY">The target row index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyFromZyx(PixelArea<TColor> area, int targetX, int targetY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                BufferSpan<byte> source = area.GetRowSpan(y);
                BufferSpan<TColor> destination = this.GetRowSpan(targetX, targetY + y);

                Operations.PackFromZyxBytes(source, destination, width);
            }
        }

        /// <summary>
        /// Copies from an area in <see cref="ComponentOrder.Zyxw"/> format.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="targetX">The target column index.</param>
        /// <param name="targetY">The target row index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyFromZyxw(PixelArea<TColor> area, int targetX, int targetY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                BufferSpan<byte> source = area.GetRowSpan(y);
                BufferSpan<TColor> destination = this.GetRowSpan(targetX, targetY + y);

                Operations.PackFromZyxwBytes(source, destination, width);
            }
        }

        /// <summary>
        /// Copies from an area in <see cref="ComponentOrder.Xyz"/> format.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="targetX">The target column index.</param>
        /// <param name="targetY">The target row index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyFromXyz(PixelArea<TColor> area, int targetX, int targetY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                BufferSpan<byte> source = area.GetRowSpan(y);
                BufferSpan<TColor> destination = this.GetRowSpan(targetX, targetY + y);

                Operations.PackFromXyzBytes(source, destination, width);
            }
        }

        /// <summary>
        /// Copies from an area in <see cref="ComponentOrder.Xyzw"/> format.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="targetX">The target column index.</param>
        /// <param name="targetY">The target row index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyFromXyzw(PixelArea<TColor> area, int targetX, int targetY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                BufferSpan<byte> source = area.GetRowSpan(y);
                BufferSpan<TColor> destination = this.GetRowSpan(targetX, targetY + y);
                Operations.PackFromXyzwBytes(source, destination, width);
            }
        }

        /// <summary>
        /// Copies to an area in <see cref="ComponentOrder.Zyx"/> format.
        /// </summary>
        /// <param name="area">The row.</param>
        /// <param name="sourceX">The source column index.</param>
        /// <param name="sourceY">The source row index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyToZyx(PixelArea<TColor> area, int sourceX, int sourceY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                BufferSpan<TColor> source = this.GetRowSpan(sourceX, sourceY + y);
                BufferSpan<byte> destination = area.GetRowSpan(y);
                Operations.ToZyxBytes(source, destination, width);
            }
        }

        /// <summary>
        /// Copies to an area in <see cref="ComponentOrder.Zyxw"/> format.
        /// </summary>
        /// <param name="area">The row.</param>
        /// <param name="sourceX">The source column index.</param>
        /// <param name="sourceY">The source row index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyToZyxw(PixelArea<TColor> area, int sourceX, int sourceY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                BufferSpan<TColor> source = this.GetRowSpan(sourceX, sourceY + y);
                BufferSpan<byte> destination = area.GetRowSpan(y);
                Operations.ToZyxwBytes(source, destination, width);
            }
        }

        /// <summary>
        /// Copies to an area in <see cref="ComponentOrder.Xyz"/> format.
        /// </summary>
        /// <param name="area">The row.</param>
        /// <param name="sourceX">The source column index.</param>
        /// <param name="sourceY">The source row index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyToXyz(PixelArea<TColor> area, int sourceX, int sourceY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                BufferSpan<TColor> source = this.GetRowSpan(sourceX, sourceY + y);
                BufferSpan<byte> destination = area.GetRowSpan(y);
                Operations.ToXyzBytes(source, destination, width);
            }
        }

        /// <summary>
        /// Copies to an area in <see cref="ComponentOrder.Xyzw"/> format.
        /// </summary>
        /// <param name="area">The row.</param>
        /// <param name="sourceX">The source column index.</param>
        /// <param name="sourceY">The source row index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyToXyzw(PixelArea<TColor> area, int sourceX, int sourceY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                BufferSpan<TColor> source = this.GetRowSpan(sourceX, sourceY + y);
                BufferSpan<byte> destination = area.GetRowSpan(y);
                Operations.ToXyzwBytes(source, destination, width);
            }
        }

        private void SetPixelBufferUnsafe(int width, int height, TColor[] pixels)
        {
            this.SetPixelBufferUnsafe(width, height, new PinnedImageBuffer<TColor>(pixels, width, height));
        }

        /// <summary>
        /// Sets the pixel buffer in an unsafe manor this should not be used unless you know what its doing!!!
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="pixels">The pixel buffer</param>
        private void SetPixelBufferUnsafe(int width, int height, PinnedImageBuffer<TColor> pixels)
        {
            this.pixelBuffer = pixels;
            this.pixelsBase = (byte*)pixels.Pointer;

            this.Width = width;
            this.Height = height;
            this.PixelSize = Unsafe.SizeOf<TColor>();
            this.RowStride = this.Width * this.PixelSize;
        }

        /// <summary>
        /// Copy an area of pixels to the image.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="targetX">The target column index.</param>
        /// <param name="targetY">The target row index.</param>
        /// <param name="width">The width of the area to copy.</param>
        /// <param name="height">The height of the area to copy.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown when an unsupported component order value is passed.
        /// </exception>
        private void CopyFrom(PixelArea<TColor> area, int targetX, int targetY, int width, int height)
        {
            switch (area.ComponentOrder)
            {
                case ComponentOrder.Zyx:
                    this.CopyFromZyx(area, targetX, targetY, width, height);
                    break;
                case ComponentOrder.Zyxw:
                    this.CopyFromZyxw(area, targetX, targetY, width, height);
                    break;
                case ComponentOrder.Xyz:
                    this.CopyFromXyz(area, targetX, targetY, width, height);
                    break;
                case ComponentOrder.Xyzw:
                    this.CopyFromXyzw(area, targetX, targetY, width, height);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Copy pixels from the image to an area of pixels.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="sourceX">The source column index.</param>
        /// <param name="sourceY">The source row index.</param>
        /// <param name="width">The width of the area to copy.</param>
        /// <param name="height">The height of the area to copy.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown when an unsupported component order value is passed.
        /// </exception>
        private void CopyTo(PixelArea<TColor> area, int sourceX, int sourceY, int width, int height)
        {
            switch (area.ComponentOrder)
            {
                case ComponentOrder.Zyx:
                    this.CopyToZyx(area, sourceX, sourceY, width, height);
                    break;
                case ComponentOrder.Zyxw:
                    this.CopyToZyxw(area, sourceX, sourceY, width, height);
                    break;
                case ComponentOrder.Xyz:
                    this.CopyToXyz(area, sourceX, sourceY, width, height);
                    break;
                case ComponentOrder.Xyzw:
                    this.CopyToXyzw(area, sourceX, sourceY, width, height);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Checks that the given area and offset are within the bounds of the image.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than zero and less than the width of the image.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than zero and less than the height of the image.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the dimensions are not within the bounds of the image.
        /// </exception>
        [Conditional("DEBUG")]
        private void CheckCoordinates(PixelArea<TColor> area, int x, int y)
        {
            int width = Math.Min(area.Width, this.Width - x);
            if (width < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Invalid area size specified.");
            }

            int height = Math.Min(area.Height, this.Height - y);
            if (height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Invalid area size specified.");
            }
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