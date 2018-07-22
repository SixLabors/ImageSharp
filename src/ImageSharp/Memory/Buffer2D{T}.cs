// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.Primitives;

namespace SixLabors.Memory
{
    /// <summary>
    /// Represents a buffer of value type objects
    /// interpreted as a 2D region of <see cref="Width"/> x <see cref="Height"/> elements.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    internal sealed class Buffer2D<T> : IDisposable
        where T : struct
    {
        private BufferManager<T> buffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer2D{T}"/> class.
        /// </summary>
        /// <param name="wrappedBuffer">The buffer to wrap</param>
        /// <param name="width">The number of elements in a row</param>
        /// <param name="height">The number of rows</param>
        public Buffer2D(BufferManager<T> wrappedBuffer, int width, int height)
        {
            this.buffer = wrappedBuffer;
            this.Width = width;
            this.Height = height;
        }

        public Buffer2D(IMemoryOwner<T> ownedMemory, int width, int height)
            : this(new BufferManager<T>(ownedMemory), width, height)
        {
        }

        public Buffer2D(Memory<T> observedMemory, int width, int height)
            : this(new BufferManager<T>(observedMemory), width, height)
        {
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
        /// Gets the backing <see cref="BufferManager{T}"/>
        /// </summary>
        public BufferManager<T> Buffer => this.buffer;

        public Memory<T> Memory => this.Buffer.Memory;

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
                ImageSharp.DebugGuard.MustBeLessThan(x, this.Width, nameof(x));
                DebugGuard.MustBeLessThan(y, this.Height, nameof(y));
                Span<T> span = this.Span;
                return ref span[(this.Width * y) + x];
            }
        }

        /// <summary>
        /// Disposes the <see cref="Buffer2D{T}"/> instance
        /// </summary>
        public void Dispose()
        {
            this.Buffer.Dispose();
        }

        /// <summary>
        /// Swaps the contents of 'destination' with 'source' if the buffers are owned (1),
        /// copies the contents of 'source' to 'destination' otherwise (2). Buffers should be of same size in case 2!
        /// </summary>
        public static void SwapOrCopyContent(Buffer2D<T> destination, Buffer2D<T> source)
        {
            BufferManager<T>.SwapOrCopyContent(ref destination.buffer, ref source.buffer);
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