// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
            this.FastMemoryGroup = memoryGroup;
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
        /// Gets the backing <see cref="IMemoryGroup{T}"/>.
        /// </summary>
        /// <returns>The MemoryGroup.</returns>
        public IMemoryGroup<T> MemoryGroup => this.FastMemoryGroup.View;

        /// <summary>
        /// Gets the backing <see cref="MemoryGroup{T}"/> without the view abstraction.
        /// </summary>
        /// <remarks>
        /// This property has been kept internal intentionally.
        /// It's public counterpart is <see cref="MemoryGroup"/>,
        /// which only exposes the view of the MemoryGroup.
        /// </remarks>
        internal MemoryGroup<T> FastMemoryGroup { get; }

        /// <summary>
        /// Gets a reference to the element at the specified position.
        /// </summary>
        /// <param name="x">The x coordinate (row)</param>
        /// <param name="y">The y coordinate (position at row)</param>
        /// <returns>A reference to the element.</returns>
        /// <exception cref="IndexOutOfRangeException">When index is out of range of the buffer.</exception>
        public ref T this[int x, int y]
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get
            {
                DebugGuard.MustBeGreaterThanOrEqualTo(x, 0, nameof(x));
                DebugGuard.MustBeGreaterThanOrEqualTo(y, 0, nameof(y));
                DebugGuard.MustBeLessThan(x, this.Width, nameof(x));
                DebugGuard.MustBeLessThan(y, this.Height, nameof(y));

                return ref this.GetRowSpan(y)[x];
            }
        }

        /// <summary>
        /// Disposes the <see cref="Buffer2D{T}"/> instance
        /// </summary>
        public void Dispose() => this.FastMemoryGroup.Dispose();

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to the row 'y' beginning from the pixel at the first pixel on that row.
        /// </summary>
        /// <remarks>
        /// This method does not validate the y argument for performance reason,
        /// <see cref="ArgumentOutOfRangeException"/> is being propagated from lower levels.
        /// </remarks>
        /// <param name="y">The row index.</param>
        /// <returns>The <see cref="Span{T}"/> of the pixels in the row.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when row index is out of range.</exception>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Span<T> GetRowSpan(int y)
        {
            DebugGuard.MustBeGreaterThanOrEqualTo(y, 0, nameof(y));
            DebugGuard.MustBeLessThan(y, this.Height, nameof(y));

            return this.GetRowMemoryCore(y).Span;
        }

        internal bool TryGetPaddedRowSpan(int y, int padding, out Span<T> paddedSpan)
        {
            DebugGuard.MustBeGreaterThanOrEqualTo(y, 0, nameof(y));
            DebugGuard.MustBeLessThan(y, this.Height, nameof(y));

            int stride = this.Width + padding;

            Memory<T> memory = this.FastMemoryGroup.GetRemainingSliceOfBuffer(y * (long)this.Width);

            if (memory.Length < stride)
            {
                paddedSpan = default;
                return false;
            }

            paddedSpan = memory.Span.Slice(0, stride);
            return true;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal ref T GetElementUnsafe(int x, int y)
        {
            Span<T> span = this.GetRowMemoryCore(y).Span;
            return ref span[x];
        }

        /// <summary>
        /// Gets a <see cref="Memory{T}"/> to the row 'y' beginning from the pixel at the first pixel on that row.
        /// </summary>
        /// <param name="y">The y (row) coordinate.</param>
        /// <returns>The <see cref="Span{T}"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal Memory<T> GetSafeRowMemory(int y)
        {
            DebugGuard.MustBeGreaterThanOrEqualTo(y, 0, nameof(y));
            DebugGuard.MustBeLessThan(y, this.Height, nameof(y));
            return this.FastMemoryGroup.View.GetBoundedSlice(y * (long)this.Width, this.Width);
        }

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to the backing data if the backing group consists of a single contiguous memory buffer.
        /// Throws <see cref="InvalidOperationException"/> otherwise.
        /// </summary>
        /// <returns>The <see cref="Span{T}"/> referencing the memory area.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the backing group is discontiguous.
        /// </exception>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal Span<T> DangerousGetSingleSpan() => this.FastMemoryGroup.Single().Span;

        /// <summary>
        /// Gets a <see cref="Memory{T}"/> to the backing data of if the backing group consists of a single contiguous memory buffer.
        /// Throws <see cref="InvalidOperationException"/> otherwise.
        /// </summary>
        /// <returns>The <see cref="Memory{T}"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the backing group is discontiguous.
        /// </exception>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal Memory<T> DangerousGetSingleMemory() => this.FastMemoryGroup.Single();

        /// <summary>
        /// Swaps the contents of 'destination' with 'source' if the buffers are owned (1),
        /// copies the contents of 'source' to 'destination' otherwise (2). Buffers should be of same size in case 2!
        /// </summary>
        internal static void SwapOrCopyContent(Buffer2D<T> destination, Buffer2D<T> source)
        {
            MemoryGroup<T>.SwapOrCopyContent(destination.FastMemoryGroup, source.FastMemoryGroup);
            SwapOwnData(destination, source);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private Memory<T> GetRowMemoryCore(int y) => this.FastMemoryGroup.GetBoundedSlice(y * (long)this.Width, this.Width);

        private static void SwapOwnData(Buffer2D<T> a, Buffer2D<T> b)
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
