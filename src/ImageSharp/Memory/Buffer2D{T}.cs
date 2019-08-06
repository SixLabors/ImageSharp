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
    internal sealed class Buffer2D<T> : IDisposable
        where T : struct
    {
        private MemorySource<T> memorySource;

        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer2D{T}"/> class.
        /// </summary>
        /// <param name="memorySource">The buffer to wrap</param>
        /// <param name="width">The number of elements in a row</param>
        /// <param name="height">The number of rows</param>
        public Buffer2D(MemorySource<T> memorySource, int width, int height)
        {
            this.memorySource = memorySource;
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
        /// Gets the backing <see cref="MemorySource{T}"/>
        /// </summary>
        public MemorySource<T> MemorySource => this.memorySource;

        public Memory<T> Memory => this.MemorySource.Memory;

        public Span<T> Span => this.Memory.Span;

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

                Span<T> span = this.Span;
                return ref span[(this.Width * y) + x];
            }
        }

        /// <summary>
        /// Creates a new <see cref="Buffer2D{T}"/> instance that maps to a target rows interval from the current instance.
        /// </summary>
        /// <param name="y">The target vertical offset for the rows interval to retrieve.</param>
        /// <param name="h">The desired number of rows to extract.</param>
        /// <returns>The new <see cref="Buffer2D{T}"/> instance with the requested rows interval.</returns>
        public Buffer2D<T> Slice(int y, int h)
        {
            DebugGuard.MustBeGreaterThanOrEqualTo(y, 0, nameof(y));
            DebugGuard.MustBeGreaterThan(h, 0, nameof(h));
            DebugGuard.MustBeLessThanOrEqualTo(y + h, this.Height, nameof(h));

            Memory<T> slice = this.Memory.Slice(y * this.Width, h * this.Width);
            var memory = new MemorySource<T>(slice);
            return new Buffer2D<T>(memory, this.Width, h);
        }

        /// <summary>
        /// Disposes the <see cref="Buffer2D{T}"/> instance
        /// </summary>
        public void Dispose()
        {
            this.MemorySource.Dispose();
        }

        /// <summary>
        /// Swaps the contents of 'destination' with 'source' if the buffers are owned (1),
        /// copies the contents of 'source' to 'destination' otherwise (2). Buffers should be of same size in case 2!
        /// </summary>
        public static void SwapOrCopyContent(Buffer2D<T> destination, Buffer2D<T> source)
        {
            MemorySource<T>.SwapOrCopyContent(ref destination.memorySource, ref source.memorySource);
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
