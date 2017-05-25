// <copyright file="Buffer2D{T}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Memory
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents a buffer of value type objects
    /// interpreted as a 2D region of <see cref="Width"/> x <see cref="Height"/> elements.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    internal class Buffer2D<T> : Buffer<T>, IBuffer2D<T>
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer2D{T}"/> class.
        /// </summary>
        /// <param name="width">The number of elements in a row</param>
        /// <param name="height">The number of rows</param>
        public Buffer2D(int width, int height)
            : base(width * height)
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
            : base(array, width * height)
        {
            this.Width = width;
            this.Height = height;
        }

        /// <inheritdoc />
        public int Width { get; }

        /// <inheritdoc />
        public int Height { get; }

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
                return ref this.Array[(this.Width * y) + x];
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
            var buffer = new Buffer2D<T>(width, height);
            buffer.Clear();
            return buffer;
        }
    }
}