// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.Primitives;

namespace SixLabors.Memory
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
        /// Gets the backing <see cref="IBuffer{T}"/>
        /// </summary>
        public IBuffer<T> Buffer { get; private set; }

        public Memory<T> Memory => this.Buffer.Memory;

        public Span<T> Span => this.Buffer.GetSpan();

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
                Span<T> span = this.Buffer.GetSpan();
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
        /// Swaps the contents of 'destination' with 'source' if the buffers are owned (1),
        /// copies the contents of 'source' to 'destination' otherwise (2). Buffers should be of same size in case 2!
        /// </summary>
        public static void SwapOrCopyContent(Buffer2D<T> destination, Buffer2D<T> source)
        {
            if (source.Buffer.IsMemoryOwner && destination.Buffer.IsMemoryOwner)
            {
                SwapContents(destination, source);
            }
            else
            {
                if (destination.Size() != source.Size())
                {
                    throw new InvalidOperationException("SwapOrCopyContents(): buffers should both owned or the same size!");
                }

                source.Span.CopyTo(destination.Span);
            }
        }

        /// <summary>
        /// Swap the contents (<see cref="Buffer"/>, <see cref="Width"/>, <see cref="Height"/>) of the two buffers.
        /// Useful to transfer the contents of a temporary <see cref="Buffer2D{T}"/> to a persistent <see cref="SixLabors.ImageSharp.ImageFrame{T}.PixelBuffer"/>
        /// </summary>
        /// <param name="a">The first buffer</param>
        /// <param name="b">The second buffer</param>
        private static void SwapContents(Buffer2D<T> a, Buffer2D<T> b)
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