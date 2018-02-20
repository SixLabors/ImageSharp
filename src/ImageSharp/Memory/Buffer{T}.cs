// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <inheritdoc />
    /// <summary>
    /// Manages a buffer of value type objects as a Disposable resource.
    /// The backing array is either pooled or comes from the outside.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    internal class Buffer<T> : IBuffer<T>
        where T : struct
    {
        private MemoryManager memoryManager;

        // why is there such a rule? :S Protected should be fine for a field!
#pragma warning disable SA1401 // Fields should be private
        /// <summary>
        /// The backing array.
        /// </summary>
        protected T[] array;
#pragma warning restore SA1401 // Fields should be private

        internal Buffer(T[] array, int length, MemoryManager memoryManager)
        {
            if (array.Length < length)
            {
                throw new ArgumentException("Can't initialize a PinnedBuffer with array.Length < count", nameof(array));
            }

            this.Length = length;
            this.array = array;
            this.memoryManager = memoryManager;
        }

        /// <summary>
        /// Gets the count of "relevant" elements. It's usually smaller than 'Array.Length' when <see cref="array"/> is pooled.
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to the backing buffer.
        /// </summary>
        public Span<T> Span => new Span<T>(this.array, 0, this.Length);

        /// <summary>
        /// Disposes the <see cref="Buffer{T}"/> instance by unpinning the array, and returning the pooled buffer when necessary.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (this.array == null)
            {
                return;
            }

            this.memoryManager?.Release(this);

            this.memoryManager = null;
            this.array = null;
            this.Length = 0;

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// TODO: Refactor this
        /// </summary>
        internal T[] GetArray()
        {
            return this.array;
        }
    }
}