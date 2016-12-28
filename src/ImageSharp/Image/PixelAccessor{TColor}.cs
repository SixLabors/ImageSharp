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

    /// <summary>
    /// Provides per-pixel access to generic <see cref="Image{TColor}"/> pixels.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public unsafe class PixelAccessor<TColor> : IDisposable
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The pointer to the pixel buffer.
        /// </summary>
        private IntPtr dataPointer;

        /// <summary>
        /// The position of the first pixel in the image.
        /// </summary>
        private byte* pixelsBase;

        /// <summary>
        /// Provides a way to access the pixels from unmanaged memory.
        /// </summary>
        private GCHandle pixelsHandle;

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
        /// Initializes a new instance of the <see cref="PixelAccessor{TColor}"/> class.
        /// </summary>
        /// <param name="image">The image to provide pixel access for.</param>
        public PixelAccessor(ImageBase<TColor> image)
        {
            Guard.NotNull(image, nameof(image));
            Guard.MustBeGreaterThan(image.Width, 0, "image width");
            Guard.MustBeGreaterThan(image.Height, 0, "image height");

            this.Width = image.Width;
            this.Height = image.Height;
            this.pixelsHandle = GCHandle.Alloc(image.Pixels, GCHandleType.Pinned);
            this.dataPointer = this.pixelsHandle.AddrOfPinnedObject();
            this.pixelsBase = (byte*)this.dataPointer.ToPointer();
            this.PixelSize = Unsafe.SizeOf<TColor>();
            this.RowStride = this.Width * this.PixelSize;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelAccessor{TColor}"/> class.
        /// </summary>
        /// <param name="width">Gets the width of the image represented by the pixel buffer.</param>
        /// <param name="height">The height of the image represented by the pixel buffer.</param>
        /// <param name="pixels">The pixel buffer.</param>
        public PixelAccessor(int width, int height, TColor[] pixels)
        {
            Guard.NotNull(pixels, nameof(pixels));
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            if (pixels.Length != width * height)
            {
                throw new ArgumentException("Pixel array must have the length of Width * Height.");
            }

            this.Width = width;
            this.Height = height;
            this.pixelsHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            this.dataPointer = this.pixelsHandle.AddrOfPinnedObject();
            this.pixelsBase = (byte*)this.dataPointer.ToPointer();
            this.PixelSize = Unsafe.SizeOf<TColor>();
            this.RowStride = this.Width * this.PixelSize;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="PixelAccessor{TColor}"/> class.
        /// </summary>
        ~PixelAccessor()
        {
            this.Dispose();
        }

        /// <summary>
        /// Gets the pointer to the pixel buffer.
        /// </summary>
        public IntPtr DataPointer => this.dataPointer;

        /// <summary>
        /// Gets the size of a single pixel in the number of bytes.
        /// </summary>
        public int PixelSize { get; }

        /// <summary>
        /// Gets the width of one row in the number of bytes.
        /// </summary>
        public int RowStride { get; }

        /// <summary>
        /// Gets the width of the image.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height of the image.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets or sets the pixel at the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than zero and smaller than the width of the pixel.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than zero and smaller than the width of the pixel.</param>
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
        /// Copies a block of pixels at the specified position.
        /// </summary>
        /// <param name="sourceX">The x-coordinate of the source image.</param>
        /// <param name="sourceY">The y-coordinate of the source image.</param>
        /// <param name="target">The target pixel buffer accessor.</param>
        /// <param name="targetX">The x-coordinate of the target image.</param>
        /// <param name="targetY">The y-coordinate of the target image.</param>
        /// <param name="pixelCount">The number of pixels to copy</param>
        public void CopyBlock(int sourceX, int sourceY, PixelAccessor<TColor> target, int targetX, int targetY, int pixelCount)
        {
            int size = Unsafe.SizeOf<TColor>();
            byte* sourcePtr = this.pixelsBase + (((sourceY * this.Width) + sourceX) * size);
            byte* targetPtr = target.pixelsBase + (((targetY * target.Width) + targetX) * size);
            uint byteCount = (uint)(pixelCount * size);

            Unsafe.CopyBlock(targetPtr, sourcePtr, byteCount);
        }

        /// <summary>
        /// Copies an entire image.
        /// </summary>
        /// <param name="target">The target pixel buffer accessor.</param>
        public void CopyImage(PixelAccessor<TColor> target)
        {
            this.CopyBlock(0, 0, target, 0, 0, target.Width * target.Height);
        }

        /// <summary>
        /// Copied a row of pixels from the image.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="targetY">The target row index.</param>
        /// <param name="targetX">The target column index.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown when an unsupported component order value is passed.
        /// </exception>
        public void CopyFrom(PixelArea<TColor> area, int targetY, int targetX = 0)
        {
            int width = Math.Min(area.Width, this.Width - targetX);
            int height = Math.Min(area.Height, this.Height - targetY);

            this.CheckDimensions(width, height);
            switch (area.ComponentOrder)
            {
                case ComponentOrder.Zyx:
                    this.CopyFromZyx(area, targetY, targetX, width, height);
                    break;
                case ComponentOrder.Zyxw:
                    this.CopyFromZyxw(area, targetY, targetX, width, height);
                    break;
                case ComponentOrder.Xyz:
                    this.CopyFromXyz(area, targetY, targetX, width, height);
                    break;
                case ComponentOrder.Xyzw:
                    this.CopyFromXyzw(area, targetY, targetX, width, height);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Copied an area of pixels to the image.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="sourceY">The source row index.</param>
        /// <param name="sourceX">The source column index.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown when an unsupported component order value is passed.
        /// </exception>
        public void CopyTo(PixelArea<TColor> area, int sourceY, int sourceX = 0)
        {
            int width = Math.Min(area.Width, this.Width - sourceX);
            int height = Math.Min(area.Height, this.Height - sourceY);

            this.CheckDimensions(width, height);
            switch (area.ComponentOrder)
            {
                case ComponentOrder.Zyx:
                    this.CopyToZyx(area, sourceY, sourceX, width, height);
                    break;
                case ComponentOrder.Zyxw:
                    this.CopyToZyxw(area, sourceY, sourceX, width, height);
                    break;
                case ComponentOrder.Xyz:
                    this.CopyToXyz(area, sourceY, sourceX, width, height);
                    break;
                case ComponentOrder.Xyzw:
                    this.CopyToXyzw(area, sourceY, sourceX, width, height);
                    break;
                default:
                    throw new NotSupportedException();
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

            if (this.pixelsHandle.IsAllocated)
            {
                this.pixelsHandle.Free();
            }

            this.dataPointer = IntPtr.Zero;
            this.pixelsBase = null;

            // Note disposing is done.
            this.isDisposed = true;

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
        internal void Reset()
        {
            Unsafe.InitBlock(this.pixelsBase, 0, (uint)(this.RowStride * this.Height));
        }

        /// <summary>
        /// Copies from an area in <see cref="ComponentOrder.Zyx"/> format.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="targetY">The target row index.</param>
        /// <param name="targetX">The target column index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        protected virtual void CopyFromZyx(PixelArea<TColor> area, int targetY, int targetX, int width, int height)
        {
            TColor packed = default(TColor);
            int size = Unsafe.SizeOf<TColor>();

            for (int y = 0; y < height; y++)
            {
                byte* source = area.PixelBase + (y * area.RowStride);
                byte* destination = this.GetRowPointer(targetX, targetY + y);

                for (int x = 0; x < width; x++)
                {
                    packed.PackFromBytes(*(source + 2), *(source + 1), *source, 255);
                    Unsafe.Write(destination, packed);

                    source += 3;
                    destination += size;
                }
            }
        }

        /// <summary>
        /// Copies from an area in <see cref="ComponentOrder.Zyxw"/> format.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="targetY">The target row index.</param>
        /// <param name="targetX">The target column index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        protected virtual void CopyFromZyxw(PixelArea<TColor> area, int targetY, int targetX, int width, int height)
        {
            TColor packed = default(TColor);
            int size = Unsafe.SizeOf<TColor>();

            for (int y = 0; y < height; y++)
            {
                byte* source = area.PixelBase + (y * area.RowStride);
                byte* destination = this.GetRowPointer(targetX, targetY + y);

                for (int x = 0; x < width; x++)
                {
                    packed.PackFromBytes(*(source + 2), *(source + 1), *source, *(source + 3));
                    Unsafe.Write(destination, packed);

                    source += 4;
                    destination += size;
                }
            }
        }

        /// <summary>
        /// Copies from an area in <see cref="ComponentOrder.Xyz"/> format.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="targetY">The target row index.</param>
        /// <param name="targetX">The target column index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        protected virtual void CopyFromXyz(PixelArea<TColor> area, int targetY, int targetX, int width, int height)
        {
            TColor packed = default(TColor);
            int size = Unsafe.SizeOf<TColor>();

            for (int y = 0; y < height; y++)
            {
                byte* source = area.PixelBase + (y * area.RowStride);
                byte* destination = this.GetRowPointer(targetX, targetY + y);

                for (int x = 0; x < width; x++)
                {
                    packed.PackFromBytes(*source, *(source + 1), *(source + 2), 255);
                    Unsafe.Write(destination, packed);

                    source += 3;
                    destination += size;
                }
            }
        }

        /// <summary>
        /// Copies from an area in <see cref="ComponentOrder.Xyzw"/> format.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <param name="targetY">The target row index.</param>
        /// <param name="targetX">The target column index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        protected virtual void CopyFromXyzw(PixelArea<TColor> area, int targetY, int targetX, int width, int height)
        {
            TColor packed = default(TColor);
            int size = Unsafe.SizeOf<TColor>();

            for (int y = 0; y < height; y++)
            {
                byte* source = area.PixelBase + (y * area.RowStride);
                byte* destination = this.GetRowPointer(targetX, targetY + y);

                for (int x = 0; x < width; x++)
                {
                    packed.PackFromBytes(*source, *(source + 1), *(source + 2), *(source + 3));
                    Unsafe.Write(destination, packed);

                    source += 4;
                    destination += size;
                }
            }
        }

        /// <summary>
        /// Copies to an area in <see cref="ComponentOrder.Zyx"/> format.
        /// </summary>
        /// <param name="area">The row.</param>
        /// <param name="sourceY">The source row index.</param>
        /// <param name="sourceX">The source column index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        protected virtual void CopyToZyx(PixelArea<TColor> area, int sourceY, int sourceX, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int offset = y * area.RowStride;
                for (int x = 0; x < width; x++)
                {
                    this[sourceX + x, sourceY + y].ToZyxBytes(area.Bytes, offset);
                    offset += 3;
                }
            }
        }

        /// <summary>
        /// Copies to an area in <see cref="ComponentOrder.Zyxw"/> format.
        /// </summary>
        /// <param name="area">The row.</param>
        /// <param name="sourceY">The source row index.</param>
        /// <param name="sourceX">The source column index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        protected virtual void CopyToZyxw(PixelArea<TColor> area, int sourceY, int sourceX, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int offset = y * area.RowStride;
                for (int x = 0; x < width; x++)
                {
                    this[sourceX + x, sourceY + y].ToZyxwBytes(area.Bytes, offset);
                    offset += 4;
                }
            }
        }

        /// <summary>
        /// Copies to an area in <see cref="ComponentOrder.Xyz"/> format.
        /// </summary>
        /// <param name="area">The row.</param>
        /// <param name="sourceY">The source row index.</param>
        /// <param name="sourceX">The source column index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        protected virtual void CopyToXyz(PixelArea<TColor> area, int sourceY, int sourceX, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int offset = y * area.RowStride;
                for (int x = 0; x < width; x++)
                {
                    this[sourceX + x, sourceY + y].ToXyzBytes(area.Bytes, offset);
                    offset += 3;
                }
            }
        }

        /// <summary>
        /// Copies to an area in <see cref="ComponentOrder.Xyzw"/> format.
        /// </summary>
        /// <param name="area">The row.</param>
        /// <param name="sourceY">The source row index.</param>
        /// <param name="sourceX">The source column index.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        protected virtual void CopyToXyzw(PixelArea<TColor> area, int sourceY, int sourceX, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int offset = y * area.RowStride;
                for (int x = 0; x < width; x++)
                {
                    this[sourceX + x, sourceY + y].ToXyzwBytes(area.Bytes, offset);
                    offset += 4;
                }
            }
        }

        /// <summary>
        /// Gets the pointer at the specified row.
        /// </summary>
        /// <param name="x">The column index.</param>
        /// <param name="y">The row index.</param>
        /// <returns>
        /// The <see cref="T:byte*"/>.
        /// </returns>
        protected byte* GetRowPointer(int x, int y)
        {
            return this.pixelsBase + (((y * this.Width) + x) * Unsafe.SizeOf<TColor>());
        }

        /// <summary>
        /// Checks that the given dimensions are within the bounds of the image.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the dimensions are not within the bounds of the image.
        /// </exception>
        [Conditional("DEBUG")]
        private void CheckDimensions(int width, int height)
        {
            if (width < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, $"Invalid area size specified.");
            }

            if (height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, $"Invalid area size specified.");
            }
        }

        /// <summary>
        /// Checks the coordinates to ensure they are within bounds.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than zero and smaller than the width of the pixel.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than zero and smaller than the width of the pixel.</param>
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