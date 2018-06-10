// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.Memory
{
    /// <summary>
    /// Wraps an array as an <see cref="IBuffer{T}"/> instance. In this implementation <see cref="IBuffer{T}.Memory"/> is owned.
    /// </summary>
    internal class BasicArrayBuffer<T> : ManagedBufferBase<T>, IBuffer<T>
        where T : struct
    {
        public BasicArrayBuffer(T[] array, int length)
        {
            ImageSharp.DebugGuard.MustBeLessThanOrEqualTo(length, array.Length, nameof(length));
            this.Array = array;
            this.Length = length;
        }

        public BasicArrayBuffer(T[] array)
            : this(array, array.Length)
        {
        }

        public T[] Array { get; }

        public int Length { get; }

        /// <summary>
        /// Returns a reference to specified element of the buffer.
        /// </summary>
        /// <param name="index">The index</param>
        /// <returns>The reference to the specified element</returns>
        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                DebugGuard.MustBeLessThan(index, this.Length, nameof(index));

                Span<T> span = this.GetSpan();
                return ref span[index];
            }
        }

        protected override void Dispose(bool disposing)
        {
        }

        public override Span<T> GetSpan() => this.Array.AsSpan(0, this.Length);

        protected override object GetPinnableObject()
        {
            return this.Array;
        }
    }
}