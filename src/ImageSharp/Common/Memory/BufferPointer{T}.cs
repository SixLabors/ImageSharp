// <copyright file="BufferPointer{T}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Provides access to elements in an array from a given position.
    /// This type shares many similarities with corefx System.Span&lt;T&gt; but there are significant differences in it's functionalities and semantics:
    /// - It's not possible to use it with stack objects or pointers to unmanaged memory, only with managed arrays
    /// - It's possible to retrieve a reference to the array (<see cref="Array"/>) so we can pass it to API-s like <see cref="Marshal.Copy(byte[], int, IntPtr, int)"/>
    /// - There is no bounds checking for performance reasons. Therefore we don't need to store length. (However this could be added as DEBUG-only feature.)
    ///   This makes <see cref="BufferPointer{T}"/> an unsafe type!
    /// - Currently the arrays provided to BufferPointer need to be pinned. This behaviour could be changed using C#7 features.
    /// </summary>
    /// <typeparam name="T">The type of elements of the array</typeparam>
    internal unsafe struct BufferPointer<T>
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BufferPointer{T}"/> struct from a pinned array and an offset.
        /// </summary>
        /// <param name="array">The pinned array</param>
        /// <param name="pointerToArray">Pointer to the beginning of array</param>
        /// <param name="offset">The offset inside the array</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferPointer(T[] array, void* pointerToArray, int offset)
        {
            DebugGuard.NotNull(array, nameof(array));

            this.Array = array;
            this.Offset = offset;
            this.PointerAtOffset = (IntPtr)pointerToArray + (Unsafe.SizeOf<T>() * offset);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BufferPointer{T}"/> struct from a pinned array.
        /// </summary>
        /// <param name="array">The pinned array</param>
        /// <param name="pointerToArray">Pointer to the start of 'array'</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferPointer(T[] array, void* pointerToArray)
        {
            DebugGuard.NotNull(array, nameof(array));

            this.Array = array;
            this.Offset = 0;
            this.PointerAtOffset = (IntPtr)pointerToArray;
        }

        /// <summary>
        /// Gets the array
        /// </summary>
        public T[] Array { get; private set; }

        /// <summary>
        /// Gets the offset inside <see cref="Array"/>
        /// </summary>
        public int Offset { get; private set; }

        /// <summary>
        /// Gets the pointer to the offseted array position
        /// </summary>
        public IntPtr PointerAtOffset { get; private set; }

        /// <summary>
        /// Convertes <see cref="BufferPointer{T}"/> instance to a raw 'void*' pointer
        /// </summary>
        /// <param name="bufferPointer">The <see cref="BufferPointer{T}"/> to convert</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator void*(BufferPointer<T> bufferPointer)
        {
            return (void*)bufferPointer.PointerAtOffset;
        }

        /// <summary>
        /// Converts <see cref="BufferPointer{T}"/> instance to a raw 'byte*' pointer
        /// </summary>
        /// <param name="bufferPointer">The <see cref="BufferPointer{T}"/> to convert</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator byte*(BufferPointer<T> bufferPointer)
        {
            return (byte*)bufferPointer.PointerAtOffset;
        }

        /// <summary>
        /// Converts <see cref="BufferPointer{T}"/> instance to <see cref="BufferPointer{Byte}"/>
        /// setting it's <see cref="Offset"/> and <see cref="PointerAtOffset"/> to correct values.
        /// </summary>
        /// <param name="source">The <see cref="BufferPointer{T}"/> to convert</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator BufferPointer<byte>(BufferPointer<T> source)
        {
            BufferPointer<byte> result = default(BufferPointer<byte>);
            result.Array = Unsafe.As<byte[]>(source.Array);
            result.Offset = source.Offset * Unsafe.SizeOf<T>();
            result.PointerAtOffset = source.PointerAtOffset;
            return result;
        }

        /// <summary>
        /// Forms a slice out of the given BufferPointer, beginning at 'offset'.
        /// </summary>
        /// <param name="offset">The offset in number of elements</param>
        /// <returns>The offseted (sliced) BufferPointer</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferPointer<T> Slice(int offset)
        {
            BufferPointer<T> result = default(BufferPointer<T>);
            result.Array = this.Array;
            result.Offset = this.Offset + offset;
            result.PointerAtOffset = this.PointerAtOffset + (Unsafe.SizeOf<T>() * offset);
            return result;
        }
    }
}