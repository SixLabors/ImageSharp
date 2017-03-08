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
    /// Manages a pinned buffer of value type data 'T' as a Disposable resource.
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
        /// The <see cref="PixelDataPool{T}"/> if the <see cref="Array"/> is pooled.
        /// </summary>
        private PixelDataPool<T> pool;

        /// <summary>
        /// Initializes a new instance of the <see cref="PinnedBuffer{T}"/> class.
        /// </summary>
        /// <param name="count">The desired count of elements. (Minimum size for <see cref="Array"/>)</param>
        /// <param name="pool">The <see cref="PixelDataPool{T}"/> to be used to rent the data.</param>
        public PinnedBuffer(int count, PixelDataPool<T> pool)
        {
            this.Count = count;
            this.pool = pool;
            this.Array = this.pool.Rent(count);
            this.Pin();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PinnedBuffer{T}"/> class.
        /// </summary>
        /// <param name="count">The desired count of elements. (Minimum size for <see cref="Array"/>)</param>
        public PinnedBuffer(int count)
            : this(count, PixelDataPool<T>.Clean)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PinnedBuffer{T}"/> class.
        /// </summary>
        /// <param name="array">The array to pin.</param>
        public PinnedBuffer(T[] array)
        {
            this.Count = array.Length;
            this.Array = array;
            this.pool = null;
            this.Pin();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PinnedBuffer{T}"/> class.
        /// </summary>
        /// <param name="count">The count of "relevant" elements in 'array'.</param>
        /// <param name="array">The array to pin.</param>
        public PinnedBuffer(int count, T[] array)
        {
            if (array.Length < count)
            {
                throw new ArgumentException("Can't initialize a PinnedBuffer with array.Length < count", nameof(array));
            }

            this.Count = count;
            this.Array = array;
            this.pool = null;
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
        /// Gets the count of "relevant" elements. Usually be smaller than 'Array.Length' when <see cref="Array"/> is pooled.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Gets the backing pinned array.
        /// </summary>
        public T[] Array { get; private set; }

        /// <summary>
        /// Gets a pointer to the pinned <see cref="Array"/>.
        /// </summary>
        public IntPtr Pointer { get; private set; }

        /// <summary>
        /// Converts <see cref="PinnedBuffer{T}"/> to an <see cref="BufferPointer{T}"/>.
        /// </summary>
        /// <param name="buffer">The <see cref="PinnedBuffer{T}"/> to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator BufferPointer<T>(PinnedBuffer<T> buffer)
        {
            return buffer.Slice();
        }

        /// <summary>
        /// Gets a <see cref="BufferPointer{T}"/> to the beginning of the raw data of the buffer.
        /// </summary>
        /// <returns>The <see cref="BufferPointer{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe BufferPointer<T> Slice()
        {
            return new BufferPointer<T>(this.Array, (void*)this.Pointer);
        }

        /// <summary>
        /// Gets a <see cref="BufferPointer{T}"/> to an offseted position inside the buffer.
        /// </summary>
        /// <param name="offset">The offset</param>
        /// <returns>The <see cref="BufferPointer{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe BufferPointer<T> Slice(int offset)
        {
            return new BufferPointer<T>(this.Array, (void*)this.Pointer, offset);
        }

        /// <summary>
        /// Disposes the <see cref="PinnedBuffer{T}"/> instance by unpinning the array, and returning the pooled buffer when necessary.
        /// </summary>
        public void Dispose()
        {
            if (this.IsDisposedOrLostArrayOwnership)
            {
                return;
            }

            this.IsDisposedOrLostArrayOwnership = true;
            this.UnPin();

            this.pool?.Return(this.Array);
            this.pool = null;
            this.Array = null;
            this.Count = 0;

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Unpins <see cref="Array"/> and makes the object "quasi-disposed" so the array is no longer owned by this object.
        /// If <see cref="Array"/> is rented, it's the callers responsibility to return it to it's pool. (Most likely <see cref="PixelDataPool{T}"/>)
        /// </summary>
        /// <returns>The unpinned <see cref="Array"/></returns>
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
            return array;
        }

        /// <summary>
        /// Pins <see cref="Array"/>.
        /// </summary>
        private void Pin()
        {
            this.handle = GCHandle.Alloc(this.Array, GCHandleType.Pinned);
            this.Pointer = this.handle.AddrOfPinnedObject();
        }

        /// <summary>
        /// Unpins <see cref="Array"/>.
        /// </summary>
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