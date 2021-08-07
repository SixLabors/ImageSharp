// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal partial class UniformUnmanagedMemoryPool
    {
        public unsafe class Buffer<T> : MemoryManager<T>
            where T : struct
        {
            private UniformUnmanagedMemoryPool pool;
            private readonly int length;

            public Buffer(UniformUnmanagedMemoryPool pool, UnmanagedMemoryHandle bufferHandle, int length)
            {
                this.pool = pool;
                this.BufferHandle = bufferHandle;
                this.length = length;
            }

            private void* Pointer => (void*)this.BufferHandle.DangerousGetHandle();

            protected UnmanagedMemoryHandle BufferHandle { get; private set; }

            public override Span<T> GetSpan() => new Span<T>(this.Pointer, this.length);

            /// <inheritdoc />
            public override MemoryHandle Pin(int elementIndex = 0)
            {
                // Will be released in Unpin
                bool unused = false;
                this.BufferHandle.DangerousAddRef(ref unused);

                void* pbData = Unsafe.Add<T>(this.Pointer, elementIndex);
                return new MemoryHandle(pbData);
            }

            /// <inheritdoc />
            public override void Unpin() => this.BufferHandle.DangerousRelease();

            /// <inheritdoc />
            protected override void Dispose(bool disposing)
            {
                if (this.pool == null)
                {
                    return;
                }

                this.pool.Return(this.BufferHandle);
                this.pool = null;
                this.BufferHandle = null;
            }

            internal void MarkDisposed()
            {
                this.pool = null;
                this.BufferHandle = null;
            }
        }

        public class FinalizableBuffer<T> : Buffer<T>
            where T : struct
        {
            public FinalizableBuffer(UniformUnmanagedMemoryPool pool, UnmanagedMemoryHandle bufferHandle, int length)
                : base(pool, bufferHandle, length)
            {
                bufferHandle.AssignedToNewOwner();
            }

            ~FinalizableBuffer() => this.Dispose(false);

            protected override void Dispose(bool disposing)
            {
                if (!disposing && this.BufferHandle != null)
                {
                    // We need to prevent handle finalization here.
                    // See comments on UnmanagedMemoryHandle.Resurrect()
                    this.BufferHandle.Resurrect();
                }

                base.Dispose(disposing);
            }
        }
    }
}
