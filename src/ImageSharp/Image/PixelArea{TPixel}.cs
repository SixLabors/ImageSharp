// <copyright file="PixelArea{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Diagnostics;
    using System.IO;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Represents an area of generic <see cref="Image{TPixel}"/> pixels.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal sealed class PixelArea<TPixel> : IDisposable
        where TPixel : struct, IPixel<TPixel>
    {
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
        /// The underlying buffer containing the raw pixel data.
        /// </summary>
        private Buffer<byte> byteBuffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelArea{TPixel}"/> class.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="bytes">The bytes.</param>
        /// <param name="componentOrder">The component order.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="bytes"></paramref> is the incorrect length.
        /// </exception>
        public PixelArea(int width, byte[] bytes, ComponentOrder componentOrder)
            : this(width, 1, bytes, componentOrder)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelArea{TPixel}"/> class.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="bytes">The bytes.</param>
        /// <param name="componentOrder">The component order.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="bytes"></paramref> is the incorrect length.
        /// </exception>
        public PixelArea(int width, int height, byte[] bytes, ComponentOrder componentOrder)
        {
            this.CheckBytesLength(width, height, bytes, componentOrder);

            this.Width = width;
            this.Height = height;
            this.ComponentOrder = componentOrder;
            this.RowStride = width * GetComponentCount(componentOrder);
            this.Length = bytes.Length; // TODO: Is this the right value for Length?

            this.byteBuffer = new Buffer<byte>(bytes);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelArea{TPixel}"/> class.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="componentOrder">The component order.</param>
        public PixelArea(int width, ComponentOrder componentOrder)
            : this(width, 1, componentOrder, 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelArea{TPixel}"/> class.
        /// </summary>
        /// <param name="width">The width. </param>
        /// <param name="componentOrder">The component order.</param>
        /// <param name="padding">The number of bytes to pad each row.</param>
        public PixelArea(int width, ComponentOrder componentOrder, int padding)
            : this(width, 1, componentOrder, padding)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelArea{TPixel}"/> class.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="componentOrder">The component order.</param>
        public PixelArea(int width, int height, ComponentOrder componentOrder)
            : this(width, height, componentOrder, 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelArea{TPixel}"/> class.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="componentOrder">The component order.</param>
        /// <param name="padding">The number of bytes to pad each row.</param>
        public PixelArea(int width, int height, ComponentOrder componentOrder, int padding)
        {
            this.Width = width;
            this.Height = height;
            this.ComponentOrder = componentOrder;
            this.RowStride = (width * GetComponentCount(componentOrder)) + padding;
            this.Length = this.RowStride * height;

            this.byteBuffer = new Buffer<byte>(this.Length);
        }

        /// <summary>
        /// Gets the data in bytes.
        /// </summary>
        public byte[] Bytes => this.byteBuffer.Array;

        /// <summary>
        /// Gets the length of the buffer.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the component order.
        /// </summary>
        public ComponentOrder ComponentOrder { get; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the width of one row in the number of bytes.
        /// </summary>
        public int RowStride { get; }

        /// <summary>
        /// Gets the width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.byteBuffer.Dispose();
            this.isDisposed = true;
        }

        /// <summary>
        /// Reads the stream to the area.
        /// </summary>
        /// <param name="stream">The stream.</param>
        public void Read(Stream stream)
        {
            stream.Read(this.Bytes, 0, this.Length);
        }

        /// <summary>
        /// Writes the area to the stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        public void Write(Stream stream)
        {
            stream.Write(this.Bytes, 0, this.Length);
        }

        /// <summary>
        /// Resets the bytes of the array to it's initial value.
        /// </summary>
        public void Reset()
        {
            this.byteBuffer.Clear();
        }

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to the row y.
        /// </summary>
        /// <param name="y">The y coordinate</param>
        /// <returns>The <see cref="Span{T}"/></returns>
        internal Span<byte> GetRowSpan(int y)
        {
            return this.byteBuffer.Slice(y * this.RowStride);
        }

        /// <summary>
        /// Gets component count for the given order.
        /// </summary>
        /// <param name="componentOrder">The component order.</param>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Thrown if an invalid order is given.
        /// </exception>
        private static int GetComponentCount(ComponentOrder componentOrder)
        {
            switch (componentOrder)
            {
                case ComponentOrder.Zyx:
                case ComponentOrder.Xyz:
                    return 3;
                case ComponentOrder.Zyxw:
                case ComponentOrder.Xyzw:
                    return 4;
            }

            throw new NotSupportedException();
        }

        /// <summary>
        /// Checks that the length of the byte array to ensure that it matches the given width and height.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="bytes">The byte array.</param>
        /// <param name="componentOrder">The component order.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the byte array is th incorrect length.
        /// </exception>
        [Conditional("DEBUG")]
        private void CheckBytesLength(int width, int height, byte[] bytes, ComponentOrder componentOrder)
        {
            int requiredLength = (width * GetComponentCount(componentOrder)) * height;
            if (bytes.Length != requiredLength)
            {
                throw new ArgumentOutOfRangeException(
                          nameof(bytes),
                          $"Invalid byte array length. Length {bytes.Length}; Should be {requiredLength}.");
            }
        }
    }
}