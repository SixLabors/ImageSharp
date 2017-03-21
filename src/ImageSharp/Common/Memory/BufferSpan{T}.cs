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
        /// <param name="pointerToArray">Pointer to the beginning of the array</param>
        /// <param name="start">The index at which to begin the span.</param>
        /// <param name="length">The length</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferSpan(T[] array, void* pointerToArray, int start, int length)
        {
            GuardArrayAndPointer(array, pointerToArray);

            DebugGuard.MustBeLessThanOrEqualTo(start, array.Length, nameof(start));
            DebugGuard.MustBeLessThanOrEqualTo(length, array.Length - start, nameof(length));

            this.Array = array;
            this.Length = length;
            this.Start = start;
            this.PointerAtOffset = (IntPtr)pointerToArray + (Unsafe.SizeOf<T>() * start);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BufferSpan{T}"/> struct from a pinned array and an start.
        /// </summary>
        /// <param name="array">The pinned array</param>
        /// <param name="pointerToArray">Pointer to the beginning of the array</param>
        /// <param name="start">The index at which to begin the span.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferSpan(T[] array, void* pointerToArray, int start)
        {
            GuardArrayAndPointer(array, pointerToArray);
            DebugGuard.MustBeLessThanOrEqualTo(start, array.Length, nameof(start));

            this.Array = array;
            this.Length = array.Length - start;
            this.Start = start;
            this.PointerAtOffset = (IntPtr)pointerToArray + (Unsafe.SizeOf<T>() * start);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BufferSpan{T}"/> struct from a pinned array.
        /// </summary>
        /// <param name="array">The pinned array</param>
        /// <param name="pointerToArray">Pointer to the start of 'array'</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferSpan(T[] array, void* pointerToArray)
        {
            GuardArrayAndPointer(array, pointerToArray);

            this.Array = array;
            this.Start = 0;
            this.Length = array.Length;
            this.PointerAtOffset = (IntPtr)pointerToArray;
        }

        /// <summary>
        /// Gets the backing array
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
        /// Gets the pointer to the offseted array position
        /// </summary>
        public IntPtr PointerAtOffset { get; private set; }

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

                byte* ptr = (byte*)this.PointerAtOffset + BufferSpan.SizeOf<T>(index);
                return ref Unsafe.AsRef<T>(ptr);
            }
        }

        /// <summary>
        /// Convertes <see cref="BufferSpan{T}"/> instance to a raw 'void*' pointer
        /// </summary>
        /// <param name="bufferSpan">The <see cref="BufferSpan{T}"/> to convert</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator void*(BufferSpan<T> bufferSpan)
        {
            return (void*)bufferSpan.PointerAtOffset;
        }

        /// <summary>
        /// Converts <see cref="BufferSpan{T}"/> instance to a raw 'byte*' pointer
        /// </summary>
        /// <param name="bufferSpan">The <see cref="BufferSpan{T}"/> to convert</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator byte*(BufferSpan<T> bufferSpan)
        {
            return (byte*)bufferSpan.PointerAtOffset;
        }

        /// <summary>
        /// Converts generic <see cref="BufferSpan{T}"/> to a <see cref="BufferSpan{T}"/> of bytes
        /// setting it's <see cref="Start"/> and <see cref="PointerAtOffset"/> to correct values.
        /// </summary>
        /// <param name="source">The <see cref="BufferSpan{T}"/> to convert</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator BufferSpan<byte>(BufferSpan<T> source)
        {
            BufferSpan<byte> result = default(BufferSpan<byte>);
            result.Array = Unsafe.As<byte[]>(source.Array);
            result.Start = source.Start * Unsafe.SizeOf<T>();
            result.PointerAtOffset = source.PointerAtOffset;
            return result;
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
            result.PointerAtOffset = this.PointerAtOffset + (Unsafe.SizeOf<T>() * start);
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
            result.PointerAtOffset = this.PointerAtOffset + (Unsafe.SizeOf<T>() * start);
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

            if (count < 256)
            {
                Unsafe.InitBlock((void*)this.PointerAtOffset, 0, BufferSpan.USizeOf<T>(count));
            }
            else
            {
                System.Array.Clear(this.Array, this.Start, count);
            }
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
        private static void GuardArrayAndPointer(T[] array, void* pointerToArray)
        {
            DebugGuard.NotNull(array, nameof(array));
            DebugGuard.IsFalse(
                pointerToArray == (void*)0,
                nameof(pointerToArray),
                "pointerToArray should not be null pointer!");
        }
    }
}