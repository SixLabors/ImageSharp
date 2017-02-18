// <copyright file="ArrayPointer{T}.cs" company="James Jackson-South">
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
    ///   This makes <see cref="ArrayPointer{T}"/> an unsafe type!
    /// - Currently the arrays provided to ArrayPointer need to be pinned. This behaviour could be changed using C#7 features.
    /// </summary>
    /// <typeparam name="T">The type of elements of the array</typeparam>
    internal unsafe struct ArrayPointer<T>
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPointer{T}"/> struct from a pinned array and an offset.
        /// </summary>
        /// <param name="array">The pinned array</param>
        /// <param name="pointerToArray">Pointer to the beginning of array</param>
        /// <param name="offset">The offset inside the array</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayPointer(T[] array, void* pointerToArray, int offset)
        {
            // TODO: Use Guard.NotNull() here after optimizing it by eliminating the default argument case and applying ThrowHelper!
            if (array == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(array));
            }

            this.Array = array;
            this.Offset = offset;
            this.PointerAtOffset = (IntPtr)pointerToArray + (Unsafe.SizeOf<T>() * offset);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayPointer{T}"/> struct from a pinned array.
        /// </summary>
        /// <param name="array">The pinned array</param>
        /// <param name="pointerToArray">Pointer to the start of 'array'</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayPointer(T[] array, void* pointerToArray)
        {
            // TODO: Use Guard.NotNull() here after optimizing it by eliminating the default argument case and applying ThrowHelper!
            if (array == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(array));
            }

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
        /// Forms a slice out of the given ArrayPointer, beginning at 'offset'.
        /// </summary>
        /// <param name="offset">The offset in number of elements</param>
        /// <returns>The offseted (sliced) ArrayPointer</returns>
        public ArrayPointer<T> Slice(int offset)
        {
            ArrayPointer<T> result = default(ArrayPointer<T>);
            result.Array = this.Array;
            result.Offset = this.Offset + offset;
            result.PointerAtOffset = this.PointerAtOffset + (Unsafe.SizeOf<T>() * offset);
            return result;
        }
    }
}