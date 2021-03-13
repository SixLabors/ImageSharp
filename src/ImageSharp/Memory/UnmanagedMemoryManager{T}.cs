// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// A custom <see cref="MemoryManager{T}"/> that can wrap a rawpointer to a buffer of a specified type.
    /// </summary>
    /// <typeparam name="T">The value type to use when casting the wrapped <see cref="Memory{T}"/> instance.</typeparam>
    /// <remarks>This manager doesn't own the memory buffer that it points to.</remarks>
    internal sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T>
        where T : unmanaged
    {
        /// <summary>
        /// The pointer to the memory buffer.
        /// </summary>
        private readonly void* pointer;

        /// <summary>
        /// The length of the memory area.
        /// </summary>
        private readonly int length;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnmanagedMemoryManager{T}"/> class.
        /// </summary>
        /// <param name="pointer">The pointer to the memory buffer.</param>
        /// <param name="length">The length of the memory area.</param>
        public UnmanagedMemoryManager(void* pointer, int length)
        {
            this.pointer = pointer;
            this.length = length;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
        }

        /// <inheritdoc/>
        public override Span<T> GetSpan()
        {
            return new Span<T>(this.pointer, this.length);
        }

        /// <inheritdoc/>
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            return new MemoryHandle(((T*)this.pointer) + elementIndex);
        }

        /// <inheritdoc/>
        public override void Unpin()
        {
        }
    }
}
