// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents a buffer of value type objects
    /// interpreted as a 2D region of <see cref="Width"/> x <see cref="Height"/> elements.
    /// </summary>
    /// <remarks>
    /// Before RC1, this class might be target of API changes, use it on your own risk!
    /// </remarks>
    /// <typeparam name="T">The value type.</typeparam>
    public sealed class Buffer2D<T> : IDisposable
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer2D{T}"/> class.
        /// </summary>
        /// <param name="memoryGroup">The <see cref="MemoryGroup{T}"/> to wrap.</param>
        /// <param name="width">The number of elements in a row.</param>
        /// <param name="height">The number of rows.</param>
        internal Buffer2D(MemoryGroup<T> memoryGroup, int width, int height)
        {
            this.MemoryGroup = memoryGroup;
            this.Width = width;
            this.Height = height;
        }

        /// <summary>
        /// Gets the width.
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// Gets the backing <see cref="MemoryGroup{T}"/>.
        /// </summary>
        internal MemoryGroup<T> MemoryGroup { get; }

        /// <summary>
        /// Gets a reference to the element at the specified position.
        /// </summary>
        /// <param name="x">The x coordinate (row)</param>
        /// <param name="y">The y coordinate (position at row)</param>
        /// <returns>A reference to the element.</returns>
        internal ref T this[int x, int y]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                DebugGuard.MustBeLessThan(x, this.Width, nameof(x));
                DebugGuard.MustBeLessThan(y, this.Height, nameof(y));

                return ref this.GetRowSpan(y)[x];
            }
        }

        /// <summary>
        /// Disposes the <see cref="Buffer2D{T}"/> instance
        /// </summary>
        public void Dispose()
        {
            this.MemoryGroup.Dispose();
        }

        /// <summary>
        /// Swaps the contents of 'destination' with 'source' if the buffers are owned (1),
        /// copies the contents of 'source' to 'destination' otherwise (2). Buffers should be of same size in case 2!
        /// </summary>
        internal static void SwapOrCopyContent(Buffer2D<T> destination, Buffer2D<T> source)
        {
            MemoryGroup<T>.SwapOrCopyContent(destination.MemoryGroup, source.MemoryGroup);
            SwapDimensionData(destination, source);
        }

        private static void SwapDimensionData(Buffer2D<T> a, Buffer2D<T> b)
        {
            Size aSize = a.Size();
            Size bSize = b.Size();

            b.Width = aSize.Width;
            b.Height = aSize.Height;

            a.Width = bSize.Width;
            a.Height = bSize.Height;
        }
    }
}
