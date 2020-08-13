// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.
using System;
using System.Buffers;
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
            if (MemoryMarshal.TryGetArray(this.memory, out ArraySegment<byte> arraySegment))
            {
                return MemoryMarshal.Cast<byte, T>(arraySegment.AsSpan());
            }

            if (MemoryMarshal.TryGetMemoryManager<byte, MemoryManager<byte>>(this.memory, out MemoryManager<byte> memoryManager))
            {
                return MemoryMarshal.Cast<byte, T>(memoryManager.GetSpan());
            }

            // This should never be reached, as Memory<T> can currently only be wrapping
            // either a byte[] array or a MemoryManager<byte> instance in this case.
            ThrowHelper.ThrowArgumentException("The input Memory<byte> instance was not valid.", nameof(this.memory));

            return default;
        }

        /// <inheritdoc/>
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            return this.memory.Pin();
        }

        /// <inheritdoc/>
        public override void Unpin()
        {
        }
    }
}
