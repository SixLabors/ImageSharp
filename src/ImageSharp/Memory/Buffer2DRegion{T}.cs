// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.
using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents a rectangular region inside a 2D memory buffer (<see cref="Buffer2D{T}"/>).
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public readonly struct Buffer2DRegion<T>
        where T : unmanaged
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer2DRegion{T}"/> struct.
        /// </summary>
        /// <param name="buffer">The <see cref="Buffer2D{T}"/>.</param>
        /// <param name="rectangle">The <see cref="Rectangle"/> defining a rectangular area within the buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Buffer2DRegion(Buffer2D<T> buffer, Rectangle rectangle)
        {
            DebugGuard.MustBeGreaterThanOrEqualTo(rectangle.X, 0, nameof(rectangle));
            DebugGuard.MustBeGreaterThanOrEqualTo(rectangle.Y, 0, nameof(rectangle));
            DebugGuard.MustBeLessThanOrEqualTo(rectangle.Width, buffer.Width, nameof(rectangle));
            DebugGuard.MustBeLessThanOrEqualTo(rectangle.Height, buffer.Height, nameof(rectangle));

            this.Buffer = buffer;
            this.Rectangle = rectangle;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer2DRegion{T}"/> struct.
        /// </summary>
        /// <param name="buffer">The <see cref="Buffer2D{T}"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Buffer2DRegion(Buffer2D<T> buffer)
            : this(buffer, buffer.FullRectangle())
        {
        }

        /// <summary>
        /// Gets the rectangle specifying the boundaries of the area in <see cref="Buffer"/>.
        /// </summary>
        public Rectangle Rectangle { get; }

        /// <summary>
        /// Gets the <see cref="Buffer2D{T}"/> being pointed by this instance.
        /// </summary>
        public Buffer2D<T> Buffer { get; }

        /// <summary>
        /// Gets the width
        /// </summary>
        public int Width => this.Rectangle.Width;

        /// <summary>
        /// Gets the height
        /// </summary>
        public int Height => this.Rectangle.Height;

        /// <summary>
        /// Gets the pixel stride which is equal to the width of <see cref="Buffer"/>.
        /// </summary>
        public int Stride => this.Buffer.Width;

        /// <summary>
        /// Gets the size of the area.
        /// </summary>
        internal Size Size => this.Rectangle.Size;

        /// <summary>
        /// Gets a value indicating whether the area refers to the entire <see cref="Buffer"/>
        /// </summary>
        internal bool IsFullBufferArea => this.Size == this.Buffer.Size();

        /// <summary>
        /// Gets or sets a value at the given index.
        /// </summary>
        /// <param name="x">The position inside a row</param>
        /// <param name="y">The row index</param>
        /// <returns>The reference to the value</returns>
        internal ref T this[int x, int y] => ref this.Buffer[x + this.Rectangle.X, y + this.Rectangle.Y];

        /// <summary>
        /// Gets a span to row 'y' inside this area.
        /// </summary>
        /// <param name="y">The row index</param>
        /// <returns>The span</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetRowSpan(int y)
        {
            int yy = this.Rectangle.Y + y;
            int xx = this.Rectangle.X;
            int width = this.Rectangle.Width;

            return this.Buffer.GetRowSpan(yy).Slice(xx, width);
        }

        /// <summary>
        /// Returns a subregion as <see cref="Buffer2DRegion{T}"/>. (Similar to <see cref="Span{T}.Slice(int, int)"/>.)
        /// </summary>
        /// <param name="x">The x index at the subregion origin.</param>
        /// <param name="y">The y index at the subregion origin.</param>
        /// <param name="width">The desired width of the subregion.</param>
        /// <param name="height">The desired height of the subregion.</param>
        /// <returns>The subregion</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Buffer2DRegion<T> GetSubRegion(int x, int y, int width, int height)
        {
            var rectangle = new Rectangle(x, y, width, height);
            return this.GetSubRegion(rectangle);
        }

        /// <summary>
        /// Returns a subregion as <see cref="Buffer2DRegion{T}"/>. (Similar to <see cref="Span{T}.Slice(int, int)"/>.)
        /// </summary>
        /// <param name="rectangle">The <see cref="Rectangle"/> specifying the boundaries of the subregion</param>
        /// <returns>The subregion</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Buffer2DRegion<T> GetSubRegion(Rectangle rectangle)
        {
            DebugGuard.MustBeLessThanOrEqualTo(rectangle.Width, this.Rectangle.Width, nameof(rectangle));
            DebugGuard.MustBeLessThanOrEqualTo(rectangle.Height, this.Rectangle.Height, nameof(rectangle));

            int x = this.Rectangle.X + rectangle.X;
            int y = this.Rectangle.Y + rectangle.Y;
            rectangle = new Rectangle(x, y, rectangle.Width, rectangle.Height);
            return new Buffer2DRegion<T>(this.Buffer, rectangle);
        }

        /// <summary>
        /// Gets a reference to the [0,0] element.
        /// </summary>
        /// <returns>The reference to the [0,0] element</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref T GetReferenceToOrigin()
        {
            int y = this.Rectangle.Y;
            int x = this.Rectangle.X;
            return ref this.Buffer.GetRowSpan(y)[x];
        }

        internal void Clear()
        {
            // Optimization for when the size of the area is the same as the buffer size.
            if (this.IsFullBufferArea)
            {
                this.Buffer.FastMemoryGroup.Clear();
                return;
            }

            for (int y = 0; y < this.Rectangle.Height; y++)
            {
                Span<T> row = this.GetRowSpan(y);
                row.Clear();
            }
        }
    }
}
