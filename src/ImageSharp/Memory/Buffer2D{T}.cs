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
        public Buffer2D(Size size)
            : this(size.Width, size.Height)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer2D{T}"/> class.
        /// </summary>
        /// <param name="width">The number of elements in a row</param>
        /// <param name="height">The number of rows</param>
        public Buffer2D(int width, int height)
            : this(MemoryManager.Current.Allocate<T>(width * height), width, height)
        {
            this.Width = width;
            this.Height = height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer2D{T}"/> class.
        /// </summary>
        /// <param name="array">The array to pin</param>
        /// <param name="width">The number of elements in a row</param>
        /// <param name="height">The number of rows</param>
        public Buffer2D(T[] array, int width, int height)
        {
            this.Buffer = new Buffer<T>(array, width * height);
            this.Width = width;
            this.Height = height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer2D{T}"/> class.
        /// </summary>
        /// <param name="wrappedBuffer">The buffer to wrap</param>
        /// <param name="width">The number of elements in a row</param>
        /// <param name="height">The number of rows</param>
        public Buffer2D(Buffer<T> wrappedBuffer, int width, int height)
        {
            this.Buffer = wrappedBuffer;
            this.Width = width;
            this.Height = height;
        }

        /// <inheritdoc />
        public int Width { get; }

        /// <inheritdoc />
        public int Height { get; }

        public Span<T> Span => this.Buffer.Span;

        public Buffer<T> Buffer { get; }

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

                return ref this.Buffer.Array[(this.Width * y) + x];
            }
        }

        /// <summary>
        /// Creates a clean instance of <see cref="Buffer2D{T}"/> initializing it's elements with 'default(T)'.
        /// </summary>
        /// <param name="width">The number of elements in a row</param>
        /// <param name="height">The number of rows</param>
        /// <returns>The <see cref="Buffer{T}"/> instance</returns>
        public static Buffer2D<T> CreateClean(int width, int height)
        {
            return new Buffer2D<T>(MemoryManager.Current.Allocate<T>(width * height, true), width, height);
        }

        /// <summary>
        /// Creates a clean instance of <see cref="Buffer2D{T}"/> initializing it's elements with 'default(T)'.
        /// </summary>
        /// <param name="size">The size of the buffer</param>
        /// <returns>The <see cref="Buffer2D{T}"/> instance</returns>
        public static Buffer2D<T> CreateClean(Size size) => CreateClean(size.Width, size.Height);

        public void Dispose()
        {
            this.Buffer?.Dispose();
        }
    }
}