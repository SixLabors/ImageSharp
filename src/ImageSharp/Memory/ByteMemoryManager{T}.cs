// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// A custom <see cref="MemoryManager{T}"/> that can wrap <see cref="Memory{T}"/> of <see cref="byte"/> instances
    /// and cast them to be <see cref="Memory{T}"/> for any arbitrary unmanaged <typeparamref name="T"/> value type.
    /// </summary>
    /// <typeparam name="T">The value type to use when casting the wrapped <see cref="Memory{T}"/> instance.</typeparam>
    internal sealed class ByteMemoryManager<T> : MemoryManager<T>
        where T : unmanaged
    {
        /// <summary>
        /// The wrapped <see cref="Memory{T}"/> of <see cref="byte"/> instance.
        /// </summary>
        private readonly Memory<byte> memory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ByteMemoryManager{T}"/> class.
        /// </summary>
        /// <param name="memory">The <see cref="Memory{T}"/> of <see cref="byte"/> instance to wrap.</param>
        public ByteMemoryManager(Memory<byte> memory)
        {
            this.memory = memory;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
        }

        /// <inheritdoc/>
        public override Span<T> GetSpan()
        {
            return MemoryMarshal.Cast<byte, T>(this.memory.Span);
        }

        /// <inheritdoc/>
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            // We need to adjust the offset into the wrapped byte segment,
            // as the input index refers to the target-cast memory of T.
            // We just have to shift this index by the byte size of T.
            return this.memory.Slice(elementIndex * Unsafe.SizeOf<T>()).Pin();
        }

        /// <inheritdoc/>
        public override void Unpin()
        {
        }
    }
}
