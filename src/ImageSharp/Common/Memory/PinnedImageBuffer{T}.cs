// <copyright file="PinnedImageBuffer{T}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// Represents a pinned buffer of value type objects
    /// interpreted as a 2D region of <see cref="Width"/> x <see cref="Height"/> elements.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    internal class PinnedImageBuffer<T> : PinnedBuffer<T>, IPinnedImageBuffer<T>
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PinnedImageBuffer{T}"/> class.
        /// </summary>
        /// <param name="width">The number of elements in a row</param>
        /// <param name="height">The number of rows</param>
        public PinnedImageBuffer(int width, int height)
            : base(width * height)
        {
            this.Width = width;
            this.Height = height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PinnedImageBuffer{T}"/> class.
        /// </summary>
        /// <param name="array">The array to pin</param>
        /// <param name="width">The number of elements in a row</param>
        /// <param name="height">The number of rows</param>
        public PinnedImageBuffer(T[] array, int width, int height)
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
        /// Creates a clean instance of <see cref="PinnedImageBuffer{T}"/> initializing it's elements with 'default(T)'.
        /// </summary>
        /// <param name="width">The number of elements in a row</param>
        /// <param name="height">The number of rows</param>
        /// <returns>The <see cref="PinnedBuffer{T}"/> instance</returns>
        public static PinnedImageBuffer<T> CreateClean(int width, int height)
        {
            PinnedImageBuffer<T> buffer = new PinnedImageBuffer<T>(width, height);
            buffer.Clear();
            return buffer;
        }
    }
}