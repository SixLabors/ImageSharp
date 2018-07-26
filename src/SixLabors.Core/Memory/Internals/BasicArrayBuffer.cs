// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.Memory.Internals
{
    /// <summary>
    /// Wraps an array as an <see cref="IManagedByteBuffer"/> instance.
    /// </summary>
    /// <inheritdoc />
    internal class BasicArrayBuffer<T> : ManagedBufferBase<T>
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BasicArrayBuffer{T}"/> class
        /// </summary>
        /// <param name="array">The array</param>
        /// <param name="length">The length of the buffer</param>
        public BasicArrayBuffer(T[] array, int length)
        {
            DebugGuard.MustBeLessThanOrEqualTo(length, array.Length, nameof(length));
            this.Array = array;
            this.Length = length;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BasicArrayBuffer{T}"/> class
        /// </summary>
        /// <param name="array">The array</param>
        public BasicArrayBuffer(T[] array)
            : this(array, array.Length)
        {
        }

        /// <summary>
        /// Gets the array
        /// </summary>
        public T[] Array { get; }

        /// <summary>
        /// Gets the length
        /// </summary>
        public int Length { get; }

        /// <inheritdoc />
        public override Span<T> GetSpan() => this.Array.AsSpan(0, this.Length);

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
        }

        /// <inheritdoc />
        protected override object GetPinnableObject()
        {
            return this.Array;
        }
    }
}