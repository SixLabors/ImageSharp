// <copyright file="BufferSpan{T}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents a contiguous region of a pinned managed array.
    /// The array is usually owned by a <see cref="PinnedBuffer{T}"/> instance.
    /// </summary>
    /// <remarks>
    /// <see cref="BufferSpan{T}"/> is very similar to corefx System.Span&lt;T&gt;, and we try to maintain a compatible API.
    /// There are several differences though:
    /// - It's not possible to use it with stack objects or pointers to unmanaged memory, only with managed arrays.
    /// - It's possible to retrieve a reference to the array (<see cref="Array"/>) so we can pass it to API-s like <see cref="Marshal.Copy(byte[], int, IntPtr, int)"/>
    /// - It's possible to retrieve the pinned pointer. This enables optimized (unchecked) unsafe operations.
    /// - There is no bounds checking for performance reasons, only in debug mode. This makes <see cref="BufferSpan{T}"/> an unsafe type!
    /// </remarks>
    /// <typeparam name="T">The type of elements of the array</typeparam>
    internal unsafe struct BufferSpan<T>
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BufferSpan{T}"/> struct from a pinned array and an start.
        /// </summary>
        /// <param name="array">The pinned array</param>
        /// <param name="start">The index at which to begin the span.</param>
        /// <param name="length">The length</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferSpan(T[] array, int start, int length)
        {
            GuardArray(array);

            DebugGuard.MustBeLessThanOrEqualTo(start, array.Length, nameof(start));
            DebugGuard.MustBeLessThanOrEqualTo(length, array.Length - start, nameof(length));

            this.Array = array;
            this.Length = length;
            this.Start = start;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BufferSpan{T}"/> struct from a pinned array and an start.
        /// </summary>
        /// <param name="array">The pinned array</param>
        /// <param name="start">The index at which to begin the span.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferSpan(T[] array, int start)
        {
            GuardArray(array);
            DebugGuard.MustBeLessThanOrEqualTo(start, array.Length, nameof(start));

            this.Array = array;
            this.Length = array.Length - start;
            this.Start = start;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BufferSpan{T}"/> struct from a pinned array.
        /// </summary>
        /// <param name="array">The pinned array</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferSpan(T[] array)
        {
            GuardArray(array);

            this.Array = array;
            this.Start = 0;
            this.Length = array.Length;
        }

        /// <summary>
        /// Gets the backing array.
        /// </summary>
        public T[] Array { get; private set; }

        /// <summary>
        /// Gets the length of the <see cref="BufferSpan{T}"/>
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        /// Gets the start inside <see cref="Array"/>
        /// </summary>
        public int Start { get; private set; }

        /// <summary>
        /// Gets the start inside <see cref="Array"/> in bytes.
        /// </summary>
        public int ByteOffset => this.Start * Unsafe.SizeOf<T>();

        /// <summary>
        /// Returns a reference to specified element of the span.
        /// </summary>
        /// <param name="index">The index</param>
        /// <returns>The reference to the specified element</returns>
        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                DebugGuard.MustBeLessThan(index, this.Length, nameof(index));
                ref T startRef = ref this.DangerousGetPinnableReference();
                return ref Unsafe.Add(ref startRef, index);
            }
        }

        /// <summary>
        /// Converts generic <see cref="BufferSpan{T}"/> to a <see cref="BufferSpan{T}"/> of bytes
        /// setting it's <see cref="Start"/> and <see cref="Length"/> to correct values.
        /// </summary>
        /// <returns>The span of bytes</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferSpan<byte> AsBytes()
        {
            BufferSpan<byte> result = default(BufferSpan<byte>);
            result.Array = Unsafe.As<byte[]>(this.Array);
            result.Start = this.Start * Unsafe.SizeOf<T>();
            result.Length = this.Length * Unsafe.SizeOf<T>();
            return result;
        }

        /// <summary>
        /// Returns a reference to the 0th element of the Span. If the Span is empty, returns a reference to the location where the 0th element
        /// would have been stored. Such a reference can be used for pinning but must never be dereferenced.
        /// </summary>
        /// <returns>The reference to the 0th element</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T DangerousGetPinnableReference()
        {
            ref T origin = ref this.Array[0];
            return ref Unsafe.Add(ref origin, this.Start);
        }

        /// <summary>
        /// Forms a slice out of the given BufferSpan, beginning at 'start'.
        /// </summary>
        /// <param name="start">TThe index at which to begin this slice.</param>
        /// <returns>The offseted (sliced) BufferSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferSpan<T> Slice(int start)
        {
            DebugGuard.MustBeLessThan(start, this.Length, nameof(start));

            BufferSpan<T> result = default(BufferSpan<T>);
            result.Array = this.Array;
            result.Start = this.Start + start;
            result.Length = this.Length - start;
            return result;
        }

        /// <summary>
        /// Forms a slice out of the given BufferSpan, beginning at 'start'.
        /// </summary>
        /// <param name="start">The index at which to begin this slice.</param>
        /// <param name="length">The desired length for the slice (exclusive).</param>
        /// <returns>The sliced BufferSpan</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferSpan<T> Slice(int start, int length)
        {
            DebugGuard.MustBeLessThanOrEqualTo(start, this.Length, nameof(start));
            DebugGuard.MustBeLessThanOrEqualTo(length, this.Length - start, nameof(length));

            BufferSpan<T> result = default(BufferSpan<T>);
            result.Array = this.Array;
            result.Start = this.Start + start;
            result.Length = length;
            return result;
        }

        /// <summary>
        /// Clears `count` elements from the beginning of the span.
        /// </summary>
        /// <param name="count">The number of elements to clear</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(int count)
        {
            DebugGuard.MustBeLessThanOrEqualTo(count, this.Length, nameof(count));

            // TODO: Use Unsafe.InitBlock(ref T) for small arrays, when it get's official
            System.Array.Clear(this.Array, this.Start, count);
        }

        /// <summary>
        /// Clears the the span
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            this.Clear(this.Length);
        }

        [Conditional("DEBUG")]
        private static void GuardArray(T[] array)
        {
            DebugGuard.NotNull(array, nameof(array));
        }
    }
}