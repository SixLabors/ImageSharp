// <copyright file="PinnedBuffer{T}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Manages a pinned buffer of value type objects as a Disposable resource.
    /// The backing array is either pooled or comes from the outside.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    internal class PinnedBuffer<T> : IDisposable
        where T : struct
    {
        /// <summary>
        /// A handle that allows to access the managed <see cref="Array"/> as an unmanaged memory by pinning.
        /// </summary>
        private GCHandle handle;

        /// <summary>
        /// A value indicating wheter <see cref="Array"/> should be returned to <see cref="PixelDataPool{T}"/>
        /// when disposing this <see cref="PinnedBuffer{T}"/> instance.
        /// </summary>
        private bool isPoolingOwner;

        /// <summary>
        /// Initializes a new instance of the <see cref="PinnedBuffer{T}"/> class.
        /// </summary>
        /// <param name="length">The desired count of elements. (Minimum size for <see cref="Array"/>)</param>
        public PinnedBuffer(int length)
        {
            this.Length = length;
            this.Array = PixelDataPool<T>.Rent(length);
            this.isPoolingOwner = true;
            this.Pin();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PinnedBuffer{T}"/> class.
        /// </summary>
        /// <param name="array">The array to pin.</param>
        public PinnedBuffer(T[] array)
        {
            this.Length = array.Length;
            this.Array = array;
            this.isPoolingOwner = false;
            this.Pin();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PinnedBuffer{T}"/> class.
        /// </summary>
        /// <param name="array">The array to pin.</param>
        /// <param name="length">The count of "relevant" elements in 'array'.</param>
        public PinnedBuffer(T[] array, int length)
        {
            if (array.Length < length)
            {
                throw new ArgumentException("Can't initialize a PinnedBuffer with array.Length < count", nameof(array));
            }

            this.Length = length;
            this.Array = array;
            this.isPoolingOwner = false;
            this.Pin();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="PinnedBuffer{T}"/> class.
        /// </summary>
        ~PinnedBuffer()
        {
            this.UnPin();
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="PinnedBuffer{T}"/> instance is disposed, or has lost ownership of <see cref="Array"/>.
        /// </summary>
        public bool IsDisposedOrLostArrayOwnership { get; private set; }

        /// <summary>
        /// Gets the count of "relevant" elements. It's usually smaller than 'Array.Length' when <see cref="Array"/> is pooled.
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        /// Gets the backing pinned array.
        /// </summary>
        public T[] Array { get; private set; }

        /// <summary>
        /// Gets a pointer to the pinned <see cref="Array"/>.
        /// </summary>
        public IntPtr Pointer { get; private set; }

        /// <summary>
        /// Gets a <see cref="BufferSpan{T}"/> to the backing buffer.
        /// </summary>
        public BufferSpan<T> Span => this;

        /// <summary>
        /// Converts <see cref="PinnedBuffer{T}"/> to an <see cref="BufferSpan{T}"/>.
        /// </summary>
        /// <param name="buffer">The <see cref="PinnedBuffer{T}"/> to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe implicit operator BufferSpan<T>(PinnedBuffer<T> buffer)
        {
            return new BufferSpan<T>(buffer.Array, (void*)buffer.Pointer, 0, buffer.Length);
        }

        /// <summary>
        /// Creates a clean instance of <see cref="PinnedBuffer{T}"/> initializing it's elements with 'default(T)'.
        /// </summary>
        /// <param name="count">The desired count of elements. (Minimum size for <see cref="Array"/>)</param>
        /// <returns>The <see cref="PinnedBuffer{T}"/> instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PinnedBuffer<T> CreateClean(int count)
        {
            PinnedBuffer<T> buffer = new PinnedBuffer<T>(count);
            buffer.Clear();
            return buffer;
        }

        /// <summary>
        /// Gets a <see cref="BufferSpan{T}"/> to an offseted position inside the buffer.
        /// </summary>
        /// <param name="start">The start</param>
        /// <returns>The <see cref="BufferSpan{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe BufferSpan<T> Slice(int start)
        {
            return new BufferSpan<T>(this.Array, (void*)this.Pointer, start, this.Length - start);
        }

        /// <summary>
        /// Gets a <see cref="BufferSpan{T}"/> to an offseted position inside the buffer.
        /// </summary>
        /// <param name="start">The start</param>
        /// <param name="length">The length of the slice</param>
        /// <returns>The <see cref="BufferSpan{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe BufferSpan<T> Slice(int start, int length)
        {
            return new BufferSpan<T>(this.Array, (void*)this.Pointer, start, length);
        }

        /// <summary>
        /// Disposes the <see cref="PinnedBuffer{T}"/> instance by unpinning the array, and returning the pooled buffer when necessary.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (this.IsDisposedOrLostArrayOwnership)
            {
                return;
            }

            this.IsDisposedOrLostArrayOwnership = true;
            this.UnPin();

            if (this.isPoolingOwner)
            {
                PixelDataPool<T>.Return(this.Array);
            }

            this.isPoolingOwner = false;
            this.Array = null;
            this.Length = 0;

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Unpins <see cref="Array"/> and makes the object "quasi-disposed" so the array is no longer owned by this object.
        /// If <see cref="Array"/> is rented, it's the callers responsibility to return it to it's pool. (Most likely <see cref="PixelDataPool{T}"/>)
        /// </summary>
        /// <returns>The unpinned <see cref="Array"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] UnPinAndTakeArrayOwnership()
        {
            if (this.IsDisposedOrLostArrayOwnership)
            {
                throw new InvalidOperationException("UnPinAndTakeArrayOwnership() is invalid: either PinnedBuffer<T> is disposed or UnPinAndTakeArrayOwnership() has been called multiple times!");
            }

            this.IsDisposedOrLostArrayOwnership = true;
            this.UnPin();
            T[] array = this.Array;
            this.Array = null;
            this.isPoolingOwner = false;
            return array;
        }

        /// <summary>
        /// Clears the buffer, filling elements between 0 and <see cref="Length"/>-1 with default(T)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            ((BufferSpan<T>)this).Clear();
        }

        /// <summary>
        /// Pins <see cref="Array"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Pin()
        {
            this.handle = GCHandle.Alloc(this.Array, GCHandleType.Pinned);
            this.Pointer = this.handle.AddrOfPinnedObject();
        }

        /// <summary>
        /// Unpins <see cref="Array"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UnPin()
        {
            if (this.Pointer == IntPtr.Zero || !this.handle.IsAllocated)
            {
                return;
            }

            this.handle.Free();
            this.Pointer = IntPtr.Zero;
        }
    }
}