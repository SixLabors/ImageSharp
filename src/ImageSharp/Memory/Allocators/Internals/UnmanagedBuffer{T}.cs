// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory.Internals
{
    /// <summary>
    /// Allocates and provides an <see cref="IMemoryOwner{T}"/> implementation giving
    /// access to unmanaged buffers allocated by <see cref="Marshal.AllocHGlobal(int)"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    internal unsafe class UnmanagedBuffer<T> : MemoryManager<T>
        where T : struct
    {
        private readonly int lengthInElements;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnmanagedBuffer{T}"/> class.
        /// </summary>
        /// <param name="lengthInElements">The number of elements to allocate.</param>
        public UnmanagedBuffer(int lengthInElements)
            : this(UnmanagedMemoryHandle.Allocate(lengthInElements * Unsafe.SizeOf<T>()), lengthInElements)
        {
        }

        protected UnmanagedBuffer(UnmanagedMemoryHandle bufferHandle, int lengthInElements)
        {
            this.lengthInElements = lengthInElements;
            this.BufferHandle = bufferHandle;
        }

        public UnmanagedMemoryHandle BufferHandle { get; protected set; }

        private void* Pointer => (void*)this.BufferHandle.DangerousGetHandle();

        public override Span<T> GetSpan() => new(this.Pointer, this.lengthInElements);

        /// <inheritdoc />
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            // Will be released in Unpin
            bool unused = false;
            this.BufferHandle.DangerousAddRef(ref unused);

            void* pbData = Unsafe.Add<T>(this.Pointer, elementIndex);
            return new MemoryHandle(pbData, pinnable: this);
        }

        /// <inheritdoc />
        public override void Unpin() => this.BufferHandle.DangerousRelease();

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (this.BufferHandle.IsInvalid)
            {
                return;
            }

            if (disposing)
            {
                this.BufferHandle.Dispose();
            }
        }
    }
}
