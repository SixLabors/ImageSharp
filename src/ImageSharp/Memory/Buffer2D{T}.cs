// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents a buffer of value type objects
    /// interpreted as a 2D region of <see cref="Width"/> x <see cref="Height"/> elements.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    internal class Buffer2D<T> : IBuffer2D<T>, IDisposable
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer2D{T}"/> class.
        /// </summary>
        /// <param name="wrappedBuffer">The buffer to wrap</param>
        /// <param name="width">The number of elements in a row</param>
        /// <param name="height">The number of rows</param>
        public Buffer2D(IBuffer<T> wrappedBuffer, int width, int height)
        {
            this.Buffer = wrappedBuffer;
            this.Width = width;
            this.Height = height;
        }

        /// <inheritdoc />
        public int Width { get; private set; }

        /// <inheritdoc />
        public int Height { get; private set; }

        /// <summary>
        /// Gets the span to the whole area.
        /// </summary>
        public Span<T> Span => this.Buffer.Span;

        /// <summary>
        /// Gets the backing <see cref="IBuffer{T}"/>
        /// </summary>
        public IBuffer<T> Buffer { get; private set; }

        /// <summary>
        /// Gets a reference to the element at the specified position.
        /// </summary>
        /// <param name="x">The x coordinate (row)</param>
        /// <param name="y">The y coordinate (position at row)</param>
        /// <returns>A reference to the element.</returns>
        public ref T this[int x, int y]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                DebugGuard.MustBeLessThan(x, this.Width, nameof(x));
                DebugGuard.MustBeLessThan(y, this.Height, nameof(y));
                Span<T> span = this.Buffer.Span;
                return ref span[(this.Width * y) + x];
            }
        }

        /// <summary>
        /// Disposes the <see cref="Buffer2D{T}"/> instance
        /// </summary>
        public void Dispose()
        {
            this.Buffer?.Dispose();
        }

        /// <summary>
        /// Swap the contents (<see cref="Buffer"/>, <see cref="Width"/>, <see cref="Height"/>) of the two buffers.
        /// Useful to transfer the contents of a temporal <see cref="Buffer2D{T}"/> to a persistent <see cref="ImageFrame{TPixel}.PixelBuffer"/>
        /// </summary>
        /// <param name="a">The first buffer</param>
        /// <param name="b">The second buffer</param>
        public static void SwapContents(Buffer2D<T> a, Buffer2D<T> b)
        {
            Size aSize = a.Size();
            Size bSize = b.Size();

            IBuffer<T> temp = a.Buffer;
            a.Buffer = b.Buffer;
            b.Buffer = temp;

            b.Width = aSize.Width;
            b.Height = aSize.Height;

            a.Width = bSize.Width;
            a.Height = bSize.Height;
        }
    }
}